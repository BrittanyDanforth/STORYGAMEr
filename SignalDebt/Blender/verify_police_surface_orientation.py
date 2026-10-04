"""Bounded face-orientation check for closed police surface components."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Blender/Characters.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
records=[];closed_count=0;open_count=0
for obj in bpy.data.objects:
    if obj.type!='MESH' or not obj.name.startswith(('Patrol_Officer','SWAT_Officer')):continue
    bm=bmesh.new();bm.from_mesh(obj.data);pending=set(bm.faces)
    while pending:
        first=pending.pop();todo=[first];faces=[first]
        while todo:
            face=todo.pop()
            for edge in face.edges:
                for linked in edge.link_faces:
                    if linked in pending:pending.remove(linked);todo.append(linked);faces.append(linked)
        edges={e for f in faces for e in f.edges}
        if not all(len(e.link_faces)==2 for e in edges):open_count+=1;continue
        closed_count+=1;volume=0
        for face in faces:
            a=face.verts[0].co
            for index in range(1,len(face.verts)-1):volume+=a.dot(face.verts[index].co.cross(face.verts[index+1].co))/6
        if volume < -1e-9:
            verts={v for face in faces for v in face.verts};center=sum((v.co for v in verts),Vector())/len(verts)
            records.append({'object':obj.name,'material':obj.data.materials[first.material_index].name,'signed_volume_m3':volume,'center_local_m':list(center),'faces':len(faces)})
    bm.free()
report={'status':'PASS' if not records else 'FAIL','source_blend_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'closed_components_checked':closed_count,'open_skin_region_components_not_volume_tested':open_count,'inward_closed_components':records,'scope':'Rest-mesh signed volumes for closed connected components only; not a runtime deformation or collision guarantee.'}
(ROOT/'Validation/police_surface_orientation.json').write_text(json.dumps(report,indent=2))
print('POLICE_SURFACE_ORIENTATION',json.dumps(report));assert not records
