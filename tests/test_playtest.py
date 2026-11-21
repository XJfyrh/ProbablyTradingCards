"""Check release checkpoints without launching the game or touching its installation."""
import argparse
import json
from pathlib import Path
import shutil
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

import playtest


MANIFEST = (b"<Manifest><ID>probably_trading_cards</ID><ModVersion>0.7.0</ModVersion>"
            b"<Assemblies><Assembly>ProbablyTradingCards.dll</Assembly></Assemblies></Manifest>\n")
DLL = b"release assembly fixture\n"
LOADER_HASH = "06f67bb900326d602d8781638b2ff9eb5f2719366882980f6c1d46f2b5f02fbe"


class PlaytestTest(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.release = self.root / "release"
        self.installed = self.root / "game" / "Mods" / playtest.MOD
        self.release.mkdir()
        (self.release / "manifest.xml").write_bytes(MANIFEST)
        (self.release / f"{playtest.MOD}.dll").write_bytes(DLL)
        texture = self.release / "Textures" / "Paper" / "card.png"
        texture.parent.mkdir(parents=True)
        texture.write_bytes(b"texture fixture")
        shutil.copytree(self.release, self.installed)
        self.data = self.root / "data"
        self.data.mkdir()
        self.args = argparse.Namespace(
            run=self.root / "run", step="before-buy", game_dir=self.root / "game",
            release_dir=self.release, data_dir=self.data, player_root=self.root / "player",
            player_python=None, delay=0, note="fixture",
        )
        self.publish_audit()

    def publish_audit(self, source="Local", workshop=0, digest=LOADER_HASH, version="0.7.0"):
        (self.data / "Player.log").write_text(
            "[ModLoader] audit: game 0.46D, trust list: none\n"
            f"[ModLoader] audit: {playtest.CORE} v{version} source={source} "
            f"workshop={workshop} owner=0 running=True trust=Updated caps=none hash={digest}\n",
            encoding="utf-8",
        )

    @staticmethod
    def capture(_player_root, _python, path):
        path.write_bytes(b"screenshot fixture")
        return 1234

    def checkpoint(self):
        with patch.object(playtest, "git", return_value="fixture"), \
                patch.object(playtest, "capture", side_effect=self.capture):
            return playtest.checkpoint(self.args)

    def assert_rejected(self, message):
        with patch.object(playtest, "git", return_value="fixture"), \
                patch.object(playtest, "capture", side_effect=self.capture) as capture:
            with self.assertRaisesRegex(ValueError, message):
                playtest.checkpoint(self.args)
            capture.assert_not_called()
        self.assertFalse(self.args.run.exists())

    def test_current_local_package_records_matching_loader_identity(self):
        record = json.loads(self.checkpoint().read_text(encoding="utf-8"))
        self.assertEqual(record["package"]["loaderHash"], LOADER_HASH)
        self.assertEqual(record["startupHash"], LOADER_HASH)
        self.assertEqual(record["startupSource"], "Local")
        self.assertEqual(record["version"], "0.7.0")
        self.assertEqual(record["gamePid"], 1234)

    def test_matching_disk_package_cannot_use_an_older_loaded_assembly(self):
        self.publish_audit(digest="b" * 64)
        self.assert_rejected("指纹")

    def test_matching_fingerprint_from_workshop_is_not_the_local_install(self):
        self.publish_audit(source="Workshop", workshop=3842861920)
        self.assert_rejected("本地")

    def test_local_source_requires_zero_workshop_id(self):
        self.publish_audit(workshop=3842861920)
        self.assert_rejected("本地")

    def test_audit_version_must_match_release_manifest(self):
        self.publish_audit(version="0.6.1")
        self.assert_rejected("版本")

    def test_texture_difference_still_fails_with_matching_loader_hash(self):
        (self.installed / "Textures" / "Paper" / "card.png").write_bytes(b"old texture")
        self.assert_rejected("安装包")

    def test_release_with_unchecked_assemblies_is_rejected(self):
        for folder in (self.release, self.installed):
            (folder / "manifest.xml").write_bytes(MANIFEST.replace(
                b"</Assemblies>", b"<Assembly>Other.dll</Assembly></Assemblies>"))
        self.assert_rejected("程序集")


if __name__ == "__main__":
    unittest.main()
