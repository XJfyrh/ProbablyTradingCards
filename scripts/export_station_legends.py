"""Export game-ready Space Station Legends artwork using Pillow."""

from pathlib import Path
import json

from PIL import Image
from card_textures import export_card, export_packaging, load_wear


PROJECT = Path(__file__).resolve().parents[1] / "ProbablyTradingCards"
SOURCE = PROJECT / "Art" / "StationLegends"
PAPER = PROJECT / "Textures" / "Cards"
ITEMS = PROJECT / "Textures" / "Items"
PACKAGING = {
    "legends_slim": (32, 48),
    "legends_fat": (32, 48),
    "legends_slim_box": (48, 48),
    "legends_fat_box": (64, 48),
    "legends_album": (32, 48),
}
ALBUM_UI = {
    "legends_album_bound": (860, 448),
    "legends_album_sleeve": (88, 128),
}


def export_alternates(wear):
    cards = sorted((SOURCE / "Alternate" / "cards").glob("*.png"))
    if len(cards) != 12:
        raise ValueError(f"Expected 12 alternate artworks, found {len(cards)}")
    for path in cards:
        export_card(path, f"card_legends_{path.stem}", PAPER, ITEMS, wear)


def main():
    wear = load_wear(PAPER)
    total = 0
    for folder in ("Standard", "SSR", "UR"):
        root = SOURCE / folder
        entries = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
        for entry in entries:
            export_card(root / entry["image"], f"card_legends_{entry['id']}", PAPER, ITEMS, wear)
            total += 1
    if total != 40:
        raise ValueError(f"Expected 40 regular cards, found {total}")
    export_alternates(wear)
    for condition, layer in enumerate(wear):
        layer.save(PAPER / f"card_legends_wear_{condition}.png")
    for key, size in PACKAGING.items():
        export_packaging(SOURCE / "Packaging" / f"{key}.png", ITEMS / f"{key}.png", size)
    for key, size in ALBUM_UI.items():
        with Image.open(SOURCE / "Packaging" / f"{key}.png") as source:
            art = source.convert("RGBA")
            bounds = art.getchannel("A").point(lambda value: 255 if value >= 96 else 0).getbbox()
            if not bounds:
                raise ValueError(f"{key} has no foreground")
            art.crop(bounds).resize(size, Image.Resampling.LANCZOS).save(PAPER / f"{key}.png", optimize=True)


if __name__ == "__main__":
    main()
