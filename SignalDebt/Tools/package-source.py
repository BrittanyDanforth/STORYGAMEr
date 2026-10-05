"""Package SignalDebt source and small transfer ZIPs, with a verifiable manifest.

The default packages a clean committed revision. Use --working-tree explicitly
to include current edits and nonignored new source files without committing them.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import tempfile
from zipfile import ZIP_DEFLATED, ZipFile


DEFAULT_NAME = 'SignalDebt_Quality_Pass'
PART_BUDGET = 28 * 1024 * 1024
EXCLUDED_DIRECTORIES = {
    '.git', '__pycache__', '.pytest_cache', '.mypy_cache', '.ruff_cache',
    '.cache', 'node_modules', 'saves', 'userdata', 'exports',
}
UNITY_GENERATED = {'Library', 'Temp', 'Logs', 'Obj', 'UserSettings', 'Builds'}


def git(repository, *arguments):
    return subprocess.check_output(['git', '-C', str(repository), *arguments])


def live_environment(path):
    name = path.name.lower()
    return name != '.env.example' and (name == '.env' or name.startswith('.env.'))


def excluded(path):
    if any(part.lower() in EXCLUDED_DIRECTORIES for part in path.parts[:-1]):
        return True
    if len(path.parts) > 1 and path.parts[0] == 'UnityProject':
        if path.parts[1] in UNITY_GENERATED:
            return True
    if path.parts[:2] == ('AI', 'data'):
        return True
    return path.suffix.lower() in {'.pyc', '.pyo', '.sqlite', '.sqlite3', '.db'} or (
        path.name.lower().endswith(('.sqlite-wal', '.sqlite-shm', '.sqlite3-wal', '.sqlite3-shm'))
    )


def working_files(repository, project):
    relative = project.relative_to(repository).as_posix()
    tracked = set(git(repository, 'ls-files', '-z', '--', relative).split(b'\0'))
    candidates = set(git(repository, 'ls-files', '-z', '--cached', '--others',
                         '--exclude-standard', '--', relative).split(b'\0'))
    files = []
    for entry in sorted(candidates - {b''}):
        source = repository / entry.decode('utf-8', errors='surrogateescape')
        path = PurePosixPath(source.relative_to(project).as_posix())
        # Replacing a tracked directory with a symlink must never pull files
        # from elsewhere on the machine into the archive.
        cursor = source
        while cursor != project:
            if cursor.is_symlink():
                raise ValueError('Refusing to package a symlink: ' + str(path))
            cursor = cursor.parent
        if not source.exists():
            continue  # A locally deleted tracked file is not in this snapshot.
        if live_environment(path):
            if entry in tracked:
                raise ValueError('Refusing to package a tracked live environment file: ' + str(path))
            continue
        if excluded(path):
            continue
        if not source.is_file():
            raise ValueError('Expected a regular source file: ' + str(path))
        files.append((source, 'SignalDebt/' + path.as_posix()))
    return files


def validate_member(item):
    path = PurePosixPath(item.filename)
    if path.is_absolute() or '..' in path.parts or path.parts[0] != 'SignalDebt':
        raise ValueError('Unsafe source ZIP path: ' + item.filename)
    if stat.S_ISLNK(item.external_attr >> 16):
        raise ValueError('Refusing to package a symlink: ' + item.filename)
    relative = PurePosixPath(*path.parts[1:])
    if live_environment(relative):
        raise ValueError('Refusing to package a tracked live environment file: ' + item.filename)
    if excluded(relative):
        raise ValueError('Refusing to package tracked generated or local data: ' + item.filename)


def package_source(project, output, name=DEFAULT_NAME, working_tree=False):
    project = project.resolve()
    repository = Path(git(project, 'rev-parse', '--show-toplevel').decode().strip())
    relative = project.relative_to(repository).as_posix()
    dirty = bool(git(repository, 'status', '--porcelain', '--untracked-files=all', '--', relative))
    if dirty and not working_tree:
        raise ValueError('Commit the SignalDebt changes before packaging an exact revision, '
                         'or explicitly use --working-tree for an uncommitted snapshot.')
    commit = git(repository, 'rev-parse', 'HEAD').decode().strip()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*', name):
        raise ValueError('Archive name must contain only letters, numbers, dots, underscores, or hyphens.')
    output = output.resolve()
    if output.is_relative_to(project):
        raise ValueError('Choose an output directory outside the source project.')
    full_name = name + '.zip'
    parts_name = name + '_Parts'
    manifest_name = 'package-manifest.json'
    destinations = [output / child for child in (full_name, parts_name, manifest_name)]
    if any(path.exists() or path.is_symlink() for path in destinations):
        raise ValueError('Output already exists; choose a new output directory to preserve it.')
    candidates = working_files(repository, project) if working_tree else None
    output.mkdir(parents=True, exist_ok=True)
    # A failed validation leaves no misleading final archive or partial manifest.
    with tempfile.TemporaryDirectory(prefix='.signal-debt-package-', dir=output) as temporary:
        staging = Path(temporary)
        full = staging / full_name
        if working_tree:
            with ZipFile(full, 'w', compression=ZIP_DEFLATED, compresslevel=6) as archive:
                for source, destination in candidates:
                    archive.write(source, destination)
        else:
            subprocess.run([
                'git', '-C', str(repository), 'archive', '--format=zip',
                '--prefix=SignalDebt/', '--output=' + str(full), commit + ':' + relative,
            ], check=True)
        parts_dir = staging / parts_name
        parts_dir.mkdir()
        file_report = []
        with ZipFile(full) as source:
            if source.testzip() is not None:
                raise RuntimeError('Full source ZIP failed its CRC check.')
            files = [item for item in source.infolist() if not item.is_dir()]
            for item in sorted(files, key=lambda entry: entry.filename):
                validate_member(item)
                with source.open(item) as stream:
                    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
                file_report.append({'path': item.filename, 'bytes': item.file_size, 'sha256': digest})
            groups = []
            for item in sorted(files, key=lambda entry: entry.compress_size, reverse=True):
                cost = item.compress_size + 2 * len(item.filename.encode()) + 1024
                if cost > PART_BUDGET:
                    raise RuntimeError('One compressed file exceeds the small-ZIP budget: ' + item.filename)
                group = next((group for group in groups if group['bytes'] + cost <= PART_BUDGET), None)
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
            'snapshot_kind': 'working_tree' if working_tree else 'committed_revision',
            'base_git_commit': commit,
            # A working snapshot is not assigned a fabricated commit identity.
            'git_commit': None if working_tree else commit,
            'dirty': dirty,
            'dirty_scope': relative,
            'file_count': len(files),
            'files': file_report,
            'parts_usage': 'Extract all Part ZIPs into the same directory; they contain distinct files.',
            'archives': [],
        }
        for path in [full, *part_paths]:
            with path.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            report['archives'].append({
                'path': str(path.relative_to(staging)), 'bytes': path.stat().st_size, 'sha256': digest,
            })
        (staging / manifest_name).write_text(json.dumps(report, indent=2) + '\n')
        for destination in destinations:
            if destination.exists() or destination.is_symlink():
                raise ValueError('Output appeared during packaging; refusing to overwrite it.')
        for destination in destinations:
            (staging / destination.name).rename(destination)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path('/workspace/exports'))
    parser.add_argument('--name', default=DEFAULT_NAME, help='Archive base name (without .zip).')
    parser.add_argument('--working-tree', action='store_true',
                        help='Package current edits and nonignored new files without committing.')
    args = parser.parse_args()
    try:
        report = package_source(Path(__file__).resolve().parents[1], args.output,
                                args.name, args.working_tree)
    except ValueError as error:
        raise SystemExit(str(error)) from error
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
