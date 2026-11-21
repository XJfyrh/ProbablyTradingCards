"""Export game-ready Space Station Legends artwork using Pillow."""

from pathlib import Path
import json

from PIL import Image
from card_textures import export_card, export_packaging, load_wear


PROJECT = Path(__file__).resolve().parents[1] / "ProbablyTradingCards"
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


def export_legends(source, paper, items, wear):
    total = 0
    for folder in ("Standard", "SSR", "UR"):
        root = source / folder
        entries = json.loads((root / "manifest.json").read_text(encoding="utf-8-sig"))
        for entry in entries:
            export_card(root / entry["image"], f"card_legends_{entry['id']}", paper, items, wear)
            total += 1
    if total != 40:
        raise ValueError(f"Expected 40 regular cards, found {total}")
    alternates = sorted((source / "Alternate" / "cards").glob("*.png"))
    if len(alternates) != 12:
        raise ValueError(f"Expected 12 alternate artworks, found {len(alternates)}")
    for path in alternates:
        export_card(path, f"card_legends_{path.stem}", paper, items, wear)
    for condition, layer in enumerate(wear):
        layer.save(paper / f"card_legends_wear_{condition}.png")
    for key, size in PACKAGING.items():
        export_packaging(source / "Packaging" / f"{key}.png", items / f"{key}.png", size)
    for key, size in ALBUM_UI.items():
        with Image.open(source / "Packaging" / f"{key}.png") as original:
            art = original.convert("RGBA")
            bounds = art.getchannel("A").point(lambda value: 255 if value >= 96 else 0).getbbox()
            if not bounds:
                raise ValueError(f"{key} has no foreground")
            art.crop(bounds).resize(size, Image.Resampling.LANCZOS).save(paper / f"{key}.png", optimize=True)


if __name__ == "__main__":
    textures = PROJECT / "Textures"
    paper = textures / "Cards"
    export_legends(PROJECT / "Art" / "StationLegends", paper, textures / "Items", load_wear(paper))
