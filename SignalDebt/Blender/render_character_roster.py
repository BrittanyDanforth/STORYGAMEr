"""Render the current base roster without reauthoring or exporting any assets."""
import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Blender/Characters.blend';RENDERS=ROOT/'Blender/Renders'
source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
for name,x in [('Player',-3.1),('Mara_Victim',-1.55),('Judge',0),('Patrol_Officer',1.55),('SWAT_Officer',3.1)]:
    assert abs(bpy.data.objects[name+'_Rig'].location.x-x)<1e-5,(name,'incorrect preview placement')
scene=bpy.context.scene;scene.render.filepath=str(RENDERS/'Characters_Roster.png')
bpy.ops.render.render(write_still=True)
assert source_hash==hashlib.sha256(SOURCE.read_bytes()).hexdigest()
path=RENDERS/'Characters_Roster.png'
manifest_path=RENDERS/'geometry_qa_manifest.json';manifest=json.loads(manifest_path.read_text())
old_source=manifest['source_blend_sha256']
manifest['renders']['Characters_Roster.png']={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size,'source_blend_sha256':source_hash}
manifest['render_sources']={name:(source_hash if name=='Characters_Roster.png' else old_source) for name in manifest['renders']}
manifest['current_source_blend_sha256']=source_hash
manifest['presentation_equivalence_report']='Validation/character_roster_translation_fix.json'
manifest['provenance_note']='The three player deformation views retain their actual render-source hash. Only the patrol display-root translation changed afterward; exact local asset state equivalence is recorded. The roster was rerendered from the corrected current source.'
manifest_path.write_text(json.dumps(manifest,indent=2))
print('CURRENT_ROSTER_RENDERED',json.dumps(manifest['renders']['Characters_Roster.png']))
