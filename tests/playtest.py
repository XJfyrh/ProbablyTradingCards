"""Capture release playtest checkpoints through the external player screenshot CLI."""
import argparse
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import time


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from game_verify import CORE, Blocked, audit, client_lock


MOD = "ProbablyTradingCards"
STEPS = ("before-buy", "bought", "moved", "opened", "filed", "retrieved", "saved", "reloaded")


def package_files(folder):
    paths = [folder / f"{MOD}.dll", folder / "manifest.xml"]
    paths.extend(path for path in (folder / "Textures").rglob("*") if path.is_file())
    return {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in paths}


def release_identity(game_dir, release):
    installed = game_dir / "Mods" / MOD
    expected = package_files(release)
    actual = package_files(installed)
    differences = sorted(key for key in expected.keys() | actual.keys()
                         if expected.get(key) != actual.get(key))
    if differences:
        raise ValueError(f"安装包与正式构建有 {len(differences)} 处差异，例如 {differences[0]}。")
    index = json.dumps(expected, sort_keys=True).encode("utf-8")
    return {"installed": str(installed.resolve()), "dllSha256": actual[f"{MOD}.dll"],
            "packageSha256": hashlib.sha256(index).hexdigest()}


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args], encoding="utf-8",
                                   timeout=10).strip()


def capture(player_root, python, path):
    result = subprocess.run(
        [str(python), str(player_root.resolve() / "scripts/game_play.py"), "--output", str(path), "look"],
        capture_output=True, encoding="utf-8", timeout=15,
    )
    if not result.stdout.strip():
        raise ValueError(result.stderr.strip() or "玩家工具未返回截图回执。")
    receipt = json.loads(result.stdout)
    if not isinstance(receipt, dict):
        raise ValueError("玩家工具的截图回执格式不正确。")
    if result.returncode or receipt.get("status") != "ok" or not path.is_file():
        raise ValueError(receipt.get("error") or "玩家工具未取得游戏截图。")
    if type(receipt.get("pid")) is not int:
        raise ValueError("玩家工具的截图回执缺少游戏进程号。")
    return receipt["pid"]


def checkpoint(args):
    folder = args.run.resolve()
    step_index = STEPS.index(args.step)
    filenames = [folder / f"{i:02d}-{step}.json" for i, step in enumerate(STEPS, start=1)]
    record_path = filenames[step_index]
    if record_path.exists():
        raise ValueError(f"已有检查点 {record_path.name}。重做整轮时请指定新目录。")
    for previous in filenames[:step_index]:
        if not previous.is_file() or not previous.with_suffix(".png").is_file():
            raise ValueError(f"缺少上游记录 {previous.stem}，请按顺序完成检查点。")

    identity = release_identity(args.game_dir, args.release_dir)
    code, startup = audit(args.data_dir, [CORE])
    if code:
        raise ValueError(startup.get("error") or "最新游戏审计中未启用卡牌模组。")
    loaded = startup["mods"][CORE]
    source = {"commit": git("rev-parse", "HEAD"), "dirty": bool(git("status", "--porcelain"))}
    first = json.loads(filenames[0].read_text(encoding="utf-8")) if step_index else None
    if step_index and (not isinstance(first, dict) or first.get("package") != identity
                       or first.get("startupHash") != loaded["hash"] or type(first.get("gamePid")) is not int):
        raise ValueError("当前安装包或游戏审计哈希已变更，请开始新一轮。")

    folder.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=folder) as temporary:
        image = Path(temporary) / "screenshot.png"
        python = args.player_python or args.player_root.resolve() / ".venv/Scripts/python.exe"
        if args.delay:
            time.sleep(args.delay)
        pid = capture(args.player_root, python, image)
        if first and args.step != "reloaded" and pid != first["gamePid"]:
            raise ValueError("保存检查点之前需要在同一个游戏进程操作。")
        if args.step == "reloaded" and pid == first["gamePid"]:
            raise ValueError("请退出并重启游戏、载入原测试档后再记录读档结果。")

        record = {
            "checkpoint": args.step, "capturedAt": datetime.now(timezone.utc).isoformat(),
            "checkout": source, "package": identity, "version": loaded["version"],
            "startupHash": loaded["hash"], "gamePid": pid, "note": args.note,
            "screenshot": record_path.with_suffix(".png").name,
        }
        metadata = image.with_suffix(".json")
        metadata.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        with client_lock(folder):
            if record_path.exists():
                raise ValueError(f"已有检查点 {record_path.name}。重做整轮时请指定新目录。")
            image.replace(record_path.with_suffix(".png"))
            metadata.replace(record_path)
    return record_path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("step", choices=STEPS)
    parser.add_argument("--run", required=True, type=Path, help="本轮证据目录")
    game_dir = os.environ.get("PROBABLY_STOLEN_GAME_DIR")
    parser.add_argument("--game-dir", type=Path, default=game_dir, required=not game_dir,
                        help="游戏安装目录，可用 PROBABLY_STOLEN_GAME_DIR 指定")
    parser.add_argument("--release-dir", type=Path, default=ROOT / MOD / "dist" / MOD,
                        help="要核对的正式包目录，根目录应有 manifest.xml")
    parser.add_argument("--data-dir", type=Path,
                        default=Path.home() / "AppData/LocalLow/Questing Goose Studio/Probably Stolen")
    parser.add_argument("--player-root", type=Path, required=True, help="已安装的玩家工具目录")
    parser.add_argument("--player-python", type=Path, help="玩家工具的 Python，默认使用其 .venv")
    parser.add_argument("--delay", type=int, default=0, help="截图前等待的秒数，便于切回游戏窗口")
    parser.add_argument("--note", default="", help="操作后观察到的现金、物品、画面或问题")
    args = parser.parse_args()
    if not 0 <= args.delay <= 30:
        parser.error("--delay 须在 0 至 30 秒之间。")
    try:
        print(checkpoint(args))
        return 0
    except (Blocked, OSError, ValueError, subprocess.SubprocessError) as error:
        print(error, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    sys.exit(main())
