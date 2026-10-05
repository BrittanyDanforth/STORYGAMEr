"""Render the actual authored 30 fps animation for offline visual review."""
import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from preview_render_manifest import sha256, finish_render
SOURCE=ROOT/'Blender/Character_Animation_Showcase.blend'
source_hash=sha256(SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
scene=bpy.context.scene
swat=bpy.data.objects['SWAT_Officer_Rig'];patrol=bpy.data.objects['Patrol_Officer_Rig']
swat.location=(0,0,0);patrol.location=(1.1,1.55,0);patrol.rotation_euler[2]=-math.pi/2
swat.animation_data.action=bpy.data.actions['SWAT_DoorKick']
patrol.animation_data.action=bpy.data.actions['Patrol_LowReadyToAim']
scene.frame_start=1;scene.frame_end=61;scene.render.fps=30
if '--frame' in sys.argv:
    chosen=int(sys.argv[sys.argv.index('--frame')+1]);scene.frame_start=chosen;scene.frame_end=chosen
cam=scene.camera
cam.data.ortho_scale=3.9
cam.location=(-5.0,0.0,2.6)
cam.rotation_euler=(Vector((.3,.40,1.0))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=864;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.image_settings.file_format='PNG'
frames=ROOT/'Blender/Renders/Animation/MotionFrames_Final';frames.mkdir(parents=True,exist_ok=True)
scene.render.filepath=str(frames/'frame_')
if '--layout' in sys.argv:
    scene.frame_set(25);scene.cycles.samples=12
    scene.render.filepath=str(ROOT/'Blender/Renders/Animation/Motion_layout.png')
    bpy.ops.render.render(write_still=True)
else:
    bpy.ops.render.render(animation=True)
    if '--frame' not in sys.argv:
        finish_render(SOURCE,__file__,[frames/f'frame_{f:04d}.png' for f in range(1,62)],
                      ROOT/'Validation/animation_motion_render.json',source_hash,
                      {'camera_location':list(cam.location),'camera_target':[.3,.4,1.0],
                       'ortho_scale':cam.data.ortho_scale,'samples':scene.cycles.samples,
                       'size':[864,640],'fps':30,'frames':[1,61]})
print('MOTION_FRAMES_COMPLETE',flush=True)
