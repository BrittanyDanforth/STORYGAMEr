"""Round-trip base character FBXs: structure, orientation, weights, real deformation."""
import bpy,json,math,hashlib,sys
from pathlib import Path
from mathutils import Vector,Quaternion,Matrix
root=Path(__file__).resolve().parents[1]
directory=root/'UnityProject/Assets/Art/Characters'
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
output=Path(args[0]) if args else root/'Validation/quality_pass_base_character_roundtrip.json'
reports=[]
for filename in ['PlayerBody.fbx','Mara_Victim.fbx','Judge.fbx','Patrol_Officer.fbx','SWAT_Officer.fbx']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(directory/filename),use_anim=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1,filename
    rig=rigs[0];refined=filename in ['PlayerBody.fbx','Patrol_Officer.fbx','SWAT_Officer.fbx']
    required={'LeftUpperArm','RightUpperArm','LeftUpperLeg','RightUpperLeg','LeftIndexProximal','RightThumbDistal','Head','Hips'}
    assert required.issubset(set(rig.data.bones.keys())),filename
    assert len(rig.data.bones)==52,(filename,len(rig.data.bones))
    assert len(meshes)==(8 if refined else 16),(filename,len(meshes))
    bpy.context.view_layer.update()
    def find(role):return next(o for o in bpy.context.scene.objects if o.name.endswith('_'+role))
    origin=find('BodyOrigin').matrix_world.translation
    forward=(find('BodyForward').matrix_world.translation-origin).normalized()
    up=(find('BodyUp').matrix_world.translation-origin).normalized()
    left=up.cross(forward).normalized()
    left_head=rig.matrix_world@rig.data.bones['LeftUpperArm'].head_local
    right_head=rig.matrix_world@rig.data.bones['RightUpperArm'].head_local
    assert (left_head-right_head).dot(left)>.3,(filename,'anatomical side inversion')
    assert abs(forward.dot(up))<.001
    for side,short in [('Left','L'),('Right','R')]:
        wrist=rig.matrix_world@rig.data.bones[side+'Hand'].head_local
        for role,low,high in [('PalmTip',.04,.10),('PalmBack',.04,.06),('Grip',.04,.07)]:
            distance=(find(role+'_'+short).matrix_world.translation-wrist).length
            assert low<distance<high,(filename,role,side,distance)
        assert (find('Sole_'+short).matrix_world.translation-origin).length>.05
        assert (find('Toe_'+short).matrix_world.translation-find('Sole_'+short).matrix_world.translation).length>.1
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    minima=[min(v[i] for v in points) for i in range(3)]
    maxima=[max(v[i] for v in points) for i in range(3)]
    report={'file':filename,'file_sha256':hashlib.sha256((directory/filename).read_bytes()).hexdigest(),'blender_version':bpy.app.version_string,'meshes':len(meshes),'bones':len(rig.data.bones),'min_xyz':minima,'max_xyz':maxima,'source_left_verified':True,'orientation_markers':True,'round_trip_pass':True}
    if refined:
        max_error=0;zero=0;max_influences=0;mixed={}
        for o in meshes:
            mods=[m for m in o.modifiers if m.type=='ARMATURE' and m.object==rig]
            assert len(mods)==1,(filename,o.name,'missing Armature modifier')
            for v in o.data.vertices:
                weights=[g.weight for g in v.groups if g.weight>1e-7 and o.vertex_groups[g.group].name in rig.data.bones]
                if not weights:zero+=1
                max_error=max(max_error,abs(sum(weights)-1))
                max_influences=max(max_influences,len(weights))
        assert zero==0,(filename,'unweighted vertices',zero)
        assert max_error<.0001,(filename,'weight normalization',max_error)
        assert max_influences<=4,(filename,'too many influences',max_influences)
        for side in ['Left','Right']:
            for name1,name2 in [('UpperArm','LowerArm'),('UpperLeg','LowerLeg')]:
                count=0
                for o in meshes:
                    a=o.vertex_groups.get(side+name1);b=o.vertex_groups.get(side+name2)
                    if not a or not b:continue
                    for v in o.data.vertices:
                        weights={g.group:g.weight for g in v.groups}
                        if weights.get(a.index,0)>.05 and weights.get(b.index,0)>.05:count+=1
                assert count>=40,(filename,side+name2,'no smooth joint region',count)
                mixed[side+name2]=count
        arm=next(o for o in meshes if o.name.endswith('_Arm_L'))
        deps=bpy.context.evaluated_depsgraph_get()
        before=[arm.matrix_world@v.co.copy() for v in arm.evaluated_get(deps).data.vertices]
        pb=rig.pose.bones['LeftLowerArm'];axis=pb.bone.matrix_local.to_3x3().inverted()@Vector((1,0,0))
        pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion(axis,-math.radians(100))
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        after=[arm.matrix_world@v.co.copy() for v in arm.evaluated_get(deps).data.vertices]
        displacement=max((a-b).length for a,b in zip(after,before))
        assert displacement>.1,(filename,'skinning did not deform elbow')
        pb.rotation_quaternion=Quaternion();bpy.context.view_layer.update()
        for side in ['Left','Right']:
            thumb=rig.data.bones[side+'ThumbProximal'].head_local
            index=rig.data.bones[side+'IndexProximal'].head_local
            little=rig.data.bones[side+'LittleProximal'].head_local
            assert abs(index.x-thumb.x)<abs(little.x-thumb.x),(filename,side,'finger ordering')
        posed_chirality={}
        def frame(f,u):
            z=-f.normalized();x=u.cross(z).normalized();y=z.cross(x).normalized()
            return Matrix((x,y,z)).transposed()
        for side,short,expected in [('Left','L',1),('Right','R',-1)]:
            wrist=rig.matrix_world@rig.data.bones[side+'Hand'].head_local
            tip=find('PalmTip_'+short).matrix_world.translation-wrist
            back=find('PalmBack_'+short).matrix_world.translation-wrist
            turn=frame(forward,up)@frame(tip,back).inverted()
            thumb=rig.matrix_world@rig.data.bones[side+'ThumbProximal'].head_local-wrist
            inward=(turn@thumb).dot(forward.cross(up))*expected
            index_root=rig.matrix_world@rig.data.bones[side+'IndexProximal'].head_local
            index_tip=rig.matrix_world@rig.data.bones[side+'IndexDistal'].tail_local
            curl=(turn@(index_tip-index_root)).dot(up)
            assert inward>.02,(filename,side,'thumb points outward in typing pose',inward)
            assert curl<0,(filename,side,'curl is dorsal not palmar',curl)
            posed_chirality[side]={'thumb_centerward_m':inward,'index_curl_down_m':curl}
        report.update({'posed_hand_chirality':posed_chirality,'unweighted_vertices':zero,'max_weight_sum_error':max_error,'max_influences':max_influences,'mixed_joint_vertices':mixed,'elbow_max_displacement_m':displacement,'finger_ordering_verified':True})
    else:
        limb=next(o for o in meshes if o.parent_bone=='LeftUpperArm')
        before=limb.matrix_world@Vector(limb.bound_box[0])
        bone=rig.pose.bones['LeftUpperArm'];bone.rotation_mode='XYZ';bone.rotation_euler.x=.35
        bpy.context.view_layer.update();after=limb.matrix_world@Vector(limb.bound_box[0])
        assert (after-before).length>.005
        report['rigid_limb_movement_m']=(after-before).length
    if filename=='PlayerBody.fbx':
        assert any(o.name=='PlayerHead' for o in meshes)
        assert not any('NormalReference' in o.name for o in bpy.context.scene.objects)
        hip=next(o for o in meshes if o.name=='Player_Hips');leg=next(o for o in meshes if o.name=='Player_Leg_L')
        index={tuple(round(c,5) for c in hip.matrix_world@v.co):v.index for v in hip.data.vertices}
        pairs=[(index[tuple(round(c,5) for c in leg.matrix_world@v.co)],v.index) for v in leg.data.vertices if tuple(round(c,5) for c in leg.matrix_world@v.co) in index]
        def split_normals(o):
            if hasattr(o.data,'calc_normals_split'):o.data.calc_normals_split()
            matrix=o.matrix_world.to_3x3().inverted().transposed()
            # Blender 4.1+ exposes calculated split normals as corner_normals.
            normals=o.data.corner_normals if hasattr(o.data,'corner_normals') else None
            return {loop.vertex_index:(matrix@(normals[loop.index].vector if normals is not None else loop.normal)).normalized() for loop in o.data.loops}
        na=split_normals(hip);nb=split_normals(leg)
        minimum_dot=min(na[i].dot(nb[j]) for i,j in pairs)
        maximum_gap=max((hip.matrix_world@hip.data.vertices[i].co-leg.matrix_world@leg.data.vertices[j].co).length for i,j in pairs)
        def weights(o,i):return {o.vertex_groups[g.group].name:g.weight for g in o.data.vertices[i].groups if g.weight>1e-6}
        maximum_weight_difference=0
        for i,j in pairs:
            a=weights(hip,i);b=weights(leg,j)
            maximum_weight_difference=max(maximum_weight_difference,max(abs(a.get(k,0)-b.get(k,0)) for k in set(a)|set(b)))
        assert len(pairs)>=40 and minimum_dot>.999 and maximum_gap<1e-5 and maximum_weight_difference<1e-5
        report['exported_trouser_seam']={'shared_vertices':len(pairs),'minimum_rest_normal_dot':minimum_dot,'maximum_rest_position_gap_m':maximum_gap,'maximum_weight_difference':maximum_weight_difference,'normal_helper_excluded_from_export':True,'unity_import_normals':'Import; do not recalculate'}
    reports.append(report)
output.parent.mkdir(parents=True,exist_ok=True)
with open(output,'w') as f:json.dump(reports,f,indent=2)
print('ROUNDTRIP_VERIFIED',json.dumps(reports))
