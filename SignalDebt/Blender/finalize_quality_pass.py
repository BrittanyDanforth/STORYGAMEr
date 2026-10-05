"""Encode genuine Blender frames and assemble a labeled before/after review.

This performs format/layout conversion only. It does not alter scene imagery.
"""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Blender/Renders/QualityPass'
font_path='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
font=ImageFont.truetype(font_path,26)
small=ImageFont.truetype(font_path,19)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=ROOT/'Blender/Character_Animation_Showcase.blend'
report={'blender_source_sha256':sha(source),'images_generated_by':'Blender Cycles; no synthesized or AI-generated images','gif_display_rate_fps':15,'source_animation_rate_fps':30,'outputs':{},'limitations':['Stylized early-source character art, not photorealistic production characters.','Rendered Blender proof and export readback do not establish Unity Play Mode, runtime cloth or GTX1080Ti performance.']}

# Identical pose/camera/light comparison, scaled uniformly only for layout.
panels=[('PatrolLow_Front','Low ready · front'),('PatrolLow_Side','Low ready · side'),('PatrolAim_Front','Raised hold · front')]
w=700;row=740;sheet=Image.new('RGB',(w*2,row*3+145),(17,27,33));draw=ImageDraw.Draw(sheet)
draw.text((24,15),'SIGNAL DEBT / SHOULDER AND SLEEVE REVIEW',font=font,fill=(228,238,239))
draw.text((24,53),'Actual Blender geometry · matching cameras, lighting and animation poses',font=small,fill=(165,187,196))
for col,label in enumerate(['Before · uploaded source','After · refined garment and shoulder weights']):draw.text((24+col*w,94),label,font=small,fill=(225,230,229))
for i,(basename,label) in enumerate(panels):
 for col,folder in enumerate(['Before','After']):
  p=OUT/folder/(basename+'.png')
  still_manifest=json.loads((OUT/folder/('manifest_stills.json' if folder=='Before' else 'manifest_patrol_stills.json')).read_text())
  assert sha(p)==still_manifest['images'][p.name]['sha256'],('Comparison input changed',p)
  with Image.open(p) as im:im.load();sheet.paste(im.convert('RGB').resize((w,w),Image.Resampling.LANCZOS),(col*w,145+i*row))
  draw.text((20+col*w,145+i*row+w+8),label,font=small,fill=(190,209,216))
p=OUT/'Shoulder_Before_After.png';sheet.save(p)
report['outputs'][p.name]={'sha256':sha(p),'source_manifests':['Blender/Renders/QualityPass/Before/manifest_stills.json','Blender/Renders/QualityPass/After/manifest_patrol_stills.json'],'width':sheet.width,'height':sheet.height,'kind':'labeled comparison contact sheet'}

if '--stills-only' in sys.argv:
 print(str(OUT/'Shoulder_Before_After.png'));sys.exit(0)

for folder,prefix,dest,count in [('KickFrames','Kick_','SWAT_DoorKick_Updated.gif',31),('PatrolFrames','Patrol_','Patrol_Grip_Updated.gif',46)]:
 directory=OUT/folder
 manifest=json.loads((directory/('manifest_'+('kick' if prefix=='Kick_' else 'patrol')+'.json')).read_text())
 if manifest['source_sha256']!=sha(source):
  # Only unchanged patrol imagery can be reused, with original file hash kept.
  # The separate verifier compares the complete actor/action/evaluated meshes
  # against the later source whose only asset change was SWAT patch seating.
  proof_path=ROOT/'Validation/quality_pass_patrol_actor_equivalence.json'
  proof=json.loads(proof_path.read_text())
  assert prefix=='Patrol_'
  assert proof['status']=='PASS_BOUNDED_RENDER_EQUIVALENCE' and proof['equivalent_with_bounded_normals']
  assert proof['normal_component_bound']<=1e-6
  assert proof['exact_geometry_topology_weights_materials_actions_poses']
  assert proof['equivalent_with_bounded_uvs'] and proof['uv_component_bound']<=1e-6
  assert proof['max_observed_uv_component_difference']<=proof['uv_component_bound']
  assert proof['shader_uses_uv_coordinates_or_images'] is False
  assert proof['max_observed_normalized_normal_component_difference']<=proof['normal_component_bound']
  assert manifest['source_sha256']==proof['before_animation_sha256']
  assert sha(source)==proof['after_animation_sha256']
  report['unchanged_patrol_render_equivalence']={'report':str(proof_path.relative_to(ROOT)),'sha256':sha(proof_path),'original_render_source_sha256':manifest['source_sha256'],'current_equivalent_actor_source_sha256':sha(source),'normal_component_bound':proof['normal_component_bound'],'max_normal_component_difference':proof['max_observed_normalized_normal_component_difference'],'uv_component_bound':proof['uv_component_bound'],'max_uv_component_difference':proof['max_observed_uv_component_difference'],'shader_uses_uv_coordinates_or_images':False,'exact_equivalence':False}
 files=sorted(directory.glob(prefix+'*.png'));assert len(files)==count,(folder,len(files),count)
 frames=[]
 for p in files:
  assert sha(p)==manifest['images'][p.name]['sha256'],('Frame changed',p)
  with Image.open(p) as im:
   im.load();raw=im.convert('RGB')
   labeled=Image.new('RGB',(raw.width,raw.height+34),(17,27,33))
   labeled.paste(raw,(0,34))
   caption='UPDATED SWAT / Blender animation' if prefix=='Kick_' else 'UPDATED PATROL / Blender animation'
   ImageDraw.Draw(labeled).text((12,7),caption,font=small,fill=(219,237,239))
   frames.append(labeled)
 # Fixed shared palette prevents color-table popping between moving frames.
 montage=Image.new('RGB',(frames[0].width*min(8,len(frames)),frames[0].height))
 for i in range(min(8,len(frames))):montage.paste(frames[round(i*(len(frames)-1)/7)],(i*frames[0].width,0))
 palette=montage.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
 indexed=[im.quantize(palette=palette,dither=Image.Dither.FLOYDSTEINBERG) for im in frames]
 # GIF time units are10ms: alternate60/70/70ms for mean66.67ms (15fps).
 durations=[60 if i%3==0 else 70 for i in range(len(indexed))]
 p=OUT/dest;indexed[0].save(p,save_all=True,append_images=indexed[1:],duration=durations,loop=0,optimize=False,disposal=2)
 with Image.open(p) as check:
  encoded_frames=check.n_frames;decoded_ms=0
  assert 1<encoded_frames<=count
  for i in range(check.n_frames):
   check.seek(i);check.load();decoded_ms+=check.info.get('duration',0)
  assert decoded_ms==sum(durations),('GIF timing changed',decoded_ms,sum(durations))
 report['outputs'][dest]={'sha256':sha(p),'actual_render_source_sha256':manifest['source_sha256'],'presentation':'Unaltered scene frames with a separate34px identification banner; palette conversion only.','rendered_frames':count,'encoded_frames':encoded_frames,'encoded_duration_seconds':sum(durations)/1000,'source_frames':[manifest['images'][p.name]['frame'] for p in files],'frame_sha256':{p.name:sha(p) for p in files}}
( ROOT/'Validation/quality_pass_render_delivery.json').write_text(json.dumps(report,indent=2))
print(json.dumps({name:{k:v for k,v in data.items() if k not in ('frame_sha256','source_frames')} for name,data in report['outputs'].items()},indent=2))
