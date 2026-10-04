"""Render genuine Blender source comparisons and short motion review frames.

No generated imagery, downloads or source scene writes. Open uploaded .blend
files with use_scripts=False and invoke with --disable-autoexec.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--source',type=Path,default=ROOT/'Blender/Character_Animation_Showcase.blend')
parser.add_argument('--label',default='After')
parser.add_argument('--motion',choices=['kick','patrol'])
parser.add_argument('--rest',action='store_true')
parser.add_argument('--quick',action='store_true')
parser.add_argument('--actor',choices=['patrol','swat'])
parser.add_argument('--contact-only',action='store_true')
parser.add_argument('--samples',type=int)
parser.add_argument('--size',type=int)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=ROOT/'Blender/Renders/QualityPass'/args.label
OUT.mkdir(parents=True,exist_ok=True)
source_hash=hashlib.sha256(args.source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(args.source),use_scripts=False)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=(10 if args.motion=='patrol' else 16) if args.motion else 48
if args.samples:scene.cycles.samples=args.samples
scene.cycles.use_denoising=True
scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=540 if args.motion else 800
scene.render.resolution_y=620 if args.motion else 800
if args.size:scene.render.resolution_x=scene.render.resolution_y=args.size
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
cam=scene.camera;cam.data.type='ORTHO'
# Identical neutral lighting and cameras for original/new-source comparisons.
for obj in list(scene.objects):
    if obj.type=='LIGHT':bpy.data.objects.remove(obj,do_unlink=True)
for name,pos,energy,size,color in [
    ('Review key',(-3,-4,5),650,4,(1,.92,.84)),
    ('Review fill',(4,-1,3),350,4,(.73,.86,1)),
    ('Review rim',(1,3,4),750,3,(.7,.86,1)),
]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=pos
    obj.rotation_euler=(Vector((0,0,1.1))-obj.location).to_track_quat('-Z','Y').to_euler()
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.065,.077,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
images={}

def render(name,prefix,action,frame,target,delta,scale,show_door=False):
    rig=bpy.data.objects[prefix+'_Rig'];rig.location=(0,0,0)
    if action:rig.animation_data.action=bpy.data.actions[action]
    for obj in scene.objects:
        if obj.type in {'MESH','FONT'}:obj.hide_render=True
    for obj in rig.children_recursive:
        if obj.type=='MESH':obj.hide_render=False
    if show_door:
        for obj in scene.objects:
            if obj.type=='MESH' and (obj.name.startswith(('Demo door','Reference floor','Studio floor'))):obj.hide_render=False
    scene.frame_set(frame);bpy.context.view_layer.update()
    target=Vector(target);cam.location=target+Vector(delta)
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    path=OUT/(name+'.png');scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)
    images[path.name]={'frame':frame,'rig':prefix,'action':action,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

if args.rest:
    for prefix in ('Patrol_Officer','SWAT_Officer'):
        if args.actor and ((args.actor=='swat')!=prefix.startswith('SWAT')):continue
        render(prefix+'_Rest',prefix,None,1,(0,0,1.32),(1.3,-5,.55),1.28)
elif args.motion:
    if args.motion=='kick':
        for frame in range(1,62,2):
            render('Kick_'+str(frame).zfill(3),'SWAT_Officer','SWAT_DoorKick',frame,(0,-.3,1.02),(-4.8,.95,1.1),2.53,True)
    else:
        for i,frame in enumerate(list(range(1,47,2))+list(range(46,0,-2))):
            render('Patrol_'+str(i).zfill(3),'Patrol_Officer','Patrol_LowReadyToAim',frame,(0,-.23,1.4),(-1.6,-4,.38),1.18)
else:
    poses=[('PatrolLow','Patrol_Officer','Patrol_LowReadyToAim',1),('PatrolAim','Patrol_Officer','Patrol_LowReadyToAim',46),('SWATContact','SWAT_Officer','SWAT_DoorKick',25)]
    if args.actor=='swat':poses=[('SWATGuard','SWAT_Officer','SWAT_DoorKick',1),('SWATContact','SWAT_Officer','SWAT_DoorKick',25),('SWATRecovery','SWAT_Officer','SWAT_DoorKick',38)]
    elif args.actor=='patrol':poses=[pose for pose in poses if pose[1]=='Patrol_Officer']
    if args.contact_only:poses=[pose for pose in poses if pose[0]=='SWATContact']
    for name,prefix,action,frame in poses:
        for view,delta in [('Front',(0,-3,.14)),('Side',(-3,0,.14)),('Rear',(0,3,.14)),('Top',(0,0,3))]:
            if args.quick and view not in ('Front','Side'):continue
            render(name+'_'+view,prefix,action,frame,(0,-.13,1.36),delta,1.15)
    if not args.quick and args.actor!='swat':
        for view,delta in [('Right',(-2.5,-3,1.1)),('Left',(2.5,-3,1.1))]:
            render('PatrolGrip_'+view,'Patrol_Officer','Patrol_LowReadyToAim',46,(0,-.54,1.43),delta,.52)
assert hashlib.sha256(args.source.read_bytes()).hexdigest()==source_hash
(OUT/('manifest_'+('rest' if args.rest else args.motion or 'stills')+'.json')).write_text(json.dumps({'blender_version':bpy.app.version_string,'source':str(args.source),'source_sha256':source_hash,'render_engine':'CYCLES','samples':scene.cycles.samples,'motion_frame_step':2 if args.motion else None,'images':images},indent=2))
print('QUALITY_PASS_RENDERS',len(images),str(OUT),flush=True)
