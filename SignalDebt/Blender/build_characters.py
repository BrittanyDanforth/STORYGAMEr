"""Signal Debt: editable, stylized adult cast first-pass assets.

Run with Blender 4.0.2 in background mode. Procedural source is reusable;
the .blend retains real editable meshes, named materials, and simple rigs.
Player and police limbs use continuous skinned geometry with articulated hands.
Judge and victim retain their original rigid-region first pass.
"""
import bpy
import bmesh
import math
import sys
import json
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'UnityProject/Assets/Art/Characters'
RENDERS = ROOT / 'Blender/Renders'
OUT.mkdir(parents=True, exist_ok=True)
RENDERS.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)

def mat(name, rgb, rough=.7, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=rough
    p.inputs['Metallic'].default_value=metallic
    return m

M={
    'skin_player':mat('Skin | warm olive',(.47,.28,.18)),
    'skin_victim':mat('Skin | soft warm',(.66,.39,.27)),
    'skin_judge':mat('Skin | deep warm',(.24,.12,.075)),
    'skin_officer':mat('Skin | warm light',(.64,.40,.29)),
    'skin_swat':mat('Skin | warm tan',(.48,.29,.19)),
    'lip':mat('Face | restrained lips',(.27,.095,.075)),
    'eye':mat('Face | ivory sclera',(.86,.85,.78),.48),
    'iris':mat('Face | brown iris',(.16,.095,.055),.4),
    'pupil':mat('Face | pupil',(.012,.015,.020),.35),
    'hair':mat('Hair | charcoal brown',(.035,.027,.025)),
    'grayhair':mat('Hair | salt and pepper',(.31,.32,.34)),
    'hoodie':mat('Player | muted petrol hoodie',(.055,.155,.18)),
    'hooddark':mat('Player | hood inner',(.035,.07,.082)),
    'rib':mat('Player | rib trim',(.035,.095,.11)),
    'denim':mat('Player | slate denim',(.10,.17,.24)),
    'denimdark':mat('Player | denim seams',(.065,.10,.135)),
    'shirt':mat('Civilian | ivory blouse',(.71,.67,.56)),
    'jacket':mat('Civilian | terracotta jacket',(.37,.13,.10)),
    'jacketdark':mat('Civilian | jacket seams',(.23,.07,.055)),
    'trousers':mat('Civilian | warm charcoal trousers',(.12,.115,.11)),
    'robe':mat('Judge | midnight robe',(.025,.035,.052)),
    'robetrim':mat('Judge | slate lapels',(.06,.075,.10)),
    'white':mat('Uniform | warm white',(.86,.85,.78)),
    'navy':mat('Officer | navy uniform',(.042,.075,.13)),
    'navylight':mat('Officer | raised uniform panels',(.065,.12,.19)),
    'swat':mat('SWAT | desaturated field blue',(.075,.095,.11)),
    'vest':mat('SWAT | charcoal protective vest',(.033,.044,.048)),
    'vestedge':mat('SWAT | webbing',(.068,.085,.084)),
    'fabricseam':mat('Police | tailored fabric seam',(.095,.13,.16)),
    'carriertrim':mat('SWAT | carrier bound edge',(.095,.12,.12)),
    'holster':mat('Police | molded holster polymer',(.027,.039,.044),.61),
    'stowedprop':mat('SWAT | inert holstered sidearm exterior',(.14,.17,.185),.5,.18),
    'radioplastic':mat('Police | radio molded shell',(.035,.046,.049),.5),
    'lens':mat('SWAT | protective eyewear dark lens',(.038,.055,.062),.24),
    'black':mat('Equipment | rubber and black cloth',(.02,.026,.031)),
    'leather':mat('Equipment | dark leather',(.065,.045,.035),.53),
    'sole':mat('Shoes | rubber sole',(.115,.135,.14)),
    'shoelace':mat('Shoes | woven laces',(.38,.42,.42)),
    'metal':mat('Hardware | gunmetal',(.18,.22,.24),.32,.65),
    'gold':mat('Officer | restrained brass badge',(.60,.41,.15),.36,.6),
    'floor':mat('Preview | midnight floor',(.025,.043,.054)),
    'plinth':mat('Preview | plinth',(.052,.078,.091)),
    'label':mat('Preview | warm typography',(.78,.82,.81)),
    'accent':mat('Preview | teal accent',(.10,.42,.43)),
}
parts=[]
current_collection=None
REFINED={'Player','Patrol_Officer','SWAT_Officer'}
normal_references={}
BONE_MAP={}
for _side,_label in [('L','Left'),('R','Right')]:
    for _src,_dst in [('Shoulder','Shoulder'),('UpperArm','UpperArm'),('Forearm','LowerArm'),('Hand','Hand'),('Thigh','UpperLeg'),('Calf','LowerLeg'),('Foot','Foot')]:
        BONE_MAP[_src+'_'+_side]=_label+_dst

def register(obj, name, material, bone=None, smooth=True):
    obj.name=name
    for coll in list(obj.users_collection): coll.objects.unlink(obj)
    (current_collection or bpy.context.scene.collection).objects.link(obj)
    if material: obj.data.materials.append(material)
    if obj.type=='MESH' and smooth:
        for p in obj.data.polygons: p.use_smooth=True
    if bone: parts.append((obj,bone))
    return obj

def bevel(obj, width=.01, segments=2):
    mod=obj.modifiers.new('Soft manufactured edge','BEVEL')
    mod.width=width;mod.segments=segments
    return obj

def cube(name, loc, size, material, bone=None, radius=.01):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=register(bpy.context.object,name,material,bone)
    o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if radius: bevel(o,radius,3)
    return o

def ellipsoid(name,loc,scale,material,bone=None,seg=16,rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg,ring_count=rings,location=loc)
    o=register(bpy.context.object,name,material,bone)
    o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return o

def loft(name, sections, material, bone=None, n=16, subdiv=1, power=1):
    # sections = z, half width, half depth, center y, optional center x
    vs=[]
    for row in sections:
        z,rx,ry,cy=row[:4];cx=row[4] if len(row)>4 else 0
        for j in range(n):
            a=2*math.pi*j/n
            sx=math.sin(a);sy=math.cos(a)
            vs.append((cx+rx*math.copysign(abs(sx)**power,sx),
                       cy-ry*math.copysign(abs(sy)**power,sy),z))
    fs=[]
    for i in range(len(sections)-1):
        for j in range(n):
            a=i*n+j;b=i*n+(j+1)%n
            fs.append((a,b,b+n,a+n))
    fs.append(tuple(reversed(range(n))))
    fs.append(tuple((len(sections)-1)*n+j for j in range(n)))
    mesh=bpy.data.meshes.new(name+' editable surface');mesh.from_pydata(vs,[],fs);mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    register(obj,name,material,bone)
    if subdiv:
        s=obj.modifiers.new('Clothing form smoothing','SUBSURF');s.levels=subdiv;s.render_levels=subdiv
    return obj

def limb(name,a,b,rings,material,bone,n=12,subdiv=1):
    a=Vector(a);b=Vector(b);d=(b-a).normalized()
    side=Vector((1,0,0))
    if abs(d.dot(side))>.9: side=Vector((0,1,0))
    side=(side-d*d.dot(side)).normalized();front=d.cross(side).normalized()
    vs=[]
    for t,rw,rd in rings:
        c=a+(b-a)*t
        for j in range(n):
            q=2*math.pi*j/n
            vs.append(c+side*(rw*math.cos(q))+front*(rd*math.sin(q)))
    fs=[]
    for i in range(len(rings)-1):
        for j in range(n):
            a0=i*n+j;b0=i*n+(j+1)%n;fs.append((a0,b0,b0+n,a0+n))
    fs.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+j for j in range(n))])
    mesh=bpy.data.meshes.new(name+' form');mesh.from_pydata(vs,[],fs);mesh.update()
    o=bpy.data.objects.new(name,mesh);register(o,name,material,bone)
    if subdiv:
        s=o.modifiers.new('Limb shape smoothing','SUBSURF');s.levels=subdiv;s.render_levels=subdiv
    return o

def strip(name, a,b,width,depth,material,bone):
    a=Vector(a);b=Vector(b)
    o=cube(name,(a+b)/2,(width,depth,(b-a).length),material,bone,.003)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return o

def text_obj(name,text,loc,size,material,bone=None):
    curve=bpy.data.curves.new(name,'FONT');curve.body=text;curve.align_x='CENTER';curve.size=size
    curve.extrude=.0006;curve.resolution_u=3
    o=bpy.data.objects.new(name,curve);register(o,name,material,None)
    o.location=loc;o.rotation_euler=(math.pi/2,0,0)
    if bone:
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        bpy.ops.object.convert(target='MESH');parts.append((o,bone))
    return o

def shaped_panel(name,outline,front_y,depth,material,bone,bevel_width=.004):
    """Purpose-shaped cloth/equipment panel; outline is in the X/Z plane."""
    count=len(outline)
    vertices=[(x,front_y(x,z) if callable(front_y) else front_y,z) for x,z in outline]
    vertices += [(x,y+depth,z) for x,y,z in vertices[:]]
    faces=[tuple(reversed(range(count))),tuple(range(count,count*2))]
    faces.extend((i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count))
    mesh=bpy.data.meshes.new(name+' silhouette');mesh.from_pydata(vertices,[],faces);mesh.update()
    # Mirrored outlines must remain outward-facing for one-sided engine materials.
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(name,mesh);register(o,name,material,bone,False)
    mesh.use_auto_smooth=True
    if bevel_width:bevel(o,bevel_width,3)
    normal=o.modifiers.new('Panel weighted corner normals','WEIGHTED_NORMAL');normal.keep_sharp=True
    return o

def cable(name,points,radius,material,bone):
    """Editable mesh cable, not a curve or external dependency."""
    for i,(a,b) in enumerate(zip(points,points[1:])):
        limb(name+' segment '+str(i),a,b,[(0,radius,radius),(.08,radius,radius),(.92,radius,radius),(1,radius,radius)],material,bone,n=8,subdiv=0)
    for i,p in enumerate(points[1:-1]):ellipsoid(name+' bend '+str(i),p,(radius,radius,radius),material,bone,8,6)

def police_shoulder_weights(position,weights,prefix):
    """Broad torso stays on the chest; a narrow axillary strip transfers into arm.

    Nearest-surface copying mixed chest weights far along the deltoid, dragging
    the sleeve into the carrier as the upper arm came forward. The source joint
    coordinates remain unchanged; this only authors the continuous cloth skin.
    """
    x,y,z=position
    if z<1.225:return weights
    side='Left' if x>=0 else 'Right';u=side+'UpperArm';s=side+'Shoulder'
    width=1.04 if prefix=='SWAT_Officer' else 1.0
    # Axilla follows the shirt's narrowed side cut; shoulder cap is upper-arm driven.
    inner=(.177+.020*max(0,min(1,(z-1.31)/.11)))*width
    outer=(.233+.013*max(0,min(1,(z-1.32)/.11)))*width
    blend=max(0,min(1,(abs(x)-inner)/(outer-inner)))
    blend=blend*blend*(3-2*blend)
    # Clavicle carries the high cap; the lower deltoid transfers into the arm.
    # A pure upper-arm cap swung its top corner upward into a sharp point.
    arm_factor=max(.08,min(1,(1.438-z)/.095))
    arm_factor=arm_factor*arm_factor*(3-2*arm_factor)
    result={'Chest':1-blend,s:blend*(1-arm_factor),u:blend*arm_factor}
    # Feather into the lower chest weights; a hard height cutoff produces a
    # triangular ridge at the rear axilla under clavicle protraction.
    influence=max(0,min(1,(z-1.225)/.065));influence=influence*influence*(3-2*influence)
    total=max(sum(weights.values()),1e-12)
    final={bone:value/total*(1-influence) for bone,value in weights.items()}
    for bone,value in result.items():final[bone]=final.get(bone,0)+value*influence
    return final

def set_weights(obj, index, weights):
    total=sum(weights.values())
    for bone,value in weights.items():
        bone=BONE_MAP.get(bone,bone)
        group=obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
        group.add([index],value/total,'REPLACE')

def weighted_tube(name,samples,material,region,n=16,subdiv=1):
    """Continuous quad rings; each ring carries explicit normalized bone weights."""
    vertices=[]
    for i,(pos,width,depth,weights) in enumerate(samples):
        pos=Vector(pos)
        tangent=(Vector(samples[min(i+1,len(samples)-1)][0])-Vector(samples[max(0,i-1)][0])).normalized()
        side=Vector((1,0,0))
        if abs(tangent.dot(side))>.88:side=Vector((0,1,0))
        side=(side-tangent*tangent.dot(side)).normalized();front=tangent.cross(side).normalized()
        for j in range(n):
            a=j*2*math.pi/n
            vertices.append(pos+side*(width*math.cos(a))+front*(depth*math.sin(a)))
    faces=[]
    for i in range(len(samples)-1):
        for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
    faces += [tuple(reversed(range(n))),tuple((len(samples)-1)*n+j for j in range(n))]
    mesh=bpy.data.meshes.new(name+' continuous topology');mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);register(o,name,material,'Chest')
    o['skin_region']=region
    for i,sample in enumerate(samples):
        for j in range(n):set_weights(o,i*n+j,sample[3])
    if subdiv:
        mod=o.modifiers.new('Continuous surface refinement','SUBSURF');mod.levels=subdiv;mod.render_levels=subdiv
    return o

def finger_joints(side,wrist,refined=False):
    sgn=1 if side=='L' else -1;w=Vector(wrist);result={}
    for name,offset,length in [('Index',-.025,.073),('Middle',-.007,.082),('Ring',.012,.076),('Little',.030,.062)]:
        p=w+Vector((sgn*offset,-.021,-.066))
        p1=p+Vector((0,-.011,-length*.45))
        p2=p1+Vector((0,-.012,-length*.32))
        p3=p2+Vector((0,-.012,-length*.23))
        result[name]=[p,p1,p2,p3]
    p=w+Vector((-sgn*.029,-.007,-.027))
    p1=p+Vector((-sgn*.023,-.019,-.026))
    p2=p1+Vector((-sgn*.011,-.009,-.021))
    p3=p2+Vector((-sgn*.007,-.012,-.016))
    result['Thumb']=[p,p1,p2,p3]
    if refined:
        for points in result.values():
            for point in points:point.x=w.x-(point.x-w.x)
    return result

def smooth_hand(prefix,side,wrist,skin,gloves=False):
    label='Left' if side=='L' else 'Right';hand=label+'Hand';lower=label+'LowerArm';region='Arm_'+side
    w=Vector(wrist);material=M['black'] if gloves else skin
    def palm_offset(x,y,z):return Vector((x,y,z))
    weighted_tube(prefix+' articulated palm',[
        (w+palm_offset(0,.006,.027),.029,.023,{lower:.75,hand:.25}),
        (w+palm_offset(0,0,.009),.033,.024,{lower:.35,hand:.65}),
        (w+palm_offset(0,-.008,-.018),.040,.026,{hand:1}),
        (w+palm_offset(0,-.016,-.044),.042,.026,{hand:1}),
        (w+palm_offset(0,-.021,-.066),.039,.021,{hand:1}),
        (w+palm_offset(0,-.022,-.075),.034,.017,{hand:1}),
    ],material,region,n=16)
    for finger,points in finger_joints(side,wrist,prefix in REFINED).items():
        bones=[label+finger+t for t in ('Proximal','Intermediate','Distal')]
        widths=[.0105,.0098,.0092,.0083] if finger!='Thumb' else [.014,.013,.0115,.0095]
        samples=[]
        samples.append((points[0],widths[0],widths[0]*1.1,{hand:.30,bones[0]:.70}))
        for segment in range(3):
            a,b=points[segment:segment+2];bone=bones[segment]
            samples.append((a.lerp(b,.33),widths[segment],widths[segment]*1.05,{bone:1}))
            samples.append((a.lerp(b,.72),widths[segment]*.95,widths[segment],{bone:.80, bones[min(segment+1,2)]:.20} if segment<2 else {bone:1}))
            mix={bone:.5,bones[segment+1]:.5} if segment<2 else {bone:1}
            samples.append((b,widths[segment+1],widths[segment+1],mix))
        samples.append((points[-1]+(points[-1]-points[-2]).normalized()*.004,.0045,.0045,{bones[-1]:1}))
        weighted_tube(prefix+' '+side+' '+finger+' skin',samples,material,region,n=10)
        if not gloves:
            nail_center=points[-2].lerp(points[-1],.57)+Vector((0,.008,0))
            nail=ellipsoid(prefix+' '+finger+' nail',nail_center,(.006,.0018,.008),skin,bones[-1],12,8)
            nail['skin_region']=region
        else:
            pad=ellipsoid(prefix+' '+finger+' glove knuckle',points[0]+Vector((0,.016,0)),(.012,.006,.010),M['vestedge'],bones[0],12,8)
            pad['skin_region']=region

def smooth_arm(prefix,side,shoulder,elbow,wrist,material):
    label='Left' if side=='L' else 'Right';u=label+'UpperArm';l=label+'LowerArm';h=label+'Hand';s=label+'Shoulder'
    a,b,c=map(Vector,(shoulder,elbow,wrist));sgn=1 if side=='L' else -1
    samples=[
        (a+Vector((-sgn*.032,0,.006)),.083,.085,{'Chest':.40,s:.30,u:.30}),
        (a,.090,.087,{s:.18,u:.82}),
        (a.lerp(b,.17),.088,.085,{u:1}),
        (a.lerp(b,.44),.080,.077,{u:1}),
        (a.lerp(b,.72),.072,.070,{u:.9,l:.1}),
        (a.lerp(b,.90),.067,.066,{u:.7,l:.3}),
        (b,.065,.064,{u:.5,l:.5}),
        (b.lerp(c,.13),.065,.063,{u:.28,l:.72}),
        (b.lerp(c,.29),.063,.060,{u:.07,l:.93}),
        (b.lerp(c,.51),.057,.054,{l:1}),
        (b.lerp(c,.78),.049,.047,{l:1}),
        (b.lerp(c,.93),.045,.043,{l:.88,h:.12}),
        (c,.044,.042,{l:.55,h:.45}),
    ]
    if prefix in ('Patrol_Officer','SWAT_Officer'):
        # A tailored sleeve with a flatter inner axilla and deltoid cap. Joint
        # lengths/hand proportions are unchanged from the approved rig contract.
        samples=[(pos,rw*(.94 if i<5 else .98),rd*(.91 if i<5 else .96),weights)
                 for i,(pos,rw,rd,weights) in enumerate(samples)]
        samples[0]=(a+Vector((-sgn*.045,0,-.018)),.062,.059,{'Chest':.4,s:.3,u:.3})
        samples[1]=(a,.079,.075,{s:.15,u:.85})
    weighted_tube(prefix+' continuous sleeve '+side,samples,material,'Arm_'+side,n=20)

def smooth_leg(prefix,side,hip,knee,ankle,material):
    label='Left' if side=='L' else 'Right';u=label+'UpperLeg';l=label+'LowerLeg';f=label+'Foot'
    a,b,c=map(Vector,(hip,knee,ankle))
    samples=[
        (a+Vector((0,0,.042)),.102,.110,{'Hips':.6,u:.4}),
        (a,.108,.111,{'Hips':.25,u:.75}),
        (a.lerp(b,.20),.106,.109,{u:1}),
        (a.lerp(b,.45),.094,.099,{u:1}),
        (a.lerp(b,.72),.082,.086,{u:.88,l:.12}),
        (a.lerp(b,.91),.079,.082,{u:.68,l:.32}),
        (b,.078,.081,{u:.50,l:.50}),
        (b.lerp(c,.12),.079,.081,{u:.28,l:.72}),
        (b.lerp(c,.29),.080,.082,{u:.08,l:.92}),
        (b.lerp(c,.51),.075,.077,{l:1}),
        (b.lerp(c,.78),.066,.068,{l:1}),
        (b.lerp(c,.94),.062,.064,{l:.9,f:.1}),
        (c,.060,.063,{l:.7,f:.3}),
    ]
    weighted_tube(prefix+' continuous trousers '+side,samples,material,'Leg_'+side,n=20)

def make_head(prefix,skin,kind):
    hb='Head';zoff=.018 if kind=='judge' else 0
    police=kind in ('officer','swat');eye_y=.008 if police else 0
    # Jaw narrows below the cheek plane; forehead and crown are shaped separately.
    sections=[(1.545,.055,.052,-.008),(1.56,.074,.069,-.019),
              (1.585,.091,.081,-.011),(1.625,.109,.090,0),
              (1.68,.114,.097,.006),(1.73,.111,.097,.014),
              (1.77,.103,.088,.020),(1.81,.082,.068,.020),(1.835,.032,.032,.019)]
    if kind=='victim': sections=[(z,w*.94,d,cy) for z,w,d,cy in sections]
    loft(prefix+' head shape',sections,skin,hb,n=20,subdiv=2,power=.92)
    loft(prefix+' neck',[(1.46,.073,.065,.015),(1.48,.071,.064,.015),(1.56,.065,.061,.005)],skin,'Neck',subdiv=1)
    for s in (-1,1):
        ellipsoid(prefix+' ear', (s*.112,.007,1.672),(.023,.022,.038),skin,hb)
        ellipsoid(prefix+' inner ear',(s*.127,-.006,1.671),(.006,.012,.021),M['lip'],hb,12,8)
        # Eyelid rim gives the eyes a socket instead of exposed bulging spheres.
        ellipsoid(prefix+' eyelid',(s*.047,-.084+eye_y,1.698),(.030 if police else .031,.010 if police else .017,.015 if police else .017),skin,hb)
        ellipsoid(prefix+' eye',(s*.047,-.097+eye_y,1.699),(.021 if police else .022,.005 if police else .006,.008 if police else .010),M['eye'],hb)
        ellipsoid(prefix+' iris',(s*.047,-.1025+eye_y,1.699),(.0066,.0026,.0075),M['iris'],hb,12,8)
        ellipsoid(prefix+' pupil',(s*.047,-.1046+eye_y,1.699),(.0033,.0015,.0048),M['pupil'],hb,12,8)
        ellipsoid(prefix+' eye catchlight',(s*.044,-.106+eye_y,1.702),(.0018,.001,.002),M['white'],hb,8,6)
        brow=cube(prefix+' brow',(s*.046,-.091+eye_y*.6,1.720 if police else 1.722),(.050 if police else .052,.010 if police else .013,.006 if police else .009),M['grayhair'] if kind=='judge' else M['hair'],hb,.003)
        brow.rotation_euler[1]=s*.07
    # A compact bridged nose with a defined tip, rather than a flat facial texture.
    loft(prefix+' nose',[(1.644,.017,.014,-.093),(1.651,.019,.023,-.105),
                         (1.665,.015,.027,-.109),(1.69,.010,.016,-.095),(1.709,.009,.009,-.089)],skin,hb,n=10,subdiv=1)
    lip=cube(prefix+' mouth',(0,-.090,1.620),(.042,.009,.005),M['lip'],hb,.0024)
    ellipsoid(prefix+' lower lip',(0,-.088,1.616),(.022,.006,.004),skin,hb)
    if kind=='judge':
        hair=M['grayhair']
        for s in (-1,1):
            ellipsoid(prefix+' side hair',(s*.096,.026,1.746),(.026,.075,.065),hair,hb)
        ellipsoid(prefix+' back hair',(0,.067,1.765),(.088,.04,.070),hair,hb)
        # Thin glasses with lenses left open.
        for s in (-1,1):
            for za in (1.686,1.712): strip(prefix+' glasses rim',(s*.047-.029,-.112,za),(s*.047+.029,-.112,za),.003,.003,M['metal'],hb)
            for xa in (s*.047-.029,s*.047+.029): strip(prefix+' glasses rim',(xa,-.112,1.686),(xa,-.112,1.712),.003,.003,M['metal'],hb)
        strip(prefix+' glasses bridge',(-.018,-.113,1.701),(.018,-.113,1.701),.003,.003,M['metal'],hb)
    elif kind=='victim':
        loft(prefix+' hair cap',[(1.742,.114,.100,.024),(1.77,.116,.102,.025),(1.81,.099,.085,.026),(1.844,.045,.045,.024)],M['hair'],hb,n=20,subdiv=1)
        for s in (-1,1):
            ellipsoid(prefix+' hair side',(s*.102,.038,1.699),(.028,.073,.106),M['hair'],hb)
        ellipsoid(prefix+' tied hair',(0,.114,1.705),(.067,.059,.13),M['hair'],hb)
        strip(prefix+' side part',(-.093,-.072,1.772),(.047,-.083,1.795),.027,.017,M['hair'],hb)
    else:
        loft(prefix+' hair cap',[(1.74,.111,.092,.027),(1.77,.115,.097,.024),(1.815,.086,.079,.027),(1.846,.035,.036,.025)],M['hair'],hb,n=18,subdiv=1)
        for s in (-1,1):
            cube(prefix+' sideburn',(s*.102,-.025,1.704),(.016,.027,.047),M['hair'],hb,.005)
        if kind=='player':
            for i in range(4):
                o=cube(prefix+' swept fringe',(-.073+i*.042,-.070,1.771+i*.004),(.060,.051,.034),M['hair'],hb,.013)
                o.rotation_euler[1]=-.17

def make_hand(prefix,side,wrist,skin,gloves=False):
    bone='Hand_'+side;sgn=1 if side=='L' else -1
    w=Vector(wrist);p=w+Vector((sgn*.008,-.012,-.068))
    material=M['black'] if gloves else skin
    limb(prefix+' palm',w,p,[(0,.033,.025),(.12,.037,.026),(.65,.041,.024),(1,.037,.021)],material,bone,n=12)
    for j in range(4):
        x=w.x+sgn*(j-1.5)*.020
        start=Vector((x,p.y-.002,p.z+.01))
        leng=[.052,.061,.058,.045][j]
        mid=start+Vector((0,-.007,-leng*.60));tip=mid+Vector((0,-.011,-leng*.4))
        limb(prefix+' finger proximal',start,mid,[(0,.010,.011),(.45,.0105,.011),(1,.009,.01)],material,bone,n=8)
        limb(prefix+' finger distal',mid,tip,[(0,.009,.010),(.6,.008,.009),(1,.005,.006)],material,bone,n=8)
    th=w+Vector((-sgn*.035,-.003,-.025))
    end=th+Vector((-sgn*.025,-.020,-.045))
    limb(prefix+' thumb',th,end,[(0,.014,.014),(.45,.013,.013),(1,.009,.010)],material,bone,n=10)

def make_shoe(prefix,x,side,kind):
    b='Foot_'+side;boots=kind in ('officer','swat')
    cube(prefix+' sole',(x,-.060,.027),(.164,.316,.054),M['sole'],b,.020)
    loft(prefix+' shoe upper',[(.048,.079,.145,-.062,x),(.065,.083,.151,-.065,x),
          (.106,.078,.143,-.063,x),(.140,.066,.102,-.016,x),(.165,.058,.068,.012,x)],M['black'] if boots else M['leather'],b,n=16,subdiv=1,power=.75)
    if boots: loft(prefix+' boot cuff',[(.12,.065,.077,.01,x),(.15,.066,.075,.01,x),(.235,.058,.063,.01,x)],M['black'],b,n=12,subdiv=1)
    for i in range(3):
        strip(prefix+' lace',(x-.037,-.055-i*.026,.139-i*.008),(x+.037,-.055-i*.026,.139-i*.008),.008,.007,M['shoelace'],b)

def make_rig(name,joints):
    arm=bpy.data.armatures.new(name+' Skeleton')
    obj=bpy.data.objects.new(name+'_Rig',arm);current_collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    def bone(n,h,t,parent=None):
        b=arm.edit_bones.new(n);b.head=h;b.tail=t
        if parent:b.parent=arm.edit_bones[parent]
        return b
    bone('Root',(0,0,0),(0,0,.18))
    bone('Hips',(0,0,.89),(0,0,1.04),'Root')
    bone('Spine',(0,0,1.04),(0,0,1.25),'Hips')
    bone('Chest',(0,0,1.25),(0,0,1.46),'Spine')
    bone('Neck',(0,0,1.46),(0,0,1.56),'Chest')
    bone('Head',(0,0,1.56),(0,0,1.83),'Neck')
    for side in ('L','R'):
        sh,el,wr,hip,knee,ankle=joints[side]
        bone('Shoulder_'+side,(0,0,1.43),sh,'Chest')
        bone('UpperArm_'+side,sh,el,'Shoulder_'+side)
        bone('Forearm_'+side,el,wr,'UpperArm_'+side)
        bone('Hand_'+side,wr,Vector(wr)+Vector((0,-.025,-.12)),'Forearm_'+side)
        bone('Thigh_'+side,hip,knee,'Hips')
        bone('Calf_'+side,knee,ankle,'Thigh_'+side)
        foot_tip=Vector(ankle)+Vector((0,-.15,-.07))
        bone('Foot_'+side,ankle,foot_tip,'Calf_'+side)
        label='Left' if side=='L' else 'Right'
        bone(label+'Toes',foot_tip,foot_tip+Vector((0,-.090,-.010)),'Foot_'+side)
        for finger,points in finger_joints(side,wr,name in REFINED).items():
            names=[label+finger+part for part in ('Proximal','Intermediate','Distal')]
            for i,n in enumerate(names):bone(n,points[i],points[i+1],'Hand_'+side if i==0 else names[i-1])
    bpy.ops.object.mode_set(mode='OBJECT');obj.show_in_front=True
    for old,new in BONE_MAP.items():
        arm.bones[old].name=new
    obj['integration_status']='Continuous limb skinning and articulated fingers; base rig has no animation.' if name in REFINED else 'Original rigid-region first pass; animation deformation unfinished.'
    obj['forward_axis']='Blender -Y; FBX exported -Z forward / Y up.'
    obj['adult_character']=True
    return obj

def cap_player_sleeve(obj,side):
    """Cloth shoulder closure within the torso, exported with the sleeve renderer."""
    bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table()
    layer=bm.verts.layers.deform.verify();sgn=1 if side=='L' else -1
    boundary=[edge for edge in bm.edges if len(edge.link_faces)==1]
    unused=set(boundary)
    while unused:
        edge=next(iter(unused));loop=[edge.verts[0],edge.verts[1]];unused.remove(edge)
        while True:
            candidates=[e for e in loop[-1].link_edges if e in unused]
            if not candidates:break
            e=candidates[0];unused.remove(e);nxt=e.other_vert(loop[-1])
            if nxt==loop[0]:break
            loop.append(nxt)
        if len(loop)<3:continue
        middle=sum((v.co for v in loop),Vector())/len(loop)
        middle.x-=sgn*.050
        center_weights={}
        for v in loop:
            for group,value in v[layer].items():center_weights[group]=center_weights.get(group,0)+value/len(loop)
        kept=sorted(center_weights.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(v for k,v in kept)
        center_weights={k:v/total for k,v in kept}
        center=bm.verts.new(middle)
        for group,value in center_weights.items():center[layer][group]=value
        ring=[]
        for v in loop:
            new=bm.verts.new(v.co.lerp(middle,.45));ring.append(new)
            weights={group:value*.55 for group,value in v[layer].items()}
            for group,value in center_weights.items():weights[group]=weights.get(group,0)+value*.45
            kept=sorted(weights.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(v for k,v in kept)
            for group,value in kept:new[layer][group]=value/total
        for i,v in enumerate(loop):
            nxt=(i+1)%len(loop)
            bm.faces.new((v,loop[nxt],ring[nxt],ring[i]))
            bm.faces.new((ring[i],ring[nxt],center))
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    for face in obj.data.polygons:face.use_smooth=True
    obj['shoulder_closure']='Exported weighted cloth cap; lies within torso in third person.'

def fuse_garment(prefix,names,regions,voxel=.013,rig=None):
    """Union primary cloth into one smooth skin before splitting render regions.

    The duplicated border positions and weights on region splits match exactly;
    there are no intersecting closed caps at the shoulder or trouser/hip seams.
    """
    global parts
    objs=[o for o,b in parts if o.name in names]
    assert len(objs)==3,(prefix,names,[o.name for o in objs])
    parts=[(o,b) for o,b in parts if o not in objs]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();source=bpy.context.object
    kd=KDTree(len(source.data.vertices));source_weights=[]
    for v in source.data.vertices:
        kd.insert(v.co,v.index)
        source_weights.append({source.vertex_groups[g.group].name:g.weight for g in v.groups if g.weight>1e-7})
    kd.balance()
    surface=bpy.data.objects.new(prefix+' union cloth',source.data.copy());current_collection.objects.link(surface)
    surface.matrix_world=source.matrix_world.copy();bpy.context.view_layer.objects.active=surface
    remesh=surface.modifiers.new('Joined continuous garment surface','REMESH');remesh.mode='VOXEL';remesh.voxel_size=voxel;remesh.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    if prefix in ('Patrol_Officer','SWAT_Officer') and not any('trousers' in name for name in names):
        # The deltoid cap meets the collar with a descending shoulder line.
        # Do not let a horizontal tube end produce a raised sphere above it.
        for vertex in surface.data.vertices:
            ceiling=1.487-.24*abs(vertex.co.x)-.12*abs(vertex.co.y)
            if vertex.co.z>ceiling:vertex.co.z=ceiling
    smooth=surface.modifiers.new('Relax shoulder and hip seam','SMOOTH');smooth.factor=.65;smooth.iterations=3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    decimate=surface.modifiers.new('Midpoly garment reduction','DECIMATE');decimate.ratio=.60
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    lower_garment=any('trousers' in name for name in names)
    if lower_garment:
        for v in surface.data.vertices:
            t=max(0,min(1,(v.co.z-.88)/.065))
            v.co.x*=1-.13*t;v.co.y*=1-.11*t
    weights=[]
    for v in surface.data.vertices:
        nearby=kd.find_n(v.co,4);sums={}
        for position,index,distance in nearby:
            factor=1/max(distance,.0004)**3
            for bone,value in source_weights[index].items():sums[bone]=sums.get(bone,0)+factor*value
        if lower_garment:
            hips_blend=max(0,min(1,(v.co.z-.79)/.09));total_before=sum(sums.values())
            sums={bone:value*(1-hips_blend) for bone,value in sums.items()}
            sums['Hips']=sums.get('Hips',0)+total_before*hips_blend
        elif prefix in ('Patrol_Officer','SWAT_Officer'):
            sums=police_shoulder_weights(v.co,sums,prefix)
        keep=sorted(sums.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(v for k,v in keep)
        weights.append({k:v/total for k,v in keep if v/total>1e-7})
    surface.data.update();surface.data.calc_normals_split()
    shared_normals=[v.normal.copy() for v in surface.data.vertices]
    if prefix=='Player' and lower_garment:
        # Blender split-normal fans diverge after deformation on separate render regions.
        # This non-export full trouser surface supplies one shared deformed normal field.
        proxy=bpy.data.objects.new('Player_Trouser_NormalReference',surface.data.copy())
        current_collection.objects.link(proxy);proxy.matrix_world=surface.matrix_world.copy()
        for v in proxy.data.vertices:set_weights(proxy,v.index,weights[v.index])
        matrix=proxy.matrix_world.copy();proxy.parent=rig;proxy.matrix_world=matrix
        proxy.modifiers.new('Continuous trouser skin reference','ARMATURE').object=rig
        proxy.hide_render=True;proxy.hide_set(True);proxy.display_type='WIRE';proxy['non_export_helper']=True
        normal_references[prefix]=proxy
    material=source.data.materials[0]
    for region,selector in regions.items():
        polygons=[p for p in surface.data.polygons if selector(sum((surface.data.vertices[i].co for i in p.vertices),Vector())/len(p.vertices))]
        used=sorted({i for p in polygons for i in p.vertices});mapping={old:new for new,old in enumerate(used)}
        mesh=bpy.data.meshes.new(prefix+' '+region+' shared seam skin')
        mesh.from_pydata([surface.data.vertices[i].co.copy() for i in used],[],[tuple(mapping[i] for i in p.vertices) for p in polygons]);mesh.update()
        obj=bpy.data.objects.new(prefix+' continuous '+region,mesh);register(obj,obj.name,material,'Chest');obj['skin_region']=region
        for old,new in mapping.items():set_weights(obj,new,weights[old])
        if prefix in REFINED:
            attr=mesh.attributes.new('sd_shared_normal','FLOAT_VECTOR','POINT')
            for old,new in mapping.items():attr.data[new].vector=shared_normals[old]
        for poly in mesh.polygons:poly.use_smooth=True
        if prefix=='Player' and region.startswith('Arm_'):cap_player_sleeve(obj,region[-1])
    bpy.data.objects.remove(surface,do_unlink=True);bpy.data.objects.remove(source,do_unlink=True)

def apply_and_join(rig,prefix):
    refined=prefix in REFINED
    groups={}
    for o,bone in parts:
        bpy.context.view_layer.objects.active=o
        if refined and not o.vertex_groups:
            if prefix in ('Patrol_Officer','SWAT_Officer') and any(label in o.name for label in ('stitched epaulet','epaulet fastener','upper sleeve loop panel')):
                for v in o.data.vertices:
                    set_weights(o,v.index,police_shoulder_weights(o.matrix_world@v.co,{'Chest':1},prefix))
            elif o.name==prefix+' torso':
                for v in o.data.vertices:
                    z=(o.matrix_world@v.co).z
                    if z<1.04:weights={'Hips':1}
                    elif z<1.25:
                        t=(z-1.04)/.21;weights={'Hips':1-t,'Spine':t}
                    elif z<1.38:
                        t=(z-1.25)/.13;weights={'Spine':1-t,'Chest':t}
                    else:weights={'Chest':1}
                    set_weights(o,v.index,weights)
            else:
                for v in o.data.vertices:set_weights(o,v.index,{bone:1})
        for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
    if refined:
        fuse_garment(prefix,[prefix+' torso',prefix+' continuous sleeve L',prefix+' continuous sleeve R'],{
            'Chest':lambda p:abs(p.x)<=.210,
            'Arm_L':lambda p:p.x>.210,
            'Arm_R':lambda p:p.x<-.210})
        fuse_garment(prefix,[prefix+' pelvis',prefix+' continuous trousers L',prefix+' continuous trousers R'],{
            'Hips':lambda p:p.z>=.86,
            'Leg_L':lambda p:p.z<.86 and p.x>=0,
            'Leg_R':lambda p:p.z<.86 and p.x<0},rig=rig)
    for o,bone in parts:
        region=o.get('skin_region',bone)
        if refined and region==bone:
            for side in ('L','R'):
                if bone in ['Shoulder_'+side,'UpperArm_'+side,'Forearm_'+side,'Hand_'+side]:region='Arm_'+side
                if bone in ['Thigh_'+side,'Calf_'+side,'Foot_'+side]:region='Leg_'+side
        groups.setdefault(region,[]).append(o)
    final=[]
    for region,objs in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:o.select_set(True)
        bpy.context.view_layer.objects.active=objs[0]
        bpy.ops.object.join();o=bpy.context.object
        o.name=('PlayerHead' if prefix=='Player' and region=='Head' else prefix+'_'+region)
        if prefix in REFINED and o.data.attributes.get('sd_shared_normal') and not (prefix=='Player' and region.startswith('Arm_')):
            o.data.calc_normals_split();attr=o.data.attributes['sd_shared_normal']
            normals=[attr.data[v.index].vector.normalized() if attr.data[v.index].vector.length>.1 else v.normal.copy() for v in o.data.vertices]
            o.data.normals_split_custom_set_from_vertices(normals);o.data.use_auto_smooth=True
        matrix=o.matrix_world.copy();o.parent=rig
        if refined:
            o.parent_type='OBJECT';o.matrix_world=matrix
            mod=o.modifiers.new('Signal Debt continuous skin','ARMATURE');mod.object=rig
            mod.use_deform_preserve_volume=False
            if prefix=='Player' and region in ('Hips','Leg_L','Leg_R'):
                selection=o.vertex_groups.new(name='ClothNormalTransfer')
                normal_attribute=o.data.attributes.get('sd_shared_normal')
                cloth_vertices=[v.index for v in o.data.vertices if normal_attribute and normal_attribute.data[v.index].vector.length>.1]
                selection.add(cloth_vertices,1.0,'REPLACE')
                transfer=o.modifiers.new('Shared trouser seam shading','DATA_TRANSFER')
                transfer.vertex_group=selection.name
                transfer.object=normal_references[prefix];transfer.use_loop_data=True
                transfer.data_types_loops={'CUSTOM_NORMAL'};transfer.loop_mapping='POLYINTERP_NEAREST'
            o['deformation']='Linear skinning, normalized weights; continuous joined garment.'
        else:
            o.parent_type='BONE';o.parent_bone=BONE_MAP.get(region,region);o.matrix_world=matrix
        o['body_region']=region
        o['hide_for_first_person']=region in ('Head','Neck','Chest') or (region=='Hips' and prefix!='Player')
        final.append(o)
    return final

def make_anchors(rig,prefix,joints):
    result=[]
    def anchor(suffix,position,bone=None):
        o=bpy.data.objects.new(prefix+'_'+suffix,None);current_collection.objects.link(o)
        o.empty_display_type='PLAIN_AXES';o.empty_display_size=.035;o.location=position
        o['anchor_role']=suffix
        matrix=Matrix.Translation(Vector(position));o.parent=rig
        if bone:o.parent_type='BONE';o.parent_bone=bone
        o.matrix_world=matrix;result.append(o)
        return o
    anchor('BodyOrigin',(0,0,0));anchor('BodyForward',(0,-1,0));anchor('BodyUp',(0,0,1))
    if prefix=='Player':
        anchor('EyeView',(0,-.1046,1.699),'Head')
        anchor('HeadPivot',(0,0,1.56),'Head')
    for side in ('L','R'):
        label='Left' if side=='L' else 'Right';wrist=Vector(joints[side][2]);ankle=Vector(joints[side][5])
        anchor('Grip_'+side,wrist+Vector((0,-.022,-.050)),label+'Hand')
        anchor('PalmTip_'+side,wrist+Vector((0,-.021,-.066)),label+'Hand')
        anchor('PalmBack_'+side,wrist+Vector((0,.05,0)),label+'Hand')
        anchor('Sole_'+side,(ankle.x,-.060,0),label+'Foot')
        anchor('Toe_'+side,(ankle.x,-.215,.04),label+'Toes')
    return result

def character(name,kind,display_x):
    global current_collection,parts
    current_collection=bpy.data.collections.new(name+' | editable character')
    bpy.context.scene.collection.children.link(current_collection);parts=[]
    skin=M['skin_'+kind]
    torso={'player':'hoodie','victim':'shirt','judge':'robe','officer':'navy','swat':'swat'}[kind]
    pants={'player':'denim','victim':'trousers','judge':'black','officer':'navy','swat':'swat'}[kind]
    width=.95 if kind=='victim' else (1.04 if kind in ('judge','swat') else 1)
    torso_sections=[(.87,.17,.11,.016),(.89,.19,.121,.014),(.99,.194,.128,.012),
                    (1.10,.188,.125,.004),(1.22,.208,.143,.005),(1.34,.249,.145,.008),
                    (1.405,.264,.133,.011),(1.45,.19,.094,.011),(1.482,.083,.064,.012)]
    if kind=='player':
        # Shorter hoodie hem clears the top of the thigh during sitting/raised knees.
        torso_sections=[((.970 if z==.87 else .977 if z==.89 else 1.012 if z==.99 else z),w,d,y) for z,w,d,y in torso_sections]
    elif kind in ('officer','swat'):
        torso_sections=[(.925,.170,.113,.012),(.946,.180,.118,.012),(1.025,.177,.119,.010),
                        (1.115,.173,.125,.007),(1.230,.197,.136,.009),(1.325,.226,.136,.011),
                        (1.394,.235,.123,.011),(1.445,.178,.086,.010),(1.482,.080,.061,.010)]
    loft(name+' torso',[(z,w*width,d,y) for z,w,d,y in torso_sections],M[torso],'Chest',n=20,subdiv=1,power=.86)
    def cloth_front(x,z,offset=.007):
        low,high=torso_sections[0],torso_sections[-1]
        for aa,bb in zip(torso_sections,torso_sections[1:]):
            if aa[0]<=z<=bb[0]:low,high=aa,bb;break
        t=max(0,min(1,(z-low[0])/max(high[0]-low[0],.001)))
        rx=(low[1]*(1-t)+high[1]*t)*width
        ry=low[2]*(1-t)+high[2]*t;cy=low[3]*(1-t)+high[3]*t
        fraction=min(.997,abs(x)/max(rx,.001))
        return cy-ry*max(0,1-fraction**(2/.86))**(.86/2)-offset

    loft(name+' pelvis',[(.845,.176,.11,.019),(.90,.198,.132,.018),(1.00,.184,.126,.016)],M[pants],'Hips',n=16,subdiv=1)
    joints={}
    for side,sgn in [('L',1),('R',-1)]:
        sh=(sgn*.248*width,.01,1.405)
        el=(sgn*.362*width,-.035,1.16)
        wr=(sgn*.382*width,-.115,.936)
        hip=(sgn*.105,.013,.925);knee=(sgn*.120,-.009,.555);ankle=(sgn*.128,.018,.168)
        joints[side]=(sh,el,wr,hip,knee,ankle)
        sleeve=M[torso] if kind!='victim' else M['jacket']
        if name in REFINED:
            smooth_arm(name,side,sh,el,wr,sleeve)
            smooth_hand(name,side,wr,skin,kind=='swat')
            smooth_leg(name,side,hip,knee,ankle,M[pants])
        else:
            elbow_radius=.064 if kind not in ('judge','swat') else .079
            ellipsoid(name+' shoulder clothing bridge',sh,(.090,.087,.091),sleeve,'UpperArm_'+side)
            limb(name+' upper sleeve',sh,el,[(0,.089,.089),(.12,.094,.091),(.48,.082,.078),(.9,elbow_radius,elbow_radius),(1,elbow_radius,elbow_radius)],sleeve,'UpperArm_'+side)
            limb(name+' lower sleeve',el,wr,[(0,elbow_radius,elbow_radius),(.25,.07,.067),(.70,.057,.052),(.93,.046,.042),(1,.045,.041)],sleeve,'Forearm_'+side)
            ellipsoid(name+' elbow cloth continuity',el,(elbow_radius*.93,elbow_radius*.93,elbow_radius*.96),sleeve,'Forearm_'+side)
            cuff_a=Vector(wr)+Vector((0,.005,.034))
            limb(name+' wrist cuff',cuff_a,wr,[(0,.047,.042),(.12,.047,.042),(.9,.045,.041),(1,.044,.04)],M['rib'] if kind=='player' else sleeve,'Forearm_'+side)
            make_hand(name,side,wr,skin,kind=='swat')
            limb(name+' trouser thigh',hip,knee,[(0,.104,.111),(.13,.108,.111),(.43,.097,.101),(.80,.081,.084),(1,.079,.082)],M[pants],'Thigh_'+side,n=16)
            limb(name+' trouser calf',knee,ankle,[(0,.079,.082),(.20,.080,.082),(.48,.076,.078),(.90,.062,.064),(1,.060,.064)],M[pants],'Calf_'+side,n=16)
            ellipsoid(name+' knee cloth continuity',knee,(.074,.077,.076),M[pants],'Calf_'+side)
        make_shoe(name,sgn*.128,side,kind)
    make_head(name,skin,kind)
    if kind in ('officer','swat'):
        loft(name+' fitted stand collar',[(1.467,.083,.066,.010),(1.486,.080,.065,.008),(1.506,.075,.063,.007)],M['navylight'] if kind=='officer' else M['swat'],'Neck',n=24,subdiv=1)
    if kind=='player':
        # Layered hood sits around the neck and back, not over the first-person face.
        ellipsoid(name+' hood back',(0,.111,1.456),(.157,.090,.133),M['hoodie'],'Chest')
        for s in (-1,1):
            limb(name+' hood opening',(s*.076,-.031,1.472),(s*.113,-.108,1.379),[(0,.024,.029),(.45,.027,.025),(1,.018,.018)],M['hooddark'],'Chest')
            strip(name+' drawstring',(s*.059,-.080,1.452),(s*.072,-.147,1.292),.007,.007,M['shoelace'],'Chest')
        cube(name+' kangaroo pocket',(0,-.132,1.034),(.271,.022,.125),M['hoodie'],'Chest',.026)
        strip(name+' pocket left',(-.123,-.151,1.085),(-.095,-.151,1.029),.008,.007,M['rib'],'Chest')
        strip(name+' pocket right',(.123,-.151,1.085),(.095,-.151,1.029),.008,.007,M['rib'],'Chest')
        loft(name+' hoodie hem',[(.970,.182,.12,.014),(.977,.192,.129,.014),(1.010,.194,.13,.014)],M['rib'],'Chest',n=20,subdiv=1)
        for s in (-1,1): strip(name+' jeans outer seam',(s*.194,.005,.89),(s*.194,.005,.71),.004,.005,M['denimdark'],'Thigh_'+('L' if s>0 else 'R'))
    elif kind=='victim':
        for s in (-1,1):
            o=cube(name+' jacket front',(s*.116,-.122,1.215),(.159,.055,.433),M['jacket'],'Chest',.026)
            o.rotation_euler[1]=s*.095
            strip(name+' lapel',(s*.06,-.148,1.442),(s*.114,-.157,1.266),.050,.019,M['jacketdark'],'Chest')
            cube(name+' jacket pocket',(s*.117,-.159,1.085),(.097,.022,.071),M['jacket'],'Chest',.012)
        ellipsoid(name+' necklace',(0,-.145,1.384),(.014,.008,.017),M['gold'],'Chest')
    elif kind=='judge':
        loft(name+' robe skirt',[(.23,.288,.178,.01),(.26,.291,.18,.01),(.54,.27,.174,.01),(.86,.223,.149,.01),(1.04,.197,.134,.006)],M['robe'],'Hips',n=24,subdiv=1,power=.9)
        loft(name+' continuous robe waist',[(.81,.225,.151,.01),(.84,.225,.151,.01),(.94,.218,.148,.01),(1.06,.205,.141,.01),(1.15,.199,.133,.009),(1.17,.198,.132,.008)],M['robe'],'Hips',n=24,subdiv=1,power=.9)
        for s in (-1,1):
            strip(name+' judicial lapel',(s*.068,-.098,1.46),(s*.135,-.151,1.065),.068,.022,M['robetrim'],'Chest')
            strip(name+' robe front pleat',(s*.11,-.166,.93),(s*.16,-.178,.31),.016,.012,M['robetrim'],'Hips')
            o=cube(name+' white neck band',(s*.025,-.152,1.393),(.034,.020,.126),M['white'],'Chest',.005)
            o.rotation_euler[1]=-s*.10
    elif kind=='officer':
        # Tailored chest panels, not floating rectangular shoulder wedges.
        for side,sgn in [('L',1),('R',-1)]:
            x=sgn*.098
            outline=[(x-.049,1.326),(x+.049,1.326),(x+.045,1.238),(x,1.226),(x-.045,1.238)]
            shaped_panel(name+' shaped shirt pocket '+side,outline,lambda x,z:cloth_front(x,z,.009),.010,M['navylight'],'Chest',.003)
            shaped_panel(name+' pentagonal pocket flap '+side,[(x-.053,1.331),(x+.053,1.331),(x+.047,1.312),(x,1.302),(x-.047,1.312)],lambda x,z:cloth_front(x,z,.016),.009,M['navy'],'Chest',.002)
            ellipsoid(name+' pocket button',(x,cloth_front(x,1.318,.019),1.318),(.004,.002,.004),M['metal'],'Chest',10,6)
            shaped_panel(name+' pointed collar '+side,[(sgn*.028,1.480),(sgn*.073,1.466),(sgn*.093,1.415),(sgn*.034,1.441)],lambda x,z:cloth_front(x,z,.008),.011,M['navylight'],'Chest',.004)
            epaulet=strip(name+' stitched epaulet '+side,(sgn*.104,.006,1.451),(sgn*.225,.007,1.418),.032,.010,M['navylight'],'Shoulder_'+side)
            ellipsoid(name+' epaulet fastener',(sgn*.134,-.001,1.448),(.006,.005,.003),M['metal'],'Shoulder_'+side,10,6)
            sh,el,wr,*_=joints[side]
            for t in (.66,.77):
                point=Vector(sh).lerp(Vector(el),t)+Vector((sgn*.061,-.013,0))
                strip(name+' sleeve articulated seam '+side,point+Vector((0,.017,.008)),point+Vector((0,-.017,-.005)),.004,.002,M['fabricseam'],'UpperArm_'+side)
            cuff_a=Vector(wr)+(Vector(el)-Vector(wr)).normalized()*.029
            limb(name+' tailored cuff '+side,cuff_a,wr,[(0,.045,.041),(.08,.046,.042),(.9,.044,.040),(1,.044,.040)],M['navylight'],'Forearm_'+side,n=16,subdiv=1)
        shaped_panel(name+' center shirt placket',[(.010,z) for z in (1.10,1.18,1.25,1.32,1.40,1.43)]+[(-.010,z) for z in (1.43,1.40,1.32,1.25,1.18,1.10)],lambda x,z:cloth_front(x,z,.006),.009,M['navylight'],'Chest',.001)
        for z in (1.13,1.22,1.31,1.40):ellipsoid(name+' shirt button',(0,cloth_front(0,z,.009),z),(.004,.003,.004),M['metal'],'Chest',10,6)
        shaped_panel(name+' enamel shield badge',[(-.123,1.363),(-.082,1.363),(-.083,1.337),(-.1025,1.321),(-.122,1.337)],lambda x,z:cloth_front(x,z,.017),.006,M['gold'],'Chest',.002)
        cube(name+' engraved name plate',(.098,cloth_front(.098,1.354,.013),1.354),(.067,.008,.015),M['metal'],'Chest',.002)
        text_obj(name+' nameplate lettering','N. VALE',(.098,cloth_front(.098,1.354,.018),1.350),.008,M['white'],'Chest')
        loft(name+' police cap',[(1.779,.12,.107,.016),(1.81,.122,.108,.016),(1.851,.098,.09,.022)],M['navy'],'Head',n=24,subdiv=1)
        loft(name+' cap binding',[(1.779,.120,.107,.016),(1.782,.122,.109,.016),(1.794,.121,.108,.016)],M['black'],'Head',n=24,subdiv=1)
        visor=ellipsoid(name+' curved cap visor',(0,-.112,1.783),(.120,.094,.009),M['black'],'Head',24,12)
        ellipsoid(name+' cap badge',(0,-.096,1.824),(.016,.006,.024),M['gold'],'Head',12,8)
        # Shoulder microphone and a restrained cable route to the belt radio.
        cube(name+' shoulder microphone',(.127,-.118,1.404),(.034,.023,.049),M['radioplastic'],'Chest',.006)
        for z in (1.393,1.402,1.411):cube(name+' microphone grille',(.127,-.132,z),(.021,.002,.003),M['black'],'Chest',.001)
        cable(name+' microphone cable',[(.129,-.114,1.392),(.149,-.115,1.342),(.158,-.106,1.255),(.153,-.090,1.164)],.003,M['black'],'Chest')
    elif kind=='swat':
        # A tapered carrier with shoulder/arm cutouts follows the chest envelope.
        front=[(-.088,1.413),(.088,1.413),(.151,1.369),(.175,1.280),(.174,1.113),(.143,1.079),(-.143,1.079),(-.174,1.113),(-.175,1.280),(-.151,1.369)]
        shaped_panel(name+' cutaway front plate carrier',front,-.166,.048,M['vest'],'Chest',.011)
        rear=[(-.098,1.430),(.098,1.430),(.157,1.377),(.177,1.273),(.167,1.115),(.136,1.089),(-.136,1.089),(-.167,1.115),(-.177,1.273),(-.157,1.377)]
        shaped_panel(name+' shaped rear plate carrier',rear,.126,.050,M['vest'],'Chest',.011)
        for side,sgn in [('L',1),('R',-1)]:
            strip(name+' front carrier bound edge '+side,(sgn*.142,-.175,1.103),(sgn*.161,-.175,1.273),.008,.006,M['carriertrim'],'Chest')
            strip(name+' shoulder carrier attachment '+side,(sgn*.108,-.135,1.391),(sgn*.106,.109,1.452),.045,.016,M['vestedge'],'Chest')
            cube(name+' shoulder webbing keeper '+side,(sgn*.108,-.070,1.431),(.049,.024,.013),M['black'],'Chest',.003)
            # Individual flat webbing rows with separated stitch channels.
            for z in (1.200,1.227,1.254,1.281):
                cube(name+' front MOLLE webbing '+side,(sgn*.091,-.196,z),(.151,.006,.012),M['vestedge'],'Chest',.002)
                for xx in (.037,.082,.127):cube(name+' webbing stitch '+side,(sgn*xx,-.200,z),(.002,.002,.010),M['carriertrim'],'Chest',.0007)
            pouchx=sgn*.080
            outline=[(pouchx-.043,1.236),(pouchx+.043,1.236),(pouchx+.047,1.113),(pouchx+.033,1.094),(pouchx-.033,1.094),(pouchx-.047,1.113)]
            shaped_panel(name+' fitted closed utility pouch '+side,outline,-.230,.047,M['vestedge'],'Chest',.007)
            shaped_panel(name+' pouch flap '+side,[(pouchx-.046,1.243),(pouchx+.046,1.243),(pouchx+.040,1.215),(pouchx,1.206),(pouchx-.040,1.215)],-.238,.009,M['vest'],'Chest',.004)
            cube(name+' pouch pull tab '+side,(pouchx,-.246,1.212),(.018,.004,.023),M['carriertrim'],'Chest',.002)
            # Low-profile sleeve velcro and a thin collar opening keep arms readable.
            shoulder,elbow,wrist,*_=joints[side]
            sleeve_axis=(Vector(elbow)-Vector(shoulder)).normalized()
            panel_normal=Vector((sgn,0,0));panel_normal=(panel_normal-sleeve_axis*panel_normal.dot(sleeve_axis)).normalized()
            panel_cross=sleeve_axis.cross(panel_normal).normalized()
            patch=ellipsoid(name+' upper sleeve loop panel '+side,Vector(shoulder).lerp(Vector(elbow),.34)+panel_normal*.075,(.003,.032,.041),M['vest'],'UpperArm_'+side,16,10)
            patch.rotation_euler=Matrix((panel_normal,panel_cross,sleeve_axis)).transposed().to_euler()
            strip(name+' combat shirt collar '+side,(sgn*.060,-.057,1.476),(sgn*.073,-.093,1.443),.034,.016,M['vestedge'],'Chest')
            knee=joints[side][4]
            kneebone='Calf_'+side
            # Distinct hard central cup with cloth perimeter and compact straps.
            ellipsoid(name+' kneepad cloth backing '+side,(sgn*.120,-.074,.550),(.073,.028,.096),M['black'],kneebone,20,12)
            ellipsoid(name+' kneepad shaped shell '+side,(sgn*.120,-.100,.554),(.059,.020,.075),M['vestedge'],kneebone,20,12)
            for z in (.512,.581):cube(name+' kneepad inset rib '+side,(sgn*.120,-.119,z),(.071,.004,.007),M['vest'],kneebone,.003)
            # Ear protection fitted beneath rails, not unattached floating blocks.
            ellipsoid(name+' headset cushioned earcup '+side,(sgn*.125,.009,1.677),(.026,.042,.057),M['black'],'Head',20,12)
            ellipsoid(name+' headset shell '+side,(sgn*.145,.009,1.677),(.015,.034,.045),M['vestedge'],'Head',20,12)
            strip(name+' helmet suspension strap '+side,(sgn*.117,.051,1.778),(sgn*.132,.032,1.709),.009,.008,M['black'],'Head')
            cable(name+' helmet chin Y strap '+side,[(sgn*.115,-.046,1.740),(sgn*.095,-.072,1.645),(sgn*.034,-.091,1.562)],.005,M['black'],'Head')
            cable(name+' rear chin strap '+side,[(sgn*.108,.067,1.732),(sgn*.095,-.008,1.638),(sgn*.034,-.091,1.562)],.004,M['black'],'Head')
            cube(name+' helmet accessory rail '+side,(sgn*.143,.011,1.754),(.013,.076,.018),M['carriertrim'],'Head',.004)
            for yy in (-.015,.015,.045):ellipsoid(name+' rail fastener '+side,(sgn*.152,yy,1.754),(.002,.004,.004),M['metal'],'Head',8,6)
        cube(name+' chin retention pad',(0,-.091,1.558),(.085,.022,.018),M['black'],'Head',.007)
        cube(name+' POLICE identification backing',(0,-.176,1.340),(.214,.008,.052),M['vestedge'],'Chest',.004)
        text_obj(name+' police patch','POLICE',(0,-.183,1.326),.038,M['white'],'Chest')
        text_obj(name+' back police patch','POLICE',(0,.184,1.326),.038,M['white'],'Chest').rotation_euler=(math.pi/2,0,math.pi)
        loft(name+' helmet shell',[(1.737,.131,.113,.016),(1.747,.138,.120,.021),(1.794,.140,.127,.025),(1.840,.122,.114,.027),(1.875,.080,.078,.027),(1.890,.027,.029,.025)],M['vest'],'Head',n=32,subdiv=1)
        loft(name+' helmet bound rim',[(1.733,.132,.114,.016),(1.739,.140,.122,.021),(1.749,.142,.124,.021),(1.754,.138,.120,.021)],M['black'],'Head',n=32,subdiv=1)
        shaped_panel(name+' helmet front mount',[(-.024,1.806),(.024,1.806),(.030,1.774),(.014,1.766),(-.014,1.766),(-.030,1.774)],-.106,.009,M['metal'],'Head',.003)
        cable(name+' headset boom',[(.149,-.008,1.660),(.133,-.055,1.636),(.086,-.108,1.626),(.038,-.111,1.622)],.003,M['metal'],'Head')
        ellipsoid(name+' microphone foam',(.035,-.112,1.622),(.019,.007,.008),M['black'],'Head',12,8)
        # Back radio/power pack is distinguishable from the soft carrier.
        shaped_panel(name+' carrier radio pouch',[(.073,1.340),(.145,1.340),(.145,1.182),(.073,1.182)],.174,.032,M['vestedge'],'Chest',.006)
        cube(name+' radio case',(.111,.204,1.316),(.055,.029,.095),M['radioplastic'],'Chest',.006)
        cable(name+' radio antenna',[(.127,.205,1.360),(.125,.210,1.474),(.124,.210,1.497)],.003,M['black'],'Chest')
        cable(name+' headset radio lead',[(.096,.103,1.447),(.075,.157,1.399),(.110,.212,1.349)],.003,M['black'],'Chest')
    if kind in ('officer','swat'):
        loft(name+' duty belt',[(.939,.187,.129,.01),(.944,.194,.135,.01),(.984,.194,.135,.01),(.99,.187,.128,.01)],M['black'],'Hips',n=28,subdiv=1)
        cube(name+' belt buckle',(0,-.131,.965),(.045,.017,.032),M['metal'],'Hips',.004)
        for side,sgn in [('L',1),('R',-1)]:
            cube(name+' belt keeper '+side,(sgn*.114,-.111,.965),(.016,.013,.048),M['vestedge'],'Hips',.003)
        cube(name+' closed rear medical pouch',(0,.157,.926),(.131,.056,.093),M['vestedge'],'Hips',.011)
        cube(name+' medical pouch flap',(0,.189,.955),(.128,.009,.031),M['vest'],'Hips',.003)
        cube(name+' belt radio shell',(.204,.017,.954),(.057,.043,.099),M['radioplastic'],'Hips',.006)
        for z in (.941,.951,.961):cube(name+' belt radio grill',(.235,.017,z),(.002,.025,.004),M['black'],'Hips',.001)
        # Anatomical-right retention holster; patrol is empty while prop is held.
        holster_outline=[(-.263,.967),(-.218,.967),(-.209,.918),(-.216,.836),(-.252,.831),(-.265,.873)]
        shaped_panel(name+' molded duty holster',holster_outline,-.005,.069,M['holster'],'Hips',.007)
        cube(name+' holster belt mount',(-.197,.020,.958),(.042,.057,.038),M['black'],'Hips',.006)
        cube(name+' holster retention hood',(-.243,.025,.974),(.055,.078,.017),M['holster'],'Hips',.004)
        if kind=='swat':
            # Inert partial exterior only: no barrel bore, mechanism or firing logic.
            cube(name+' stowed inert prop exposed grip',(-.238,.067,.995),(.032,.050,.067),M['stowedprop'],'Hips',.007)
            cube(name+' stowed inert prop rear slide',(-.239,.018,.999),(.029,.068,.024),M['stowedprop'],'Hips',.004)
            for z in (.981,.993,1.005):cube(name+' stowed grip molded rib',(-.256,.073,z),(.002,.026,.002),M['holster'],'Hips',.001)
    bpy.ops.object.select_all(action='DESELECT')
    rig=make_rig(name,joints)
    if kind in ('officer','swat'):
        rig['equipment_manifest_json']=json.dumps({'carrier':'Chest' if kind=='swat' else None,'radio':'Chest/Hips','headgear':'Head','holster':{'bone':'Hips','side':'anatomical right','occupied':kind=='swat','occupant_material':'SWAT | inert holstered sidearm exterior' if kind=='swat' else None},'inert_prop_only':True,'semantic_materials_preserved':True})
    objects=apply_and_join(rig,name)
    anchors=make_anchors(rig,name,joints)
    rig.location.x=display_x
    if kind=='player':rig.scale=(.95,.95,.95)
    current_collection['asset_status']='Editable stylized adult: skinned limbs/fingers; production animation integration pending.' if name in REFINED else 'Editable rigid-region first pass; animation deformation unfinished.'
    rig['anchors_json']=json.dumps({o['anchor_role']:o.name for o in anchors})
    return rig,objects,current_collection

CAST=[('Player','player',-3.1),('Mara_Victim','victim',-1.55),('Judge','judge',0),('Patrol_Officer','officer',1.55),('SWAT_Officer','swat',3.1)]
all_chars=[]
for name,kind,x in CAST:
    all_chars.append((name,*character(name,kind,x)))

stats={}
for name,rig,objs,coll in all_chars:
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in objs:o.select_set(True)
    for o in coll.objects:
        if o.type=='EMPTY':o.select_set(True)
    bpy.context.view_layer.objects.active=rig
    old=rig.location.copy();rig.location=(0,0,0)
    bpy.context.view_layer.update()
    filename='PlayerBody.fbx' if name=='Player' else name+'.fbx'
    if '--player-only-export' not in sys.argv or name=='Player':
        bpy.ops.export_scene.fbx(filepath=str(OUT/filename),use_selection=True,
            object_types={'ARMATURE','MESH','EMPTY'},global_scale=1.0,apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',use_mesh_modifiers=True,
            mesh_smooth_type='FACE',add_leaf_bones=False,armature_nodetype='ROOT',
            bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    rig.location=old
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs)
    stats[name]={'height_m_approx':1.76 if name=='Player' else (1.89 if name=='SWAT_Officer' else 1.85),
                 'mesh_objects':len(objs),'triangles':tris,'bones':len(rig.data.bones),
                 'rig':('Continuous skinned limbs, articulated fingers; no animation clips in base' if name in REFINED else 'Rigid-region first pass'),
                 'fbx':filename,'head_hidden_separately':True}

current_collection=bpy.data.collections.new('Preview studio | excluded from exports')
bpy.context.scene.collection.children.link(current_collection)
cube('Studio floor',(0,0,-.095),(14,9,.12),M['floor'],radius=.05)
for name,kind,x in CAST:
    cube(name+' preview platform',(x,0,-.013),(1.30,.90,.07),M['plinth'],radius=.05)
    text_obj(name+' label',{'player':'PLAYER','victim':'MARA / VICTIM','judge':'JUDGE','officer':'PATROL','swat':'SWAT'}[kind],(x,-.51,.074),.125,M['label'])
    text_obj(name+' role label',{'player':'HOODIE + DENIM','victim':'CIVILIAN','judge':'COURT','officer':'UNIFORM','swat':'LATER ESCALATION'}[kind],(x,-.515,-.002),.060,M['accent'])
text_obj('Roster title','SIGNAL DEBT  /  CHARACTER STUDIES',(0,.24,2.55),.20,M['label'])
text_obj('Roster subtitle','STYLIZED ADULT CAST     |     EDITABLE FIRST PASS     |     1.85 m BASE HEIGHT',(0,.24,2.35),.073,M['accent'])
text_obj('Roster status','SKINNED PLAYER + POLICE  /  ARTICULATED HANDS  /  EDITABLE FIRST PASS',(0,-.70,-.22),.077,M['label'])

world=bpy.context.scene.world or bpy.data.worlds.new('Studio world');bpy.context.scene.world=world
world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.061,.072,1);world.node_tree.nodes['Background'].inputs[1].default_value=.42
def area(name,loc,energy,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size;data.color=color
    o=bpy.data.objects.new(name,data);current_collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large soft key',(-3,-5,7),1300,7,(1,.85,.73))
area('Cool fill',(5,-3,4),1050,6,(.65,.81,1))
area('Top rim',(1,3,6),1600,6,(.56,.83,1))
camera_data=bpy.data.cameras.new('Roster camera');camera=bpy.data.objects.new('Roster camera',camera_data);current_collection.objects.link(camera)
camera.location=(4.9,-16,5.9);target=Vector((0,0,1.18));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=9.25
bpy.context.scene.camera=camera
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64
scene.cycles.use_denoising=False
scene.render.resolution_x=2400;scene.render.resolution_y=1450;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.render.filepath=str(RENDERS/'Characters_Roster.png')
scene['asset_delivery_note']='First-pass stylized adult characters. Not finished animation or facial rigs.'
scene['source_script']='build_characters.py'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender/Characters.blend'))
with open(OUT/'character_stats.json','w') as f:json.dump(stats,f,indent=2)
bpy.context.view_layer.update()
contract={'version':2,'units':'meters','source_up':'+Z','source_forward':'-Y','anatomical_left':'+X','fbx_axis_forward':'-Z','fbx_axis_up':'Y','bone_roll':0,'bone_longitudinal_axis':'+Y','constraints':False,'drivers':False,'animations_in_base':False,'characters':{}}
for name,rig,objs,coll in all_chars:
    anchors=json.loads(rig['anchors_json'])
    scale=rig.scale.x
    bones={b.name:{'head_source_m':list(b.head_local),'tail_source_m':list(b.tail_local),'parent':b.parent.name if b.parent else None,'matrix_local':[list(row) for row in b.matrix_local]} for b in rig.data.bones}
    lengths={}
    for side in ('Left','Right'):
        for part in ('UpperArm','LowerArm','UpperLeg','LowerLeg'):
            lengths[side+part]=rig.data.bones[side+part].length*scale
    anchor_positions={role:list((rig.matrix_world.inverted()@bpy.data.objects[obj_name].matrix_world).translation) for role,obj_name in anchors.items()}
    contract['characters'][name]={'rig_object':rig.name,'scale':scale,'source_bones':bones,'chain_lengths_m_after_scale':lengths,'anchors':anchors,'anchor_positions_source_m':anchor_positions,'visible_first_person_regions':[o.name for o in objs if o['body_region'].startswith(('Arm_','Leg_')) or (name=='Player' and o['body_region']=='Hips')],'hide_first_person_regions':[o.name for o in objs if o.get('hide_for_first_person')],'skin_type':'linear armature weights' if name in REFINED else 'rigid bone parenting','mesh_objects':[o.name for o in objs]}
with open(OUT/'rig_contract.json','w') as f:json.dump(contract,f,indent=2)
with open(OUT/'README.md','w') as f:
    f.write("""# Signal Debt — editable stylized adult characters

Player, patrol, and SWAT have actual continuous skinned sleeves/trousers across elbow and knee joints, weighted shoulder/wrist regions, articulated hands, 30 finger bones, and two toe bones. Each rig contains 52 bones. Skinning uses standard linear blending and normalized weights. Judge and Mara retain their first-pass rigid-region meshes; their animation deformation is unfinished.

Source: `Blender/Characters.blend`; regenerate with `Blender/build_characters.py`. Core joint names, exact rest coordinates, region names, source orientation markers, and chain lengths are specified in `rig_contract.json`. Source faces -Y with +Z up. Anatomical left is +X. FBX exporter uses -Z forward/Y up; the importing runtime should align the three named Body markers rather than assume a rotation.

Player first-person visible renderers are `Player_Arm_L`, `Player_Arm_R`, `Player_Leg_L`, `Player_Leg_R`, and `Player_Hips` (clothed pants waist). Weighted cloth shoulder closures are included inside the two arm renderers. Set `Player_Chest`, `Player_Neck`, and `PlayerHead` to ShadowsOnly for local first person. Preserve every bone and renderer for third person/shadows. Geometry is attached through Armature modifiers, not runtime scale tricks. Anchors use each character prefix plus BodyOrigin, BodyForward, BodyUp, Grip_L/R, PalmTip_L/R, PalmBack_L/R, Sole_L/R, Toe_L/R. PalmBack minus Hand gives the dorsal reference normal for wrist roll.

Player-only `Player_EyeView` and `Player_HeadPivot` markers are parented to Head. EyeView source position is `(0, -0.1046, 1.699)` m; at the player scale its resting height is 1.61405 m and forward offset 0.09937 m. HeadPivot source height is 1.56 m (1.482 m scaled). Standing/crouch cameras follow the posed eye marker, compensating only procedural reach-based pelvis lowering; the seated workstation camera uses its separately approved placement. Proof images use 76° vertical FOV, 16:9, and a 75° downward standing/crouched look. The weighted shoulder closures and visible trouser waist close the actual exported first-person clothing surfaces.

These are actual first-pass modeling and skinning assets, not finished production characters. No animation clips, embedded scripts, constraints, drivers, networking, or external textures are included in these base FBXs. SWAT has an inert holstered exterior prop; the patrol holster is empty for the held-prop animation. No prop has a mechanism or firing logic. The separate animation builder supplies police clips and the patrol held prop. Police garments now use tailored torso/armhole profiles and explicit axillary weight blending; equipment uses shaped panels, readable seams and low-profile attachments. The SWAT carrier has shoulder cutouts, webbing, fitted pouches, rear radio, ear protection, retention straps and shaped knee cups. Distinct equipment materials and `equipment_manifest_json` preserve occupancy/binding inspection after renderer merging. Generic rig integration is intended; no claim of verified Unity Humanoid import is made. A hidden Blender-only full-trouser normal reference prevents split-region shading seams after deformation; its transfer is limited to cloth vertices, excludes shoes/accessories, and is excluded from FBX. The actual exported shared seam positions, weights, and rest normals are verified. Unity must import supplied normals; runtime shading still needs editor validation. A coarse folded crease remains near the upper trouser leg under deep hip flexion; shared seam positions, weights, and rest normals match, so it is a polish limitation rather than an open seam. Facial animation, production topology retouching, clothing collision, polished locomotion, retargeting, and Unity Play Mode validation remain work.

All five characters are fictional adults. Source material palettes and mesh regions remain editable. Mesh and triangle counts are in `character_stats.json`. FBX structural and deformation checks are in `fbx_roundtrip_verification.json`; those Blender reimport checks do not replace testing in the Unity editor.
""")
print('GEOMETRY_AND_RIG_CONTRACT_READY',flush=True)
if '--skip-render' not in sys.argv:bpy.ops.render.render(write_still=True)
print('CHARACTER_DELIVERY_COMPLETE',json.dumps(stats))
