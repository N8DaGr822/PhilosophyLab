"""Prepare a published Blazor wwwroot for a Pages project or account-root site."""
import argparse
import pathlib
import re
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=pathlib.Path)
parser.add_argument("--base-path", required=True)
args = parser.parse_args()
base = args.base_path
if not re.fullmatch(r"/(?:[A-Za-z0-9._-]+/)*", base):
    parser.error("base path must start and end with / and contain only URL-safe path segments")
if any(part in (".", "..") for part in base.split("/")):
    parser.error("relative path segments are not allowed")
index = args.directory / "index.html"
html, count = re.subn(r'<base href="[^"]*"\s*/?>', f'<base href="{base}" />', index.read_text(encoding="utf-8"))
if count != 1 or not (args.directory / "_framework").is_dir():
    parser.error("expected a published Blazor wwwroot containing one base tag and _framework")
if "#[." in html:
    parser.error("unresolved fingerprint placeholder: use published output, not source wwwroot")
index.write_text(html, encoding="utf-8")
shutil.copyfile(index, args.directory / "404.html")
(args.directory / ".nojekyll").touch()
print(f"Prepared {args.directory} for {base}; deep links use a custom 404 app shell.")
