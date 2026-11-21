"""Game textures shared by the Legends and expansion artwork exporters."""

from PIL import Image


CARD_SIZE = (512, 768)
ICON_SIZE = (16, 32)
PACKAGING_SIZE = (32, 48)


def read_rgba(path):
    with Image.open(path) as source:
        return source.convert("RGBA")


def load_wear(paper):
    return [read_rgba(paper / name) for name in ("wear_creased.png", "wear_light.png")]


def export_card(path, key, paper, items, wear, wear_suffix="_wear_"):
    art = read_rgba(path).resize(CARD_SIZE, Image.Resampling.LANCZOS)
    art.save(paper / f"{key}.png", optimize=True)
    for condition in (0, 1, 2):
        composite = Image.alpha_composite(art, wear[condition]) if condition < 2 else art
        icon = Image.new("RGBA", ICON_SIZE)
        icon.alpha_composite(composite.resize((16, 24), Image.Resampling.LANCZOS), (0, 4))
        name = key if condition == 2 else f"{key}{wear_suffix}{condition}"
        icon.save(items / f"{name}.png")


def export_packaging(path, destination, size):
    sprite = read_rgba(path)
    bounds = sprite.getchannel("A").point(lambda value: 255 if value >= 96 else 0).getbbox()
    if not bounds:
        raise ValueError(f"{path} has no foreground")
    sprite = sprite.crop(bounds)
    sprite.thumbnail((size[0] - 2, size[1] - 2), Image.Resampling.LANCZOS)
    alpha = sprite.getchannel("A").point(lambda value: 255 if value >= 110 else 0)
    if not alpha.getbbox():
        raise ValueError(f"{path} has no visible foreground at inventory size")
    sprite.putalpha(alpha)
    output = Image.new("RGBA", size)
    output.alpha_composite(sprite, ((size[0] - sprite.width) // 2, (size[1] - sprite.height) // 2))
    output.save(destination)
