"""Offline Blender authoring: police performance studies for Signal Debt.

Reads Characters.blend without changing it. Produces portable actions, baked
Generic FBX files, a nondestructive door study and rendered evidence. No game
code, network code, real weapon internals or tactical behavior is included.
Run: blender --background --python animate_characters.py
"""
import bpy
import math
import json
import sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'UnityProject/Assets/Art/Animations'
PREVIEW=ROOT/'Blender/Renders/Animation'
OUT.mkdir(parents=True,exist_ok=True);PREVIEW.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender/Characters.blend'),use_scripts=False)
scene=bpy.context.scene
scene.render.fps=30
swat=bpy.data.objects['SWAT_Officer_Rig'];patrol=bpy.data.objects['Patrol_Officer_Rig']
keep={swat,patrol,*swat.children_recursive,*patrol.children_recursive}
for obj in list(bpy.data.objects):
    if obj not in keep:bpy.data.objects.remove(obj,do_unlink=True)
for rig in (swat,patrol):
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0);rig.scale=(1,1,1)
    rig.animation_data_clear()
    for pb in rig.pose.bones:
        pb.matrix_basis=Matrix.Identity(4);pb.rotation_mode='QUATERNION'
    rig['animation_status']='Generic skinned performance study; original keyed poses at 30 fps.'
bpy.context.view_layer.update()

V=lambda value:Vector(value)
def smooth(t):return t*t*(3-2*t)
def curve(frame,keys):
    for (f0,v0),(f1,v1) in zip(keys,keys[1:]):
        if frame<=f1:
            t=smooth(max(0,min(1,(frame-f0)/(f1-f0))))
            return v0+(v1-v0)*t
    return keys[-1][1]
def vector_curve(frame,keys):
    return V([curve(frame,[(f,v[axis]) for f,v in keys]) for axis in range(3)])
def rest(rig,name):return rig.data.bones[name]
def h(rig,name):return rest(rig,name).head_local.copy()
def t(rig,name):return rest(rig,name).tail_local.copy()
pose_cache={}
def current_bone_matrix(rig,name):
    key=(rig.name,name)
    if key in pose_cache:return pose_cache[key]
    rb=rest(rig,name);pb=rig.pose.bones[name]
    if rb.parent:
        matrix=rb.convert_local_to_pose(pb.matrix_basis,rb.matrix_local,
            parent_matrix=current_bone_matrix(rig,rb.parent.name),parent_matrix_local=rb.parent.matrix_local)
    else:matrix=rb.convert_local_to_pose(pb.matrix_basis,rb.matrix_local)
    pose_cache[key]=matrix.copy();return matrix

def set_world(rig,name,head,quat=None):
    rb=rest(rig,name)
    m=(quat or rb.matrix_local.to_quaternion()).to_matrix().to_4x4()
    m.translation=V(head)
    if rb.parent:
        basis=rb.convert_local_to_pose(m,rb.matrix_local,
            parent_matrix=current_bone_matrix(rig,rb.parent.name),parent_matrix_local=rb.parent.matrix_local,invert=True)
    else:basis=rb.convert_local_to_pose(m,rb.matrix_local,invert=True)
    rig.pose.bones[name].matrix_basis=basis
    pose_cache[(rig.name,name)]=m.copy()
def point_bone(rig,name,a,b):
    rb=rest(rig,name)
    q=(rb.tail_local-rb.head_local).normalized().rotation_difference((V(b)-V(a)).normalized())
    set_world(rig,name,a,q@rb.matrix_local.to_quaternion())
def two_bone(a,target,l1,l2,pole):
    a=V(a);target=V(target)
    axis=(target-a).normalized();dist=(target-a).length
    dist=max(abs(l1-l2)+.003,min(dist,l1+l2-.006))
    target=a+axis*dist
    along=(l1*l1-l2*l2+dist*dist)/(2*dist)
    perp=V(pole)-a;perp-=axis*perp.dot(axis)
    if perp.length<.001:perp=V((0,-1,0))
    knee=a+axis*along+perp.normalized()*math.sqrt(max(0,l1*l1-along*along))
    flex=math.degrees(math.acos(max(-1,min(1,(dist*dist-l1*l1-l2*l2)/(2*l1*l2)))))
    return knee,target,flex

def upper_body(rig,delta,lean):
    pivot=h(rig,'Hips');q=Quaternion((1,0,0),math.radians(lean))
    for name in ('Root','Hips','Spine','Chest','Neck','Head','LeftShoulder','RightShoulder'):
        if name=='Root':set_world(rig,name,h(rig,name));continue
        a=pivot+q@(h(rig,name)-pivot)+delta
        rotation=q@rest(rig,name).matrix_local.to_quaternion()
        set_world(rig,name,a,rotation)
    return pivot,q

def leg(rig,side,ankle,pivot,q,delta,foot_angle=0):
    upper=side+'UpperLeg';lower=side+'LowerLeg';foot=side+'Foot'
    hip=pivot+q@(h(rig,upper)-pivot)+delta
    pole=hip+V((0,-1,-.2))
    knee,end,flex=two_bone(hip,ankle,rest(rig,upper).length,rest(rig,lower).length,pole)
    point_bone(rig,upper,hip,knee);point_bone(rig,lower,knee,end)
    set_world(rig,foot,end,Quaternion((1,0,0),math.radians(foot_angle))@rest(rig,foot).matrix_local.to_quaternion())
    return end,flex

def orientation_frame(forward,dorsal):
    forward=V(forward).normalized()
    dorsal=V(dorsal)-forward*V(dorsal).dot(forward)
    dorsal.normalize()
    lateral=forward.cross(dorsal).normalized()
    return Matrix((lateral,dorsal,-forward)).transposed()

PALM_AXIS=V((0,-.021,-.066)).normalized()
REST_PALM_FRAME=orientation_frame(PALM_AXIS,V((0,1,0)))
wrist_samples={}
def arm(rig,side,wrist,pivot,q,delta,hand_rotation=None,follow_forearm=False,protraction_degrees=8):
    upper=side+'UpperArm';lower=side+'LowerArm';hand=side+'Hand'
    sign=1 if h(rig,upper).x>0 else -1
    clavicle=side+'Shoulder'
    clavicle_head=pivot+q@(h(rig,clavicle)-pivot)+delta
    # A reaching shoulder moves around the rib cage. Keep the clavicle length
    # unchanged and rotate its endpoint forward, rather than translating an
    # upper-arm joint away from the rig or collapsing its elbow into the chest.
    clavicle_delta=Quaternion((0,0,1),math.radians(-sign*protraction_degrees))
    shoulder=clavicle_head+q@(clavicle_delta@(h(rig,upper)-h(rig,clavicle)))
    point_bone(rig,clavicle,clavicle_head,shoulder)
    # This body-relative pole has a continuous outward/downward elbow plane.
    # The previous palm-only pole minimized wrist angle by putting elbows at
    # x=0.04–0.08 m, through the rib cage. Wrist goals now sit farther forward
    # and shoulders protract, so a real elbow bend and chest clearance coexist.
    pole=shoulder+q@V((sign*.3,-.1,-.55))
    elbow,end,flex=two_bone(shoulder,wrist,rest(rig,upper).length,rest(rig,lower).length,pole)
    point_bone(rig,upper,shoulder,elbow);point_bone(rig,lower,elbow,end)
    if follow_forearm:
        hand_rotation=(orientation_frame(end-elbow,V((sign,0,.12)))@REST_PALM_FRAME.inverted()).to_quaternion()
    set_world(rig,hand,end,hand_rotation@rest(rig,hand).matrix_local.to_quaternion())
    actual_axis=hand_rotation@PALM_AXIS
    angle=math.degrees((end-elbow).normalized().angle(actual_axis))
    target_error=(end-V(wrist)).length
    if target_error>.00001:
        raise RuntimeError(f'{rig.name} {side} hand target is outside the authored reach: {target_error:.6f} m')
    if not follow_forearm and angle>32:
        raise RuntimeError(f'{rig.name} {side} wrist deviates more than the authored 32-degree envelope: {angle:.3f}')
    wrist_samples.setdefault(rig.animation_data.action.name,[]).append({'frame':scene.frame_current,'side':side,'palm_forearm_angle_degrees':angle,
        'elbow_flexion_degrees':flex,'clavicle_protraction_degrees':protraction_degrees,
        'shoulder_m':list(shoulder),'elbow_m':list(elbow),'wrist_target_error_m':target_error})
    return end


def finger_chain(rig,side,finger,directions,reference_rotation=None):
    """Orient anatomical phalanges without translating joints or scaling skin."""
    previous=None
    for segment,direction in zip(('Proximal','Intermediate','Distal'),directions):
        name=side+finger+segment
        a=previous if previous is not None else current_bone_matrix(rig,name).translation.copy()
        direction=V(direction).normalized()
        previous=a+direction*rest(rig,name).length
        # Solve in a stable palm/prop frame. Solving from world-rest independently
        # introduced an unwanted phalange roll during the raise/lower motion.
        reference=reference_rotation or Quaternion()
        local_direction=reference.inverted()@direction
        rest_direction=(t(rig,name)-h(rig,name)).normalized()
        orientation=reference@rest_direction.rotation_difference(local_direction)@rest(rig,name).matrix_local.to_quaternion()
        set_world(rig,name,a,orientation)


def curl_fingers(rig,side,amount=1):
    # Closed but not rigidly clenched. Thumb opposition is authored separately;
    # rotating every digit around the same local X was anatomically incorrect.
    hand_delta=current_bone_matrix(rig,side+'Hand').to_quaternion()@rest(rig,side+'Hand').matrix_local.to_quaternion().inverted()
    for finger in ('Index','Middle','Ring','Little'):
        angles=[-50,-72,-40]
        for segment,angle in zip(('Proximal','Intermediate','Distal'),angles):
            name=side+finger+segment
            rig.pose.bones[name].rotation_quaternion=Quaternion((1,0,0),math.radians(angle*amount))
            pose_cache.pop((rig.name,name),None)
    sign=1 if h(rig,side+'Hand').x>0 else -1
    # In the source hand dorsal is +Y. Fold the thumb toward the palm (-Y)
    # and across the first phalanges, never sticking upright from the fist.
    thumb_dirs=[V((sign*.22,-.92,-.32)),V((-sign*.50,-.75,-.43)),V((-sign*.78,-.60,-.18))]
    finger_chain(rig,side,'Thumb',[hand_delta@v for v in thumb_dirs],hand_delta)


def prop_grip_fingers(rig,side,rotation):
    inward=1 if side=='Right' else -1
    for finger in ('Index','Middle','Ring','Little'):
        if side=='Right' and finger=='Index':
            # Rest visibly along the outside of the solid prop, with a small
            # comfortable bend. No trigger interaction exists in this project.
            directions=[V((0,-1,.33)),V((0,-1,.14)),V((0,-1,-.08))]
        elif side=='Right':
            directions=[V((0,-1,-.12)),V((.83,-.56,-.03)),V((.93,.37,.02))]
            if finger=='Little':directions=[V((0,-1,-.10)),V((.40,-.916,0)),V((.98,.198,.03))]
        else:
            directions=[V((0,-1,-.10)),V((-.96,.10,0)),V((-.85,.52,.04))]
            if finger=='Index':directions=[V((0,-1,-.46)),V((-.94,-.10,-.16)),V((-.94,.33,.04))]
            if finger=='Little':directions=[V((0,-1,-.06)),V((-.85,-.35,.20)),V((-.95,.22,.20))]
        finger_chain(rig,side,finger,[rotation@v for v in directions],rotation)
    thumb=[V((inward*.34,-.92,.19)),V((inward*.10,-.98,.14)),V((inward*.06,-1,-.04))]
    finger_chain(rig,side,'Thumb',[rotation@v for v in thumb],rotation)

def sole_vertices(rig,side):
    points=[];depsgraph=bpy.context.evaluated_depsgraph_get()
    for obj in rig.children_recursive:
        if obj.type!='MESH' or side+'Foot' not in obj.vertex_groups:continue
        group=obj.vertex_groups[side+'Foot'].index
        evaluated=obj.evaluated_get(depsgraph);mesh=evaluated.to_mesh()
        sole_materials={i for i,m in enumerate(mesh.materials) if m and 'sole' in m.name.lower()}
        ids={vi for polygon in mesh.polygons if polygon.material_index in sole_materials for vi in polygon.vertices}
        for vi in ids:
            vertex=mesh.vertices[vi]
            if any(g.group==group and g.weight>.45 for g in vertex.groups):
                points.append(rig.matrix_world.inverted()@evaluated.matrix_world@vertex.co)
        evaluated.to_mesh_clear()
    return points

rest_right_sole=sole_vertices(swat,'Right')
if not rest_right_sole:raise RuntimeError('No evaluated RightFoot sole geometry found; check rig/skin contract.')
contact_rotation=Quaternion((1,0,0),math.radians(-72))
contact_y=-.80-min((contact_rotation@(p-h(swat,'RightFoot'))).y for p in rest_right_sole)
measurements={}
previous_quaternions={}
def record_pose(rig,frame):
    for pb in rig.pose.bones:
        key=(rig.name,pb.name)
        old=previous_quaternions.get(key)
        if old is not None and old.dot(pb.rotation_quaternion)<0:pb.rotation_quaternion.negate()
        previous_quaternions[key]=pb.rotation_quaternion.copy()
        pb.keyframe_insert('location',frame=frame,group=pb.name)
        pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        pb.keyframe_insert('scale',frame=frame,group=pb.name)
def new_action(rig,name):
    pose_cache.clear()
    for key in list(previous_quaternions):
        if key[0]==rig.name:del previous_quaternions[key]
    rig.animation_data_create();a=bpy.data.actions.new(name);a.use_fake_user=True
    rig.animation_data.action=a
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    return a

def build_kick():
    action=new_action(swat,'SWAT_DoorKick')
    support=h(swat,'LeftFoot');start=h(swat,'RightFoot');kick_x=math.copysign(.14,start.x);support_sign=math.copysign(1,support.x)
    kick_keys=[(1,start),(9,start),(17,V((kick_x,-.27,.51))),(25,V((kick_x,contact_y,.60))),
               (29,V((kick_x,-.60,.60))),(38,V((kick_x,-.22,.47))),(49,start),(61,start)]
    minimum=180;slide=0
    for frame in range(1,62):
        scene.frame_set(frame);pose_cache.clear()
        delta=V((support_sign*curve(frame,[(1,0),(9,.022),(25,.028),(38,.01),(61,0)]),
                 curve(frame,[(1,0),(9,.012),(25,-.045),(38,-.015),(61,0)]),
                 curve(frame,[(1,-.018),(9,-.043),(25,-.025),(38,-.025),(61,-.018)])))
        lean=curve(frame,[(1,0),(9,3),(25,-5),(38,-2),(61,0)])
        pivot,q=upper_body(swat,delta,lean)
        left,flex_l=leg(swat,'Left',support,pivot,q,delta)
        right,flex_r=leg(swat,'Right',vector_curve(frame,kick_keys),pivot,q,delta,
            curve(frame,[(1,0),(9,0),(17,-30),(25,-72),(29,-72),(38,-20),(49,0),(61,0)]))
        minimum=min(minimum,flex_l,flex_r);slide=max(slide,(left-support).length)
        for side in ('Left','Right'):
            sgn=math.copysign(1,h(swat,side+'Hand').x)
            wrist=V((sgn*.27,-.29,1.29))+delta+V((0,0,curve(frame,[(1,0),(17,.035),(29,.045),(61,0)])))
            arm(swat,side,wrist,pivot,q,delta,follow_forearm=True)
            curl_fingers(swat,side,.92)
        record_pose(swat,frame)
    for name,frame in [('Anticipation',9),('Lift',17),('DoorContact',25),('Recovery',38),('Settled',61)]:
        action.pose_markers.new(name).frame=frame
    measurements['SWAT_DoorKick']={'frames':[1,61],'fps':30,'duration_seconds':2.0,'contact_frame':25,
        'support_ankle_max_drift_m':slide,'minimum_knee_flexion_degrees':minimum,'contact_ankle_y_m':contact_y,'root_motion':'in_place','loop':False}
    return action

def patrol_pose(frame,blend,idle=0):
    delta=V((0,0,-.02+idle));pivot,q=upper_body(patrol,delta,2)
    for side in ('Left','Right'):leg(patrol,side,h(patrol,side+'Foot'),pivot,q,delta)
    pitch=math.radians(25*(1-blend));rot=Quaternion((1,0,0),pitch)
    # Preserve the fingers' prop-relative pose while placing the whole grip
    # within a believable extended reach instead of close against the sternum.
    grip=V((0,-.55-.09*blend,1.18+.24*blend+idle))
    set_world(patrol,'Head',current_bone_matrix(patrol,'Head').translation,
        q@Quaternion((1,0,0),math.radians(8*blend))@rest(patrol,'Head').matrix_local.to_quaternion())
    for side in ('Left','Right'):
        sgn=math.copysign(1,h(patrol,side+'Hand').x)
        offset=V((sgn*.047,.085 if side=='Right' else .071,-.047 if side=='Right' else -.056))
        handrot=rot@Matrix(((0,sgn,0),(0,0,1),(sgn,0,0))).to_quaternion()
        arm(patrol,side,grip+rot@offset,pivot,q,delta,handrot,protraction_degrees=16+8*blend)
        prop_grip_fingers(patrol,side,rot)
    return grip,rot

def build_patrol(name,start,end,frames):
    a=new_action(patrol,name)
    for frame in range(1,frames+1):
        scene.frame_set(frame);pose_cache.clear()
        blend=start+(end-start)*smooth((frame-1)/(frames-1))
        idle=.002*math.sin(2*math.pi*(frame-1)/(frames-1)) if start==end else 0
        patrol_pose(frame,blend,idle);record_pose(patrol,frame)
    a.pose_markers.new('LowReady' if start==0 else 'Aim').frame=1
    a.pose_markers.new('Aim' if end==1 else 'LowReady').frame=frames
    measurements[name]={'frames':[1,frames],'fps':30,'duration_seconds':(frames-1)/30,'root_motion':'in_place','loop':start==end}
    return a

kick=build_kick()
scene.frame_set(25);bpy.context.view_layer.update()
contact_points=sole_vertices(swat,'Right');support_points=sole_vertices(swat,'Left')
measurements['SWAT_DoorKick']['evaluated_sole_to_door_gap_m']=abs(min(p.y for p in contact_points)-(-.80))
measurements['SWAT_DoorKick']['evaluated_support_sole_lowest_z_m']=min(p.z for p in support_points)
raise_action=build_patrol('Patrol_LowReadyToAim',0,1,46)
lower_action=build_patrol('Patrol_AimToLowReady',1,0,46)
idle_action=build_patrol('Patrol_LowReadyIdle',0,0,61)
def action_curves(action):
    # Blender 4.4+ stores keyframes in layered Action channel bags; 5.x removed
    # Action.fcurves. Walk the actual keyed bags rather than a legacy shim.
    if hasattr(action,'fcurves'):return list(action.fcurves)
    return [curve for layer in action.layers for strip in layer.strips
            for bag in strip.channelbags for curve in bag.fcurves]

for action in (kick,raise_action,lower_action,idle_action):
    for fc in action_curves(action):
        for key in fc.keyframe_points:key.interpolation='LINEAR'

# Fictional, nonfunctional handgun silhouette. No internals, ammunition or firing.
coll=bpy.data.collections.new('Animation Props and Studio');scene.collection.children.link(coll)
def material(name,color,metal=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=.46;bs.inputs['Metallic'].default_value=metal
    return m
metal=material('Fictional sidearm | graphite',(.045,.059,.064),.42)
gripmat=material('Fictional sidearm | textured grip',(.026,.033,.038))
mark=material('Fictional sidearm | neutral inset',(.23,.28,.30),.45)
floor_mat=material('Animation studio | slate',(.034,.052,.065))
door_mat=material('Demo door | painted teal',(.075,.26,.29))
trim_mat=material('Demo frame | slate steel',(.21,.25,.27),.3)

def cube(name,loc,size,mat,bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name
    for c in list(o.users_collection):c.objects.unlink(o)
    coll.objects.link(o);o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('Soft prop edge','BEVEL');mod.width=bevel;mod.segments=3
        # Blender 4.0 requires Auto Smooth for the weighted-normal modifier;
        # otherwise it reports a warning and leaves the small bevels unweighted.
        if hasattr(o.data, 'use_auto_smooth'):o.data.use_auto_smooth=True
        for polygon in o.data.polygons:polygon.use_smooth=True
        normal=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');normal.keep_sharp=True
    return o

patrol.animation_data.action=raise_action;scene.frame_set(1)
# Use the same authored grip transform as the keyed first low-ready pose.
pose_cache.clear()
grip_position,grip_rotation=patrol_pose(1,0)
bpy.context.view_layer.update()
pistol=bpy.data.objects.new('FictionalSidearm_Grip',None);coll.objects.link(pistol)
pistol.location=grip_position;pistol.rotation_mode='QUATERNION';pistol.rotation_quaternion=grip_rotation
prop_parts=[]
for name,loc,size,mat in [
    ('Sidearm solid slide',(0,-.05,.043),(.040,.175,.037),metal),
    ('Sidearm frame',(0,-.035,.017),(.033,.132,.022),metal),
    ('Sidearm grip',(0,.006,-.037),(.031,.045,.088),gripmat),
    ('Sidearm grip heel',(0,.009,-.079),(.035,.05,.012),metal),
    ('Sidearm guard lower',(0,-.037,-.033),(.025,.046,.009),metal),
    ('Sidearm guard front',(0,-.056,-.016),(.025,.01,.038),metal),
    ('Sidearm rear sight',(0,.025,.066),(.026,.012,.009),mark),
    ('Sidearm front sight',(0,-.119,.066),(.009,.012,.009),mark),
]:
    obj=cube(name,(0,0,0),size,mat,.003);obj.parent=pistol;obj.location=loc;prop_parts.append(obj)
# A flat dark muzzle inset communicates shape without a modeled bore or internals.
obj=cube('Sidearm flat muzzle inset',(0,0,0),(.019,.001,.014),gripmat,.002);obj.parent=pistol;obj.location=(0,-.138,.043);prop_parts.append(obj)
bpy.context.view_layer.update();matrix=pistol.matrix_world.copy();pistol.parent=patrol;pistol.parent_type='BONE';pistol.parent_bone='RightHand';pistol.matrix_world=matrix
pistol['prop_status']='Fictional solid visual prop only; no mechanism, firing logic or game commands.'

# Export each clip as a single selected armature+character with one active take.
def export_clip(rig,action,frames,name):
    bpy.ops.object.select_all(action='DESELECT');rig.animation_data.action=action;scene.frame_start=1;scene.frame_end=frames;scene.frame_set(1)
    for obj in [rig,*rig.children_recursive]:
        if obj.type in {'ARMATURE','MESH','EMPTY'}:obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    pending=OUT/(name+'.pending.fbx')
    bpy.ops.export_scene.fbx(filepath=str(pending),use_selection=True,
        object_types={'ARMATURE','MESH','EMPTY'},global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',use_mesh_modifiers=True,mesh_smooth_type='FACE',
        add_leaf_bones=False,armature_nodetype='ROOT',bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,
        bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    from io_scene_fbx import parse_fbx
    try:
        parse_fbx.parse(str(pending))
        pending.replace(OUT/(name+'.fbx'))
    finally:
        if pending.exists():pending.unlink()
for rig,a,frames,name in [(swat,kick,61,'SWAT_DoorKick'),(patrol,raise_action,46,'Patrol_LowReadyToAim'),
        (patrol,lower_action,46,'Patrol_AimToLowReady'),(patrol,idle_action,61,'Patrol_LowReadyIdle')]:
    export_clip(rig,a,frames,name)

cube('Studio floor',(0,0,-.045),(9,9,.08),floor_mat,.015)
for x in [i*.5 for i in range(-5,6)]:cube('Reference floor grid X',(x,0,-.003),(.004,5,.003),trim_mat,0)
for y in [i*.5 for i in range(-5,6)]:cube('Reference floor grid Y',(0,y,-.003),(5,.004,.003),trim_mat,0)
hinge=bpy.data.objects.new('DemoDoor_Hinge_VisualOnly',None);coll.objects.link(hinge);hinge.location=(-.43,-.80,0)
door=cube('Demo door solid panel',(0,0,0),(.86,.034,2.04),door_mat,.012);door.parent=hinge;door.location=(.43,0,1.02)
for x in (-.475,.475):cube('Demo door frame jamb',(x,-.80,1.04),(.055,.075,2.08),trim_mat,.008)
cube('Demo door lintel',(0,-.80,2.105),(1.00,.075,.055),trim_mat,.008)
handle=cube('Demo door visual handle',(0,0,0),(.09,.06,.02),mark,.005);handle.parent=hinge;handle.location=(.73,.045,1.02)
for frame,angle in [(1,0),(25,0),(30,-18),(39,-66),(61,-66)]:
    hinge.rotation_euler[2]=math.radians(angle);hinge.keyframe_insert('rotation_euler',frame=frame)
hinge.animation_data.action.name='DemoDoor_VisualContactResponse'
hinge['not_gameplay']='Presentation-only hinged reference; no broken door mesh, force simulation or runtime event.'

world=scene.world or bpy.data.worlds.new('Animation world');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.068,.084,1);world.node_tree.nodes['Background'].inputs[1].default_value=.40
for name,loc,energy,size,color in [('Warm key',(2,-4,5),800,4,(1,.88,.76)),('Cool fill',(-3,-2,3),700,4,(.62,.81,1)),('Rim',(1,3,4),1000,3,(.59,.82,1))]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);coll.objects.link(obj);obj.location=loc;obj.rotation_euler=(V((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Animation evidence camera');cam=bpy.data.objects.new('Animation evidence camera',camdata);coll.objects.link(cam)
scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.65
cam.location=(-4.8,.9,2.3);cam.rotation_euler=(V((0,-.2,1.0))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False;scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=600;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
swat.animation_data.action=kick;patrol.animation_data.action=raise_action
# The source opens on a clearly readable kick-contact pose; patrol is staged beside it.
patrol.location.x=2.0;scene.frame_start=1;scene.frame_end=61;scene.frame_set(25)
scene['animation_notes']='Original stylized performance study. Source actions and baked Generic FBX. Fixed support foot and contact marker. No real breach or weapon functionality.'
scene['source_script']='animate_characters.py'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender/Character_Animation_Showcase.blend'))

if '--no-previews' not in sys.argv:
    # Render the actual meshes at authored key moments. No image generation involved.
    for obj in [patrol,*patrol.children_recursive]:obj.hide_render=True
    for frame in ((25,) if '--quick-poses' in sys.argv else (1,9,17,25,38,61)):
        scene.frame_set(frame);scene.render.filepath=str(PREVIEW/f'SWAT_kick_{frame:03d}.png');bpy.ops.render.render(write_still=True)
    for obj in [swat,*swat.children_recursive,hinge,*hinge.children_recursive]:obj.hide_render=True
    for obj in coll.objects:
        if obj.name.startswith('Demo door'):obj.hide_render=True
    for obj in [patrol,*patrol.children_recursive]:obj.hide_render=False
    patrol.location.x=0;patrol.animation_data.action=raise_action
    cam.location=(-2.4,-4.0,2.1);cam.rotation_euler=(V((0,-.12,1.0))-cam.location).to_track_quat('-Z','Y').to_euler()
    for frame in ((46,) if '--quick-poses' in sys.argv else (1,23,46)):
        scene.frame_set(frame);scene.render.filepath=str(PREVIEW/f'Patrol_hold_{frame:03d}.png');bpy.ops.render.render(write_still=True)
    camdata.ortho_scale=.85;cam.location=(-1.8,-2.7,2.15);cam.rotation_euler=(V((0,-.32,1.52))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.frame_set(46);scene.render.filepath=str(PREVIEW/'Patrol_grip_detail.png');bpy.ops.render.render(write_still=True)
    
measurements['format']={'source':'Z up, -Y forward','fbx':'Y up, -Z forward','rig':'Generic','sample_rate':30,'actions':'Quaternion transforms sampled every frame; analytic poses use smoothstep easing.'}
measurements['fictional_prop']={'approx_length_m':.184,'functional_parts':False,'right_index':'extended along exterior; no trigger modeled','door':'visual-only hinge, no destructive geometry or gameplay event'}
measurements['wrist_alignment']={name:{'max_palm_forearm_angle_degrees':max(s['palm_forearm_angle_degrees'] for s in samples),'per_frame':samples} for name,samples in wrist_samples.items()}
measurements['shoulder_elbow_pose']={
    'method':'Fixed body-relative outward/downward elbow pole; clavicle rotates forward at unchanged length. Whole prop reaches farther forward while its relative palm/finger transforms remain fixed.',
    'old_failure':'A pole chosen only to minimize palm/forearm angle moved patrol elbows nearly onto the sternum and pulled the upper sleeve through the torso.',
    'separate_mesh_gate':'Validation/arm_body_clearance.json records independently measured evaluated sleeve/torso clearance when available; joint angles alone do not prove mesh clearance.',
    'actions':{name:{'elbow_flexion_range_degrees':[min(s['elbow_flexion_degrees'] for s in samples),max(s['elbow_flexion_degrees'] for s in samples)],
        'clavicle_protraction_range_degrees':[min(s['clavicle_protraction_degrees'] for s in samples),max(s['clavicle_protraction_degrees'] for s in samples)],
        'max_wrist_target_error_m':max(s['wrist_target_error_m'] for s in samples)} for name,samples in wrist_samples.items()}}
with open(OUT/'animation_manifest.json','w') as f:json.dump(measurements,f,indent=2)
print('ANIMATION_AUTHORING_COMPLETE',json.dumps(measurements),flush=True)
