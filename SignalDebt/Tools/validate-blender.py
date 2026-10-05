"""Local smoke test: CPU render, native save and FBX export, using a factory cube."""
import bpy
import json
from pathlib import Path
import tempfile

base = Path('/workspace/SignalDebt-setup/validation')
base.mkdir(parents=True, exist_ok=True)
output = Path(tempfile.mkdtemp(prefix='blender-', dir=base))
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 8
scene.cycles.use_denoising = False
scene.render.resolution_x = 128
scene.render.resolution_y = 128
scene.render.resolution_percentage = 100
scene.render.threads_mode = 'FIXED'
scene.render.threads = 4
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(output / 'cpu-render.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output / 'smoke.blend'))
bpy.ops.object.select_all(action='DESELECT')
cube = bpy.data.objects['Cube']
cube.select_set(True)
bpy.context.view_layer.objects.active = cube
bpy.ops.export_scene.fbx(filepath=str(output / 'smoke.fbx'), use_selection=True,
                         add_leaf_bones=False, bake_anim=False)
for filename in ('cpu-render.png', 'smoke.blend', 'smoke.fbx'):
    if (output / filename).stat().st_size <= 0:
        raise RuntimeError(f'Empty output: {filename}')
report = {'blender_version': bpy.app.version_string, 'engine': 'CYCLES',
          'device': 'CPU', 'samples': 8, 'resolution': [128, 128],
          'native_save': 'passed', 'fbx_export': 'passed', 'render': 'passed',
          'output_directory': str(output)}
(output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
print('SIGNALDEBT_BLENDER_VALIDATION ' + json.dumps(report))
