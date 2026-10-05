"""Exact finite segment/triangle checks on ORIGINAL garment surfaces only.
Ignores shared-vertex/shared-edge contact at the intentionally joined seam.
No synthetic volume caps. Noncoplanar crossings are sampled at every authored
frame; coplanar near-parallel candidate pairs are counted as a limitation.
"""
import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import intersect_ray_tri
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1];SRC=ROOT/'Blender/Character_Animation_Showcase.blend'
bpy.ops.wm.open_mainfile(filepath=str(SRC),use_scripts=False);scene=bpy.context.scene
CLIPS={'SWAT_DoorKick':('SWAT_Officer',61),'Patrol_LowReadyIdle':('Patrol_Officer',61),'Patrol_LowReadyToAim':('Patrol_Officer',46),'Patrol_AimToLowReady':('Patrol_Officer',46)}
def garment(n):return 'desaturated field blue' in n or 'navy uniform' in n
def triangles(obj):
 obj.data.calc_loop_triangles();return [tuple(t.vertices) for t in obj.data.loop_triangles if garment(obj.data.materials[t.material_index].name)]
def evpoints(obj,rig):
 ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh();xf=rig.matrix_world.inverted()@ev.matrix_world;p=[xf@v.co for v in m.vertices];ev.to_mesh_clear();return p

def crossings(a,b):
 shared=[p for p in a if any((p-q).length<2e-6 for q in b)]
 hits=[]
 for src,dest in ((a,b),(b,a)):
  for p,q in zip(src,src[1:]+src[:1]):
   direction=q-p
   if direction.length<1e-8:continue
   hit=intersect_ray_tri(*dest,direction,p,True)
   if hit is None:continue
   t=(hit-p).dot(direction)/direction.length_squared
   if t<1e-6 or t>1-1e-6:continue
   if any((hit-s).length<2e-5 for s in shared):continue
   if len(shared)>=2:
    axis=shared[1]-shared[0];d=(hit-shared[0]).dot(axis)/axis.length_squared
    if -1e-6<=d<=1+1e-6 and (hit-(shared[0]+axis*d)).length<2e-5:continue
   hits.append(hit)
 return hits

def fraction(p,rig,side):
 a=rig.data.bones[side+'UpperArm'].head_local;b=rig.data.bones[side+'LowerArm'].head_local
 return (p-a).dot(b-a)/(b-a).length_squared
report={'status':'EXACT_SURFACE_DIAGNOSTIC_PENDING_VISUAL_ADJUDICATION','source_sha256':{'Blender/Character_Animation_Showcase.blend':hashlib.sha256(SRC.read_bytes()).hexdigest()},'method':'Every authored integer frame; BVH candidates on original sleeve and Chest garment triangles, then finite edge/triangle intersection. Shared seam vertex/edge contact under20micrometers is omitted explicitly. Original surfaces only, no audit caps. Rest-space distance from each crossing sleeve triangle to its nearest matching torso/sleeve boundary vertex localizes attachment contacts.','limitations':['Finite sample frames do not prove interpolation or runtime blends.','Coplanar surface overlap is not comprehensively resolved by this noncoplanar segment test.','Triangle crossings at a joined garment can be localized cloth folding, requiring source-bound visual review.'],'clips':{}}
for name,(prefix,count) in CLIPS.items():
 rig=bpy.data.objects[prefix+'_Rig'];rig.animation_data.action=bpy.data.actions[name];chest=bpy.data.objects[prefix+'_Chest'];ct=triangles(chest)
 chestrest=[(rig.matrix_world.inverted()@chest.matrix_world)@v.co for v in chest.data.vertices]
 chesttree=KDTree(len(chestrest))
 for i,p in enumerate(chestrest):chesttree.insert(p,i)
 chesttree.balance();sides={}
 for side,short in [('Left','L'),('Right','R')]:
  obj=bpy.data.objects[prefix+'_Arm_'+short];at=triangles(obj);rest=[(rig.matrix_world.inverted()@obj.matrix_world)@v.co for v in obj.data.vertices]
  shared=[i for i in {i for t in at for i in t} if chesttree.find(rest[i])[2]<1e-6]
  seam=KDTree(len(shared))
  for i,vi in enumerate(shared):seam.insert(rest[vi],i)
  seam.balance();sides[side]=(obj,at,rest,seam,shared)
 row={'frames_sampled':count,'sides':{s:{'shared_boundary_vertices':len(v[-1]),'max_crossing_triangle_pairs_per_frame':0,'frames_with_crossings':[],'max_sleeve_triangle_rest_centroid_distance_to_seam_m':0,'max_sleeve_crossing_vertex_rest_distance_to_seam_m':0,'crossing_rest_upperarm_fraction_range':[9,-9],'sample_crossings':[]} for s,v in sides.items()}}
 for frame in range(1,count+1):
  scene.frame_set(frame);bpy.context.view_layer.update();cv=evpoints(chest,rig);cbvh=BVHTree.FromPolygons(cv,ct,all_triangles=True)
  for side,(obj,at,rest,seam,shared) in sides.items():
   av=evpoints(obj,rig);abvh=BVHTree.FromPolygons(av,at,all_triangles=True);found=[]
   for ai,ci in abvh.overlap(cbvh):
    aa=[av[i] for i in at[ai]];bb=[cv[i] for i in ct[ci]];hits=crossings(aa,bb)
    if not hits:continue
    center=sum((rest[i] for i in at[ai]),Vector())/3;distance=seam.find(center)[2];vertexdist=max(seam.find(rest[i])[2] for i in at[ai]);frac=fraction(center,rig,side)
    found.append({'sleeve_triangle':at[ai],'chest_triangle':ct[ci],'rest_centroid_distance_to_seam_m':distance,'max_rest_vertex_distance_to_seam_m':vertexdist,'rest_upperarm_fraction':frac,'hit_positions_rig_m':[list(p) for p in hits]})
   d=row['sides'][side];d['max_crossing_triangle_pairs_per_frame']=max(d['max_crossing_triangle_pairs_per_frame'],len(found))
   if found:
    d['frames_with_crossings'].append(frame)
    d['max_sleeve_triangle_rest_centroid_distance_to_seam_m']=max(d['max_sleeve_triangle_rest_centroid_distance_to_seam_m'],max(h['rest_centroid_distance_to_seam_m'] for h in found));d['max_sleeve_crossing_vertex_rest_distance_to_seam_m']=max(d['max_sleeve_crossing_vertex_rest_distance_to_seam_m'],max(h['max_rest_vertex_distance_to_seam_m'] for h in found))
    d['crossing_rest_upperarm_fraction_range'][0]=min(d['crossing_rest_upperarm_fraction_range'][0],min(h['rest_upperarm_fraction'] for h in found));d['crossing_rest_upperarm_fraction_range'][1]=max(d['crossing_rest_upperarm_fraction_range'][1],max(h['rest_upperarm_fraction'] for h in found))
    if frame in {1,25,count} or not d['sample_crossings']:d['sample_crossings'].append({'frame':frame,'pairs':found})
 report['clips'][name]=row
 print('CROSSINGS',name,json.dumps({s:{k:v for k,v in d.items() if k!='sample_crossings'} for s,d in row['sides'].items()}),flush=True)
OUT=ROOT/'Validation/garment_surface_crossings.json';OUT.write_text(json.dumps(report,indent=2));print('CROSSING_REPORT_WRITTEN',OUT,flush=True)
