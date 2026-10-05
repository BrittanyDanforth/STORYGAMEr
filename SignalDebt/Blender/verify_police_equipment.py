"""Read-only semantic equipment binding and occupancy review, sampled all clip frames.
Rigid binding is necessary but does not itself approve fit, contact or art quality.
"""
import bpy,json,hashlib,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
SOURCE=Path(args[1]) if len(args)>1 else ROOT/'Blender/Character_Animation_Showcase.blend'
OUT=Path(args[0]) if args else ROOT/'Validation/police_equipment_review.json'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False);scene=bpy.context.scene
CLIPS={'SWAT_DoorKick':('SWAT_Officer',61),'Patrol_LowReadyIdle':('Patrol_Officer',61),'Patrol_LowReadyToAim':('Patrol_Officer',46),'Patrol_AimToLowReady':('Patrol_Officer',46)}
TOKENS=['protective vest','webbing','carrier bound edge','molded holster','holstered sidearm','radio molded shell','protective eyewear','gunmetal','restrained brass','rubber and black cloth']
def evpoints(obj,rig):
 e=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh();xf=rig.matrix_world.inverted()@e.matrix_world;p=[xf@v.co for v in m.vertices];e.to_mesh_clear();return p
def bounds(p):return [[min(v[i] for v in p),max(v[i] for v in p)] for i in range(3)]
report={'status':'PENDING_VISUAL_FIT_REVIEW','source_sha256':{'Blender/Character_Animation_Showcase.blend':hashlib.sha256(SOURCE.read_bytes()).hexdigest()},'scope':'Actual evaluated equipment vertices after skinning; first vs every authored frame in local bound-bone coordinates. This detects sliding/incorrect multi-bone binding, not hidden mesh-fit or triangle intersections. Fit is reviewed in multi-angle renders separately.','clips':{}}
for name,(prefix,count) in CLIPS.items():
 rig=bpy.data.objects[prefix+'_Rig'];rig.animation_data.action=bpy.data.actions[name];manifest=json.loads(rig.get('equipment_manifest_json','{}'))
 assert manifest and manifest.get('inert_prop_only'),(name,'Missing explicit equipment manifest')
 selected={};maxweight=0;bad=[]
 for obj in rig.children_recursive:
  if obj.type!='MESH' or not any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers):continue
  region=obj.get('body_region','')
  if region.startswith('Arm_'):continue # Gloved hand surface is deformable, not rigid equipment.
  ids={vi for p in obj.data.polygons if any(t in obj.data.materials[p.material_index].name.lower() for t in TOKENS) for vi in p.vertices}
  pairs=[]
  for vi in sorted(ids):
   weights=[(obj.vertex_groups[g.group].name,g.weight) for g in obj.data.vertices[vi].groups if obj.vertex_groups[g.group].name in rig.data.bones and g.weight>1e-7]
   # Footwear can use legitimate foot/toe skin blending; this audit targets visible police equipment.
   if any('Foot' in n or 'Toes' in n for n,w in weights):continue
   bone,value=max(weights,key=lambda t:t[1]);maxweight=max(maxweight,abs(value-1))
   if len(weights)!=1 or abs(value-1)>1e-6:bad.append((obj.name,vi,weights))
   if region in ('Chest','Hips','Head') and bone!=region:bad.append((obj.name,vi,'wrong region bone',bone,region))
   pairs.append((vi,bone))
  if pairs:selected[obj.name]=pairs
 assert not bad,(name,'equipment binding',bad[:6])
 first={};drift=0;points=0
 for f in range(1,count+1):
  scene.frame_set(f);bpy.context.view_layer.update()
  for objname,pairs in selected.items():
   obj=bpy.data.objects[objname];vertices=evpoints(obj,rig)
   matrices={b:rig.pose.bones[b].matrix.inverted() for _,b in pairs}
   for vi,bone in pairs:
    local=matrices[bone]@vertices[vi];key=(objname,vi)
    if f==1:first[key]=local;points+=1
    else:drift=max(drift,(local-first[key]).length)
 assert drift<1e-5,(name,'rigid gear sliding',drift)
 hips=bpy.data.objects[prefix+'_Hips'];scene.frame_set(1);bpy.context.view_layer.update();verts=evpoints(hips,rig)
 def matpoints(token):
  ids={vi for p in hips.data.polygons if token in hips.data.materials[p.material_index].name.lower() for vi in p.vertices}
  return [verts[i] for i in ids]
 holster=matpoints('molded holster');stowed=matpoints('holstered sidearm')
 held=[o.name for o in rig.children_recursive if o.name=='FictionalSidearm_Grip']
 assert len(holster)>0
 occupied=prefix.startswith('SWAT');assert bool(stowed)==occupied and bool(held)!=occupied,(name,'duplicate or missing sidearm occupancy')
 row={'frames_sampled':count,'equipment_manifest':manifest,'rigid_equipment_vertices_sampled':points,'max_nonunit_weight_error':maxweight,'max_relative_to_bound_bone_drift_m':drift,'bad_weight_bindings':bad,'holster_vertices':len(holster),'holster_bounds_rig_space_m':bounds(holster),'stowed_prop_vertices':len(stowed),'held_prop_roots':held,'occupancy':'SWAT holster contains one inert partial exterior; no held prop' if occupied else 'Patrol holster empty; one inert held prop','binding_status':'PASS','fit_status':'PENDING_MULTIVIEW_VISUAL'}
 if stowed:row['exposed_stowed_prop_bounds_rig_space_m']=bounds(stowed)
 report['clips'][name]=row
OUT.write_text(json.dumps(report,indent=2));print('EQUIPMENT_BINDING_CHECK_COMPLETE',json.dumps({n:r['max_relative_to_bound_bone_drift_m'] for n,r in report['clips'].items()}),flush=True)
