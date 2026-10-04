"""Read-only camera contact sheet renders of actual source meshes, no asset edits."""
import bpy,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
OUT=Path(args[0]) if args else ROOT/'Blender/Renders/UpperBodyAudit'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender/Character_Animation_Showcase.blend'),use_scripts=False)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=False
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
scene.render.threads_mode='FIXED';scene.render.threads=2
scene.render.image_settings.file_format='PNG'
cam=scene.camera;cam.data.type='ORTHO';cam.data.ortho_scale=1.15
for name,prefix,f in [('PatrolLow','Patrol_Officer',1),('PatrolAim','Patrol_Officer',46),('SWATContact','SWAT_Officer',25)]:
    rig=bpy.data.objects[prefix+'_Rig'];rig.location=(0,0,0)
    rig.animation_data.action=bpy.data.actions['SWAT_DoorKick' if prefix.startswith('SWAT') else 'Patrol_LowReadyToAim']
    for obj in scene.objects:
        if obj.type=='MESH':obj.hide_render=True
    for obj in rig.children_recursive:
        if obj.type=='MESH':obj.hide_render=False
    scene.frame_set(f);bpy.context.view_layer.update()
    target=Vector((0,-.13,1.36))
    for view,delta in [('Front',(0,-3,.14)),('Side',(-3,0,.14)),('Top',(0,0,3)),('Rear',(0,3,.14))]:
        cam.location=target+Vector(delta);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(OUT/f'{name}_{view}.png');bpy.ops.render.render(write_still=True)
print('UPPER_BODY_VIEWS_WRITTEN',OUT,flush=True)
