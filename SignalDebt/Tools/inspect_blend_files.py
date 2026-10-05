"""Inspect project data without executing embedded Blender scripts."""
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

import bpy

args = sys.argv[sys.argv.index('--') + 1:]
root, output = map(Path, args)
report = {
    'recorded_utc': datetime.now(timezone.utc).isoformat(),
    'blender_version': bpy.app.version_string,
    'auto_execute_scripts_enabled': bpy.context.preferences.filepaths.use_scripts_auto_execute,
    'scope': 'Data inspection only; no embedded script execution. This is not a malware-free guarantee.',
    'files': [],
}
for name in ['SignalDebt_Apartment.blend', 'Characters.blend', 'Character_Animation_Showcase.blend']:
    path = root / 'Blender' / name
    bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    drivers = []
    for group in ('objects', 'scenes', 'materials', 'worlds', 'shape_keys'):
        for block in getattr(bpy.data, group):
            animation = getattr(block, 'animation_data', None)
            if animation:
                for curve in animation.drivers:
                    drivers.append({'owner': block.name, 'kind': curve.driver.type})
    report['files'].append({
        'path': str(path.relative_to(root)),
        'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'objects': len(bpy.data.objects),
        'meshes': len(bpy.data.meshes),
        'armatures': len(bpy.data.armatures),
        'actions': len(bpy.data.actions),
        'text_blocks': [{'name': text.name, 'auto_run': text.use_module} for text in bpy.data.texts],
        'drivers': drivers,
        'linked_libraries': len(bpy.data.libraries),
        'packed_images': sum(bool(image.packed_file) for image in bpy.data.images),
        'external_images': sum(bool(image.filepath) and not image.packed_file for image in bpy.data.images),
    })
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
