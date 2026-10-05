"""Read-only close views of the explicitly documented inner shoulder fold."""
import bpy
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[1];out=R/'Blender/Renders/UpperBodyAudit';out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(R/'Blender/Character_Animation_Showcase.blend'),use_scripts=False);s=bpy.context.scene;r=bpy.data.objects['Patrol_Officer_Rig'];r.location=(0,0,0);r.animation_data.action=bpy.data.actions['Patrol_LowReadyToAim'];s.frame_set(1)
for o in s.objects:
 if o.type=='MESH':o.hide_render=True
for o in r.children_recursive:
 if o.type=='MESH':o.hide_render=False
s.render.engine='CYCLES';s.cycles.samples=40;s.cycles.use_denoising=False;s.render.threads_mode='FIXED';s.render.threads=2;s.render.resolution_x=720;s.render.resolution_y=720;s.render.resolution_percentage=100
c=s.camera;c.data.type='ORTHO';c.data.ortho_scale=.54
for name,position,target in [('Attachment_LeftFront',(2.5,-2,1.23),(.17,-.06,1.27)),('Attachment_LeftRear',(2.5,2,1.30),(.17,-.03,1.28))]:
 c.location=Vector(position);c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
