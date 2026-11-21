"""Rebuild core game artwork in a temporary directory and compare its RGBA pixels."""
import argparse
from pathlib import Path
import shutil
import subprocess
from tempfile import TemporaryDirectory

from PIL import Image

from card_textures import load_wear
from export_series import (BUILT_INS, PROJECT, absolute_path, check_built_ins, check_series,
                           export_series, read_art_keys, read_manifest, read_packaging)
from export_station_legends import export_legends


def compare_pixels(generated, committed):
    with Image.open(generated) as new, Image.open(committed) as old:
        if (old.format != "PNG" or new.size != old.size or
                new.convert("RGBA").tobytes() != old.convert("RGBA").tobytes()):
            raise ValueError(f"{committed}: differs from the artwork export")


def check_exports(project, stage):
    textures = project / "Textures"
    series = project / "Series"
    output = stage / "Textures"
    paper, items = output / "Cards", output / "Items"
    paper.mkdir(parents=True)
    items.mkdir()

    night = read_manifest(series / "night_stories")
    sources = [Path("Cards") / f"card_{card['id']}.png" for card in night["cards"]]
    sources.extend(Path("Cards") / name for name in ("wear_creased.png", "wear_light.png"))
    sources.extend(Path("Items") / name for name in ("pack.png", "album.png"))
    for path in sources:
        shutil.copyfile(textures / path, output / path)
    for script in ("export_card_conditions.ps1", "export_card_icons.ps1"):
        subprocess.run(["pwsh", "-NoProfile", "-File", str(Path(__file__).with_name(script)),
                        "-ProjectRoot", str(stage)], check=True, stdout=subprocess.DEVNULL)

    wear = load_wear(paper)
    export_legends(project / "Art" / "StationLegends", paper, items, wear)
    check_built_ins(series, paper, items)
    for path in sorted(series.glob("*/series.json")):
        folder = path.parent
        if folder.name in BUILT_INS:
            continue
        manifest = read_manifest(folder)
        keys = read_art_keys(folder, manifest["cards"])
        packaging = read_packaging(folder, manifest)
        export_series(folder, paper, items, wear, keys, packaging)
        check_series(folder, paper, items, paper, items, keys, packaging)

    generated = sorted(path.relative_to(output) for path in output.glob("*/*.png"))
    for path in generated:
        if path not in sources:
            compare_pixels(output / path, textures / path)
    print(f"Verified {len(generated) - len(sources)} generated textures against their source pixels.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT)
    args = parser.parse_args()
    try:
        with TemporaryDirectory() as temporary:
            check_exports(absolute_path(args.project), Path(temporary))
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"{error}\n")
