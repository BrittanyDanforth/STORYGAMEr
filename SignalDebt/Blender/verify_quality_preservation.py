"""Independent source preservation audit for the police-only shoulder pass.

Run with --disable-autoexec. Opens both blends with use_scripts=False; never
saves either file. Compares all skeleton rest contracts and the unchanged
nonpolice meshes, weights, hierarchy, transforms, anchors and pose data.
"""
import bpy
import hashlib
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
if not args:
    raise SystemExit('Provide the original SignalDebt root after --')
ORIGINAL = Path(args[0]).resolve()
OUT = Path(args[1]) if len(args) > 1 else ROOT / 'Validation/quality_pass_preservation.json'
NONPOLICE = ('Player', 'Mara_Victim', 'Judge')
NONPOLICE_FBX = ('PlayerBody.fbx', 'Mara_Victim.fbx', 'Judge.fbx')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def matrix(m):
    return [[float(v) for v in row] for row in m]


def material(mat):
    # Library append can change names on collision without changing appearance.
    result = {'name_without_duplicate_suffix': re.sub(r'\.\d{3}$', '', mat.name),
              'diffuse_color': list(mat.diffuse_color), 'use_nodes': mat.use_nodes}
    if mat.use_nodes:
        result['nodes'] = []
        for node in mat.node_tree.nodes:
            sockets = {}
            for socket in node.inputs:
                if not hasattr(socket, 'default_value'):
                    continue
                value = socket.default_value
                if isinstance(value, (str, bool, int, float)):
                    sockets[socket.name] = value
                else:
                    try:
                        sockets[socket.name] = list(value)
                    except TypeError:
                        sockets[socket.name] = type(value).__name__
            result['nodes'].append({'type': node.bl_idname, 'inputs': sockets})
        result['links'] = sorted((link.from_node.bl_idname, link.from_socket.name,
                                  link.to_node.bl_idname, link.to_socket.name)
                                 for link in mat.node_tree.links)
    return result


def read(path):
    bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    bpy.context.view_layer.update()
    result = {}
    for rig in (o for o in bpy.context.scene.objects if o.type == 'ARMATURE'):
        data = {'skeleton': {}, 'children': {}, 'rig_pose': {}, 'rig_matrix': matrix(rig.matrix_world)}
        for bone in rig.data.bones:
            data['skeleton'][bone.name] = {
                'parent': bone.parent.name if bone.parent else None,
                'head': list(bone.head_local), 'tail': list(bone.tail_local),
                'matrix': matrix(bone.matrix_local), 'connected': bone.use_connect,
                'deform': bone.use_deform}
        if any(rig.name == prefix + '_Rig' for prefix in NONPOLICE):
            data['rig_pose'] = {bone.name: matrix(bone.matrix_basis) for bone in rig.pose.bones}
            for obj in rig.children_recursive:
                item = {'type': obj.type, 'matrix_basis': matrix(obj.matrix_basis),
                        'matrix_parent_inverse': matrix(obj.matrix_parent_inverse),
                        'parent_type': obj.parent_type, 'parent_bone': obj.parent_bone,
                        'parent': obj.parent.name if obj.parent else None}
                if obj.type == 'MESH':
                    item['vertices'] = [list(v.co) for v in obj.data.vertices]
                    item['weights'] = [sorted((obj.vertex_groups[g.group].name, g.weight) for g in v.groups)
                                       for v in obj.data.vertices]
                    item['polygons'] = [(list(p.vertices), p.material_index, p.use_smooth)
                                        for p in obj.data.polygons]
                    item['corner_normals'] = [list(n.vector) for n in obj.data.corner_normals]
                    item['materials'] = [material(m) for m in obj.data.materials]
                data['children'][obj.name] = item
        result[rig.name] = data
    return result


def compare(a, b, path, differences, errors):
    if isinstance(a, bool) or isinstance(a, (str, type(None))):
        if a != b:
            differences.append(path)
    elif isinstance(a, (int, float)):
        if not isinstance(b, (int, float)):
            differences.append(path)
        else:
            error = abs(a - b)
            errors.append(error)
            if not math.isfinite(error) or error > 1e-6:
                differences.append(path)
    elif isinstance(a, dict):
        if not isinstance(b, dict) or set(a) != set(b):
            differences.append(path + ': keys differ')
            return
        for key in a:
            compare(a[key], b[key], path + '/' + str(key), differences, errors)
    elif isinstance(a, (list, tuple)):
        if not isinstance(b, (list, tuple)) or len(a) != len(b):
            differences.append(path + ': length differs')
            return
        for i, (left, right) in enumerate(zip(a, b)):
            compare(left, right, path + '/' + str(i), differences, errors)
    elif a != b:
        differences.append(path)


before_path = ORIGINAL / 'Blender/Characters.blend'
after_path = ROOT / 'Blender/Characters.blend'
before, after = read(before_path), read(after_path)
differences, errors, rows = [], [], []
if set(before) != set(after):
    differences.append('Armature names changed')
for name in sorted(set(before) & set(after)):
    old, new = before[name], after[name]
    local_differences, local_errors = [], []
    compare(old['skeleton'], new['skeleton'], name + '/skeleton', local_differences, local_errors)
    is_nonpolice = bool(old['children'])
    if is_nonpolice:
        for key in ('children', 'rig_pose', 'rig_matrix'):
            compare(old[key], new[key], name + '/' + key, local_differences, local_errors)
    differences += local_differences
    errors += local_errors
    rows.append({'rig': name, 'bones': len(old['skeleton']),
                 'rest_skeleton_preserved': not any('/skeleton/' in p for p in local_differences),
                 'nonpolice_full_geometry_check': is_nonpolice,
                 'checked_children': len(old['children']),
                 'maximum_numeric_difference': max(local_errors, default=0),
                 'difference_count': len(local_differences)})

fbxs = []
for filename in NONPOLICE_FBX:
    path = Path('UnityProject/Assets/Art/Characters') / filename
    old_sha, new_sha = sha(ORIGINAL / path), sha(ROOT / path)
    if old_sha != new_sha:
        differences.append(str(path) + ': file bytes changed')
    fbxs.append({'file': str(path), 'sha256': new_sha, 'byte_identical': old_sha == new_sha})

report = {'status': 'PASS' if not differences else 'FAIL',
          'recorded_utc': datetime.now(timezone.utc).isoformat(),
          'blender_version': bpy.app.version_string,
          'scope': 'All rest skeletons; nonpolice source geometry, skin weights, normals, material settings, pose, hierarchy and transforms; unchanged nonpolice FBX file hashes. Does not approve revised police art.',
          'source_before_sha256': sha(before_path), 'source_after_sha256': sha(after_path),
          'numeric_tolerance': 1e-6, 'maximum_numeric_difference': max(errors, default=0),
          'rigs': rows, 'unchanged_fbx': fbxs, 'differences': differences[:100],
          'total_difference_count': len(differences)}
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2), flush=True)
assert not differences, ('Preservation contract failed; see report', str(OUT), len(differences))
