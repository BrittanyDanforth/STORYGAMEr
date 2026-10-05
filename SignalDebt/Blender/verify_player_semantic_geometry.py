"""Read-only player preservation; actual tolerances/maximum errors are reported.

BMesh sleeve closure regeneration may reorder indices. Canonical oriented faces
retain winding, materials and vertex multiplicity after spatial correspondence.
"""
import bpy,json,hashlib,sys,math
from pathlib import Path
from collections import Counter,defaultdict
from mathutils import Vector
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
BEFORE=Path(args[0]);AFTER=ROOT/'Blender/Characters.blend';OUT=ROOT/'Validation/player_art_preservation.json'
POSITION_TOL=1e-6;WEIGHT_TOL=1e-6;NORMAL_TOL=1e-4
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def vec(v):return tuple(float(x) for x in v)
def rounded(v):return tuple(round(float(x),8) for x in v)
def read(path):
 bpy.ops.wm.open_mainfile(filepath=str(path),use_scripts=False);bpy.context.view_layer.update();rig=bpy.data.objects['Player_Rig']
 data={'rig_transform':[rounded(r) for r in rig.matrix_world],'skeleton':{b.name:{'head':rounded(b.head_local),'tail':rounded(b.tail_local),'matrix':[rounded(r) for r in b.matrix_local],'parent':b.parent.name if b.parent else None,'deform':b.use_deform} for b in rig.data.bones},'anchors':{},'meshes':{}}
 for obj in rig.children_recursive:
  if obj.type=='EMPTY':data['anchors'][obj.name]={'matrix':[rounded(r) for r in obj.matrix_world],'parent_bone':obj.parent_bone,'parent_type':obj.parent_type}
  if obj.type!='MESH':continue
  points=[(vec(v.co),tuple(sorted((obj.vertex_groups[g.group].name,g.weight) for g in v.groups))) for v in obj.data.vertices]
  obj.data.calc_normals_split()
  faces=[(tuple(p.vertices),obj.data.materials[p.material_index].name,tuple(vec(obj.data.loops[li].normal) for li in p.loop_indices)) for p in obj.data.polygons]
  data['meshes'][obj.name]={'points':points,'faces':faces,'matrix':[rounded(r) for r in obj.matrix_world],'material_set':sorted(m.name for m in obj.data.materials),'parent_type':obj.parent_type,'parent_bone':obj.parent_bone}
 return data

def face_map(data,classes):
 result=defaultdict(list)
 for indices,mat,normals in data['faces']:
  keys=tuple(classes[i] for i in indices);offset=min(range(len(keys)),key=lambda i:keys[i:]+keys[:i]);key=(keys[offset:]+keys[:offset],mat)
  result[key].append(normals[offset:]+normals[:offset])
 return result
before=read(BEFORE);after=read(AFTER);changes={};meshes=[]
for section in ('rig_transform','skeleton','anchors'):
 if before[section]!=after[section]:changes[section]='changed'
for name,old in before['meshes'].items():
 new=after['meshes'].get(name);assert new is not None,name
 oldpoints=old['points'];newpoints=new['points'];unique={key:i for i,key in enumerate(sorted(set(oldpoints)))};oldclasses=[unique[p] for p in oldpoints]
 tree=KDTree(len(oldpoints))
 for i,(p,w) in enumerate(oldpoints):tree.insert(Vector(p),i)
 tree.balance();newclasses=[];position_error=0;weight_error=0
 for p,w in newpoints:
  w=dict(w);matches=tree.find_range(Vector(p),POSITION_TOL);assert matches,(name,'new unmatchedvertex',p)
  scores=[]
  for pos,i,dist in matches:
   ow=dict(oldpoints[i][1]);wd=max((abs(w.get(k,0)-ow.get(k,0)) for k in set(w)|set(ow)),default=0)
   scores.append((wd,dist,oldclasses[i]))
  wd,dist,klass=min(scores);newclasses.append(klass);position_error=max(position_error,dist);weight_error=max(weight_error,wd)
 vertex_match=Counter(oldclasses)==Counter(newclasses)
 oldfaces=face_map(old,oldclasses);newfaces=face_map(new,newclasses)
 topology_match={k:len(v) for k,v in oldfaces.items()}=={k:len(v) for k,v in newfaces.items()}
 normal_error=0
 if topology_match:
  for key,oldlist in oldfaces.items():
   remaining=list(newfaces[key])
   for normals in oldlist:
    scores=[max((Vector(a)-Vector(b)).length for a,b in zip(normals,candidate)) for candidate in remaining]
    best=min(range(len(scores)),key=scores.__getitem__);normal_error=max(normal_error,scores[best]);remaining.pop(best)
 else:normal_error=None
 fields=[field for field in ('matrix','material_set','parent_type','parent_bone') if old[field]!=new[field]]
 if not vertex_match:fields.append('vertex_multiplicity')
 if not topology_match:fields.append('oriented_topology_or_material_assignment')
 if position_error>POSITION_TOL:fields.append('vertex_positions')
 if weight_error>WEIGHT_TOL:fields.append('skin_weights')
 if normal_error is None or normal_error>NORMAL_TOL:fields.append('supplied_corner_normals')
 if fields:changes[name]=fields
 meshes.append({'name':name,'vertices_before':len(oldpoints),'vertices_after':len(newpoints),'faces_before':len(old['faces']),'faces_after':len(new['faces']),'semantic_match_with_explicit_tolerances':not fields,'changed_fields':fields,'vertex_multiplicity_match':vertex_match,'oriented_faces_materials_match':topology_match,'max_position_delta_m':position_error,'max_weight_delta':weight_error,'max_corner_normal_vector_delta':normal_error,'max_corner_normal_angle_bound_deg':math.degrees(2*math.asin(min(1,normal_error/2))) if normal_error is not None else None,'raw_vertex_order_or_floats_changed':oldpoints!=newpoints,'raw_polygon_order_changed':[f[:2] for f in old['faces']]!=[f[:2] for f in new['faces']]})
report={'status':'PASS within explicit numeric tolerances' if not changes else 'FAIL','scope':'Player source geometry,skin,supplied corner normals,skeleton and anchors vs prior delivered ZIP Characters.blend. Spatial vertex correspondence preserves multiplicity; canonical oriented face cycles preserve winding and per-face material assignment.','tolerances':{'position_m':POSITION_TOL,'weight':WEIGHT_TOL,'normal_vector_distance':NORMAL_TOL},'before_source_sha256':sha(BEFORE),'source_sha256':{'Blender/Characters.blend':sha(AFTER)},'meshes':meshes,'changes':changes,'skeleton_match':before['skeleton']==after['skeleton'],'anchor_contract_match':before['anchors']==after['anchors'],'rig_transform_match':before['rig_transform']==after['rig_transform'],'baseline_note':'Initial raw-order hashes and1e-7quantization flagged BMesh sleeve-closure vertex/polygon ordering plus submicrometer float noise. This report quantifies those changes rather than asserting byte identity. No intentional player geometry/skin/marker change.'}
OUT.write_text(json.dumps(report,indent=2));print(json.dumps({'status':report['status'],'changes':changes,'meshes':meshes},indent=2),flush=True)
