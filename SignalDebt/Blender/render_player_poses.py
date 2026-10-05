"""Geometric QA of player poses against apartment; mirrors controller IK math.
Not a Unity runtime capture. Does not save or modify the production .blend files.
"""
import bpy,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
root=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
draft='--draft' in args
out=root.parent/'player_pose_draft_qa' if draft else root/'Blender/Renders/PlayerPoses'
out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender/SignalDebt_Apartment.blend'),use_scripts=False)
with bpy.data.libraries.load(str(root/'Blender/Characters.blend'),link=False) as (src,dst):
    dst.collections=['Player | editable character']
collection=dst.collections[0];bpy.context.scene.collection.children.link(collection)
# Cutaway geometry proof: retain the actual workstation, floor, and chair.
for obj in bpy.context.scene.objects:
    if obj.type in {'MESH','CURVE','FONT'} and not any(c.name in {'Desk','Chair','Laptop','Floorboards'} or c==collection for c in obj.users_collection):obj.hide_render=True
rig=bpy.data.objects['Player_Rig'];meshes=[o for o in collection.objects if o.type=='MESH' and not o.get('non_export_helper')]
seat=bpy.data.objects['DeskSeat'].matrix_world.translation.copy();focus=bpy.data.objects['LaptopFocus'].matrix_world.translation.copy()
forward=focus-seat;forward.z=0;forward.normalize();up=Vector((0,0,1));right=forward.cross(up).normalized()
root_rotation=Vector((0,-1,0)).rotation_difference(forward)
rig.rotation_mode='QUATERNION';rig.rotation_quaternion=root_rotation;rig.location=seat;rig.scale=(.95,)*3
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16 if draft else 64;scene.cycles.use_denoising=False
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
# A soft fill makes skin and cloth deformation readable; it is excluded from exports.
ld=bpy.data.lights.new('Pose QA soft fill','AREA');lo=bpy.data.objects.new('Pose QA soft fill',ld);scene.collection.objects.link(lo)
lo.location=seat+right*1.8-forward*1.0+up*2.5;ld.energy=180;ld.size=3
lo.rotation_euler=(seat+up*.8-lo.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
rest={b.name:b.matrix.copy() for b in rig.pose.bones}
rest_world={n:rig.matrix_world@m for n,m in rest.items()}
rest_root=rig.matrix_world.copy();rest_targets={n:m.translation.copy() for n,m in rest_world.items()}
rest_frames={}
for side in ['Left','Right']:
    short='L' if side=='Left' else 'R';w=rest_targets[side+'Hand']
    f=bpy.data.objects['Player_PalmTip_'+short].matrix_world.translation-w
    dorsal=bpy.data.objects['Player_PalmBack_'+short].matrix_world.translation-w
    def frame(forward,up):
        z=-forward.normalized();x=up.cross(z).normalized();y=z.cross(x).normalized()
        return Matrix((x,y,z)).transposed()
    rest_frames[side]=frame(f,dorsal)

def U(v):return right*v[0]+up*v[1]+forward*v[2]
def world_head(name):return rig.matrix_world@rig.pose.bones[name].head

def rotate_world_bone(name,delta):
    p=rig.pose.bones[name];world=rig.matrix_world@p.matrix;origin=world.translation.copy()
    p.matrix=rig.matrix_world.inverted()@Matrix.Translation(origin)@delta.to_matrix().to_4x4()@Matrix.Translation(-origin)@world
    bpy.context.view_layer.update()

def solve(side,kind,target,pole,desk=False):
    upper=side+('UpperArm' if kind=='arm' else 'UpperLeg');lower=side+('LowerArm' if kind=='arm' else 'LowerLeg');end=side+('Hand' if kind=='arm' else 'Foot')
    origin=world_head(upper);offset=target-origin;raw=offset.length;direction=offset.normalized()
    a=(rest_targets[lower]-rest_targets[upper]).length;b=(rest_targets[end]-rest_targets[lower]).length
    distance=max(abs(a-b)+.002,min(raw,a+b-.002));bend_pole=pole-direction*pole.dot(direction)
    if bend_pole.length<.01:bend_pole=forward-direction*forward.dot(direction)
    bend_pole.normalize();along=(a*a-b*b+distance*distance)/(2*distance);bend=math.sqrt(max(0,a*a-along*along))
    joint=origin+direction*along+bend_pole*bend;reachable=origin+direction*distance
    rotate_world_bone(upper,(world_head(lower)-origin).rotation_difference(joint-origin))
    rotate_world_bone(lower,(world_head(end)-world_head(lower)).rotation_difference(reachable-world_head(lower)))
    end_rot=rest_world[end].to_quaternion()
    if desk:end_rot=(frame(forward,up)@rest_frames[side].inverted()).to_quaternion()@end_rot
    pos=world_head(end);matrix=Matrix.Translation(pos)@end_rot.to_matrix().to_4x4()@Matrix.Diagonal((.95,.95,.95,1))
    rig.pose.bones[end].matrix=rig.matrix_world.inverted()@matrix;bpy.context.view_layer.update()
    return {'target_error_m':(world_head(end)-target).length,'raw_reach_m':raw,'max_reach_m':a+b}

def evaluate_hand(side,key_top):
    short='L' if side=='Left' else 'R';obj=bpy.data.objects['Player_Arm_'+short]
    names={g.index:g.name for g in obj.vertex_groups}
    idx=[]
    for v in obj.data.vertices:
        if any(g.weight>.25 and names[g.group].startswith(side) and any(f in names[g.group] for f in ('Thumb','Index','Middle','Ring','Little','Hand')) for g in v.groups):idx.append(v.index)
    deps=bpy.context.evaluated_depsgraph_get();ev=obj.evaluated_get(deps)
    points=[obj.matrix_world@ev.data.vertices[i].co for i in idx]
    wrist=world_head(side+'Hand');palm=bpy.data.objects['Player_PalmTip_'+short].matrix_world.translation-wrist;dorsal=bpy.data.objects['Player_PalmBack_'+short].matrix_world.translation-wrist
    return {'min_skin_height_m':min(v.z for v in points),'key_top_height_m':key_top,'skin_clearance_above_keytop_m':min(v.z for v in points)-key_top,'finger_forward_dot':palm.normalized().dot(forward),'raw_dorsal_marker_up_dot':dorsal.normalized().dot(up),'projected_dorsal_up_dot':(dorsal-palm.normalized()*dorsal.dot(palm.normalized())).normalized().dot(up)}

cam_data=bpy.data.cameras.new('Player geometric QA camera');cam=bpy.data.objects.new('Player geometric QA camera',cam_data);scene.collection.objects.link(cam);scene.camera=cam
cam_data.clip_start=.035;cam_data.clip_end=100
report={'note':'Blender geometric proof mirroring controller poses; not Unity runtime validation.','source_blend_sha256':hashlib.sha256((root/'Blender/Characters.blend').read_bytes()).hexdigest(),'player_fbx_sha256':hashlib.sha256((root/'UnityProject/Assets/Art/Characters/PlayerBody.fbx').read_bytes()).hexdigest(),'controller_source_sha256':hashlib.sha256((root/'UnityProject/Assets/Scripts/SignalDebtPlayer.cs').read_bytes()).hexdigest(),'poses':{}}
only_first='--first-only' in args
poses=[arg for arg in args if not arg.startswith('--')] or ['Standing','Crouched','Seated']
report['first_person_vertical_fov_degrees']=76
report['first_person_aspect']='16:9'
old_report=out/'player_pose_report.json'
if old_report.exists():
    old=json.loads(old_report.read_text())
    if old.get('source_blend_sha256')==report['source_blend_sha256'] and old.get('controller_source_sha256')==report['controller_source_sha256'] and old.get('first_person_vertical_fov_degrees')==76:
        report['poses']=old.get('poses',{})
for pose in poses:
    seated=pose=='Seated';crouch=1 if pose=='Crouched' else 0
    for p in rig.pose.bones:p.matrix_basis.identity()
    player_origin=seat if seated else seat-forward*.90-right*.40
    origin_delta=player_origin-seat
    rig.location=player_origin+U((0,-.4 if seated else -.42*crouch,.25 if seated else -.07*crouch))
    p=rig.pose.bones['Spine'];axis=p.bone.matrix_local.to_3x3().inverted()@Vector((1,0,0));p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(axis,math.radians(26 if seated else 15*crouch))
    bpy.context.view_layer.update()
    reach_drop=0
    if not seated:
        for side in ['Left','Right']:
            offset=rest_targets[side+'Foot']+origin_delta-world_head(side+'UpperLeg')
            a=(rest_targets[side+'LowerLeg']-rest_targets[side+'UpperLeg']).length
            b=(rest_targets[side+'Foot']-rest_targets[side+'LowerLeg']).length
            horizontal=offset-up*offset.dot(up)
            allowed=math.sqrt(max(0,(a+b-.004)**2-horizontal.length_squared))
            reach_drop=max(reach_drop,max(0,-offset.dot(up)-allowed))
        rig.location-=up*reach_drop;bpy.context.view_layer.update()
    details={'reach_based_pelvis_lowering_m':reach_drop}
    for side,sign in [('Left',-1),('Right',1)]:
        target=rest_targets[side+'Foot']+origin_delta+forward*(.48 if seated else 0)
        details[side+'Foot']=solve(side,'leg',target,forward)
        if seated:target=focus-forward*.26-up*.111+right*(sign*.105)
        else:target=rest_targets[side+'Hand']+origin_delta+U((0,-.42*crouch-reach_drop+.06*crouch,.13*crouch))
        pole=U((sign*(.7 if seated else .6),-.3 if seated else -.15,-.3 if seated else -.8))
        details[side+'Hand']=solve(side,'arm',target,pole,seated)
    if not seated:
        p=rig.pose.bones['Head'];axis=p.bone.matrix_local.to_3x3().inverted()@Vector((1,0,0))
        p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(axis,math.radians(75-15*crouch))
        bpy.context.view_layer.update()
        details['eye_view_world_m']=list(bpy.data.objects['Player_EyeView'].matrix_world.translation)
    if seated:
        keys=[o for o in scene.objects if o.type=='MESH' and 'Key_' in o.name]
        keytop=max((o.matrix_world@Vector(p)).z for o in keys for p in o.bound_box) if keys else .8558
        for side in ['Left','Right']:details[side+'Skin']=evaluate_hand(side,keytop)
    report['poses'][pose]=details
    (out/'player_pose_report.json').write_text(json.dumps(report,indent=2))
    for o in meshes:o.hide_render=False
    cam_data.type='PERSP';cam_data.sensor_fit='AUTO';cam_data.lens=45
    scene.render.resolution_x=1200;scene.render.resolution_y=900
    target=player_origin+forward*(.24 if seated else 0)+up*(.80 if seated or crouch else .99)
    cam.location=target+right*1.9-forward*2.35+up*.60;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    if not only_first:
        scene.render.filepath=str(out/(pose+'_Side.png'));bpy.ops.render.render(write_still=True)
    for o in meshes:
        if o.get('hide_for_first_person'):o.hide_render=True
    if seated:
        cam.location=seat+U((0,1.18,.34));look=focus
    else:
        cam.location=bpy.data.objects['Player_EyeView'].matrix_world.translation.copy()+up*reach_drop;look=cam.location+forward*math.cos(math.radians(75))-up*math.sin(math.radians(75))
    details['camera_view_world_m']=list(cam.location)
    cam.rotation_euler=(look-cam.location).to_track_quat('-Z','Y').to_euler();cam_data.sensor_fit='VERTICAL';cam_data.sensor_height=24;cam_data.lens=24/(2*math.tan(math.radians(76)/2))
    scene.render.resolution_x=640 if draft else 1600;scene.render.resolution_y=360 if draft else 900
    scene.render.filepath=str(out/(pose+'_FirstPerson.png'));bpy.ops.render.render(write_still=True)
    details['renders']={filename:hashlib.sha256((out/filename).read_bytes()).hexdigest() for filename in ([pose+'_FirstPerson.png'] if only_first else [pose+'_Side.png',pose+'_FirstPerson.png'])}
    (out/'player_pose_report.json').write_text(json.dumps(report,indent=2))
print('PLAYER_POSE_GEOMETRY_QA_COMPLETE',json.dumps(report))
