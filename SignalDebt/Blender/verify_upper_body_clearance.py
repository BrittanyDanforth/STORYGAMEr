"""Read-only evaluated sleeve/torso audit. No source file writes.

This is a discrete surface-sampling diagnostic, not a watertight proof of all
possible triangle intersections or animation interpolation. Shoulder tissue is
reported separately, not silently removed. Dynamic cloth is not simulated.
"""
import bpy,bmesh,json,math,hashlib,sys,argparse
from pathlib import Path
from datetime import datetime,timezone
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('output',nargs='?',default=str(ROOT/'Validation/quality_pass_arm_body_clearance.json'))
parser.add_argument('source',nargs='?',default=str(ROOT/'Blender/Character_Animation_Showcase.blend'))
parser.add_argument('--frames',help='Comma-separated positive integer frames for an explicitly partial diagnostic.')
parser.add_argument('--topology-only',action='store_true')
options=parser.parse_args(args)
OUT=Path(options.output);SOURCE=Path(options.source)
requested_frames=sorted(set(int(f) for f in options.frames.split(','))) if options.frames else None
if requested_frames is not None and (not requested_frames or requested_frames[0]<1):parser.error('--frames requires positive frame numbers')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
scene=bpy.context.scene
CLIPS={'SWAT_DoorKick':('SWAT_Officer',61),'Patrol_LowReadyIdle':('Patrol_Officer',61),'Patrol_LowReadyToAim':('Patrol_Officer',46),'Patrol_AimToLowReady':('Patrol_Officer',46)}

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def garment(name):return 'desaturated field blue' in name or 'navy uniform' in name

def triangle_topology(obj,poly_ids,cap=False):
    """Original triangles plus explicitly closed synthetic boundary-loop fans."""
    from collections import Counter
    bm=bmesh.new();ids=bm.verts.layers.int.new('source_vertex')
    used=sorted({vi for pi in poly_ids for vi in obj.data.polygons[pi].vertices});mapping={}
    for vi in used:
        v=bm.verts.new(obj.data.vertices[vi].co);v[ids]=vi;mapping[vi]=v
    for pi in poly_ids:
        try:bm.faces.new([mapping[vi] for vi in obj.data.polygons[pi].vertices])
        except ValueError:pass
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    triangles=[tuple(v[ids] for v in f.verts) for f in bm.faces]
    boundary=[]
    for edge in bm.edges:
        if edge.is_boundary:
            loop=next(l for l in edge.link_faces[0].loops if l.edge==edge)
            boundary.append((loop.vert[ids],loop.link_loop_next.vert[ids]))
    bm.free();centers=[];cap_indices=[];unused=set(boundary)
    if cap:
        while unused:
            edge=unused.pop();loop=[edge[0]];edges=[edge];current=edge[1]
            while current!=loop[0]:
                loop.append(current)
                following=next((e for e in unused if e[0]==current),None)
                if following is None:raise RuntimeError(('Unclosed audit boundary',obj.name,current))
                unused.remove(following);edges.append(following);current=following[1]
            center=len(obj.data.vertices)+len(centers);centers.append(loop)
            for a,b in edges:
                cap_indices.append(len(triangles));triangles.append((b,a,center))
    incidence=Counter(tuple(sorted((a,b))) for tri in triangles for a,b in zip(tri,tri[1:]+tri[:1]))
    boundary_count=sum(n==1 for n in incidence.values());multiple=sum(n>2 for n in incidence.values())
    meta={'audit_cap_faces':len(centers),'audit_cap_triangle_indices':cap_indices,'audit_virtual_center_source_indices':centers,'original_boundary_edges':len(boundary),'nonmanifold_edges_after_audit_caps':boundary_count+multiple,'boundary_edges_after_audit_caps':boundary_count,'wire_edges_excluded_from_surface':0,'edges_with_more_than_two_faces':multiple}
    if cap:assert boundary_count==0 and multiple==0,(obj.name,'audit closure',meta)
    return triangles,meta

def components(obj,poly_ids):
    byvert={};unvisited=set(poly_ids)
    for pi in poly_ids:
        for vi in obj.data.polygons[pi].vertices:byvert.setdefault(vi,[]).append(pi)
    result=[]
    while unvisited:
        seed=unvisited.pop();comp=[seed];stack=[seed]
        while stack:
            pi=stack.pop()
            for vi in obj.data.polygons[pi].vertices:
                for other in byvert[vi]:
                    if other in unvisited:unvisited.remove(other);stack.append(other);comp.append(other)
        result.append(comp)
    return sorted(result,key=len,reverse=True)

def evaluated(obj,rig):
    ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();xf=rig.matrix_world.inverted()@ev.matrix_world
    pts=[xf@v.co for v in mesh.vertices];ev.to_mesh_clear();return pts

def classify(rest_point,rig,side):
    a=rig.data.bones[side+'UpperArm'].head_local;b=rig.data.bones[side+'LowerArm'].head_local;c=rig.data.bones[side+'Hand'].head_local
    ab=b-a;bc=c-b;t=(rest_point-a).dot(ab)/ab.length_squared;u=(rest_point-b).dot(bc)/bc.length_squared
    du=(rest_point-(a+ab*max(0,min(1,t)))).length;dl=(rest_point-(b+bc*max(0,min(1,u)))).length
    if dl<du and u>.02:return 'forearm'
    if t<.35:return 'proximal_shoulder_0_to_35pct'
    if t<.70:return 'middle_upper_arm_35_to_70pct'
    return 'distal_upper_arm_70_to_100pct'

def make_target(points,triangles):
    used={vi for t in triangles for vi in t}
    bounds=tuple((min(points[i][axis] for i in used),max(points[i][axis] for i in used)) for axis in range(3))
    return BVHTree.FromPolygons(points,triangles,all_triangles=True),bounds

def target_grid(targets):
    grid={};cell=.05
    for name,target in targets:
        bounds=target[1]
        lo=[math.floor(a/cell) for a,b in bounds];hi=[math.floor(b/cell) for a,b in bounds]
        for x in range(lo[0],hi[0]+1):
            for y in range(lo[1],hi[1]+1):
                for z in range(lo[2],hi[2]+1):grid.setdefault((x,y,z),[]).append((name,target))
    return grid

RAYS=[Vector(d).normalized() for d in [(1,.371,.193),(-.227,1,.411),(.313,-.191,1)]]
def inside_depth(target,p):
    tree,bounds=target
    if any(p[a]<lo or p[a]>hi for a,(lo,hi) in enumerate(bounds)):return 0
    votes=0
    for direction in RAYS:
        pos=p.copy();hits=0
        for _ in range(32):
            hit,n,index,d=tree.ray_cast(pos,direction,4)
            if hit is None:break
            hits+=1;pos=hit+direction*1e-6
        votes+=hits%2
    if votes<2:return 0
    hit,n,index,d=tree.find_nearest(p)
    return d if hit is not None else 0

report={'status':'DIAGNOSTIC_ONLY_PENDING_VISUAL_REVIEW','recorded_utc':datetime.now(timezone.utc).isoformat(),'source_sha256':{str(SOURCE.relative_to(ROOT)) if SOURCE.is_relative_to(ROOT) else str(SOURCE):sha(SOURCE)},'method':{'sample_rate':'Every authored integer frame at30fps, all4clips.','actual_surface':'Evaluated deformed mesh vertices and every garment triangle centroid from both sleeves.','torso_volume':'Largest connected base-cloth Chest component. Explicit boundary-loop triangle fans close only renderer-seam holes in an audit copy; each fan center follows its boundary mean. Every audit volume edge has exactly2incident triangles. Original surface and synthetic cap triangles are classified separately.','equipment_volume':'Each closed connected non-cloth Chest component separately; open decorative surfaces recorded but not assigned an inside volume.','anatomical_zones':'Rest-space closest shoulder/elbow/wrist centerline. Proximal shoulder is0–35%of upperarm length. Middle35–70%,distal70–100%,forearm separate. Shoulder attachment may touch; no zone silently excluded.','inside_test':'Majority odd/even ray parity along three oblique directions, after component axis-aligned bounds; nearest actual triangle gives penetration depth.','depth_threshold_m':.002,'limitations':['Vertex and centroid samples can miss very small edge-only triangle intersections.','Near artificial shoulder caps, signed distances diagnose envelope intrusion, not a physical cloth solver.','Integer samples do not prove inter-frame blends, game poses,collisions or Unity behavior.','Numerical clearance is not standalone visual or AAA quality approval.']},'clips':{}}
for action_name,(prefix,count) in CLIPS.items():
    report['blender_version']=bpy.app.version_string
    frames=list(range(1,count+1)) if requested_frames is None else [f for f in requested_frames if f<=count]
    if not frames:raise ValueError(('Requested frames fall outside clip',action_name,count))
    if requested_frames is not None:
        report['status']='PARTIAL_FRAME_DIAGNOSTIC_PENDING_VISUAL_REVIEW'
        report['method']['sample_rate']='Explicit partial diagnostic: requested integer frames '+','.join(map(str,requested_frames))+'; numbers beyond a clip duration are omitted.'
    rig=bpy.data.objects[prefix+'_Rig'];rig.animation_data.action=bpy.data.actions[action_name]
    chest=bpy.data.objects[prefix+'_Chest']
    cloth_polys=[p.index for p in chest.data.polygons if garment(chest.data.materials[p.material_index].name)]
    cloth_components=components(chest,cloth_polys)
    torso_tris,topology=triangle_topology(chest,cloth_components[0],True)
    gear_polys=[p.index for p in chest.data.polygons if p.index not in cloth_polys]
    gear_tris=[];gear_open=[];gear_descriptions=[]
    for comp in components(chest,gear_polys):
        tri,meta=triangle_topology(chest,comp,False)
        if meta['boundary_edges_after_audit_caps']==0 and meta['edges_with_more_than_two_faces']==0:
            gear_descriptions.append({'target':'equipment_'+str(len(gear_tris)),'materials':sorted({chest.data.materials[chest.data.polygons[p].material_index].name for p in comp}),'rest_centroid':list(sum((chest.data.vertices[i].co for i in {vi for t in tri for vi in t}),Vector())/len({vi for t in tri for vi in t}))})
            gear_tris.append(tri)
        else:gear_open.append({'faces':len(comp),'boundary_edges':meta['original_boundary_edges']})
    if options.topology_only:
        print('TOPOLOGY',action_name,json.dumps(topology),flush=True);continue
    sleeves={}
    for side,letter in [('Left','L'),('Right','R')]:
        obj=bpy.data.objects[prefix+'_Arm_'+letter]
        polys=[p.index for p in obj.data.polygons if garment(obj.data.materials[p.material_index].name)]
        tris,_=triangle_topology(obj,polys)
        xf=rig.matrix_world.inverted()@obj.matrix_world
        samples=[((vi,),classify(xf@obj.data.vertices[vi].co,rig,side)) for vi in sorted({vi for t in tris for vi in t})]
        samples += [(tri,classify(sum((xf@obj.data.vertices[vi].co for vi in tri),Vector())/3,rig,side)) for tri in tris]
        sleeves[side]=(obj,samples)
    row={'frames':len(frames),'authored_frames':count,'sampled_frame_numbers':frames,'complete_integer_frame_coverage':frames==list(range(1,count+1)),'torso_topology':topology,'closed_chest_equipment_components':len(gear_tris),'equipment_components':gear_descriptions,'open_chest_equipment_components_not_volumetrically_classified':gear_open,'sides':{},'per_frame':[]}
    for side,(obj,samples) in sleeves.items():
        row['sides'][side]={zone:{'samples_per_frame':sum(z==zone for _,z in samples),'max_penetration_m':0,'max_samples_deeper_than_2mm':0,'worst_frame':None,'target':None,'max_depth_nearest_actual_surface_m':0,'max_depth_nearest_audit_cap_m':0,'samples_deeper_than_2mm_nearest_actual_surface_max':0,'samples_deeper_than_2mm_nearest_audit_cap_max':0} for zone in set(z for _,z in samples)}
    for f in frames:
        scene.frame_set(f);bpy.context.view_layer.update();cv=evaluated(chest,rig)
        capped_cv=cv+[sum((cv[i] for i in indices),Vector())/len(indices) for indices in topology['audit_virtual_center_source_indices']]
        targets=[('torso',make_target(capped_cv,torso_tris))]
        targets += [('equipment_'+str(i),make_target(cv,tri)) for i,tri in enumerate(gear_tris)]
        grid=target_grid(targets)
        per={'frame':f,'zones':{}}
        for side,(obj,samples) in sleeves.items():
            av=evaluated(obj,rig);counts={z:0 for z in row['sides'][side]};surface_counts={z:0 for z in counts};cap_counts={z:0 for z in counts};maximum={z:(0,None,None,None) for z in counts}
            for indices,zone in samples:
                p=sum((av[vi] for vi in indices),Vector())/len(indices)
                candidates=grid.get(tuple(math.floor(v/.05) for v in p),[])
                depth,target=max(((inside_depth(tree,p),name) for name,tree in candidates),key=lambda x:x[0],default=(0,None))
                if depth>0:
                    tree=dict(targets)[target][0];nearest=tree.find_nearest(p);is_cap=target=='torso' and nearest[2] in topology['audit_cap_triangle_indices']
                    suffix='audit_cap' if is_cap else 'actual_surface'
                    field='max_depth_nearest_'+suffix+'_m';row['sides'][side][zone][field]=max(row['sides'][side][zone][field],depth)
                    if depth>.002:(cap_counts if is_cap else surface_counts)[zone]+=1
                if depth>.002:counts[zone]+=1
                if depth>maximum[zone][0]:maximum[zone]=(depth,target,list(p),indices)
            for zone in counts:
                datum=row['sides'][side][zone];depth,target,point,indices=maximum[zone]
                if depth>datum['max_penetration_m']:
                    xf=rig.matrix_world.inverted()@obj.matrix_world
                    rest=sum((xf@obj.data.vertices[vi].co for vi in indices),Vector())/len(indices)
                    datum.update(max_penetration_m=depth,worst_frame=f,target=target,worst_sample_rig_position=point,worst_sample_world_position=list(rig.matrix_world@Vector(point)),worst_sample_rest_rig_position=list(rest),worst_sample_source_vertex_indices=list(indices))
                datum['max_samples_deeper_than_2mm']=max(datum['max_samples_deeper_than_2mm'],counts[zone])
                for suffix,values in [('actual_surface',surface_counts),('audit_cap',cap_counts)]:
                    field='samples_deeper_than_2mm_nearest_'+suffix+'_max';datum[field]=max(datum[field],values[zone])
                if point is not None and datum['worst_frame']==f:
                    nearest=dict(targets)[target][0].find_nearest(Vector(point));datum['worst_sample_nearest_surface_kind']='audit_seam_cap' if target=='torso' and nearest[2] in topology['audit_cap_triangle_indices'] else 'actual_mesh_surface'
                per['zones'][side+'_'+zone]={'max_depth_m':depth,'samples_deeper_than_2mm':counts[zone]}
        row['per_frame'].append(per)
    report['clips'][action_name]=row
    print('CLEARANCE',action_name,json.dumps(row['sides']),flush=True)
OUT.parent.mkdir(parents=True,exist_ok=True);OUT.write_text(json.dumps(report,indent=2));print('WROTE',OUT,flush=True)
