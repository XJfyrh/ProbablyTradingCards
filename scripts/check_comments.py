"""List source comments or reject new and edited prose comments against a Git baseline."""

import argparse
from collections import Counter
from dataclasses import dataclass
from functools import cache
import io
import os
from pathlib import Path
import re
import subprocess
import sys
import tokenize
from xml.parsers import expat

from tree_sitter import Language, Parser, Query, QueryCursor
import tree_sitter_c_sharp
import tree_sitter_powershell
import tree_sitter_yaml


GRAMMARS = {
    ".cs": tree_sitter_c_sharp,
    ".ps1": tree_sitter_powershell,
    ".psd1": tree_sitter_powershell,
    ".psm1": tree_sitter_powershell,
    ".yaml": tree_sitter_yaml,
    ".yml": tree_sitter_yaml,
}
XML = {".csproj", ".props", ".targets", ".xml"}


@dataclass(frozen=True)
class Comment:
    line: int
    text: str
    kind: str


def supported(path):
    return (path.suffix.lower() in GRAMMARS or path.suffix.lower() in XML | {".py"}
            or path.name == ".gitignore" or path.match("requirements*.txt"))


@cache
def comment_parser(grammar):
    language = Language(grammar.language())
    return Parser(language), Query(language, "(comment) @comment")


def tokens(path, data):
    suffix = path.suffix.lower()
    if suffix in GRAMMARS:
        parser, query = comment_parser(GRAMMARS[suffix])
        nodes = QueryCursor(query).captures(parser.parse(data).root_node).get("comment", [])
        for node in sorted(nodes, key=lambda n: n.start_byte):
            yield node.start_point.row + 1, node.text.decode("utf-8")
    elif suffix == ".py":
        for token in tokenize.tokenize(io.BytesIO(data).readline):
            if token.type == tokenize.COMMENT:
                yield token.start[0], token.string
    elif suffix in XML:
        parser = expat.ParserCreate()
        found = []
        parser.CommentHandler = lambda text: found.append((parser.CurrentLineNumber, "<!--" + text + "-->"))
        parser.Parse(data, True)
        yield from found
    else:
        for number, line in enumerate(data.decode("utf-8-sig").splitlines(), 1):
            if path.name == ".gitignore":
                if line.startswith("#"):
                    yield number, line
            else:
                match = re.search(r"(?<!\S)#.*", line)
                if match:
                    yield number, match.group()


def kind(path, line, text, header):
    suffix = path.suffix.lower()
    if suffix == ".py":
        if (line <= 2 and re.match(rb"^[ \t\f]*#.*?coding[:=][ \t]*[-\w.]+", header[line - 1])
                and (line == 1 or re.match(rb"^[ \t\f]*(?:#|$)", header[0].rstrip(b"\r")))):
            return "directive"
    if suffix == ".py" or suffix in {".ps1", ".psd1", ".psm1"}:
        if line == 1 and header[0].startswith(b"#!"):
            return "directive"
    if suffix in {".ps1", ".psd1", ".psm1"} and re.match(
            r"#requires\s+-(Version|Modules|PSEdition|RunAsAdministrator|ShellId|PSSnapin|Assembly)\b", text, re.I):
        return "directive"
    for prefix, ending in (("<!--", "-->"), ("/*", "*/"), ("<#", "#>"), ("//", ""), ("#", "")):
        if text.startswith(prefix):
            body = text.removeprefix(prefix).removesuffix(ending).strip()
            if re.fullmatch(r"SPDX-(License-Identifier: [\w.+()-]+(?: (?:AND|OR|WITH) [\w.+()-]+)*"
                            r"|FileCopyrightText: [^\r\n]+)", body):
                return "directive"
            break
    if suffix == ".cs" and ((text.startswith("///") and not text.startswith("////"))
                            or (text.startswith("/**") and not text.startswith("/***"))):
        return "doc"
    return "comment"


def comments(path, data):
    header = data.removeprefix(b"\xef\xbb\xbf").split(b"\n", 2)
    return [Comment(line, text.replace("\r\n", "\n"), kind(path, line, text, header))
            for line, text in tokens(path, data)]


def git(root, *args):
    return subprocess.check_output(["git", *args], cwd=root)


def git_fields(root, *args):
    return [os.fsdecode(name) for name in git(root, *args).split(b"\0") if name]


def working_paths(root):
    return sorted(set(git_fields(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")))


def changed_paths(root, base):
    changes = {}
    fields = iter(git_fields(root, "diff", "--name-status", "-z", "--find-renames", base, "--"))
    for status in fields:
        name = next(fields)
        if status.startswith("R"):
            changes[next(fields)] = name
        elif status != "D":
            changes[name] = None if status in {"A", "T"} else name
    for name in git_fields(root, "ls-files", "-z", "--others", "--exclude-standard"):
        changes.setdefault(name, None)
    return changes


def check(root, base, inventory):
    if inventory or re.fullmatch(r"0{40}(?:0{24})?", base):
        paths = dict.fromkeys(working_paths(root))
    else:
        base = git(root, "rev-parse", "--verify", "--end-of-options", base + "^{tree}").decode().strip()
        paths = changed_paths(root, base)

    violations = 0
    for name, previous in sorted(paths.items()):
        path = root / name
        if not supported(path) or path.is_symlink() or not path.is_file():
            continue
        try:
            baseline = Counter()
            if previous and supported(Path(previous)):
                old = git(root, "show", base + ":" + previous)
                baseline.update(c.text for c in comments(Path(previous), old) if c.kind != "directive")
            for item in comments(path, path.read_bytes()):
                if not inventory:
                    if item.kind == "directive":
                        continue
                    if baseline[item.text]:
                        baseline[item.text] -= 1
                        continue
                    violations += 1
                print(f"{name}:{item.line}: [{item.kind}] {item.text.replace(chr(10), ' ⏎ ')}")
        except (SyntaxError, tokenize.TokenError, UnicodeError, expat.ExpatError) as error:
            raise ValueError(f"{name}: {error}") from error

    if violations:
        print(f"{violations} new or edited comment(s). Review with the delete-comment skill.", file=sys.stderr)
    elif not inventory:
        print("PASS: no new or edited prose comments.")
    return bool(violations)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--list", action="store_true", help="list all comments in the working tree, including directives")
    mode.add_argument("--base", default="HEAD", help="compare with this Git revision (default: HEAD)")
    args = parser.parse_args()
    try:
        root = Path(os.fsdecode(git(Path.cwd(), "rev-parse", "--show-toplevel")).rstrip("\r\n"))
        return check(root, args.base, args.list)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(2, f"{error}\n")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
