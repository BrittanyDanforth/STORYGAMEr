#!/usr/bin/env bash
set -euo pipefail
# Official Blender 5.2.2 LTS, verified 2026-10-04 against blender.org.
# Requires Python 3.12+ with TLS certificates and xz support. No sudo required.
python3 - <<'PY'
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import tarfile
import tempfile
import urllib.parse
import urllib.request

VERSION = '5.2.2'
FILENAME = f'blender-{VERSION}-linux-x64.tar.xz'
BASE = 'https://download.blender.org/release/Blender5.2/'
EXPECTED = '84098912789dc450e95697c4184fb8a90acbe5111c2ba4aede3fecb57806a168'
UA = 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36'
SETUP = Path('/workspace/SignalDebt-setup')
CACHE = SETUP / 'downloads'
OPT = Path('/workspace/.local/opt')
BINDIR = Path('/workspace/.local/bin')
NAME = f'blender-{VERSION}-linux-x64'
DEST = OPT / NAME

if platform.machine() != 'x86_64':
    raise SystemExit('This pinned Blender archive requires Linux x86_64.')
if not hasattr(tarfile, 'data_filter'):
    raise SystemExit('Python with tarfile.data_filter is required for safe extraction.')
for directory in (CACHE, OPT, BINDIR):
    directory.mkdir(parents=True, exist_ok=True)

class OfficialRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        parsed = urllib.parse.urlparse(newurl)
        if parsed.scheme != 'https' or parsed.hostname != 'download.blender.org':
            raise RuntimeError(f'Unexpected download redirect: {parsed.scheme}://{parsed.hostname}')
        return super().redirect_request(req, fp, code, msg, headers, newurl)

opener = urllib.request.build_opener(OfficialRedirects())
def request(url):
    return opener.open(urllib.request.Request(url, headers={'User-Agent': UA}), timeout=45)

checksum_url = BASE + f'blender-{VERSION}.sha256'
with request(checksum_url) as response:
    checksum_text = response.read(65536).decode('ascii')
matches = [line.split()[0] for line in checksum_text.splitlines()
           if len(line.split()) == 2 and line.split()[1].lstrip('*') == FILENAME]
if matches != [EXPECTED]:
    raise SystemExit('Official checksum did not match the reviewed pinned checksum; stopping.')
checksum_path = CACHE / f'blender-{VERSION}.sha256'
if checksum_path.exists() and checksum_path.read_text() != checksum_text:
    raise SystemExit(f'Preserving unexpected existing checksum file: {checksum_path}')
checksum_path.write_text(checksum_text)

archive = CACHE / FILENAME
if not archive.exists():
    fd, partial_name = tempfile.mkstemp(prefix=FILENAME + '.', suffix='.part', dir=CACHE)
    partial = Path(partial_name)
    try:
        with os.fdopen(fd, 'wb') as output, request(BASE + FILENAME) as response:
            size = 0
            report_at = 32 * 1024 * 1024
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
                size += len(chunk)
                if size >= report_at:
                    print(f'Downloaded {size // (1024 * 1024)} MiB', flush=True)
                    report_at += 32 * 1024 * 1024
        with partial.open('rb') as downloaded:
            actual = hashlib.file_digest(downloaded, 'sha256').hexdigest()
        if actual != EXPECTED:
            raise RuntimeError(f'Archive SHA256 mismatch: {actual}')
        partial.replace(archive)
    finally:
        if partial.exists():
            partial.unlink()
with archive.open('rb') as file:
    actual = hashlib.file_digest(file, 'sha256').hexdigest()
if actual != EXPECTED:
    raise SystemExit(f'Existing cached archive checksum mismatch; preserving it without execution: {archive}')
print(f'Official archive SHA256 verified: {actual}', flush=True)

marker = DEST / '.signaldebt-install.json'
if DEST.exists():
    if not marker.is_file() or json.loads(marker.read_text()).get('sha256') != EXPECTED:
        raise SystemExit(f'Preserving an unrecognized existing installation: {DEST}')
else:
    stage = Path(tempfile.mkdtemp(prefix='.blender-extract-', dir=OPT))
    try:
        with tarfile.open(archive, 'r:xz') as tar:
            members = tar.getmembers()
            if any(m.name.split('/')[0] != NAME for m in members):
                raise RuntimeError('Unexpected archive root.')
            tar.extractall(stage, members=members, filter='data')
        extracted = stage / NAME
        evidence = {
            'version': VERSION, 'channel': 'LTS stable',
            'official_download_page': 'https://www.blender.org/download/',
            'official_release_index': BASE,
            'archive_url': BASE + FILENAME, 'checksum_url': checksum_url,
            'sha256': EXPECTED, 'release_date': '2026-09-15',
            'verified_date': '2026-10-04',
        }
        (extracted / '.signaldebt-install.json').write_text(json.dumps(evidence, indent=2) + '\n')
        extracted.rename(DEST)
    finally:
        shutil.rmtree(stage)

link = BINDIR / 'blender'
target = DEST / 'blender'
if link.is_symlink():
    if link.resolve() != target.resolve():
        raise SystemExit(f'Preserving existing Blender symlink: {link}')
elif link.exists():
    raise SystemExit(f'Preserving existing Blender command: {link}')
else:
    link.symlink_to(target)
print(f'Installed {target}', flush=True)
print('Use export PATH=/workspace/.local/bin:$PATH', flush=True)
PY
/workspace/.local/bin/blender --version
