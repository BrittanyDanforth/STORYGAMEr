"""Read actual source and exported FBX back into Blender; record export integrity."""
import bpy
import bmesh
import json
import struct
import hashlib
from pathlib import Path
from mathutils.bvhtree import BVHTree

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
quality = {'blender_version':bpy.app.version_string,'asset':'SignalDebt_Apartment',
           'scope':'Authored Blender geometry, material maps and FBX/GLB interchange. Unity playback and hardware performance are not established.'}
quality['blender_build_hash']=bpy.app.build_hash.decode('ascii')
quality['source_sha256']=hashlib.sha256(source.read_bytes()).hexdigest()
quality['source_objects']=len(bpy.context.scene.objects)
quality['base_polygons']=sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH')
quality['denoiser']={'enabled':bpy.context.scene.cycles.use_denoising,
                     'name':bpy.context.scene.cycles.denoiser,
                     'threads':bpy.context.scene.render.threads}
quality['closed_pillow_shells']={}
for name in ('Pillow_-2.2','Pillow_-1.5'):
    obj=bpy.data.objects[name]
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh=evaluated.to_mesh()
    bm=bmesh.new();bm.from_mesh(mesh)
    volume=bm.calc_volume(signed=True)
    bad_edges=sum(not e.is_manifold for e in bm.edges)
    min_z=min((evaluated.matrix_world@v.co).z for v in mesh.vertices)
    quality['closed_pillow_shells'][name]={'base_vertices':len(obj.data.vertices),
        'evaluated_vertices':len(mesh.vertices),'signed_volume_m3':volume,
        'nonmanifold_edges':bad_edges,'minimum_z':min_z,
        'mattress_surface_gap_m':min_z-.685}
    assert bad_edges==0 and volume>0, ('Pillow shell must be outward and closed',name)
    assert .685<=min_z<=.687, ('Pillow underside must rest within 2 mm of mattress',name,min_z)
    assert name+'_Sewn_Edge' in bpy.data.objects
    bm.free();evaluated.to_mesh_clear()
quality['mattress_welt_surface_contact']={}
mattress=bpy.data.objects['Mattress']
tree=BVHTree.FromObject(mattress,bpy.context.evaluated_depsgraph_get())
for name in ('Mattress_Upper_Welt','Mattress_Lower_Welt'):
    obj=bpy.data.objects[name]
    gaps=[]
    for point in obj.data.splines[0].bezier_points:
        local=mattress.matrix_world.inverted()@obj.matrix_world@point.co
        nearest,normal,_,distance=tree.find_nearest(local)
        gaps.append((local-nearest).dot(normal))
    quality['mattress_welt_surface_contact'][name]={'minimum_center_offset_m':min(gaps),
        'maximum_center_offset_m':max(gaps),'piping_radius_m':obj.data.bevel_depth}
    assert min(gaps)>-.0015 and max(gaps)<.0045, ('Welt must follow mattress surface',name,min(gaps),max(gaps))
quality['portable_pbr_materials']={}
for name in ('Cotton Ivory Textile','Cotton Teal','Fabric Charcoal'):
    material=bpy.data.materials[name]
    shader=material.node_tree.nodes.get('Principled BSDF')
    assert shader.inputs['Normal'].is_linked and shader.inputs['Roughness'].is_linked, name
    maps={n.image.name:n.image.colorspace_settings.name for n in material.node_tree.nodes
          if n.type=='TEX_IMAGE' and n.image and any(s in n.image.name for s in ('Normal','Roughness'))}
    assert len(maps)==2 and set(maps.values())=={'Non-Color'}, (name,maps)
    quality['portable_pbr_materials'][name]=maps
quality['construction_details']=['Mattress_Upper_Welt','Mattress_Lower_Welt',
    'Headboard_Upholstery_Piped_Seam','Blanket_Left_Hem','Blanket_Right_Hem','Blanket_Foot_Hem']
assert all(name in bpy.data.objects for name in quality['construction_details'])
quality['all_anchor_positions']={o.name:list(o.matrix_world.translation)
                                for o in bpy.data.collections['Gameplay_Anchors'].objects}
original=root.parents[1]/'signaldebt-import'/'SignalDebt'/'Blender'/'apartment_manifest.json'
if original.is_file():
    before=json.loads(original.read_text())
    after=json.loads((root/'Blender'/'apartment_manifest.json').read_text())
    assert before['anchors']==after['anchors'], 'An imported gameplay anchor changed'
    assert before['colliders']==after['colliders'], 'A collision proxy changed'
    quality['all_original_anchors_and_colliders_unchanged']=True
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
quality['fbx_mesh_objects']=report['fbx_mesh_objects']
quality['fbx_evaluated_polygons']=sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH')
quality['fbx_evaluated_triangles']=0
for obj in bpy.data.objects:
    if obj.type=='MESH':
        obj.data.calc_loop_triangles()
        quality['fbx_evaluated_triangles']+=len(obj.data.loop_triangles)
quality['fbx_pbr_map_links']={}
for name in quality['portable_pbr_materials']:
    material=bpy.data.materials[name]
    shader=material.node_tree.nodes.get('Principled BSDF')
    assert shader.inputs['Normal'].is_linked and shader.inputs['Roughness'].is_linked, ('FBX lost PBR map',name)
    maps=[n.image.name for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    quality['fbx_pbr_map_links'][name]=maps
quality['unity_pbr_import_note']='FBX map links survive Blender reimport. Unity Standard/URP roughness-to-smoothness interpretation and Play Mode have not been verified here.'
quality['unity_import_checks_remaining']=[
    'Set Woven_Cotton_Normal.png Texture Type to Normal map; use linear data interpretation.',
    'Treat Woven_Cotton_Roughness.png as linear data, with sRGB disabled.',
    'If the active Unity shader expects smoothness, convert smoothness = 1 - roughness and pack it into the channel that shader requires.',
    'Confirm named FBX material texture assignments, scale and visible detail in the actual Unity scene before claiming in-engine parity.']
quality['fbx_roundtrip_all_anchor_positions_match_metres']=True
for name,expected in quality['all_anchor_positions'].items():
    actual=bpy.data.objects[name].matrix_world.translation
    assert max(abs(actual[i]-expected[i]) for i in range(3))<.0001, ('FBX anchor changed',name)
for name in quality['construction_details']+list(quality['closed_pillow_shells']):
    assert name in bpy.data.objects, ('Missing exported construction detail',name)
    assert bpy.data.objects[name].type=='MESH'
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
quality['glb_portable_pbr_materials']={}
for material in doc['materials']:
    if material['name'] in quality['portable_pbr_materials']:
        assert 'normalTexture' in material, ('GLB missing normal map',material['name'])
        assert 'metallicRoughnessTexture' in material['pbrMetallicRoughness'], ('GLB missing roughness map',material['name'])
        quality['glb_portable_pbr_materials'][material['name']]={'normal_texture':material['normalTexture'],
            'metallic_roughness_texture':material['pbrMetallicRoughness']['metallicRoughnessTexture']}
assert len(quality['glb_portable_pbr_materials'])==3
quality['status']='PASS'
(root/'Validation').mkdir(exist_ok=True)
(root/'Validation'/'apartment_quality_validation.json').write_text(json.dumps(quality,indent=2))
report['status']='PASS'
(root/'Blender'/'asset_verification.json').write_text(json.dumps(report,indent=2))
print('ASSET VERIFICATION PASS '+json.dumps(report),flush=True)
