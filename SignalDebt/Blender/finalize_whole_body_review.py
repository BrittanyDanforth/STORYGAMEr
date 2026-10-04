"""Record the completed measured review and human visual adjudication.
This does not manufacture new tests; it checks hashes of the completed evidence.
"""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
ROOT=Path(__file__).resolve().parents[1];V=ROOT/'Validation'
def read(n):return json.loads((V/n).read_text())
def write(n,d):(V/n).write_text(json.dumps(d,indent=2))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def evidence(n):return {'report':'Validation/'+n,'sha256':sha(V/n)}
base=read('base_character_reimport_independent.json');anim=read('wrist_grip_reimport.json');surface=read('arm_body_clearance.json');crossings=read('garment_surface_crossings.json');gear=read('police_equipment_review.json');player=read('player_art_preservation.json');roster=read('roster_translation_independent.json')
assert len(base)==5 and len(anim['files'])==4
assert player['status'].startswith('PASS') and roster['status']=='PASS'
animation_hash=sha(ROOT/'Blender/Character_Animation_Showcase.blend')
for report in (anim,surface,crossings,gear):assert report['source_sha256']['Blender/Character_Animation_Showcase.blend']==animation_hash
assert player['source_sha256']['Blender/Characters.blend']==sha(ROOT/'Blender/Characters.blend')
assert roster['source_sha256']['Blender/Characters.blend']==sha(ROOT/'Blender/Characters.blend')
assert sum(r['frames'] for r in surface['clips'].values())==214
for name,row in surface['clips'].items():
 assert row['torso_topology']['nonmanifold_edges_after_audit_caps']==0
 for side in row['sides'].values():
  for zone in ('distal_upper_arm_70_to_100pct','forearm'):assert side[zone]['max_penetration_m']<1e-5,(name,zone)
for name,row in gear['clips'].items():assert row['binding_status']=='PASS' and row['max_relative_to_bound_bone_drift_m']<1e-5
for name,row in crossings['clips'].items():
 for side in row['sides'].values():
  assert side['max_sleeve_crossing_vertex_rest_distance_to_seam_m']<.031
  if name.startswith('SWAT'):assert side['max_crossing_triangle_pairs_per_frame']==0
remaining='Patrol has localized inner-axilla cloth folding at the joined sleeve/torso attachment: signed-envelope depth up to5.11mm; exact crossing triangle centroids within25.79mm of shared seam, farthest triangle corner30.81mm. Close-range seam shading and fit roughness remain. This is not a collision-free garment or final AAA art approval.'
decision={'status':'ACCEPTED for the requested upper-arm/chest correction with explicit attachment-fold limitation','reviewed_by':'Independent visual review plus root visual adjudication','scope':'Final actual Blender front/side/top/rear key poses and two native720px attachment close views; all214 authored frames have actual-surface numerical checks.','residual':remaining,'reason':'Upper-arm shafts, distal arms and forearms no longer disappear into the torso; remaining contacts are explicitly localized to the inner garment attachment and visible as a cloth crease. They are not hidden behind a broad anatomical-zone exemption.'}
surface['status']='COMPLETED; residual attachment contacts explicitly adjudicated in whole_body_review.json'
surface['visual_adjudication']=decision
surface['method']['limitations'].append('Synthetic cap shape affects near-seam signed-envelope depth; exact original-surface crossing localization is in garment_surface_crossings.json.')
write('arm_body_clearance.json',surface)
crossings['status']='ACCEPTED_LOCAL_ATTACHMENT_FOLD_WITH_RECORDED_LIMITATION';crossings['visual_adjudication']=decision
for row in crossings['clips'].values():
 for side in row['sides'].values():
  if side['max_crossing_triangle_pairs_per_frame']==0:side['crossing_rest_upperarm_fraction_range']=None
write('garment_surface_crossings.json',crossings)
gear['status']='PASS for rigid binding, occupancy and key-view fit review'
for row in gear['clips'].values():row['fit_status']='Accepted in source-bound front/side/top/rear views; no runtime garment collision simulation claimed'
write('police_equipment_review.json',gear)
images=[];folder=ROOT/'Blender/Renders/UpperBodyAudit'
for p in sorted(folder.glob('*.png')):
 images.append({'file':str(p.relative_to(ROOT)),'sha256':sha(p),'review':'Final four-view pose evidence or attachment close view; contact sheets assemble actual renders. Before/after sheet is explicitly historical on its left side.'})
assert len(images)>=16
files=[]
for r in base:
 path='UnityProject/Assets/Art/Characters/'+r['file'];assert sha(ROOT/path)==r['file_sha256']
 files.append({'file':path,'sha256':r['file_sha256'],'kind':'base_character_fbx','status':'PASS actual Blender FBX readback','bones':r['bones'],'meshes':r['meshes']})
for r in anim['files']:
 assert sha(ROOT/r['file'])==r['sha256']
 files.append({'file':r['file'],'sha256':r['sha256'],'kind':'animated_character_fbx','status':'PASS actual Blender FBX readback','bones':r['bones'],'frames':r['frames'],'fps':r['fps'],'max_joint_head_roundtrip_error_m':r['source_pose_max_joint_head_error_m'],'max_evaluated_skin_roundtrip_error_m':r['source_evaluated_skin_max_nearest_vertex_error_m']})
paths=['Blender/build_characters.py','Blender/animate_characters.py','Blender/Characters.blend','Blender/Character_Animation_Showcase.blend','UnityProject/Assets/Art/Characters/rig_contract.json','Blender/verify_upper_body_clearance.py','Blender/verify_garment_surface_crossings.py','Blender/verify_police_equipment.py','Blender/verify_player_semantic_geometry.py','Blender/verify_roster_translation_equivalence.py','Blender/verify_wrist_animation.py','Blender/verify_characters.py']
source_hashes={p:sha(ROOT/p) for p in paths}
prior=read('History/rig_controller_review_before_whole_body_fix.json');retained={}
for path,value in prior.get('source_sha256',{}).items():
 if path.startswith('UnityProject/Assets/Scripts/') or path.startswith('UnityProject/Assets/Editor/') or path=='Blender/render_player_poses.py':
  current=sha(ROOT/path);assert current==value,(path,'unexpectedcontrollerchange');retained[path]={'sha256':current,'unchanged_since_previous_limited_controller_review':True};source_hashes[path]=current
report={'status':'PASS for requested body/gear correction; documented attachment-fold and runtime limitations remain','recorded_utc':datetime.now(timezone.utc).isoformat(),'scope':'Independent read-only source geometry review, all214 police animation frames, original garment triangle crossings, actual9FBXreimports, equipment bone binding/holster occupancy, player preservation and12orthogonal+2close proof views.','source_sha256':source_hashes,'files':files,'surface_review':evidence('arm_body_clearance.json'),'exact_surface_crossings':evidence('garment_surface_crossings.json'),'equipment_review':evidence('police_equipment_review.json'),'player_preservation':{**evidence('player_art_preservation.json'),'status':player['status'],'changed_sections':player['changes']},'base_reimport':evidence('base_character_reimport_independent.json'),'animation_reimport':evidence('wrist_grip_reimport.json'),'visual_review':{**decision,'images':images,'remaining_limitations':[remaining,'Key-view visual review is complemented by all-frame numeric sampling; no runtime blend, locomotion, collision solver or Unity PlayMode test.','No Unity editor compilation, executable build or target-GPU performance verification in this review.','Stylized early production source; facial animation, production topology and cloth deformation polish remain.']},'animation_input_provenance':{'input_base_sha256':roster['before_base_sha256'],'current_base_sha256':sha(ROOT/'Blender/Characters.blend'),'equivalence':evidence('roster_translation_independent.json'),'reason':'Animation was authored from the pre-roster-correction base. A later patrol root X translation only separates the cast in the roster; rig-local meshes,skeleton,poses,weights and anchors are exactly equivalent. The animation and exported FBXs were not regenerated by the presentation correction.'},'retained_controller_evidence':retained,'prior_limited_review_superseded':'Validation/History/rig_controller_review_before_whole_body_fix.json; prior wrist pass did not cover shoulder/chest deformation.'}
report['animation_reimport_scope']='Every frame joint matrix comparison; evaluated skinned mesh position comparison at start, midpoint, frame25 and end as enumerated in wrist_grip_reimport.json. This is not a Unity importer test.'
write('whole_body_review.json',report)
anim['status']='PASS within actual FBX wrist/grip/skin roundtrip scope; whole-body acceptance and residuals in whole_body_review.json';anim['visual_review']={'status':'Subsumed by whole_body_review.json','limitations':report['visual_review']['remaining_limitations']};write('wrist_grip_review.json',anim)
combined={'status':report['status'],'recorded_utc':report['recorded_utc'],'method':'Current whole-body review supersedes prior wrist-only art acceptance; unchanged controller evidence retained explicitly.','files':files,'source_sha256':source_hashes,'whole_body_review':evidence('whole_body_review.json'),'remaining_uncertainties':report['visual_review']['remaining_limitations'],'retained_controller_evidence':retained}
write('rig_controller_review.json',combined)
print(json.dumps({'status':report['status'],'files_verified':len(files),'visual_proofs':len(images),'whole_body_review_sha256':sha(V/'whole_body_review.json')},indent=2))
