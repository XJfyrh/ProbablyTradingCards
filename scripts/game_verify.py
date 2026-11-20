"""Run the trading-card mod's developer checks in a running Probably Stolen game."""
import argparse
import json
import os
import re
import sys
import time
import uuid
from contextlib import contextmanager
from pathlib import Path


PROTOCOL = 1
MAX_HEARTBEAT_AGE = 8
CORE = "probably_trading_cards"
AUDIT = re.compile(
    r"\[ModLoader\] audit: (\S+) v(\S+) .*?\brunning=(True|False)\b"
    r".*?\btrust=(\S+).*?\bhash=(\S+)", re.IGNORECASE
)


def read_json(path):
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
        return value if isinstance(value, dict) else None
    except (FileNotFoundError, PermissionError, ValueError):
        # The game's ModHelper can be partway through writing this file.
        return None


def heartbeat(folder):
    path = folder / "status.json"
    for _ in range(3):
        value = read_json(path)
        if value and value.get("protocol") == PROTOCOL and re.fullmatch(
                "[0-9a-f]{32}", str(value.get("session", ""))):
            sent = value.get("heartbeat")
            if type(sent) is int and -2 <= time.time() - sent <= MAX_HEARTBEAT_AGE:
                return value
            break
        time.sleep(0.05)
    return None


def audit(data_dir, required):
    """Report what the game's loader actually recorded at its latest startup."""
    path = data_dir / "Player.log"
    mods = {}
    try:
        with path.open(encoding="utf-8-sig", errors="replace") as stream:
            for line in stream:
                # Keep records from one loader audit, even if logging was appended.
                if "[ModLoader] audit: game " in line:
                    mods.clear()
                match = AUDIT.search(line)
                if match:
                    key, version, running, trust, digest = match.groups()
                    mods[key] = dict(version=version, running=running.lower() == "true",
                                    trust=trust, hash=digest)
    except FileNotFoundError:
        return 2, dict(status="blocked", error="找不到游戏的 Player.log。", log=str(path))
    if not mods:
        return 2, dict(status="blocked", error="本次日志中还没有模组加载审计。", log=str(path))
    missing = [key for key in required if not mods.get(key, {}).get("running")]
    selected = {key: mods[key] for key in required if key in mods}
    return (1 if missing else 0), dict(status="failed" if missing else "passed",
                                       source="startup-log", log=str(path), mods=selected,
                                       missingOrDisabled=missing)


class Blocked(Exception):
    pass


@contextmanager
def client_lock(folder):
    # The OS releases the byte lock if an agent exits mid-run.
    with (folder / "client.lock").open("a+b") as handle:
        if handle.tell() == 0:
            handle.write(b"0")
            handle.flush()
        handle.seek(0)
        try:
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            raise Blocked("另一个 CLI 正在等待本次游戏的结果。") from error
        yield


def live_action(folder, args):
    status = heartbeat(folder)
    if not status or status.get("state") == "offline":
        raise Blocked("没有验证包的实时心跳。请启动验证包，或用 audit 查看上次启动记录。")
    if args.command == "status":
        return 0, status
    with client_lock(folder):
        status = heartbeat(folder)
        if not status or status.get("state") != "ready":
            raise Blocked("测试场景未就绪。请进入测试档并松开拖动物品。")
        slot = status.get("slot")
        if type(slot) is not int or slot < 0 or (args.slot is not None and args.slot != slot):
            raise Blocked(f"当前存档槽为 {slot}，请求的存档槽为 {args.slot}。")
        save_run_id = status.get("runId")
        if not isinstance(save_run_id, str) or not save_run_id:
            raise Blocked("验证包尚未取得当前存档标识。")
        session = status["session"]
        action = "native" if args.command == "run" else "snapshot"
        run_id = uuid.uuid4().hex
        request = dict(protocol=PROTOCOL, session=session, id=run_id, action=action,
                       slot=slot, saveRunId=save_run_id, expires=int(time.time()) + args.timeout + 1,
                       requireSeries=getattr(args, "require_series", []))
        temporary = folder / (run_id + ".tmp")
        try:
            temporary.write_text(json.dumps(request), encoding="utf-8")
            temporary.replace(folder / "request.json")
        finally:
            temporary.unlink(missing_ok=True)
        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            result = read_json(folder / "result.json")
            if result and all(result.get(key) == request[key] for key in ("id", "session")):
                if (result.get("protocol") != PROTOCOL or result.get("action") != action
                        or result.get("status") not in ("passed", "failed", "blocked")):
                    raise Blocked("游戏回执字段无效。")
                if result["status"] == "passed":
                    if type(result.get("slot")) is not int or result["slot"] != slot:
                        raise Blocked("游戏回执的存档槽与请求不符。")
                    if action == "native" and (type(result.get("checks")) is not int or result["checks"] < 1):
                        raise Blocked("游戏没有返回通过的断言数。")
                    if action == "snapshot" and not isinstance(result.get("data"), dict):
                        raise Blocked("游戏没有返回物品快照。")
                return {"passed": 0, "failed": 1, "blocked": 2}[result["status"]], result
            current = read_json(folder / "status.json")
            if current and (current.get("session") != session or current.get("state") == "offline"):
                raise Blocked("等待回执时，游戏验证会话已结束或重启。")
            time.sleep(0.15)
        return 2, dict(status="timeout", session=session, id=run_id, action=action, slot=slot,
                       error="限时内未取得本次回执，已在游戏中开始的检查仍可能完成。")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    default = Path.home() / "AppData/LocalLow/Questing Goose Studio/Probably Stolen"
    parser.add_argument("--data-dir", type=Path, default=default, help="游戏的 persistentDataPath")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("audit", help="检查最近一次启动的原版模组审计").add_argument(
        "mods", nargs="*", help="必须确实运行的模组 ID，默认检查卡牌前置")
    commands.add_parser("status", help="读取验证包的实时场景、会话和存档槽")
    for command in ("run", "snapshot"):
        sub = commands.add_parser(command, help="在游戏内运行原生检查" if command == "run" else "读取游戏内物品和现金快照")
        sub.add_argument("--slot", type=int, required=command == "run", help="指定要验证的存档槽编号")
        sub.add_argument("--timeout", type=int, default=90, help="等待游戏回执的秒数 (1–600)")
        if command == "run":
            sub.add_argument("--require-series", action="append", default=[], metavar="OWNER/KEY",
                             help="要求场景中已经注册此卡辑，可重复")
    args = parser.parse_args()
    try:
        if args.command == "audit":
            code, result = audit(args.data_dir, args.mods or [CORE])
        else:
            if not 1 <= getattr(args, "timeout", 90) <= 600:
                raise Blocked("--timeout 须在 1 到 600 秒之间。")
            code, result = live_action(args.data_dir / "ModData" / CORE / "agent", args)
    except (OSError, Blocked) as error:
        code, result = 2, dict(status="blocked", error=str(error))
    # A stable UTF-8 encoding also works in agents which capture a Windows pipe.
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))
    return code


if __name__ == "__main__":
    sys.exit(main())
