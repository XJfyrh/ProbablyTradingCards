"""Check the current definitions and snapshots against the nearest published version on main."""
import argparse
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
PROJECTS = ("ProbablyTradingCards", "Examples/DockLetters")
CHECKER = ["dotnet", "run", "--project", "scripts/SeriesCheck/SeriesCheck.csproj",
           "-c", "Release", "-p:IdentityOnly=true"]


def check(base):
    tag = subprocess.check_output(
        ["git", "describe", "--first-parent", "--abbrev=0", "--tags", "--match", "v[0-9]*", base],
        cwd=ROOT, text=True).strip()
    print(f"Checking published series from {tag}", flush=True)
    with TemporaryDirectory() as temp:
        archive = Path(temp) / "series.zip"
        paths = [path for project in PROJECTS for path in
                 (f"{project}/manifest.xml", f"{project}/series-identities.json",
                  f":(glob){project}/Series/*/series.json")]
        subprocess.run(["git", "archive", "--format=zip", f"--output={archive}", tag, "--", *paths],
                       cwd=ROOT, check=True)
        with ZipFile(archive) as entries:
            entries.extractall(temp)
        for project in PROJECTS:
            baseline = Path(temp) / project
            args = ["--core", project] if project == "ProbablyTradingCards" else [project]
            subprocess.run([*CHECKER, "--", *args], cwd=ROOT, check=True)
            subprocess.run([*CHECKER, "--no-build", "--", "--compare", baseline / "series-identities.json",
                            ROOT / project / "series-identities.json", baseline], cwd=ROOT, check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="HEAD", help="main branch revision to find the release tag from")
    try:
        check(parser.parse_args().base)
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.returncode)
