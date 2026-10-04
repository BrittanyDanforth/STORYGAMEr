"""Read-only Patrol render-equivalence proof between two saved sources.

Run with --disable-autoexec --python-exit-code 1. No blend or render manifest is
written. Whole-file source hashes remain separate from actor-content digests.
"""
import argparse
from array import array
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('before_root', type=Path)
parser.add_argument('after_root', type=Path)
parser.add_argument('--output', type=Path, default=ROOT/'Validation/quality_pass_patrol_actor_equivalence.json')
options = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
CLIPS = {'Patrol_LowReadyIdle': 61, 'Patrol_LowReadyToAim': 46, 'Patrol_AimToLowReady': 46}
NORMAL_COMPONENT_BOUND = 1e-6
NORMALS = {}
NORMAL_COMPARISONS = []
UV_COMPONENT_BOUND = 1e-6
UVS = {}
UV_COMPARISONS = []
MATERIAL_SHADER_TYPES = {}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()).hexdigest()


def mat(m):
    return [list(row) for row in m]


def scalar(value):
    if value is None or isinstance(value, (str, bool, int, float)):
        return value
    if isinstance(value, bpy.types.ID):
        return {'id_type': value.bl_rna.identifier, 'name': value.name}
    try:
        return [scalar(v) for v in value]
    except TypeError:
        raise TypeError(('Unsupported value requires explicit audit', type(value).__name__))


def rna_simple(value, omit=()):
    result = {}
    for prop in value.bl_rna.properties:
        if prop.identifier in {'rna_type', *omit} or prop.is_readonly:
            continue
        if prop.type in {'BOOLEAN', 'INT', 'FLOAT', 'STRING', 'ENUM'}:
            v = getattr(value, prop.identifier)
            result[prop.identifier] = sorted(v) if isinstance(v, set) else scalar(v)
        elif prop.type == 'POINTER':
            v = getattr(value, prop.identifier)
            if v is None or isinstance(v, bpy.types.ID):
                result[prop.identifier] = scalar(v)
    return result


def packed(collection, prop, width=1, kind='f'):
    values = array(kind, [0]) * (len(collection) * width)
    if values:
        collection.foreach_get(prop, values)
    return {'count': len(collection), 'width': width, 'type': kind,
            'sha256': hashlib.sha256(values.tobytes()).hexdigest()}


def shader(tree):
    if tree is None:
        return None
    nodes = {}
    for node in tree.nodes:
        nodes[node.name] = {'type': node.bl_idname, 'properties': rna_simple(node),
                            'inputs': {s.identifier: scalar(s.default_value) for s in node.inputs if hasattr(s, 'default_value')}}
        if getattr(node, 'node_tree', None) is not None:
            nodes[node.name]['nested_tree'] = shader(node.node_tree)
    return {'nodes': nodes, 'links': sorted((l.from_node.name, l.from_socket.identifier,
                                            l.to_node.name, l.to_socket.identifier) for l in tree.links)}


def material(m):
    MATERIAL_SHADER_TYPES[m.name]=sorted(n.bl_idname for n in m.node_tree.nodes) if m.use_nodes else []
    return {'name': m.name, 'properties': rna_simple(m), 'shader': shader(m.node_tree) if m.use_nodes else None}


def mesh(m):
    # Blender rebuilds may permute edge/face/loop storage without altering any
    # oriented surface, UV, normal or material. Canonicalize connectivity and
    # carry every associated attribute with its element; never round a value.
    result = {'vertices': packed(m.vertices, 'co', 3), 'point_attributes': {}}
    types = {'FLOAT': ('value', 1, 'f'), 'INT': ('value', 1, 'i'), 'INT8': ('value', 1, 'i'),
             'INT32_2D': ('value', 2, 'i'), 'INT16_2D': ('value', 2, 'i'),
             'BOOLEAN': ('value', 1, 'b'), 'FLOAT_VECTOR': ('vector', 3, 'f'),
             'FLOAT2': ('vector', 2, 'f'), 'FLOAT_COLOR': ('color', 4, 'f'), 'BYTE_COLOR': ('color', 4, 'f')}
    attributes = {'EDGE': {}, 'FACE': {}, 'CORNER': {}}
    uv_attributes={}
    for a in m.attributes:
        if a.name in {'.edge_verts', '.corner_vert', '.corner_edge','custom_normal','.custom_normal'}:
            continue  # Connectivity and decoded corner normals cover these encodings.
        if a.data_type not in types:
            raise ValueError(('Unreviewed mesh attribute', m.name, a.name, a.data_type))
        prop, width, kind = types[a.data_type]
        if a.name in m.uv_layers:
            assert a.domain=='CORNER' and width==2 and kind=='f'
            values=array('f',[0])*(len(a.data)*2);a.data.foreach_get(prop,values)
            uv_attributes[a.name]=values
            continue
        if a.domain == 'POINT':
            result['point_attributes'][a.name] = {'type': a.data_type, 'data': packed(a.data, prop, width, kind)}
        else:
            values = array(kind, [0]) * (len(a.data) * width)
            a.data.foreach_get(prop, values)
            attributes[a.domain][a.name] = (a.data_type, width, values)
    def attrs(domain, i):
        return tuple((name, kind, tuple(values[i*width:(i+1)*width]))
                     for name, (kind, width, values) in sorted(attributes[domain].items()))
    edges = sorted((tuple(sorted(edge.vertices)), attrs('EDGE', edge.index)) for edge in m.edges)
    faces = []
    for p in m.polygons:
        corners = [(loop.vertex_index, tuple(m.corner_normals[loop.index].vector), attrs('CORNER', loop.index),loop.index)
                   for loop in (m.loops[i] for i in p.loop_indices)]
        first = min(range(len(corners)), key=lambda i: corners[i][0])
        corners = corners[first:] + corners[:first]
        faces.append((tuple(corners), p.material_index, p.use_smooth, attrs('FACE', p.index)))
    result['canonical_edges_sha256'] = digest(edges)
    # Face ordering must depend on connectivity and authored attributes, never
    # the calculated normal values being compared under the separate bound.
    ordered=sorted(faces,key=lambda f:(tuple(c[0] for c in f[0]),f[1],f[2],tuple(c[2] for c in f[0]),f[3]))
    result['canonical_oriented_connectivity_material_attributes_sha256'] = digest([
        (tuple((corner[0],corner[2]) for corner in corners), material_index, smooth, attributes)
        for corners,material_index,smooth,attributes in ordered])
    normal_values=array('f',(v for corners,material_index,smooth,attributes in ordered for corner in corners for v in corner[1]))
    normal_hash=hashlib.sha256(normal_values.tobytes()).hexdigest()
    NORMALS.setdefault(normal_hash,normal_values)
    result['canonical_corner_normals_sha256'] = normal_hash
    result['canonical_uvs']={}
    for name,values in uv_attributes.items():
        canonical=array('f',(v for corners,material_index,smooth,attributes in ordered for corner in corners for v in values[corner[3]*2:corner[3]*2+2]))
        h=hashlib.sha256(canonical.tobytes()).hexdigest();UVS.setdefault(h,canonical)
        result['canonical_uvs'][name]={'uv_values_sha256':h,'components':len(canonical)}
    return result


def curves(action):
    if hasattr(action, 'fcurves'):
        return list(action.fcurves)
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def action(a):
    data = {'frame_range': list(a.frame_range), 'markers': sorted((m.name, m.frame) for m in a.pose_markers), 'curves': {}}
    for fc in curves(a):
        key = fc.data_path + ':' + str(fc.array_index)
        data['curves'][key] = {'extrapolation': fc.extrapolation, 'mute': fc.mute,
                              'keys': [rna_simple(k) for k in fc.keyframe_points],
                              'modifiers': [rna_simple(m) for m in fc.modifiers]}
    return data


def object_static(o):
    result = {'type': o.type, 'parent': o.parent.name if o.parent else None,
              'parent_type': o.parent_type, 'parent_bone': o.parent_bone,
              'basis': mat(o.matrix_basis), 'parent_inverse': mat(o.matrix_parent_inverse),
              'world': mat(o.matrix_world), 'hide_render': o.hide_render,
              'hide_viewport': o.hide_viewport, 'visible_camera': o.visible_camera,
              'modifiers': [rna_simple(m) for m in o.modifiers],
              'constraints': [rna_simple(c) for c in o.constraints]}
    if o.type == 'MESH':
        result['mesh'] = mesh(o.data)
        result['weights_sha256'] = digest([sorted((o.vertex_groups[g.group].name, g.weight) for g in v.groups) for v in o.data.vertices])
        result['materials'] = [material(m) if m else None for m in o.data.materials]
        result['shape_keys'] = None if o.data.shape_keys is None else {
            key.name: {'value': key.value, 'vertices': packed(key.data, 'co', 3)} for key in o.data.shape_keys.key_blocks}
    if o.type in {'CAMERA', 'LIGHT'}:
        result['data'] = rna_simple(o.data)
    if o.animation_data:
        result['animation'] = {'action': o.animation_data.action.name if o.animation_data.action else None,
                               'drivers': [fc.data_path for fc in o.animation_data.drivers],
                               'nla_tracks': [t.name for t in o.animation_data.nla_tracks]}
        assert not o.animation_data.drivers and not o.animation_data.nla_tracks, ('Additional animation requires explicit audit', o.name)
    return result


def snapshot(path, animated):
    source_hash = sha(path)
    bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    scene = bpy.context.scene
    rig = bpy.data.objects['Patrol_Officer_Rig']
    bpy.context.view_layer.update()
    actors = sorted([rig, *rig.children_recursive], key=lambda o: o.name)
    result = {'objects': {o.name: object_static(o) for o in actors},
              'bones': {b.name: {'head': list(b.head_local), 'tail': list(b.tail_local),
                                 'matrix': mat(b.matrix_local), 'parent': b.parent.name if b.parent else None,
                                 'use_connect': b.use_connect, 'use_deform': b.use_deform} for b in rig.data.bones},
              'initial_pose': {p.name: mat(p.matrix_basis) for p in rig.pose.bones},
              'render_context': {'camera': object_static(scene.camera) if scene.camera else None,
                                 'lights': {o.name: object_static(o) for o in scene.objects if o.type == 'LIGHT'},
                                 'world': shader(scene.world.node_tree) if scene.world and scene.world.use_nodes else None,
                                 'render': rna_simple(scene.render, {'filepath', 'display', 'engine'}),
                                 'engine': scene.render.engine, 'view': rna_simple(scene.view_settings),
                                 'frame_start': scene.frame_start, 'frame_end': scene.frame_end,
                                 'frame_current': scene.frame_current}, 'actions': {}, 'frames': {}}
    if animated:
        assert any(o.name == 'FictionalSidearm_Grip' for o in actors), 'Prop must belong to the audited actor hierarchy'
        for name, count in CLIPS.items():
            a = bpy.data.actions[name]
            result['actions'][name] = action(a)
            rig.animation_data.action = a
            frames = {}
            for frame in range(1, count + 1):
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                deps = bpy.context.evaluated_depsgraph_get()
                meshes = {}
                if frame in {1, 25, round(count/2), count}:
                    for o in actors:
                        if o.type != 'MESH':
                            continue
                        evaluated = o.evaluated_get(deps)
                        m = evaluated.to_mesh()
                        meshes[o.name] = mesh(m)
                        evaluated.to_mesh_clear()
                frames[str(frame)] = {'pose': digest({p.name: {'matrix': mat(p.matrix), 'basis': mat(p.matrix_basis),
                                                               'head': list(p.head), 'tail': list(p.tail)} for p in rig.pose.bones}),
                                      'worlds': digest({o.name: mat(o.matrix_world) for o in actors}), 'evaluated_meshes': meshes}
            result['frames'][name] = frames
            print('ACTOR_FRAMES_READ', path, name, count, flush=True)
    assert sha(path) == source_hash, ('Source changed during equivalence check', str(path))
    return {'source_path': str(path), 'source_sha256': source_hash, 'payload': result}


def difference_paths(a, b, prefix=''):
    if isinstance(a, dict) and isinstance(b, dict):
        result = []
        for k in sorted(set(a) | set(b)):
            if k not in a or k not in b:
                result.append(prefix + '/' + k)
            else:
                result += difference_paths(a[k], b[k], prefix + '/' + k)
        return result
    if prefix.endswith('/uv_values_sha256'):
        left=np.frombuffer(UVS[a],dtype=np.float32).astype(np.float64)
        right=np.frombuffer(UVS[b],dtype=np.float32).astype(np.float64)
        if left.shape!=right.shape:return [prefix+': UV component count differs']
        d=left-right;maximum=float(np.max(np.abs(d))) if len(d) else 0
        UV_COMPARISONS.append({'field':prefix,'components':len(d),'max_component_difference':maximum,'squared_error_sum':float(np.sum(d*d))})
        return [] if maximum<=UV_COMPONENT_BOUND else [prefix+': exceeds predefined unused UV component bound']
    if prefix.endswith('/canonical_corner_normals_sha256'):
        left=np.frombuffer(NORMALS[a],dtype=np.float32).reshape(-1,3).astype(np.float64)
        right=np.frombuffer(NORMALS[b],dtype=np.float32).reshape(-1,3).astype(np.float64)
        if left.shape!=right.shape:return [prefix+': corner count differs']
        raw_max=float(np.max(np.abs(left-right))) if len(left) else 0
        ll=np.linalg.norm(left,axis=1);rl=np.linalg.norm(right,axis=1)
        if np.any(ll==0) or np.any(rl==0):return [prefix+': zero normal']
        left/=ll[:,None];right/=rl[:,None];delta=left-right
        maximum=float(np.max(np.abs(delta))) if len(delta) else 0
        angles=np.degrees(np.arctan2(np.linalg.norm(np.cross(left,right),axis=1),np.sum(left*right,axis=1)))
        NORMAL_COMPARISONS.append({'field':prefix,'corners':len(left),'raw_max_component_difference':raw_max,
                                   'max_normalized_component_difference':maximum,'normalized_component_square_error_sum':float(np.sum(delta*delta)),
                                   'max_angle_degrees':float(np.max(angles)) if len(angles) else 0})
        return [] if maximum<=NORMAL_COMPONENT_BOUND else [prefix+': exceeds predefined normalized component bound']
    return [] if a == b else [prefix]


rows = []
for filename, animated in [('Characters.blend', False), ('Character_Animation_Showcase.blend', True)]:
    before = snapshot(options.before_root/'Blender'/filename, animated)
    after = snapshot(options.after_root/'Blender'/filename, animated)
    diffs = difference_paths(before['payload'], after['payload'])
    print('ACTOR_COMPARISON',filename,'differences',len(diffs),diffs[:12],flush=True)
    rows.append({'file': filename, 'before_source_path': before['source_path'], 'after_source_path': after['source_path'],
                 'before_source_sha256': before['source_sha256'], 'after_source_sha256': after['source_sha256'],
                 'before_actor_sha256': digest(before['payload']), 'after_actor_sha256': digest(after['payload']),
                 'exact_equal': digest(before['payload'])==digest(after['payload']), 'bounded_equal':not diffs,
                 'difference_count': len(diffs), 'differences': diffs[:200],
                 'objects': len(before['payload']['objects']), 'checked_frames': 153 if animated else 0})
exact=all(r['exact_equal'] for r in rows)
accepted=all(r['bounded_equal'] for r in rows)
shader_no_uv=all(set(types)<={'ShaderNodeBsdfPrincipled','ShaderNodeOutputMaterial'} for types in MATERIAL_SHADER_TYPES.values())
accepted=accepted and shader_no_uv
total_components=sum(r['corners']*3 for r in NORMAL_COMPARISONS)
total_uv_components=sum(r['components'] for r in UV_COMPARISONS)
report = {'status': ('PASS' if exact else 'PASS_BOUNDED_RENDER_EQUIVALENCE') if accepted else 'FAIL', 'exact_equivalence': exact,
          'equivalent_with_bounded_normals':accepted,'normal_component_bound':NORMAL_COMPONENT_BOUND,
          'equivalent_with_bounded_uvs':accepted,'render_equivalence':accepted,'uv_component_bound':UV_COMPONENT_BOUND,
          'shader_uses_uv_coordinates_or_images':not shader_no_uv,'shader_node_types':MATERIAL_SHADER_TYPES,
          'exact_geometry_topology_weights_materials_actions_poses':accepted,
          'max_observed_uv_component_difference':max((r['max_component_difference'] for r in UV_COMPARISONS),default=0),
          'rms_uv_component_difference':math.sqrt(sum(r['squared_error_sum'] for r in UV_COMPARISONS)/total_uv_components) if total_uv_components else 0,
          'uv_comparisons':UV_COMPARISONS,
          'max_observed_normalized_normal_component_difference':max((r['max_normalized_component_difference'] for r in NORMAL_COMPARISONS),default=0),
          'max_observed_raw_normal_component_difference':max((r['raw_max_component_difference'] for r in NORMAL_COMPARISONS),default=0),
          'rms_normalized_normal_component_difference':math.sqrt(sum(r['normalized_component_square_error_sum'] for r in NORMAL_COMPARISONS)/total_components) if total_components else 0,
          'max_observed_normal_angle_degrees':max((r['max_angle_degrees'] for r in NORMAL_COMPARISONS),default=0),
          'normal_comparisons':NORMAL_COMPARISONS,
          'includes_mesh_materials_weights_normals_actions_evaluated_poses': True,
          'before_characters_sha256': rows[0]['before_source_sha256'], 'after_characters_sha256': rows[0]['after_source_sha256'],
          'before_animation_sha256': rows[1]['before_source_sha256'], 'after_animation_sha256': rows[1]['after_source_sha256'],
          'recorded_utc': datetime.now(timezone.utc).isoformat(), 'blender_version': bpy.app.version_string,
          'actor': 'Patrol_Officer', 'numeric_tolerance_for_geometry_weights_materials_actions_poses': 0, 'sources': rows,
          'evaluated_pose_frames':153,'evaluated_mesh_frames_per_action':{name:sorted({1,25,round(count/2),count}) for name,count in CLIPS.items()},
          'method': 'Exact geometry/attribute digests; oriented polygons and undirected edges are canonically ordered with associated edge/face attributes. Internal connectivity bookkeeping and basis-dependent encoded custom_normal values are replaced by canonical connectivity and decoded corner normals. Normalized decoded normal components and unused UV coordinates each have a predefined1e-6 bound; observed errors are reported. Every material must contain only Principled and Output nodes, so no shader uses UV or image inputs. Exact weights, materials, modifiers, hierarchy, root and object transforms, rest skeleton, initial pose and keyed action curves establish the same deterministic skinning inputs. All153 evaluated patrol bone poses and actor/prop world transforms are compared; deformed meshes, UVs and corner normals are independently compared at four stated frames per action. Camera, lights, world shader and stored render settings are included. Source-local render output file paths are omitted.',
          'render_evidence_policy': 'Existing render manifests retain their original whole-file source hashes. This separate equivalence proof does not change any image, GIF or render manifest, and is not a claim that entire source files are equal.',
          'render_protocol_sha256': sha(ROOT/'Blender/render_quality_pass.py')}
report['adoption_guard']={'actor':'Patrol_Officer','required_manifest_source_sha256':report['before_animation_sha256'],'required_final_animation_sha256':report['after_animation_sha256'],'required_status':report['status'],'required_normal_component_bound':NORMAL_COMPONENT_BOUND,'required_uv_component_bound':UV_COMPONENT_BOUND,'require_shader_uses_uv_coordinates_or_images':False,'preserve_original_render_manifest_hashes':True}
options.output.parent.mkdir(parents=True, exist_ok=True)
options.output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2), flush=True)
assert accepted, ('Actor equivalence failed', str(options.output))
