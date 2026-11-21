from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest


SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))
from check_comments import comments


class TokenTests(unittest.TestCase):
    def assert_tokens(self, path, source, expected):
        actual = comments(Path(path), source.encode("utf-8"))
        self.assertEqual([(item.line, item.kind, item.text) for item in actual], expected)

    def test_csharp_raw_verbatim_interpolated_and_inactive_code(self):
        source = '''class C {
    string url = @"https://test/#path";
    string raw = """
        // part of the text
        """;
    string value = $"/* {42} */"; // ordinary
#if NATIVE_VALIDATION
    /// <summary>Public contract</summary>
    int id; /* storage
    order */
#endif
}
'''
        self.assert_tokens("Example.cs", source.replace("\n", "\r\n"), [
            (6, "comment", "// ordinary"), (8, "doc", "/// <summary>Public contract</summary>"),
            (9, "comment", "/* storage\n    order */")])

    def test_python_metadata_and_strings(self):
        source = """#!/usr/bin/env python3
# -*- coding: utf-8 -*-
'''CLI help with # text'''
url = 'https://source/#hello'  # prose
# noqa
"""
        self.assert_tokens("cli.py", source, [(1, "directive", "#!/usr/bin/env python3"),
            (2, "directive", "# -*- coding: utf-8 -*-"), (4, "comment", "# prose"), (5, "comment", "# noqa")])
        self.assert_tokens("cli.py", "x = 1  #! note\n# coding: utf-8",
                           [(1, "comment", "#! note"), (2, "comment", "# coding: utf-8")])

    def test_powershell_here_string_and_directive(self):
        source = """#Requires -Version 7.0
$body = @'
# here-string content
'@
Write-Output https://test/a#b # line
<# first
second #>
"""
        self.assert_tokens("verify.ps1", source, [(1, "directive", "#Requires -Version 7.0"),
            (5, "comment", "# line"), (6, "comment", "<# first\nsecond #>")])

    def test_yaml_scalars_and_xml_cdata(self):
        self.assert_tokens("ci.yml", 'run: |\n  # literal\nnext: "# value" # explanation\n',
                           [(3, "comment", "# explanation")])
        self.assert_tokens("Build.props", "<Project>\n<P><![CDATA[<!-- text -->]]></P>\n<!-- actual -->\n</Project>",
                           [(3, "comment", "<!-- actual -->")])

    def test_config_fragments_and_spdx(self):
        self.assert_tokens(".gitignore", "\\#pattern\nfolder#literal\n# removed\n", [(3, "comment", "# removed")])
        self.assert_tokens("requirements.txt", "pkg @ https://test/#hash # pinned\n", [(1, "comment", "# pinned")])
        self.assert_tokens("Mod.cs", "// SPDX-License-Identifier: MIT OR Apache-2.0\n// TODO: add SPDX notice",
                           [(1, "directive", "// SPDX-License-Identifier: MIT OR Apache-2.0"),
                            (2, "comment", "// TODO: add SPDX notice")])


class BaselineTests(unittest.TestCase):
    def test_git_baseline_tracks_renames_edits_and_new_files(self):
        with TemporaryDirectory() as temp:
            root = Path(temp)

            def git(*args):
                subprocess.run(["git", *args], cwd=root, capture_output=True, check=True)

            def lint(*args, expected):
                result = subprocess.run([sys.executable, str(SCRIPTS / "check_comments.py"), *args],
                                        cwd=root, capture_output=True, encoding="utf-8")
                self.assertEqual(result.returncode, expected, result.stderr + result.stdout)
                return result.stdout

            git("init", "-q")
            original = root / "old.cs"
            source = "class C {\n    // old reason\n    int id;\n}\n"
            original.write_text(source, encoding="utf-8")
            git("add", "old.cs")
            git("-c", "user.name=Test", "-c", "user.email=test@localhost",
                "-c", "commit.gpgSign=false", "commit", "-qm", "baseline")
            git("mv", "old.cs", "renamed.cs")
            current = root / "renamed.cs"
            current.write_text("\n" + source, encoding="utf-8")
            lint("--base", "HEAD", expected=0)
            current.write_text(source.replace("int id;", "int id; // old reason"), encoding="utf-8")
            (root / "新增.py").write_text('"""# Help text"""\n# new prose\n', encoding="utf-8")
            output = lint("--base", "HEAD", expected=1)
            self.assertEqual(output.count("[comment] // old reason"), 1)
            self.assertIn("新增.py:2: [comment] # new prose", output)
            self.assertNotIn("Help text", output)
            current.write_text(source.replace("old reason", "changed reason"), encoding="utf-8")
            self.assertIn("renamed.cs:2: [comment] // changed reason", lint("--base", "HEAD", expected=1))
            self.assertIn("新增.py:2:", lint("--list", expected=0))
            self.assertIn("renamed.cs:2:", lint("--base", "0" * 40, expected=1))
            lint("--base", "no-such-commit", expected=2)


if __name__ == "__main__":
    unittest.main()
