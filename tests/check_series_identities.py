"""Exercise the author CLI against a released layout and a sealed pack's card IDs."""

import json
from pathlib import Path
from shutil import copytree
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
EXAMPLE = ROOT / "Examples" / "DockLetters"


def main():
    with tempfile.TemporaryDirectory() as temporary:
        root = Path(temporary)
        tool = root / "tool" / "SeriesCheck.dll"
        subprocess.run(["dotnet", "build", str(ROOT / "scripts/SeriesCheck/SeriesCheck.csproj"),
                        "-p:IdentityOnly=true", "-c", "Release", "-o", str(tool.parent)], check=True)
        project = root / "author"
        copytree(EXAMPLE / "Series", project / "Series")
        (project / "manifest.xml").write_bytes((EXAMPLE / "manifest.xml").read_bytes())
        snapshot = project / "series-identities.json"
        manifest = project / "Series/dock_demo/series.json"
        original = json.loads(manifest.read_text(encoding="utf8"))

        def check(*args, expected=0):
            result = subprocess.run(["dotnet", str(tool), *map(str, args)], capture_output=True, encoding="utf8")
            if result.returncode != expected:
                raise AssertionError((args, result.returncode, result.stdout, result.stderr))

        check(project)
        check("--write-identities", project)
        released = root / "published.json"
        released.write_bytes(snapshot.read_bytes())
        initial = json.loads(released.read_text(encoding="utf8"))
        for change in ("id", "art", "order", "slots", "delete", "owner"):
            data = json.loads(json.dumps(original))
            cards = data["cards"]
            if change == "id":
                cards[0]["id"] = 1234567
            elif change == "art":
                cards[0].update(id=1356309392, art="new-art")
            elif change == "order":
                cards.reverse()
            elif change == "slots":
                data["slots"].append({"C": 1})
            elif change == "delete":
                cards.pop()
                data["slots"] = [{"C": 1}, {"C": 1}]
            else:
                (project / "manifest.xml").write_text("<Manifest><ID>new_owner</ID></Manifest>", encoding="utf8")
            manifest.write_text(json.dumps(data), encoding="utf8")
            check(project, expected=1)
            check("--write-identities", project, expected=1)
            assert snapshot.read_bytes() == released.read_bytes()
            (project / "manifest.xml").write_bytes((EXAMPLE / "manifest.xml").read_bytes())

        original["cards"][0]["name"] = "New display name"
        manifest.write_text(json.dumps(original), encoding="utf8")
        check(project)
        original["cards"].append(dict(original["cards"][0], art="extra"))
        manifest.write_text(json.dumps(original), encoding="utf8")
        check(project, expected=1)
        check("--write-identities", project)
        check(project)
        check("--compare", released, snapshot)

        del initial["series"][0]["albumOrder"]
        snapshot.write_text(json.dumps(initial), encoding="utf8")
        check("--write-identities", project, expected=1)
        check("--legacy-project", EXAMPLE, "--write-identities", project)
        check(project)
        current = json.loads(snapshot.read_text(encoding="utf8"))
        current["series"][0]["albumOrder"].reverse()
        snapshot.write_text(json.dumps(current), encoding="utf8")
        check("--compare", released, snapshot, expected=1)
        current["series"].clear()
        snapshot.write_text(json.dumps(current), encoding="utf8")
        check("--compare", released, snapshot, expected=1)
        core = ROOT / "ProbablyTradingCards/series-identities.json"
        for field in ("fatPackSlots", "slimBoxPacks", "fatBoxPacks"):
            current = json.loads(core.read_text(encoding="utf8"))
            legends = next(series for series in current["series"] if series["key"] == "station_legends")
            legends[field] += 1
            snapshot.write_text(json.dumps(current), encoding="utf8")
            check("--compare", core, snapshot, expected=1)
        print("PASS: first snapshot, append, rename, published identities, album order and legacy upgrade")


if __name__ == "__main__":
    main()
