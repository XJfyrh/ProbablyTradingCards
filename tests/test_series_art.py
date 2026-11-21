"""Exercise the artwork command for the core mod and its author template."""

import json
from pathlib import Path
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
EXAMPLE = ROOT / "Examples" / "DockLetters" / "Series" / "dock_demo"
TEXTURES = ROOT / "ProbablyTradingCards" / "Textures"


class SeriesArtTest(unittest.TestCase):
    @unittest.skipUnless(sys.platform == "win32", "Night Stories uses the Windows System.Drawing exporter")
    def test_changed_box_art_requires_a_new_inventory_icon(self):
        with TemporaryDirectory() as temporary:
            project = Path(temporary)
            for folder in ("Art", "Series", "Textures"):
                shutil.copytree(TEXTURES.parent / folder, project / folder)
            source = project / "Art/StationLegends/Packaging/legends_fat_box.png"
            with Image.open(source) as original:
                flipped = original.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            flipped.save(source)
            icon = project / "Textures/Items/legends_fat_box.png"
            before = icon.read_bytes()
            result = subprocess.run([sys.executable, str(ROOT / "scripts/check_art_exports.py"),
                                     "--project", str(project)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn(icon.name, result.stderr)
            self.assertIn("differs from the artwork export", result.stderr)
            self.assertEqual(icon.read_bytes(), before)

    def test_core_checks_built_in_and_expansion_textures(self):
        with TemporaryDirectory() as temporary:
            project = Path(temporary)
            shutil.copytree(TEXTURES.parent / "Series", project / "Series")
            shutil.copytree(TEXTURES, project / "Textures")
            shutil.copytree(EXAMPLE, project / "Series" / "dock_demo")
            cmd = [sys.executable, str(ROOT / "scripts" / "export_series.py"), "--project", str(project)]
            subprocess.run([*cmd, "dock_demo"], check=True)
            subprocess.run([*cmd, "--check-core"], check=True)

            for relative, damage, detail in (
                ("Cards/card_0_wear_1.png", None, "missing required texture"),
                ("Cards/card_legends_eleanor-rev-oil.png", b"broken", "cannot identify image file"),
                ("Items/legends_fat_box.png", (32, 48), "expected 64 x 48 PNG"),
                ("Items/legends_fat_box.png", (64, 48), "no visible foreground"),
                ("Items/card_set_dock_demo_ticket--wear-1.png", None, "missing required texture"),
            ):
                with self.subTest(texture=relative):
                    path = project / "Textures" / relative
                    original = path.read_bytes()
                    if damage is None:
                        path.unlink()
                    elif isinstance(damage, bytes):
                        path.write_bytes(damage)
                    else:
                        Image.new("RGBA", damage).save(path)
                    result = subprocess.run([*cmd, "--check-core"], capture_output=True, text=True)
                    self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                    self.assertIn(path.name, result.stderr)
                    self.assertIn(detail, result.stderr)
                    path.write_bytes(original)

    def test_author_workflow(self):
        with TemporaryDirectory() as temporary:
            project = Path(temporary)
            series = project / "Series" / "dock_demo"
            shutil.copytree(EXAMPLE, series)
            art = series / "Art"
            paper = project / "Textures" / "Cards"
            items = project / "Textures" / "Items"
            paper.mkdir(parents=True)
            items.mkdir()
            for name in ("wear_creased", "wear_light", "card_legends_wear_0", "card_legends_wear_1",
                         "legends_album_bound", "legends_album_sleeve"):
                shutil.copyfile(TEXTURES / "Cards" / f"{name}.png", paper / f"{name}.png")
            for name in ("pack", "legends_album"):
                shutil.copyfile(TEXTURES / "Items" / f"{name}.png", items / f"{name}.png")

            def run(*arguments, success=True):
                result = subprocess.run(
                    [sys.executable, str(ROOT / "scripts" / "export_series.py"), "--project", str(project), *arguments],
                    capture_output=True, text=True,
                )
                self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
                return result

            run("dock_demo")
            run("--check")
            self.assertEqual(len(list(paper.glob("card_set_dock_demo_*.png"))), 2)
            self.assertEqual(len(list(items.glob("card_set_dock_demo_*.png"))), 6)
            self.assertFalse(list(items.glob("set_dock_demo_*.png")))
            with Image.open(art / "courier.png") as source, Image.open(paper / "card_set_dock_demo_courier.png") as final:
                self.assertEqual(source.convert("RGBA").tobytes(), final.convert("RGBA").tobytes())
            wear_icon = items / "card_set_dock_demo_ticket--wear-1.png"
            wear_icon.unlink()
            self.assertIn(wear_icon.name, run("--check", success=False).stderr)

            manifest = series / "series.json"
            data = json.loads(manifest.read_text(encoding="utf-8"))
            data.update(customPackArt=True, customAlbumArt=True)
            manifest.write_text(json.dumps(data), encoding="utf-8")
            shutil.copyfile(items / "pack.png", series / "pack.png")
            self.assertIn("album.png", run("dock_demo", success=False).stderr)
            self.assertFalse(wear_icon.exists(), "A missing source image partially overwrote the previous export")
            shutil.copyfile(items / "legends_album.png", series / "album.png")
            faint = Image.new("RGBA", (512, 512), (255, 32, 0, 100))
            faint.putpixel((0, 0), (255, 32, 0, 255))
            faint.save(series / "pack.png")
            message = run("dock_demo", success=False).stderr
            self.assertIn("pack.png", message)
            self.assertIn("no visible foreground", message)
            shutil.copyfile(items / "pack.png", series / "pack.png")
            run("dock_demo")
            for name in ("set_dock_demo_pack", "set_dock_demo_album"):
                self.assertTrue((items / f"{name}.png").is_file())
            run("dock_demo", "--check")
            (project / "Series" / "references").mkdir()
            run("--check")
            nested = series / "archived" / "series.json"
            nested.parent.mkdir()
            shutil.copyfile(manifest, nested)
            self.assertIn(str(nested.relative_to(project)), run("--check", success=False).stderr)
            rendered_pack = items / "set_dock_demo_pack.png"
            Image.new("RGBA", (32, 48)).save(rendered_pack)
            message = run("dock_demo", "--check", success=False).stderr
            self.assertIn(rendered_pack.name, message)
            self.assertIn("no visible foreground", message)

    def test_external_package_owns_only_its_art(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            project = root / "author"
            shutil.copytree(EXAMPLE, project / "Series" / "dock_demo")
            base = root / "base"
            shared = base / "Textures" / "Paper"
            shared.mkdir(parents=True)
            (base / "Textures" / "Items").mkdir()
            for key in ("card_legends_wear_0", "card_legends_wear_1", "legends_album_bound", "legends_album_sleeve"):
                shutil.copyfile(TEXTURES / "Cards" / f"{key}.png", shared / f"{key}.png")
            for key in ("pack", "legends_album"):
                shutil.copyfile(TEXTURES / "Items" / f"{key}.png", base / "Textures" / "Items" / f"{key}.png")
            textures = root / "package" / "Textures"
            cmd = [sys.executable, str(ROOT / "scripts" / "export_series.py"), "--project", str(project),
                   "--base-package", str(base), "--output", str(textures)]
            subprocess.run(cmd, check=True)
            subprocess.run([*cmd, "--check"], check=True)
            self.assertEqual(sorted(path.name for path in (textures / "Paper").glob("*.png")),
                             ["card_set_dock_demo_courier.png", "card_set_dock_demo_ticket.png"])
            icons = list((textures / "Items").glob("*.png"))
            self.assertEqual(len(icons), 6)
            self.assertTrue(all(icon.name.startswith("card_set_dock_demo_") for icon in icons))

            peer = project / "Series" / "dock_demo_courier_wear"
            (peer / "Art").mkdir(parents=True)
            data = json.loads((EXAMPLE / "series.json").read_text(encoding="utf-8"))
            data.update(key=peer.name, cards=[dict(data["cards"][0], art="0")], slots=[{"C": 1}])
            (peer / "series.json").write_text(json.dumps(data), encoding="utf-8")
            shutil.copyfile(EXAMPLE / "Art" / "ticket.png", peer / "Art" / "0.png")
            subprocess.run(cmd, check=True)
            subprocess.run([*cmd, "--check"], check=True)
            self.assertEqual(len(list((textures / "Items").glob("*.png"))), 9)
            self.assertTrue((textures / "Items" / "card_set_dock_demo_courier_wear_0.png").is_file())
            self.assertTrue((textures / "Items" / "card_set_dock_demo_courier--wear-0.png").is_file())
            (shared / "card_legends_wear_1.png").unlink()
            check_missing = subprocess.run([*cmd, "--check"], capture_output=True, text=True)
            self.assertNotEqual(check_missing.returncode, 0)
            self.assertIn("card_legends_wear_1.png", check_missing.stderr)


if __name__ == "__main__":
    unittest.main()
