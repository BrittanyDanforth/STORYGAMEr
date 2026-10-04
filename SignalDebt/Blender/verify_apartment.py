"""Read actual source and exported FBX back into Blender; record export integrity."""
import bpy
import bmesh
import json
import struct
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / 'Blender' / 'SignalDebt_Apartment.blend'
art = root / 'UnityProject' / 'Assets' / 'Art' / 'Apartment'
required = ['PlayerSpawn','DeskSeat','LaptopFocus','DoorHinge','DoorInteract','BedInteract','BillInteract']
report = {}
bpy.ops.wm.open_mainfile(filepath=str(source), use_scripts=False)
for name in required:
    assert name in bpy.data.objects, name
assert bpy.data.objects['DoorLeaf'].parent.name == 'DoorHinge'
report['source_anchor_positions'] = {name:list(bpy.data.objects[name].matrix_world.translation) for name in required}
cloth = bpy.data.objects['Blanket_Draped']
central = [v.co.z for v in cloth.data.vertices if abs(v.co.x+1.85)<.63 and v.co.y>.27]
report['central_blanket_minimum_z'] = min(central)
report['mattress_top_z'] = .685
assert min(central) > .695, 'Blanket intersects mattress within central bed area'
assert 'Preview_04_Entrance' in bpy.data.objects
assert 'Preview_05_Desk_Detail' in bpy.data.objects
mouse=bpy.data.objects['Mouse_Underside']
bm=bmesh.new();bm.from_mesh(mouse.data)
mouse_volume=bm.calc_volume(signed=True);bm.free()
assert mouse_volume>0, 'Mouse underside must have outward-facing normals for Unity culling'
report['mouse_underside_outward_volume_m3']=mouse_volume
pitch=bpy.data.objects['Keycap_0_1'].location.x-bpy.data.objects['Keycap_0_0'].location.x
assert abs(pitch-.019)<.00001, 'Laptop key pitch must remain 19 mm'
report['keyboard_key_pitch_metres']=pitch
legend=bpy.data.materials['Key Legend'].node_tree.nodes.get('Principled BSDF')
assert legend.inputs['Emission Strength'].default_value==0, 'Physical key legends should not emit light'
report['keyboard_legends_non_emissive']=True
source_positions_expected={'PlayerSpawn':(2.05,-1.89,.022),'DeskSeat':(1.68,.91,.022),
    'LaptopFocus':(1.73,1.96053648,.99504876),'DoorHinge':(1.6,-2.63,.023),
    'DoorInteract':(2.32,-2.38,1.095),'BedInteract':(-.975,.86,.63),'BillInteract':(.47,2.5,1.48)}
for name,expected in source_positions_expected.items():
    actual=bpy.data.objects[name].matrix_world.translation
    assert max(abs(actual[i]-expected[i]) for i in range(3))<.00001, 'Gameplay anchor moved: '+name
report['quality_pass_preserves_gameplay_anchor_positions']=True
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(art / 'Apartment.fbx'))
for name in required:
    assert name in bpy.data.objects, 'Missing FBX anchor '+name
assert bpy.data.objects['DoorLeaf'].parent.name == 'DoorHinge', 'FBX door pivot hierarchy broken'
for name in required:
    a=report['source_anchor_positions'][name]
    b=bpy.data.objects[name].matrix_world.translation
    assert max(abs(a[i]-b[i]) for i in range(3)) < .0001, (name,a,list(b))
report['fbx_roundtrip_anchor_positions_match_metres'] = True
report['fbx_door_parent'] = bpy.data.objects['DoorLeaf'].parent.name
report['fbx_mesh_objects'] = sum(o.type=='MESH' for o in bpy.data.objects)
glb = art / 'SignalDebt_Apartment.glb'
data=glb.read_bytes()
magic,version,size=struct.unpack_from('<4sII',data)
assert magic==b'glTF' and version==2 and size==len(data)
chunk_size,chunk_type=struct.unpack_from('<II',data,12)
assert chunk_type==0x4E4F534A
doc=json.loads(data[20:20+chunk_size])
node_names={n.get('name') for n in doc['nodes']}
assert set(required).issubset(node_names)
report['glb_size_matches_header'] = True
report['glb_required_anchor_nodes_present'] = True
report['status']='PASS'
(root/'Blender'/'asset_verification.json').write_text(json.dumps(report,indent=2))
print('ASSET VERIFICATION PASS '+json.dumps(report),flush=True)
