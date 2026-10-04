"""Refresh proof renders without changing the frozen animation source or FBXs."""
import bpy
import sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Blender/Renders/Animation'
sys.path.insert(0,str(Path(__file__).resolve().parent))
from preview_render_manifest import sha256, finish_render
SOURCE=ROOT/'Blender/Character_Animation_Showcase.blend'
source_hash=sha256(SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
scene=bpy.context.scene;swat=bpy.data.objects['SWAT_Officer_Rig'];patrol=bpy.data.objects['Patrol_Officer_Rig']
swat.animation_data.action=bpy.data.actions['SWAT_DoorKick'];patrol.animation_data.action=bpy.data.actions['Patrol_LowReadyToAim']
cam=scene.camera;cam.data.ortho_scale=2.65
cam.location=(-4.8,0.0,2.3);cam.rotation_euler=(Vector((0,-.2,1))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=600;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.threads_mode='FIXED';scene.render.threads=4;scene.cycles.samples=96;scene.cycles.use_denoising=False
scene.render.image_settings.file_format='PNG'
for obj in [patrol,*patrol.children_recursive]:obj.hide_render=True
for frame in (1,9,17,25,38,61):
    scene.frame_set(frame);scene.render.filepath=str(OUT/f'SWAT_kick_{frame:03d}.png');bpy.ops.render.render(write_still=True)
for obj in [swat,*swat.children_recursive]:obj.hide_render=True
for obj in bpy.data.objects:
    if obj.name.startswith('Demo door') or obj.name=='DemoDoor_Hinge_VisualOnly':obj.hide_render=True
for obj in [patrol,*patrol.children_recursive]:obj.hide_render=False
patrol.location.x=0
cam.location=(-2.4,-4.0,2.1);cam.rotation_euler=(Vector((0,-.12,1))-cam.location).to_track_quat('-Z','Y').to_euler()
for frame in (1,23,46):
    scene.frame_set(frame);scene.render.filepath=str(OUT/f'Patrol_hold_{frame:03d}.png');bpy.ops.render.render(write_still=True)
cam.data.ortho_scale=1.23;cam.location=(-1.4,-2.6,1.80)
cam.rotation_euler=(Vector((0,-.26,1.34))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.frame_set(46);scene.render.filepath=str(OUT/'Patrol_grip_detail.png');bpy.ops.render.render(write_still=True)
proofs=[OUT/f'SWAT_kick_{f:03d}.png' for f in (1,9,17,25,38,61)]
proofs += [OUT/f'Patrol_hold_{f:03d}.png' for f in (1,23,46)]
proofs += [OUT/'Patrol_grip_detail.png']
finish_render(SOURCE,__file__,proofs,ROOT/'Validation/animation_stills_render.json',source_hash,
              {'samples':scene.cycles.samples,'size':[600,720],
               'views':['SWAT side with door contact visible','Patrol front three-quarter full body','Patrol upper body']})
print('FROZEN_SOURCE_STILLS_COMPLETE',flush=True)
