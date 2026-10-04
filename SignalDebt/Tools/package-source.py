"""Package a committed SignalDebt source revision, including small transfer ZIPs."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess
from zipfile import ZIP_DEFLATED, ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path('/workspace/exports'))
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    repository = Path(subprocess.check_output(
        ['git', '-C', str(project), 'rev-parse', '--show-toplevel'], text=True
    ).strip())
    relative = project.relative_to(repository).as_posix()
    dirty = subprocess.check_output(
        ['git', '-C', str(repository), 'status', '--porcelain', '--', relative], text=True
    )
    if dirty:
        raise SystemExit('Commit the SignalDebt changes before packaging an exact revision.')
    commit = subprocess.check_output(
        ['git', '-C', str(repository), 'rev-parse', 'HEAD'], text=True
    ).strip()
    output = args.output.resolve()
    if output.is_relative_to(project):
        raise SystemExit('Choose an output directory outside the source project.')
    output.mkdir(parents=True, exist_ok=True)
    full = output / 'SignalDebt_Quality_Pass.zip'
    parts_dir = output / 'SignalDebt_Quality_Pass_Parts'
    if full.exists() or parts_dir.exists():
        raise SystemExit('Output already exists; choose a new output directory to preserve it.')
    subprocess.run([
        'git', '-C', str(repository), 'archive', '--format=zip',
        '--prefix=SignalDebt/', '--output=' + str(full), commit + ':' + relative,
    ], check=True)
    parts_dir.mkdir()
    budget = 28 * 1024 * 1024
    with ZipFile(full) as source:
        if source.testzip() is not None:
            raise RuntimeError('Full source ZIP failed its CRC check.')
        files = [item for item in source.infolist() if not item.is_dir()]
        groups = []
        for item in sorted(files, key=lambda entry: entry.compress_size, reverse=True):
            cost = item.compress_size + 2 * len(item.filename.encode()) + 1024
            if cost > budget:
                raise RuntimeError('One compressed file exceeds the small-ZIP budget: ' + item.filename)
            group = next((group for group in groups if group['bytes'] + cost <= budget), None)
            if group is None:
                group = {'bytes': 0, 'files': []}
                groups.append(group)
            group['bytes'] += cost
            group['files'].append(item)
        part_paths = []
        for number, group in enumerate(groups, 1):
            path = parts_dir / f'Part{number}.zip'
            with ZipFile(path, 'w', compression=ZIP_DEFLATED, compresslevel=6) as part:
                for item in sorted(group['files'], key=lambda entry: entry.filename):
                    part.writestr(copy.copy(item), source.read(item),
                                  compress_type=ZIP_DEFLATED, compresslevel=6)
            if path.stat().st_size >= 32 * 1024 * 1024:
                raise RuntimeError('Transfer ZIP exceeds 32 MiB: ' + str(path))
            with ZipFile(path) as part:
                if part.testzip() is not None:
                    raise RuntimeError('Transfer ZIP failed its CRC check: ' + str(path))
            part_paths.append(path)
    report = {
        'git_commit': commit, 'file_count': len(files),
        'parts_usage': 'Extract all Part ZIPs into the same directory; they contain distinct files.',
        'archives': [],
    }
    for path in [full, *part_paths]:
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        report['archives'].append({
            'path': str(path.relative_to(output)), 'bytes': path.stat().st_size, 'sha256': digest,
        })
    (output / 'package-manifest.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
