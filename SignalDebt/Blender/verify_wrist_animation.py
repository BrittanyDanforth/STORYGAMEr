"""Read-only final clip reimport audit; never saves or modifies a project asset."""
import bpy, json, math, hashlib, sys
from pathlib import Path
from datetime import datetime, timezone
from mathutils import Vector
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT=Path(args[0]) if args else ROOT/'Validation/quality_pass_wrist_grip_reimport.json'
SOURCE=Path(args[1]) if len(args)>1 else ROOT/'Blender/Character_Animation_Showcase.blend'
CLIPS={'SWAT_DoorKick':('SWAT_Officer_Rig',61),'Patrol_LowReadyIdle':('Patrol_Officer_Rig',61),'Patrol_LowReadyToAim':('Patrol_Officer_Rig',46),'Patrol_AimToLowReady':('Patrol_Officer_Rig',46)}
FINGERS=('Thumb','Index','Middle','Ring','Little')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def angle(a,b):return math.degrees(a.angle(b))
def roundv(v):return [round(float(x),8) for x in v]
def action_fcurves(action):
 # Legacy actions expose fcurves directly; Blender 5 stores them per channel bag.
 if hasattr(action,'fcurves'):
  yield from action.fcurves
 else:
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:yield from bag.fcurves
def pose_measure(rig):
 d={}
 for side in ('Left','Right'):
  lower=rig.pose.bones[side+'LowerArm'];hand=rig.pose.bones[side+'Hand']
  fore=hand.head-lower.head
  palm=(hand.matrix.to_3x3()@rig.data.bones[side+'Hand'].matrix_local.to_3x3().inverted())@Vector((0,-.021,-.066))
  fingers={}
  for finger in FINGERS:
   chain=[rig.pose.bones[side+finger+s] for s in ('Proximal','Intermediate','Distal')]
   directions=[p.matrix.to_3x3()@Vector((0,1,0)) for p in chain]
   fingers[finger]={'joint_heads': [roundv(p.head) for p in chain], 'inter_segment_angles_deg':[round(angle(directions[i],directions[i+1]),4) for i in (0,1)]}
  d[side]={'forearm_palm_angle_deg':round(angle(fore,palm),4),'wrist_head':roundv(hand.head),'fingers':fingers}
 return d
# Cache actual authored matrices/mesh envelopes before FBX scene resets.
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
source={}
source_meshes={}
source_contacts={}
def meshes_for(rig):
 return [o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
def evaluated_vertices(obj,rig):
 ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();transform=rig.matrix_world.inverted()@ev.matrix_world;points=[transform@v.co for v in mesh.vertices];ev.to_mesh_clear();return points
for name,(rigname,count) in CLIPS.items():
 rig=bpy.data.objects[rigname];rig.animation_data.action=bpy.data.actions[name]
 frames={};source_meshes[name]={};contacts={}
 contact_base={};contact_rotation_base={};max_contact_drift=0;max_contact_roll=0
 for f in range(1,count+1):
  bpy.context.scene.frame_set(f);bpy.context.view_layer.update()
  frames[f]={n:pb.matrix.copy() for n,pb in rig.pose.bones.items()}
  if name.startswith('Patrol'):
   prop=bpy.data.objects['FictionalSidearm_Grip'];P=prop.matrix_world.inverted()@rig.matrix_world
   for side in ('Left','Right'):
    for n in [side+'Hand']+[side+finger+part for finger in FINGERS for part in ('Proximal','Intermediate','Distal')]:
     position=P@rig.pose.bones[n].head;rotation=(P@rig.pose.bones[n].matrix).to_quaternion().normalized()
     if f==1:contact_base[n]=position.copy();contact_rotation_base[n]=rotation.copy()
     max_contact_drift=max(max_contact_drift,(position-contact_base[n]).length)
     max_contact_roll=max(max_contact_roll,math.degrees(2*math.acos(min(1,abs(rotation.dot(contact_rotation_base[n]))))))
  if f in {1,round(count/2),count,25}:
   trees={}
   for obj in meshes_for(rig):
    points=evaluated_vertices(obj,rig);tree=KDTree(len(points))
    for i,point in enumerate(points):tree.insert(point,i)
    tree.balance();trees[obj.name]=(tree,points)
   source_meshes[name][f]=trees
 source[name]=frames
 if name.startswith('Patrol'):
  source_contacts[name]={'joint_head_max_drift_relative_to_prop_m':max_contact_drift,'joint_frame_max_roll_relative_to_prop_deg':max_contact_roll}
  assert max_contact_drift<.0001,(name,'grip position drift',max_contact_drift)
  assert max_contact_roll<.1,(name,'grip frame roll drift',max_contact_roll)
report={'status':'PENDING visual review','recorded_utc':datetime.now(timezone.utc).isoformat(),'method':'Read-only Blender FBX reimport of all four corrected police clips, with anim_offset=0 and importer-inferred bone connections cleared to preserve the source unconnected-bone contract and keyed translations; compared with authored source at every integer frame. Technical bone/skin/pose checks only; art acceptance is recorded separately. Unity editor was not available.','source_sha256':{},'files':[],'prop_contact_stability':source_contacts,'visual_review':{'status':'PENDING','limitations':['No Unity compilation, PlayMode, runtime animation blending, or GPU performance validation.','Stylized early character geometry; this is a focused wrist and grip correction, not final AAA character art.']}}
report['blender_version']=bpy.app.version_string
report['technical_tolerances']={'joint_head_error_m':.0001,'bone_rotation_error_deg':.1,'bidirectional_nearest_skin_vertex_error_m':.0002,'interpretation':'Export fidelity tolerances; these do not approve silhouette, anatomy, wrist poses, or cloth clearance.'}
for p in [ROOT/'Blender/animate_characters.py',SOURCE]:
 report['source_sha256'][str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p)]=sha(p)
for name,(rigname,count) in CLIPS.items():
 p=ROOT/'UnityProject/Assets/Art/Animations'/f'{name}.fbx'
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p),use_anim=True,anim_offset=0.0)
 scene=bpy.context.scene;rigs=[o for o in scene.objects if o.type=='ARMATURE'];assert len(rigs)==1,(name,'rig count',len(rigs));rig=rigs[0]
 assert len(rig.data.bones)==52
 inferred_connections=[b.name for b in rig.data.bones if b.use_connect]
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for bone in rig.data.edit_bones:bone.use_connect=False
 bpy.ops.object.mode_set(mode='OBJECT')
 skins=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
 assert len(skins)==8,(name,'skin count',len(skins))
 actions=[a for a in bpy.data.actions if any(fc.data_path.startswith('pose.bones[') for fc in action_fcurves(a))];assert len(actions)==1,(name,'actions',len(actions));action=actions[0];rig.animation_data.action=action
 start,end=map(float,action.frame_range);assert abs((end-start)-(count-1))<.001
 fps=scene.render.fps/scene.render.fps_base;assert abs(fps-30)<.001,(name,fps)
 ws=[];influences=0;unweighted=0
 for mesh in skins:
  deform=set(b.name for b in rig.data.bones if b.use_deform);valid={g.index for g in mesh.vertex_groups if g.name in deform}
  for vertex in mesh.data.vertices:
   weights=[g.weight for g in vertex.groups if g.group in valid and g.weight>1e-7];influences=max(influences,len(weights));unweighted+=not bool(weights);ws.append(abs(sum(weights)-1))
 assert unweighted==0 and influences<=4 and max(ws)<1e-5
 positions=[];angles={'Left':[],'Right':[]};max_head_err=0;max_rot_err=0;frame_data={};skin_error=0;worst_heads={}
 # FBX import round-trips the source local rig basis (global axis transforms live on the rig object).
 for f in range(1,count+1):
  scene.frame_set(f);bpy.context.view_layer.update();positions.append(rig.pose.bones['Root'].head.copy())
  measured=pose_measure(rig)
  for side in ('Left','Right'):angles[side].append(measured[side]['forearm_palm_angle_deg'])
  for n,pb in rig.pose.bones.items():
   src=source[name][f][n]
   err=(pb.matrix.translation-src.translation).length
   if err>worst_heads.get(n,{}).get('error',0):worst_heads[n]={'error':err,'frame':f,'source':roundv(src.translation),'imported':roundv(pb.matrix.translation)}
   max_head_err=max(max_head_err,err)
   max_rot_err=max(max_rot_err,math.degrees(2*math.acos(min(1,abs(pb.matrix.to_quaternion().normalized().dot(src.to_quaternion().normalized()))))))
  if f in {1,round(count/2),count,25}:
   frame_data[str(f)]=measured
   for obj in skins:
    tree,source_points=source_meshes[name][f][obj.name]
    imported_points=evaluated_vertices(obj,rig);imported_tree=KDTree(len(imported_points))
    for i,point in enumerate(imported_points):
     skin_error=max(skin_error,tree.find(point)[2]);imported_tree.insert(point,i)
    imported_tree.balance()
    for point in source_points:skin_error=max(skin_error,imported_tree.find(point)[2])
 drift=max((p-positions[0]).length for p in positions);assert drift<1e-5
 row={'file':str(p.relative_to(ROOT)),'sha256':sha(p),'rig':rig.name,'blender_inferred_connections_cleared_to_preserve_source_contract':inferred_connections,'import_anim_offset':0,'bones':52,'skinned_meshes':8,'armature_actions':1,'frames':[start,end],'fps':fps,'duration_seconds':(end-start)/fps,'root_max_drift_m':drift,'unweighted_vertices':unweighted,'max_influences':influences,'max_weight_sum_error':max(ws),'source_pose_max_joint_head_error_m':max_head_err,'source_pose_max_rotation_error_deg':max_rot_err,'source_evaluated_skin_max_nearest_vertex_error_m':skin_error,'forearm_palm_angle_range_deg':{s:[min(a),max(a)] for s,a in angles.items()},'sampled_pose_measurements':frame_data}
 if max_head_err>=.0001:print('POSE_COMPARISON_DEBUG',name,sorted(worst_heads.items(),key=lambda kv:-kv[1]['error'])[:8],flush=True)
 assert max_head_err<.0001,(name,'source head mismatch',max_head_err)
 assert max_rot_err<.1,(name,'source bone rotation mismatch',max_rot_err)
 assert skin_error<.0002,(name,'source skin mismatch',skin_error)
 # abs(quaternion dot) handles sign-equivalent quaternion representations.
 row['source_evaluated_skin_comparison']='Bidirectional nearest vertex at the four sampled frames.'
 report['files'].append(row)
OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(report,indent=2));print(json.dumps({'status':report['status'],'output':str(OUT),'files':[{'file':r['file'],'angles':r['forearm_palm_angle_range_deg'],'head_error_m':r['source_pose_max_joint_head_error_m']} for r in report['files']]},indent=2))
