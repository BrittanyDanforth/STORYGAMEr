"""Render the actual raised/lowered prop-hold clips for close-up visual review.

Offline authoring only. Reads the showcase without changing source or FBXs.
The GIF encoder is a separate offline file-conversion step.
"""
from pathlib import Path
import bpy
import sys
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from preview_render_manifest import sha256, finish_render
SOURCE=ROOT/'Blender/Character_Animation_Showcase.blend'
source_hash=sha256(SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
scene=bpy.context.scene
swat=bpy.data.objects['SWAT_Officer_Rig']
patrol=bpy.data.objects['Patrol_Officer_Rig']
for obj in [swat,*swat.children_recursive]: obj.hide_render=True
for obj in bpy.data.objects:
    if obj.name.startswith('Demo door') or obj.name=='DemoDoor_Hinge_VisualOnly': obj.hide_render=True
for obj in [patrol,*patrol.children_recursive]: obj.hide_render=False
patrol.location=(0,0,0)
cam=scene.camera
cam.data.ortho_scale=1.13
cam.location=(-1.4,-2.6,1.80)
cam.rotation_euler=(Vector((0,-.26,1.30))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES'
scene.cycles.samples=64
scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=640;scene.render.resolution_y=480;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.fps=30
output=ROOT/'Blender/Renders/Animation/MotionFrames_Grip'
output.mkdir(parents=True,exist_ok=True)
sequence=[('Patrol_LowReadyToAim',frame) for frame in range(1,47)]
sequence += [('Patrol_AimToLowReady',frame) for frame in range(2,47)]
if '--layout' in sys.argv:
    scene.cycles.samples=12
    for frame in (1,46):
        patrol.animation_data.action=bpy.data.actions['Patrol_LowReadyToAim']
        scene.frame_set(frame)
        scene.render.filepath=str(output.parent/f'Patrol_layout_{frame:03d}.png')
        bpy.ops.render.render(write_still=True)
    print('PATROL_LAYOUT_COMPLETE',flush=True)
    raise SystemExit(0)
for output_frame,(action,frame) in enumerate(sequence,1):
    patrol.animation_data.action=bpy.data.actions[action]
    scene.frame_set(frame)
    scene.render.filepath=str(output/f'frame_{output_frame:04d}.png')
    bpy.ops.render.render(write_still=True)
finish_render(SOURCE,__file__,[output/f'frame_{f:04d}.png' for f in range(1,92)],
              ROOT/'Validation/patrol_grip_render.json',source_hash,
              {'camera_location':list(cam.location),'camera_target':[0,-.26,1.30],
               'ortho_scale':cam.data.ortho_scale,'samples':scene.cycles.samples,
               'size':[640,480],'fps':30,'frames':[1,91]})
print('PATROL_GRIP_MOTION_COMPLETE',len(sequence),flush=True)
