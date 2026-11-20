"""Export expansion art or verify the textures required by a card mod."""

import argparse
import json
import os
from pathlib import Path
import re
from tempfile import TemporaryDirectory

from PIL import Image

from card_textures import CARD_SIZE, ICON_SIZE, PACKAGING_SIZE, export_card, export_packaging, load_wear, read_rgba


PROJECT = Path(__file__).resolve().parents[1] / "ProbablyTradingCards"
BUILT_INS = {"night_stories", "station_legends"}
SERIES_KEY = re.compile(r"[a-z][a-z0-9_]*")
ART_KEY = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*")
WEAR_SUFFIX = "--wear-"
BUILT_IN_PACKAGING = {
    "pack": PACKAGING_SIZE,
    "album": PACKAGING_SIZE,
    "legends_slim": PACKAGING_SIZE,
    "legends_fat": PACKAGING_SIZE,
    "legends_slim_box": (48, 48),
    "legends_fat_box": (64, 48),
    "legends_album": PACKAGING_SIZE,
}


def absolute_path(path):
    """Pillow also needs extended paths when a Windows checkout has long filenames."""
    full = str(path.resolve())
    if os.name == "nt" and not full.startswith("\\\\?\\"):
        full = "\\\\?\\UNC\\" + full[2:] if full.startswith("\\\\") else "\\\\?\\" + full
    return Path(full)


def read_manifest(folder):
    path = folder / "series.json"
    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    if manifest["key"] != folder.name or not SERIES_KEY.fullmatch(folder.name):
        raise ValueError(f"{path}: key must match the series directory name")
    cards = manifest["cards"]
    if not isinstance(cards, list) or not cards:
        raise ValueError(f"{path}: expected a non-empty cards list")
    return manifest


def read_art_keys(folder, cards):
    art_keys = []
    for card in cards:
        key = card["art"]
        if not isinstance(key, str) or not ART_KEY.fullmatch(key) or key in art_keys:
            raise ValueError(f"{folder / 'series.json'}: invalid or duplicate card art key {key!r}")
        art_keys.append(key)
    return art_keys


def read_packaging(folder, manifest, pack="pack", album="legends_album"):
    packaging = {}
    for name, field, fallback in (("pack", "customPackArt", pack), ("album", "customAlbumArt", album)):
        custom = manifest.get(field, False)
        if not isinstance(custom, bool):
            raise ValueError(f"{folder / 'series.json'}: {field} must be a boolean")
        packaging[name] = f"set_{folder.name}_{name}" if custom else fallback
    return packaging


def export_series(folder, paper, items, wear, art_keys, packaging):
    for art in art_keys:
        source = folder / "Art" / f"{art}.png"
        if not source.is_file():
            raise ValueError(f"series.json: missing card art Art/{art}.png")
        export_card(source, f"card_set_{folder.name}_{art}", paper, items, wear, WEAR_SUFFIX)
    for name, key in packaging.items():
        if key.startswith("set_"):
            export_packaging(folder / f"{name}.png", items / f"{key}.png", PACKAGING_SIZE)


def shared_textures(paper):
    return {
        paper / "legends_album_bound.png": (860, 448),
        paper / "legends_album_sleeve.png": (88, 128),
        paper / "card_legends_wear_0.png": CARD_SIZE,
        paper / "card_legends_wear_1.png": CARD_SIZE,
    }


def expect_card(expected, paper, items, key, wear_suffix, *, separate_faces=False):
    expected[paper / f"{key}.png"] = CARD_SIZE
    expected[items / f"{key}.png"] = ICON_SIZE
    for condition in (0, 1):
        name = f"{key}{wear_suffix}{condition}.png"
        expected[items / name] = ICON_SIZE
        if separate_faces:
            expected[paper / name] = CARD_SIZE


def check_textures(expected):
    for path, size in expected.items():
        if not path.is_file():
            raise ValueError(f"{path}: missing required texture")
        try:
            with Image.open(path) as image:
                if image.format != "PNG" or image.size != size:
                    raise ValueError(f"expected {size[0]} x {size[1]} PNG, got {image.width} x {image.height} {image.format}")
                image.verify()
            with Image.open(path) as image:
                image.load()
        except (OSError, ValueError, SyntaxError, Image.DecompressionBombError) as error:
            raise ValueError(f"{path}: {error}") from error


def check_series(folder, paper, items, shared_paper, shared_items, art_keys, packaging):
    expected = shared_textures(shared_paper)
    for key in packaging.values():
        owner = items if key.startswith("set_") else shared_items
        expected[owner / f"{key}.png"] = PACKAGING_SIZE
    for art in art_keys:
        expect_card(expected, paper, items, f"card_set_{folder.name}_{art}", WEAR_SUFFIX)
    check_textures(expected)


def check_built_ins(series, paper, items):
    night = series / "night_stories"
    legends = series / "station_legends"
    night_manifest = read_manifest(night)
    legends_manifest = read_manifest(legends)
    expected = shared_textures(paper)
    expected.update({items / f"{key}.png": size for key, size in BUILT_IN_PACKAGING.items()})
    for key in read_packaging(night, night_manifest, album="album").values():
        expected[items / f"{key}.png"] = PACKAGING_SIZE
    for key in read_packaging(legends, legends_manifest, pack="legends_slim").values():
        expected[items / f"{key}.png"] = PACKAGING_SIZE
    for card in night_manifest["cards"]:
        expect_card(expected, paper, items, f"card_{card['id']}", "_wear_", separate_faces=True)
    legend_keys = read_art_keys(legends, legends_manifest["cards"])
    # NativeItems.SpriteKey uses an extra illustration for Eleanor's switchable alternate.
    for key in [*legend_keys, "eleanor-rev-oil"]:
        expect_card(expected, paper, items, f"card_legends_{key}", "_wear_")
    check_textures(expected)
    print(f"Built-in card series: verified {len(expected)} textures")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("key", nargs="?", help="series directory name under Series")
    check_mode = parser.add_mutually_exclusive_group()
    check_mode.add_argument("--check", action="store_true", help="check required game textures for one or all expansions")
    check_mode.add_argument("--check-core", action="store_true", help="check built-in and expansion textures for the core mod")
    parser.add_argument("--project", type=Path, default=PROJECT, help="mod project directory")
    parser.add_argument("--base-package", type=Path, help="external mod: read shared art from this installed core package")
    parser.add_argument("--output", type=Path, help="external mod: destination Textures directory in the generated package")
    args = parser.parse_args()
    args.project = absolute_path(args.project)
    if args.check_core and (args.key or args.base_package or args.output):
        parser.error("--check-core checks the entire core project without a series key or external package")
    if bool(args.base_package) != bool(args.output):
        parser.error("for an external mod supply both --base-package and --output")
    if args.base_package:
        textures = absolute_path(args.output)
        shared = absolute_path(args.base_package / "Textures")
        if textures == shared:
            parser.error("the external mod and its prerequisite must use separate texture directories")
        paper_dir = "Paper"
    else:
        textures = shared = args.project / "Textures"
        paper_dir = "Cards"
    shared_paper, shared_items = shared / paper_dir, shared / "Items"
    series = args.project / "Series"
    if not series.is_dir():
        parser.error(f"series directory does not exist: {series}")
    if args.key:
        if not SERIES_KEY.fullmatch(args.key) or (not args.base_package and args.key in BUILT_INS):
            parser.error("expected an expansion key, e.g. dock_demo")
        folders = [series / args.key]
    elif args.check or args.check_core or args.base_package:
        folders = []
        for manifest in sorted(series.rglob("series.json")):
            folder = manifest.parent
            if folder.parent != series:
                parser.error(f"expected Series/<key>/series.json: {manifest}")
            if args.base_package or folder.name not in BUILT_INS:
                folders.append(folder)
    else:
        parser.error("supply the expansion key to export")

    if not folders and not args.check_core:
        if args.base_package:
            parser.error(f"no Series/<key>/series.json found under {args.project}")
        print("No expansion manifests found.")
        return

    folder = series
    try:
        if args.check_core:
            check_built_ins(series, shared_paper, shared_items)
        if args.check or args.check_core:
            for folder in folders:
                manifest = read_manifest(folder)
                art_keys = read_art_keys(folder, manifest["cards"])
                packaging = read_packaging(folder, manifest)
                check_series(folder, textures / paper_dir, textures / "Items", shared_paper, shared_items, art_keys, packaging)
                print(f"{folder.name}: verified {len(art_keys)} card faces, icons and packaging")
            return
        textures.mkdir(parents=True, exist_ok=True)
        # Publish the complete set only after every input and generated texture has been checked.
        with TemporaryDirectory(dir=textures) as scratch:
            paper = Path(scratch) / paper_dir
            items = Path(scratch) / "Items"
            paper.mkdir()
            items.mkdir()
            wear = ([read_rgba(shared_paper / f"card_legends_wear_{i}.png") for i in (0, 1)]
                    if args.base_package else load_wear(shared_paper))
            for folder in folders:
                manifest = read_manifest(folder)
                art_keys = read_art_keys(folder, manifest["cards"])
                packaging = read_packaging(folder, manifest)
                export_series(folder, paper, items, wear, art_keys, packaging)
                check_series(folder, paper, items, shared_paper, shared_items, art_keys, packaging)
                print(f"{folder.name}: verified {len(art_keys)} card faces, icons and packaging")
            for directory in (paper, items):
                destination = textures / directory.name
                destination.mkdir(exist_ok=True)
                for output in directory.iterdir():
                    output.replace(destination / output.name)
    except (OSError, ValueError, KeyError, TypeError, SyntaxError) as error:
        parser.exit(1, f"{folder}: {error}\n")


if __name__ == "__main__":
    main()
