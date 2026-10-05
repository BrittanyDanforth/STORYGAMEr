"""Bake and export the approved external neighborhood; excludes apartment reference.
Invoked by build_neighborhood.py --export, or open a generated .blend and run here.
No Unity/project/repository writes. Real Cycles texture bakes, no lighting baked in.
"""
import argparse, hashlib, json, math, sys
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

SWAP=Matrix(((1,0,0),(0,0,1),(0,1,0)))
TILE=1024

def srgb(v):return 12.92*v if v<=.0031308 else 1.055*(max(0,v)**(1/2.4))-.055

def reference(o):return bool(o.get('review_apartment_reference'))

def new_uv(o,tile_size=(1,1)):
    if o.type!='MESH':return
    me=o.data;uv=me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
    for f in me.polygons:
        n=f.normal;ax=max(range(3),key=lambda k:abs(n[k]));axes=(0,1) if ax==2 else ((0,2) if ax==1 else (1,2))
        for li in f.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=(v[axes[0]]/tile_size[0],v[axes[1]]/tile_size[1])


def bake_material(material,out,normal=False):
    """Real shader evaluation into one shared metre-scaled tile, separate from scene lighting."""
    height=1.08 if 'Brick' in material.name else 1.0
    me=bpy.data.meshes.new('BakePlaneMesh');me.from_pydata([(0,0,0),(1,0,0),(1,0,height),(0,0,height)],[],[(0,1,2,3)]);me.update()
    obj=bpy.data.objects.new('BakePlane',me);obj['export_bake_fixture']=True;bpy.context.scene.collection.objects.link(obj);new_uv(obj,(1,height))
    temporary=material.copy();temporary.name='Bake_'+material.name;me.materials.append(temporary)
    n=temporary.node_tree.nodes;l=temporary.node_tree.links;p=n.get('Principled BSDF');output=n.get('Material Output')
    suffix='Normal' if normal else 'BaseColor';im=bpy.data.images.new(material.name+'_'+suffix,width=TILE,height=TILE,alpha=False,float_buffer=False)
    im.colorspace_settings.name='Non-Color' if normal else 'sRGB'
    image_node=n.new('ShaderNodeTexImage');image_node.image=im
    for node in n:node.select=False
    image_node.select=True;n.active=image_node
    if not normal:
        em=n.new('ShaderNodeEmission');inp=p.inputs['Base Color']
        if inp.is_linked:l.new(inp.links[0].from_socket,em.inputs['Color'])
        else:em.inputs['Color'].default_value=inp.default_value
        em.inputs['Strength'].default_value=1;l.new(em.outputs[0],output.inputs['Surface'])
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=4;scene.render.bake.use_clear=True;scene.render.bake.margin=8;scene.render.bake.normal_space='TANGENT'
    bpy.ops.object.bake(type='NORMAL' if normal else 'EMIT',use_clear=True,margin=8)
    path=out/'Textures'/f'{material.name}_{suffix}.png';im.filepath_raw=str(path);im.file_format='PNG';im.save()
    bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(me);bpy.data.materials.remove(temporary)
    return im,path,(1,height)


def converted_materials(objects,out):
    materials=sorted({m for o in objects if o.type in {'MESH','FONT'} for m in o.data.materials if m},key=lambda m:m.name)
    entries=[];textures=[];tile_sizes={}
    for index,m in enumerate(materials):
        p=m.node_tree.nodes.get('Principled BSDF') if m.use_nodes else None
        if not p:raise RuntimeError('Unsupported authored material '+m.name)
        base=list(p.inputs['Base Color'].default_value);rough=float(p.inputs['Roughness'].default_value);metal=float(p.inputs['Metallic'].default_value);strength=float(p.inputs['Emission Strength'].default_value);em=list(p.inputs['Emission Color'].default_value)[:3]
        isglass=m.name=='ClearGlass';entry={'name':m.name,'base_color_rgba':[srgb(v) for v in base[:3]]+[.12 if isglass else base[3]],'metallic':metal,'smoothness':1-rough,'alpha_mode':'transparent' if isglass else 'opaque','alpha_cutoff':.5,'emission_rgb':[v*strength for v in em],'base_color':'','normal':'','metallic_smoothness':'','emission':''}
        base_image=None;normal_image=None
        if p.inputs['Base Color'].is_linked:
            base_image,path,tile=bake_material(m,out,False);entry['base_color']='Textures/'+path.name;entry['base_color_rgba'][:3]=[1,1,1];tile_sizes[m.name]=tile
            textures.append({'path':entry['base_color'],'role':'base_color','srgb':True,'normal_map':False,'physical_tile_size_m':list(tile),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        if p.inputs['Normal'].is_linked:
            normal_image,path,tile=bake_material(m,out,True);entry['normal']='Textures/'+path.name;tile_sizes[m.name]=tile
            textures.append({'path':entry['normal'],'role':'normal','srgb':False,'normal_map':True,'physical_tile_size_m':list(tile),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        # Replace all procedural review nodes with portable standard material inputs.
        nodes=m.node_tree.nodes;nodes.clear();pr=nodes.new('ShaderNodeBsdfPrincipled');output=nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(pr.outputs['BSDF'],output.inputs['Surface']);pr.inputs['Base Color'].default_value=tuple(base);pr.inputs['Roughness'].default_value=rough;pr.inputs['Metallic'].default_value=metal;pr.inputs['Emission Color'].default_value=(*em,1);pr.inputs['Emission Strength'].default_value=strength
        if base_image:
            t=nodes.new('ShaderNodeTexImage');t.image=base_image;t.extension='REPEAT';m.node_tree.links.new(t.outputs['Color'],pr.inputs['Base Color'])
        if normal_image:
            t=nodes.new('ShaderNodeTexImage');t.image=normal_image;t.extension='REPEAT';normal_node=nodes.new('ShaderNodeNormalMap');normal_node.uv_map='UVMap';m.node_tree.links.new(t.outputs['Color'],normal_node.inputs['Color']);m.node_tree.links.new(normal_node.outputs['Normal'],pr.inputs['Normal'])
        if isglass:
            # Blender export preview uses explicit alpha; Unity receives the same alpha contract.
            pr.inputs['Alpha'].default_value=.12;pr.inputs['Transmission Weight'].default_value=0;m.diffuse_color=(*base[:3],.12);m.surface_render_method='DITHERED'
        else:m.diffuse_color=tuple(base)
        entries.append(entry);print(f'MATERIAL_BAKED {index+1}/{len(materials)} {m.name}',flush=True)
    for o in objects:
        if o.type=='MESH':
            tile=(1,1)
            if o.data.materials:tile=tile_sizes.get(o.data.materials[0].name,(1,1))
            new_uv(o,tile)
    return entries,textures


def export_scene(out):
    out=Path(out).resolve();(out/'Textures').mkdir(parents=True,exist_ok=True)
    source_manifest=json.loads((out/'neighborhood_collision_anchors.json').read_text())
    source=Path(source_manifest['immutable_apartment_source']);assert hashlib.sha256(source.read_bytes()).hexdigest()==source_manifest['immutable_apartment_sha256']
    objects=[o for o in bpy.context.scene.objects if o.type in {'MESH','EMPTY','FONT'} and not reference(o) and not o.get('export_bake_fixture')]
    reference_names={o.name for o in bpy.context.scene.objects if reference(o)}
    assert objects and not any(reference(o) for o in objects)
    refs={a['name']:a for a in source_manifest['anchors'] if a['kind']=='basis'}
    for key in ['Ref_Origin','Ref_East','Ref_North','Ref_Up']:assert key in refs and bpy.data.objects.get(key) in objects,key
    material_entries,texture_entries=converted_materials(objects,out)
    collider_entries=[];seen=set()
    for c in source_manifest['colliders']:
        name=c['renderer_name'];assert name not in seen,name;seen.add(name);obj=bpy.data.objects.get(name);assert obj in objects,(name,'not in neighborhood export')
        entry={k:c[k] for k in ['name','renderer_name','type','visible','trigger']}
        if c['type']=='box':entry.update(center_design_unity=c['center_design_unity'],size_unity=c['size_unity'])
        else:
            entry['vertices_xyz']=[v for point in c['vertices_design_unity'] for v in point];entry['triangles']=[]
            for f in c['faces']:
                for i in range(1,len(f)-1):entry['triangles'].extend([f[0],f[i+1],f[i]])
            vv=c['vertices_design_unity']
            for i in range(0,len(entry['triangles']),3):
                a,b,d=[Vector(vv[k]) for k in entry['triangles'][i:i+3]];assert (b-a).cross(d-a).y>0,(name,'walking mesh not upward')
        collider_entries.append(entry)
    fixtures=[]
    for key in ['Corridor_Fuse_Box','Corridor_Floor_Label']:
        matches=[o for o in bpy.context.scene.objects if o.get('apartment_original_name')==key];assert len(matches)==1,(key,len(matches));o=matches[0];p=SWAP@o.matrix_world.translation;q=(SWAP@o.matrix_world.to_3x3().normalized()@SWAP).to_quaternion().normalized();fixtures.append({'name':key,'position_design_unity':list(p),'rotation_design_xyzw':[q.x,q.y,q.z,q.w]})
    sun=bpy.data.objects['SoftAfternoonSun'];forward=SWAP@(sun.matrix_world.to_quaternion()@Vector((0,0,-1)))
    manifest={'schema_version':1,'units':'m','export_scope':'neighborhood_only','status':'EXTERNAL REVIEW EXPORT; native import and traversal pending','immutable_apartment_sha256':source_manifest['immutable_apartment_sha256'],'apartment_source_to_design':[-1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1],'anchors':source_manifest['anchors']+[{'name':'ApartmentApertureFloor','position_design_unity':[-2.1,.02,4.35],'kind':'floor_reference'}],'aperture':{'floor_design_unity':[-2.1,.02,4.35],'clear_width':2.09,'clear_height':2.59},'moved_apartment_fixtures':fixtures,'colliders':collider_entries,'materials':material_entries,'textures':texture_entries,'daylight':{'forward_design_unity':list(forward),'color_srgb':[1,.97,.92],'intensity':2,'ambient_sky_srgb':[.64,.73,.82],'ambient_equator_srgb':[.43,.47,.51],'ambient_ground_srgb':[.24,.25,.25],'ambient_intensity':1.0},'geometry_checks':{'closed_boxes_outward_signed_volume_count':source_manifest['generated_closed_boxes_verified_positive_signed_volume'],'generated_horizontal_slab_overlap_area_m2':source_manifest['horizontal_slabs_same_height_overlap_area'],'mesh_triangles_upward':True,'original_apartment_hash_unchanged':True},'material_encoding':{'base_color_rgba':'sRGB UI/material values; use as Unity Color properties','emission_rgb':'linear HDR','base_color_texture':'sRGB','normal_texture':'linear tangent-space','normal_map_convention':'OpenGL/+Y tangent normal, Unity TextureImporterType.NormalMap'},'review_cameras':source_manifest['cameras']}
    # Add real aperture empty as well as JSON metadata; it is not a replacement apartment anchor.
    ap=bpy.data.objects.new('ApartmentApertureFloor',None);bpy.context.scene.collection.objects.link(ap);ap.location=(-2.1,4.35,.02);objects.append(ap)
    (out/'neighborhood_collision_anchors.json').write_text(json.dumps(manifest,indent=2)+'\n')
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=next(o for o in objects if o.type=='MESH')
    fbx=out/'Neighborhood.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY','OTHER'},global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_space_transform=False,add_leaf_bones=False,use_mesh_modifiers=True,use_tspace=True,path_mode='RELATIVE',embed_textures=False)
    # .blend retains editable reference room but every reference object is explicitly excluded from the FBX.
    for im in bpy.data.images:
        if im.source=='FILE' and im.filepath and str(out/'Textures') in bpy.path.abspath(im.filepath):im.filepath=bpy.path.relpath(im.filepath,start=str(out))
    bpy.ops.wm.save_as_mainfile(filepath=str(out/'Southbank_Neighborhood_Export_Review.blend'))
    proof={'status':'EXTERNAL EXPORT; Unity not run','fbx':fbx.name,'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'exported_objects':len(objects),'exported_names':[o.name for o in objects],'excluded_apartment_reference_objects':len(reference_names),'export_contains_apartment_reference':False,'basis_names':list(refs),'texture_count':len(texture_entries),'material_count':len(material_entries),'collider_count':len(collider_entries),'source_apartment_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'generator_sha256':hashlib.sha256((Path(__file__).parent/'build_neighborhood.py').read_bytes()).hexdigest(),'exporter_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    (out/'export-proof.json').write_text(json.dumps(proof,indent=2)+'\n')
    print('NEIGHBORHOOD_EXTERNAL_EXPORT_COMPLETE',fbx,flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--output',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);export_scene(a.output)
