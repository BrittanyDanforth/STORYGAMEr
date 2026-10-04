"""Assemble labeled actual Blender renders; no generated or repainted character pixels."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import hashlib,json
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Blender/Renders/UpperBodyAudit'
font_path='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
font=ImageFont.truetype(font_path,21);small=ImageFont.truetype(font_path,16);heading=ImageFont.truetype(font_path,30)
views=['Front','Side','Top','Rear'];rows=[('PatrolLow','Patrol · low ready'),('PatrolAim','Patrol · raised'),('SWATContact','SWAT · kick contact')]
canvas=Image.new('RGB',(1440,1270),(19,27,33));draw=ImageDraw.Draw(canvas);draw.text((24,18),'SIGNAL DEBT / actual mesh review',font=heading,fill=(226,237,237));draw.text((24,55),'Blender source views · shoulders, elbows, wrists and fitted equipment',font=small,fill=(161,189,192))
files=[]
for row,(prefix,title) in enumerate(rows):
 for col,view in enumerate(views):
  path=OUT/f'{prefix}_{view}.png';im=Image.open(path).convert('RGB');im.thumbnail((350,350));x=col*360+5;y=88+row*392
  canvas.paste(im,(x,y+31));draw.text((x+5,y),f'{title} / {view.lower()}',font=small,fill=(207,223,225));files.append(path)
canvas.save(OUT/'Police_Four_View_Proof.png')
compare=Image.new('RGB',(1280,725),(19,27,33));draw=ImageDraw.Draw(compare)
for x,filename,label in [(0,'History/PatrolLow_Side_Before.png','BEFORE / arm collapses into chest'),(640,'PatrolLow_Side.png','AFTER / clear shoulder-to-elbow silhouette')]:
 im=Image.open(OUT/filename).convert('RGB');compare.paste(im,(x,75));draw.text((x+18,21),label,font=font,fill=(224,236,237))
compare.save(OUT/'Patrol_Shoulder_Before_After.png')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
(OUT/'proof_source_manifest.json').write_text(json.dumps({'source':'Blender/Character_Animation_Showcase.blend','source_sha256':sha(ROOT/'Blender/Character_Animation_Showcase.blend'),'note':'Contact sheets assemble actual Blender frames; no mesh or pixel repainting. Before image is prior delivered-asset audit render.','renders':[{'file':str(p.relative_to(ROOT)),'sha256':sha(p)} for p in files],'sheets':[{'file':str((OUT/n).relative_to(ROOT)),'sha256':sha(OUT/n)} for n in ['Police_Four_View_Proof.png','Patrol_Shoulder_Before_After.png']]},indent=2))
print('BODY_PROOF_SHEETS_ASSEMBLED')
