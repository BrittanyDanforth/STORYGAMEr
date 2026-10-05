"""Non-destructive geometry QA views of actual exported skin under joint bends."""
import bpy,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Quaternion
root=Path(__file__).resolve().parents[1]
source_hash=hashlib.sha256((root/'Blender/Characters.blend').read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender/Characters.blend'),use_scripts=False)
rig=bpy.data.objects['Player_Rig']
for obj in bpy.data.objects:
    if obj.type in {'MESH','FONT'} and obj.parent!=rig:obj.hide_render=True
rig.location=(0,0,0)
def rotate(name,axis,angle):
    p=rig.pose.bones[name];p.rotation_mode='QUATERNION'
    local=p.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    p.rotation_quaternion=Quaternion(local,math.radians(angle))
rotate('LeftUpperArm',(1,0,0),-20)
rotate('LeftLowerArm',(1,0,0),-100)
rotate('RightUpperArm',(1,0,0),-15)
rotate('RightLowerArm',(1,0,0),-80)
rotate('LeftUpperLeg',(1,0,0),-55)
rotate('LeftLowerLeg',(1,0,0),100)
rotate('LeftFoot',(1,0,0),-35)
for side in ['Left','Right']:
    for finger in ['Thumb','Index','Middle','Ring','Little']:
        for joint,angle in [('Proximal',-22),('Intermediate',-30),('Distal',-18)]:rotate(side+finger+joint,(1,0,0),angle)
bpy.context.view_layer.update()
scene=bpy.context.scene;cam=scene.camera
scene.render.resolution_x=1400;scene.render.resolution_y=1400;scene.cycles.samples=64
cam.data.type='ORTHO'
def view(name,target,position,size):
    cam.location=Vector(position);cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=size
    scene.render.filepath=str(root/'Blender/Renders'/name);bpy.ops.render.render(write_still=True)
view('Player_Skin_Bend.png',(0,-.08,.94),(3.2,-5,2.2),1.95)
# Find evaluated left elbow and knee, not hardcoded post-pose estimates.
elbow=rig.matrix_world@rig.pose.bones['LeftLowerArm'].head
knee=rig.matrix_world@rig.pose.bones['LeftLowerLeg'].head
view('Player_Elbow_Close.png',elbow,elbow+Vector((1.7,-2,.65)),.70)
view('Player_Knee_Close.png',knee,knee+Vector((1.7,-2,.5)),.65)
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender/Characters.blend'),use_scripts=False)
bpy.context.scene.render.filepath=str(root/'Blender/Renders/Characters_Roster.png')
bpy.ops.render.render(write_still=True)
assert source_hash==hashlib.sha256((root/'Blender/Characters.blend').read_bytes()).hexdigest(),'Source changed during proof render'
manifest={'source_blend_sha256':source_hash,'proof':'Actual base mesh/weights bent in Blender; not a Unity capture.','renders':{}}
for filename in ['Player_Skin_Bend.png','Player_Elbow_Close.png','Player_Knee_Close.png','Characters_Roster.png']:
    p=root/'Blender/Renders'/filename;manifest['renders'][filename]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
(root/'Blender/Renders/geometry_qa_manifest.json').write_text(json.dumps(manifest,indent=2))
print('SKIN_DEFORMATION_QA_RENDERED',json.dumps(manifest))
