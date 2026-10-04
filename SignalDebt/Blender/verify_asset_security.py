import bpy
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parent
ASSETS = [ROOT / 'SignalDebt_Apartment.blend', ROOT / 'Characters.blend', ROOT / 'Character_Animation_Showcase.blend']

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def handlers():
    result = {}
    for name in dir(bpy.app.handlers):
        value = getattr(bpy.app.handlers, name)
        if isinstance(value, list):
            result[name] = [getattr(fn, '__module__', '') + '.' + getattr(fn, '__name__', repr(fn)) for fn in value]
    return result

def datablocks():
    seen = set()
    for prop in bpy.data.bl_rna.properties:
        if prop.type != 'COLLECTION':
            continue
        try:
            values = getattr(bpy.data, prop.identifier)
            for item in values:
                if isinstance(item, bpy.types.ID) and item.as_pointer() not in seen:
                    seen.add(item.as_pointer())
                    yield prop.identifier, item
        except (AttributeError, TypeError):
            pass

def packed_count(item):
    try:
        return len(item.packed_files)
    except AttributeError:
        try:
            return 1 if item.packed_file else 0
        except AttributeError:
            return 0

report = {'blender_version': bpy.app.version_string, 'inspection': 'Read-only; factory startup, --disable-autoexec, use_scripts=False on each open; audit script makes no network requests and performs no renders.', 'files': []}
for path in ASSETS:
    before_hash = sha256(path)
    baseline_handlers = handlers()
    bpy.context.preferences.filepaths.use_scripts_auto_execute = False
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
    drivers = []
    refs = []
    script_nodes = []
    items = list(datablocks())
    known = {item.as_pointer() for _, item in items}
    for collection, item in list(items):
        tree = getattr(item, 'node_tree', None)
        if tree and tree.as_pointer() not in known:
            known.add(tree.as_pointer())
            items.append((collection + '.node_tree', tree))
    for collection, item in items:
        animation = getattr(item, 'animation_data', None)
        if animation:
            for curve in animation.drivers:
                driver = curve.driver
                drivers.append({'collection': collection, 'datablock': item.name, 'data_path': curve.data_path, 'array_index': curve.array_index, 'type': driver.type, 'expression': driver.expression, 'use_self': driver.use_self})
        for node in getattr(item, 'nodes', []):
            if node.bl_idname == 'ShaderNodeScript':
                script_nodes.append({'node_tree': item.name, 'node': node.name, 'mode': node.mode, 'filepath': node.filepath, 'text': node.script.name if node.script else None})
        for prop in item.bl_rna.properties:
            if prop.type != 'STRING' or prop.subtype not in {'FILE_PATH', 'DIR_PATH'}:
                continue
            raw = getattr(item, prop.identifier, '')
            if not raw:
                continue
            if raw.startswith('<') and raw.endswith('>'):
                continue
            resolved = bpy.path.abspath(raw, library=item.library)
            refs.append({'collection': collection, 'datablock': item.name, 'property': prop.identifier, 'path': raw, 'resolved_path': resolved, 'packed_files': packed_count(item), 'exists_locally': os.path.exists(resolved), 'network_reference': raw.lower().startswith(('http:', 'https:', 'ftp:', 'sftp:', 'smb:', '\\\\'))})
    texts = [{'name': text.name, 'use_module': text.use_module, 'filepath': text.filepath, 'line_count': len(text.lines), 'sha256': hashlib.sha256(text.as_string().encode('utf-8')).hexdigest()} for text in bpy.data.texts]
    after_handlers = handlers()
    added_handlers = {name: [fn for fn in values if fn not in baseline_handlers.get(name, [])] for name, values in after_handlers.items()}
    added_handlers = {name: values for name, values in added_handlers.items() if values}
    after_hash = sha256(path)
    entry = {'file': path.name, 'sha256': before_hash, 'unchanged': before_hash == after_hash, 'automatic_script_execution_enabled': bpy.context.preferences.filepaths.use_scripts_auto_execute, 'autoexec_fail': getattr(bpy.app, 'autoexec_fail', None), 'autoexec_fail_message': getattr(bpy.app, 'autoexec_fail_message', ''), 'embedded_text_count': len(texts), 'embedded_texts': texts, 'registered_text_module_count': sum(text['use_module'] for text in texts), 'driver_count': len(drivers), 'scripted_driver_count': sum(driver['type'] == 'SCRIPTED' for driver in drivers), 'drivers': drivers, 'shader_script_nodes': script_nodes, 'image_inventory': [{'name': img.name, 'source': img.source, 'packed_files': packed_count(img), 'filepath': img.filepath} for img in bpy.data.images], 'load_handler_additions': added_handlers, 'linked_library_count': len(bpy.data.libraries), 'image_count': len(bpy.data.images), 'packed_image_count': sum(bool(packed_count(img)) for img in bpy.data.images), 'asset_path_references': refs, 'unpacked_external_asset_references': [ref for ref in refs if ref['packed_files'] == 0], 'network_reference_count': sum(ref['network_reference'] for ref in refs)}
    risks = []
    if entry['embedded_text_count']:
        risks.append('Embedded text blocks require review even if not registered for execution.')
    if entry['linked_library_count']:
        risks.append('Linked libraries require separate inspection.')
    if entry['registered_text_module_count']:
        risks.append('Embedded text blocks marked use_module require review.')
    if entry['scripted_driver_count']:
        risks.append('Scripted drivers require expression review.')
    if script_nodes:
        risks.append('Shader script nodes require review.')
    if added_handlers:
        risks.append('New handlers appeared while loading.')
    if entry['network_reference_count']:
        risks.append('Network asset references require review.')
    entry['executable_risk_indicators'] = risks
    entry['status'] = 'REVIEW' if risks or not entry['unchanged'] else 'PASS'
    report['files'].append(entry)
report['status'] = 'PASS' if all(item['status'] == 'PASS' for item in report['files']) else 'REVIEW'
report['runtime_note'] = 'This audit reads local asset data only; no network call or external asset loading is part of the audit script.'
report['scope_limit'] = 'Inventory of Blender embedded executable content and referenced assets; not a Blender binary vulnerability audit.'
(ROOT / 'asset_security_check.json').write_text(json.dumps(report, indent=2) + '\n')
print('ASSET SECURITY CHECK ' + report['status'])
for item in report['files']:
    print(item['file'], json.dumps({key: item[key] for key in ('unchanged', 'embedded_text_count', 'registered_text_module_count', 'driver_count', 'scripted_driver_count', 'linked_library_count', 'image_count', 'packed_image_count', 'network_reference_count', 'executable_risk_indicators', 'status')}))
