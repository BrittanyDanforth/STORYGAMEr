"""Actual local police mesh previews; no external assets or executable content."""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Blender/Characters.blend'
OUT=ROOT/'Blender/Renders/CharacterDetails';OUT.mkdir(parents=True,exist_ok=True)
source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
scene=bpy.context.scene
scene.cycles.samples=64;scene.cycles.use_denoising=False
scene.render.resolution_x=1440;scene.render.resolution_y=1440;scene.render.resolution_percentage=100
cam=scene.camera;cam.data.type='ORTHO'
images={}
for name in ['Patrol_Officer','SWAT_Officer']:
    rig=bpy.data.objects[name+'_Rig'];rig.location=(0,0,0)
    for o in bpy.data.objects:
        if o.type in {'MESH','FONT'}:o.hide_render=not(o.parent==rig)
    bpy.context.view_layer.update()
    for view,position,target,size in [('UpperBody',(1.4,-5,2.0),(0,0,1.30),1.36),('Equipment',(4,-2.7,2.3),(0,0,.98),2.00)]:
        cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=size
        output=OUT/f'{name}_{view}.png';scene.render.filepath=str(output)
        bpy.ops.render.render(write_still=True)
        images[output.name]={'sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'bytes':output.stat().st_size}
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_hash
(OUT/'character_detail_manifest.json').write_text(json.dumps({'source_blend_sha256':source_hash,'preview':'Actual rest character meshes rendered in Blender; pose and collision gates are separately recorded.','renders':images},indent=2))
print('POLICE_CHARACTER_DETAILS_RENDERED',json.dumps(images))
