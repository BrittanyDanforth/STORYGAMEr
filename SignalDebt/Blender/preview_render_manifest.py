"""Local source-to-render provenance for offline animation previews."""
from pathlib import Path
import hashlib
import json


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def finish_render(source, script, frames, target, starting_hash, settings):
    source = Path(source).resolve()
    script = Path(script).resolve()
    if sha256(source) != starting_hash:
        raise ValueError('Animation source changed during rendering')
    frames = [Path(path).resolve() for path in frames]
    for path in frames:
        if path.stat().st_mtime < source.stat().st_mtime:
            raise ValueError(f'Render predates source: {path.name}')
    root = source.parents[1]
    data = {
        'source': str(source.relative_to(root)),
        'source_sha256': starting_hash,
        'render_script': str(script.relative_to(root)),
        'render_script_sha256': sha256(script),
        'frame_count': len(frames),
        'frame_sha256': {str(path.relative_to(root)): sha256(path) for path in frames},
        'render_settings': settings,
        'source_unchanged_during_render': True,
    }
    target = Path(target)
    pending = target.with_suffix('.pending.json')
    pending.write_text(json.dumps(data, indent=2) + '\n')
    pending.replace(target)


def verify_render_manifest(path, source, frames):
    data = json.loads(Path(path).read_text())
    root = Path(source).parents[1]
    if data['source_sha256'] != sha256(source):
        raise ValueError(f'Render manifest has stale source: {Path(path).name}')
    script = root / data['render_script']
    if data['render_script_sha256'] != sha256(script):
        raise ValueError(f'Render script changed after capture: {script.name}')
    expected = {str(Path(frame).relative_to(root)): sha256(frame) for frame in frames}
    if data['frame_sha256'] != expected or data['frame_count'] != len(expected):
        raise ValueError(f'Render frame hashes do not match: {Path(path).name}')
    return data
