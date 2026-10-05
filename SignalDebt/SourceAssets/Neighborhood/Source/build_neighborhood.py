"""Editable exterior-first neighborhood study. External outputs only.
Coordinates: Blender X=design east, Y=design north, Z=height.
Not final art, Unity traversal, performance or implemented-interior acceptance.
"""
import argparse, hashlib, json, math, random, sys
from pathlib import Path
import bpy
from mathutils import Vector

RNG=random.Random(521)
M={}; COL={}; COLL=[]; ANCH=[]; VIEWS=[]; BOX_CHECKS=[]; SLAB_FOOTPRINTS={}
ROOT=Path(__file__).resolve().parent

def args():
    p=argparse.ArgumentParser();p.add_argument('--output',default=str(ROOT));p.add_argument('--apartment-source',default='/workspace/STORYGAMEr/SignalDebt/Blender/SignalDebt_Apartment.blend');p.add_argument('--samples',type=int,default=24);p.add_argument('--width',type=int,default=1280);p.add_argument('--height',type=int,default=720);p.add_argument('--views',default='ApartmentExit,ShopAcrossStreet,ShopOblique,ShopInterior,PoliceApproach,WindowPassage,WestBend,BackReturn');p.add_argument('--render-only',action='store_true');p.add_argument('--no-render',action='store_true');p.add_argument('--export',action='store_true');return p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

def group(name):
    if name not in COL:
        c=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(c);COL[name]=c
    return COL[name]

def put(o,name,material,collection):
    o.name=name
    for c in list(o.users_collection):c.objects.unlink(o)
    group(collection).objects.link(o)
    if material:o.data.materials.append(M.get(material,material))
    return o

def mat(name,color,rough=.65,metal=0,noise=0,brick=False,trans=0,emit=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal;p.inputs['Transmission Weight'].default_value=trans
    if emit:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emit
    n=m.node_tree.nodes;l=m.node_tree.links
    if noise or brick:
        tc=n.new('ShaderNodeTexCoord');mapping=n.new('ShaderNodeVectorMath');mapping.operation='SCALE';mapping.inputs[3].default_value=1;l.new(tc.outputs['Object'],mapping.inputs[0])
        if brick:
            t=n.new('ShaderNodeTexBrick');t.inputs['Color1'].default_value=(*color,1);t.inputs['Color2'].default_value=(*[v*.76 for v in color],1);t.inputs['Mortar'].default_value=(.23,.22,.19,1);t.inputs['Scale'].default_value=1;t.inputs['Mortar Size'].default_value=.008;t.inputs['Mortar Smooth'].default_value=.003;t.inputs['Brick Width'].default_value=.25;t.inputs['Row Height'].default_value=.09
            # Face-local planar brick coordinates; vertical façades use X/Z or Y/Z, never horizontal stripes.
            geo=n.new('ShaderNodeNewGeometry');sep=n.new('ShaderNodeSeparateXYZ');l.new(geo.outputs['Normal'],sep.inputs[0]);absx=n.new('ShaderNodeMath');absx.operation='ABSOLUTE';l.new(sep.outputs['X'],absx.inputs[0]);xyz=n.new('ShaderNodeSeparateXYZ');l.new(tc.outputs['Object'],xyz.inputs[0]);mix=n.new('ShaderNodeMixRGB');mix.blend_type='MIX';l.new(absx.outputs[0],mix.inputs[0]);v1=n.new('ShaderNodeCombineXYZ');v2=n.new('ShaderNodeCombineXYZ');l.new(xyz.outputs['X'],v1.inputs['X']);l.new(xyz.outputs['Z'],v1.inputs['Y']);l.new(xyz.outputs['Y'],v2.inputs['X']);l.new(xyz.outputs['Z'],v2.inputs['Y']);l.new(v1.outputs[0],mix.inputs[1]);l.new(v2.outputs[0],mix.inputs[2]);l.new(mix.outputs[0],t.inputs['Vector']);l.new(t.outputs['Color'],p.inputs['Base Color']);b=n.new('ShaderNodeBump');b.inputs['Strength'].default_value=.22;b.inputs['Distance'].default_value=.02;l.new(t.outputs['Fac'],b.inputs['Height']);l.new(b.outputs['Normal'],p.inputs['Normal'])
        else:
            t=n.new('ShaderNodeTexNoise');t.inputs['Scale'].default_value=6;t.inputs['Detail'].default_value=3;l.new(mapping.outputs[0],t.inputs['Vector']);r=n.new('ShaderNodeValToRGB');r.color_ramp.elements[0].position=.15;r.color_ramp.elements[0].color=(*[max(0,v-noise) for v in color],1);r.color_ramp.elements[1].position=.85;r.color_ramp.elements[1].color=(*[min(1,v+noise) for v in color],1);l.new(t.outputs['Fac'],r.inputs[0]);l.new(r.outputs['Color'],p.inputs['Base Color']);b=n.new('ShaderNodeBump');b.inputs['Strength'].default_value=.12;b.inputs['Distance'].default_value=.025;l.new(t.outputs['Fac'],b.inputs['Height']);l.new(b.outputs[0],p.inputs['Normal'])
    M[name]=m;return m

def box(name,loc,size,ma,co='Architecture',bevel=0,collide=False):
    verts=[(sx*size[0]/2,sy*size[1]/2,sz*size[2]/2) for sx,sy,sz in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],[(0,2,6,4),(1,5,7,3),(0,4,5,1),(2,3,7,6),(0,1,3,2),(4,6,7,5)]);me.update();o=bpy.data.objects.new(name,me);group(co).objects.link(o);o.location=loc;o.data.materials.append(M[ma])
    volume=sum(Vector(me.vertices[p.vertices[0]].co).dot(Vector(me.vertices[p.vertices[1]].co).cross(Vector(me.vertices[p.vertices[2]].co)))/6 + Vector(me.vertices[p.vertices[0]].co).dot(Vector(me.vertices[p.vertices[2]].co).cross(Vector(me.vertices[p.vertices[3]].co)))/6 for p in me.polygons)
    expected=size[0]*size[1]*size[2]
    assert volume>0 and abs(volume-expected)<max(1e-5,expected*1e-5),(name,volume,expected)
    BOX_CHECKS.append(name)
    if bevel:
        mod=o.modifiers.new('Readable edge','BEVEL');mod.width=bevel;mod.segments=2
    if collide:COLL.append(dict(name=o.name,renderer_name=o.name,type='box',trigger=False,center_design_unity=[loc[0],loc[2],loc[1]],size_unity=[size[0],size[2],size[1]],visible=True))
    return o

def cyl(name,loc,radius,depth,ma,co='StreetDetails',vertices=16):
    verts=[(radius*math.cos(math.tau*i/vertices),radius*math.sin(math.tau*i/vertices),z) for z in [-depth/2,depth/2] for i in range(vertices)]
    faces=[tuple(reversed(range(vertices))),tuple(range(vertices,vertices*2))]+[(i,(i+1)%vertices,(i+1)%vertices+vertices,i+vertices) for i in range(vertices)];o=mesh(name,verts,faces,ma,co);o.location=loc;return o

def beam(name,a,b,r,ma,co='StreetDetails'):
    v=Vector(b)-Vector(a);o=cyl(name,(Vector(a)+Vector(b))/2,r,v.length,ma,co);o.rotation_euler=v.to_track_quat('Z','Y').to_euler();return o

def mesh(name,verts,faces,ma,co='Architecture',collide=False):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);group(co).objects.link(o);o.data.materials.append(M[ma])
    if collide:COLL.append(dict(name=o.name,renderer_name=o.name,type='mesh',trigger=False,vertices_design_unity=[[v[0],v[2],v[1]] for v in verts],faces=faces,visible=True))
    return o

def slab(name,x0,x1,y0,y1,top,ma,co='Site',collide=True):
    # Same-height supported surfaces form a planar union, not overlapping ray surfaces.
    pieces=[(x0,x1,y0,y1)];old=SLAB_FOOTPRINTS.setdefault(round(top,5),[])
    for a,b,c,d in old:
        remaining=[]
        for l,r,lo,hi in pieces:
            il,ir=max(l,a),min(r,b);ib,it=max(lo,c),min(hi,d)
            if il>=ir-1e-7 or ib>=it-1e-7:remaining.append((l,r,lo,hi));continue
            for rect in [(l,il,lo,hi),(ir,r,lo,hi),(il,ir,lo,ib),(il,ir,it,hi)]:
                if rect[1]-rect[0]>1e-6 and rect[3]-rect[2]>1e-6:remaining.append(rect)
        pieces=remaining
        if not pieces:break
    objects=[]
    for i,(a,b,c,d) in enumerate(pieces):
        objects.append(box(name if len(pieces)==1 else name+f'_Part{i}',((a+b)/2,(c+d)/2,top-.1),(b-a,d-c,.2),ma,co,collide=collide));old.append((a,b,c,d))
    return objects


def text(name,label,loc,size,ma='Lettering',facing=-1,co='Signs',align='CENTER'):
    curve=bpy.data.curves.new(name,'FONT');curve.body=label;curve.size=size;curve.align_x=align;curve.align_y='CENTER';curve.extrude=.0015;curve.bevel_depth=.0004
    fontpath='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
    if Path(fontpath).exists():curve.font=bpy.data.fonts.load(fontpath,check_existing=True)
    o=bpy.data.objects.new(name,curve);group(co).objects.link(o);o.location=loc;o.rotation_euler=(math.pi/2,0,0 if facing<0 else math.pi);o.data.materials.append(M[ma]);return o

def anchor(name,loc,role='feet'):
    o=bpy.data.objects.new(name,None);group('Measured_Anchors').objects.link(o);o.location=loc;o.empty_display_type='ARROWS';o.empty_display_size=.18;ANCH.append(dict(name=name,position_design_unity=[loc[0],loc[2],loc[1]],kind=role));return o

def area(name,loc,target,power=300,size=3,color=(1,.86,.68)):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(name,d);group('Lighting').objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

def camera(name,loc,target):
    d=bpy.data.cameras.new(name);d.sensor_fit='VERTICAL';d.sensor_height=24;d.lens=24/(2*math.tan(math.radians(38)));d.clip_start=.035;d.clip_end=250;o=bpy.data.objects.new(name,d);group('Review_Cameras').objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();VIEWS.append(dict(name=name,position_design_unity=[loc[0],loc[2],loc[1]],target_design_unity=[target[0],target[2],target[1]],vertical_fov_degrees=76));return o

def roof(name,x0,x1,y0,y1,z,ma='RoofSlate',rise=1):
    over=.22;x0-=over;x1+=over;y0-=over;y1+=over;ym=(y0+y1)/2
    mesh(name,[(x0,y0,z),(x1,y0,z),(x1,ym,z+rise),(x0,ym,z+rise),(x0,y1,z),(x1,y1,z)],[(0,1,2,3),(3,2,5,4),(0,3,4),(1,5,2)],ma)
    for y in (y0,y1):beam(name+'Gutter',(x0,y,z-.02),(x1,y,z-.02),.045,'Metal')

def framed_window(name,x,y,z,w,h,facing=-1,ma='Trim',lit=False):
    # Real reveal, frame and dark interior pocket; opaque background windows promise no enterable room.
    box(name+'Recess',(x,y,z),(w+.14,.14,h+.14),'Shadow',bevel=.015)
    gy=y+facing*.085
    box(name+'Glazing',(x,gy,z),(w,.014,h),'WarmWindow' if lit else 'WindowOpaque')
    for xx in (x-w/2,x+w/2):box(name+'Jamb',(xx,gy+facing*.012,z),(.065,.055,h+.08),ma,bevel=.006)
    for zz in (z-h/2,z+h/2):box(name+'Rail',(x,gy+facing*.012,zz),(w+.08,.055,.065),ma,bevel=.006)
    box(name+'Mullion',(x,gy+facing*.025,z),(.045,.06,h),ma)
    box(name+'Sill',(x,gy+facing*.06,z-h/2-.045),(w+.22,.22,.09),'StoneTrim',bevel=.012)

def front_door(name,x,y,w=1.1,h=2.25,facing=-1,open=False,ma='ShopGreen'):
    for xx in (x-w/2-.045,x+w/2+.045):box(name+'Jamb',(xx,y,h/2+.025),(.09,.18,h+.01),ma,collide=True)
    box(name+'Lintel',(x,y,h+.09),(w+.18,.18,.12),ma,collide=True)
    if not open:
        box(name+'Leaf',(x,y,h/2+.02),(w,.08,h),'DoorPanel',bevel=.012,collide=True);box(name+'UpperGlass',(x,y+facing*.045,1.65),(w-.22,.008,.83),'WindowOpaque');beam(name+'Handle',(x+w*.32,y+facing*.10,.91),(x+w*.32,y+facing*.10,1.14),.016,'Brass')

def sign(name,label,x,y,z,w,h,facing=-1,ma='ShopGreen',size=.33):
    box(name+'Board',(x,y,z),(w,.12,h),ma,bevel=.02);text(name,label,(x,y+facing*.066,z),size,facing=facing)

def facade_mass(name,x0,x1,y0,y1,h,ma,facing=-1,floors=2,roofmat='RoofSlate',detail=True):
    box(name+'Mass',((x0+x1)/2,(y0+y1)/2,h/2),(x1-x0,y1-y0,h),ma,collide=True)
    face=y0 if facing<0 else y1;box(name+'Plinth',((x0+x1)/2,face,h*.0+.25),(x1-x0,.16,.5),'StoneTrim')
    for j in range(floors):
        zz=1.55+j*2.95
        if zz+1<h:
            n=max(2,int((x1-x0)/2.5))
            for i in range(n):framed_window(name+f'Window_{j}_{i}',x0+(i+.5)*(x1-x0)/n,face,zz,1.1,1.55,facing,lit=(j==0 and i%3==0))
    box(name+'Cornice',((x0+x1)/2,face,h-.12),(x1-x0+.18,.28,.18),'StoneTrim');roof(name+'Roof',x0,x1,y0,y1,h,roofmat,rise=.8)
    if detail:
        beam(name+'Downpipe',(x1-.2,face+facing*.16,.2),(x1-.2,face+facing*.16,h),.045,'Metal')
        front_door(name+'ClosedDoor',(x0+x1)/2,face+facing*.1,facing=facing)


def setup_mats():
    for values in [('Asphalt',(.105,.112,.12),.94,0,.025),('Paving',(.40,.405,.37),.91,0,.035),('BackPaving',(.31,.32,.30),.9,0,.02),('Curb',(.61,.59,.52),.86,0,.025),('Soil',(.18,.19,.145),.98,0,.035),('RoadPaint',(.71,.69,.57),.87,0,.015),('Plaster',(.57,.51,.405),.91,0,.04),('Cream',(.76,.71,.60),.89,0,.025),('Ochre',(.57,.34,.15),.9,0,.025),('CivicStone',(.52,.565,.55),.8,0,.03),('StoneTrim',(.52,.50,.43),.8,0,.02),('ShopGreen',(.055,.17,.145),.55,0,.02),('PrintRed',(.38,.072,.055),.6,0,.015),('PoliceBlue',(.045,.115,.18),.55,0,.01),('Trim',(.18,.175,.16),.52,0,.01),('RoofSlate',(.16,.19,.205),.87,0,.025),('RoofTile',(.29,.13,.07),.85,0,.02),('Metal',(.09,.105,.105),.38,.75,0),('Brass',(.40,.29,.12),.35,.7,0),('Lettering',(.88,.85,.73),.65,0,0),('Shadow',(.025,.03,.026),.9,0,0),('DoorPanel',(.13,.17,.16),.7,0,.01),('Wood',(.32,.19,.09),.68,0,.025),('LightWood',(.53,.38,.20),.7,0,.035),('ShopFloor',(.32,.31,.26),.9,0,.025),('Cardboard',(.42,.30,.15),.9,0,.03),('Paper',(.8,.78,.67),.92,0,.006),('Plastic',(.06,.064,.06),.49,0,.005),('Silver',(.36,.38,.38),.4,.65,0),('Leaves',(.12,.19,.08),.9,0,.02)]:mat(*values)
    mat('RedBrick',(.38,.18,.105),brick=True);mat('BrownBrick',(.25,.19,.13),brick=True);mat('PaleBrick',(.56,.49,.37),brick=True);mat('ClearGlass',(.94,.97,.97),rough=.012,trans=1);mat('WindowOpaque',(.10,.16,.18),rough=.21,metal=.4);mat('WarmWindow',(.36,.30,.18),rough=.42,emit=.12);mat('Screen',(.035,.20,.23),rough=.4,emit=.45)


def annulus(name,cx,cy,r0,r1,a0,a1,top,ma):
    n=24;vs=[]
    for i in range(n+1):
        a=math.radians(a0+(a1-a0)*i/n)
        for r in (r0,r1):vs.append((cx+r*math.cos(a),cy+r*math.sin(a),top))
    faces=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(n)];return mesh(name,vs,faces,ma,'Site',True)


def street():
    slab('SupportedNeighborhoodGround',-72,80,-40,60,-.13,'Soil')
    for label,y in [('Main',10.6),('Back',-12)]:
        slab(label+'Road',-36,44,y-3.25,y+3.25,-.10,'Asphalt')
        for side in [-1,1]:
            a,b=sorted([y+side*3.25,y+side*6.25])
            if label=='Main':
                for j,(x0,x1) in enumerate([(-36,-3.6),(-.6,28.5),(31.5,44)]):slab(label+f'Walk{side}_{j}',x0,x1,a,b,.02,'Paving')
                for crossing in [-2.1,30]:
                    inner=y+side*3.25;outer=y+side*6.25;ra,rb=sorted([inner+side*1.2,outer]);slab(label+f'RampLanding{side}',crossing-1.5,crossing+1.5,ra,rb,.02,'Paving')
            else:slab(label+f'Walk{side}',-36,44,a,b,.02,'Paving')
            # Ramp sections replace curb at each crossing; curb does not form a threshold barrier.
            breaks=[(-36,-3.65),(-.55,28.45),(31.55,44)] if label=='Main' else [(-36,44)]
            for j,(x0,x1) in enumerate(breaks):box(label+f'Curb{side}_{j}',((x0+x1)/2,y+side*3.25-.035*side,-.04),(x1-x0,.12,.12),'Curb','Site',collide=True)
        for x in range(-33,43,7):box(label+'CentreDash',(x,y,-.096),(2,.07,.007),'RoadPaint','StreetDetails')
    for side,cx in [('West',-36),('East',44)]:
        for label,cy,aa,bb in [('Front',.6,90,180) if side=='West' else ('Front',.6,0,90),('Back',-2,180,270) if side=='West' else ('Back',-2,270,360)]:
            annulus(side+label+'Road',cx,cy,6.75,13.25,aa,bb,-.10,'Asphalt');annulus(side+label+'InnerWalk',cx,cy,3.75,6.75,aa,bb,.02,'Paving');annulus(side+label+'OuterWalk',cx,cy,13.25,16.25,aa,bb,.02,'Paving');annulus(side+label+'InnerCurb',cx,cy,6.70,6.80,aa,bb,.025,'Curb');annulus(side+label+'OuterCurb',cx,cy,13.20,13.30,aa,bb,.025,'Curb')
        x=-46 if side=='West' else 54;slab(side+'LinkRoad',x-3.25,x+3.25,-2,.6,-.10,'Asphalt');slab(side+'LinkOuter',x-6.25 if side=='West' else x+3.25,x-3.25 if side=='West' else x+6.25,-2,.6,.02,'Paving');slab(side+'LinkInner',x+3.25 if side=='West' else x-6.25,x+6.25 if side=='West' else x-3.25,-2,.6,.02,'Paving')
    for x in (-2.1,30):
        for j in range(8):box('Zebra',(x,7.65+j*.79,-.091),(3,.40,.009),'RoadPaint','StreetDetails')
        for y,sgn in [(7.35,-1),(13.85,1)]:
            mesh('CrossingRamp',[(x-1.5,y,-.095),(x+1.5,y,-.095),(x+1.5,y+sgn*1.2,.025),(x-1.5,y+sgn*1.2,.025)],[(0,1,2,3) if sgn>0 else (0,3,2,1)],'Paving','Site',True)
    for x in [-34,-17,13,36]:
        for y in [6.95,14.25]:
            cyl('StreetLamp',(x,y,2.55),.046,5.1,'Metal');beam('LampArm',(x,y,5.1),(x,y+(.6 if y<10 else -.6),5.1),.035,'Metal');box('LampHead',(x,y+(.55 if y<10 else -.55),5.09),(.35,.65,.12),'Metal',bevel=.035)
    # Paving seams use small recessed-color strips, no enormous repeated square floor mesh.
    for x in range(-35,44,2):
        for y in [5.85,15.35,-7.25,-16.75]:
            if y>0 and any(abs(x-c)<1.5 for c in [-2.1,30]):continue
            box('PaverJoint',(x,y,.0205),(.009,2.85,.001),'StoneTrim','StreetDetails')
    for name,x0,x1,y0,y1 in [('EastHome',3.5,6.5,-5.75,4.35),('WestHome',-14,-11,-5.75,4.35),('DeliveryWest',1.3,4.3,16.85,29.5),('DeliveryRear',1.3,22.5,26.5,29.5),('DeliveryEast',19.5,22.5,16.85,29.5)]:slab(name+'PublicPath',x0,x1,y0,y1,.02,'BackPaving')
    slab('NorthCommercialApron',-36,44,16.85,17.15,.02,'Paving');slab('NorthBackCommonSupport',-52,60,25.5,40,.02,'BackPaving');slab('ApartmentBackApron',-14,23,-5.75,-4.2,.02,'BackPaving')


def perimeter():
    # Continuous visible architectural boundary and supported ground behind it. Ring roads remain uncapped.
    for name,loc,size in [('West',(-53.18,8,1.25),(.36,64,2.5)),('East',(61.18,8,1.25),(.36,64,2.5)),('North',(4,40.18,1.25),(114,.36,2.5)),('South',(4,-19.25,1.25),(114,.36,2.5))]:box(name+'BoundaryGardenWall',loc,size,'BrownBrick','Boundary',collide=True)
    # Distinct background buildings beyond boundary establish depth at oblique views.
    for i,(x0,x1,h,ma) in enumerate([(-53,-39,8.4,'PaleBrick'),(-39,-24,6.3,'Plaster'),(-24,-10,9.1,'RedBrick'),(-10,5,6.4,'Cream'),(5,22,8.5,'PaleBrick'),(22,38,10.1,'RedBrick'),(38,61,6.9,'Plaster')]):
        facade_mass('BackStreetProperty'+str(i),x0,x1,-25,-19.6,h,ma,1,max(2,int(h/3)),detail=False)
    for i,(x0,x1,h,ma) in enumerate([(-53,-36,7.6,'Plaster'),(-36,-20,10.1,'PaleBrick'),(-20,-5,7.2,'RedBrick'),(-5,13,8.3,'Cream'),(13,28,9.3,'PaleBrick'),(28,44,7.5,'BrownBrick'),(44,61,10,'Plaster')]):facade_mass('NorthProperty'+str(i),x0,x1,40.5,47,h,ma,-1,int(h/3),detail=False)
    for x,side in [(-58,'West'),(63.5,'East')]:
        for i,y in enumerate([-15,-1,14,29]):
            box(side+'SideProperty'+str(i),(x,y,4+i%2),(5,12,8+2*(i%2)),'PaleBrick' if i%2 else 'RedBrick',collide=True);roof(side+'Roof'+str(i),x-2.5,x+2.5,y-6,y+6,8+2*(i%2),rise=.8)


def existing_apartment(path):
    before=hashlib.sha256(path.read_bytes()).hexdigest()
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.collections=[n for n in src.collections if n not in ('Exterior','Render_Lights','Render_Cameras')]
    root=bpy.data.objects.new('ExistingApartment_CopyBasis180',None);root['review_apartment_reference']=True;group('ExistingApartment').objects.link(root)
    loaded=[]
    for c in dst.collections:
        if c:bpy.context.scene.collection.children.link(c);loaded.extend(c.all_objects)
    for o in set(loaded):
        o['review_apartment_reference']=True;o['apartment_original_name']=o.name
        if o.name=='Corridor_End':bpy.data.objects.remove(o,do_unlink=True);continue
        if o.parent is None:o.parent=root
    root.rotation_euler.z=math.pi
    bpy.context.view_layer.update()
    # Only the external copied fixture positions change; original apartment and all source files remain untouched.
    for name,p in [('Corridor_Fuse_Box',(1.18,-3.95,1.32)),('Corridor_Floor_Label',(1.175,-3.55,2.16))]:
        o=bpy.data.objects.get(name)
        if o:o.location=p;o.rotation_euler.z=math.pi/2
    bpy.context.view_layer.update()
    for o in list(bpy.data.objects):
        if o.get('review_apartment_reference') and o!=root:o.name='ApartmentReference__'+o.get('apartment_original_name',o.name)
    assert hashlib.sha256(path.read_bytes()).hexdigest()==before
    return before


def apartment_wrapper():
    # Ground floor avoids the original east window entirely. Upper floors begin above the real room ceiling.
    box('ApartmentWestAnnex',(-6.69,.10,1.49),(6.62,8.66,2.98),'BrownBrick',collide=True)
    box('ApartmentFrontRightFill',(1.1625,3.605,1.49),(4.435,1.65,2.98),'BrownBrick',collide=True)
    box('ApartmentBackServiceFill',(0,-3.59,1.49),(6.76,1.22,2.98),'BrownBrick',collide=True)
    box('ApartmentUpper',(-3.31,.10,6.15),(13.38,8.66,6.3),'Plaster',collide=True)
    for y in (-4.27,4.48):
        for z in (3.03,6.11,9.29):box('ApartmentFloorCourse',(-3.31,y,z),(13.55,.19,.14),'StoneTrim')
    for z in (4.53,7.61):
        for x in [-8.3,-5.6,-2.3,.6,2.4]:framed_window('ApartmentFront',x,4.445,z,1.12,1.68,1,'Trim')
        for x in [-8.1,-5.3,-2.2,.6,2.4]:framed_window('ApartmentBack',x,-4.255,z,1.10,1.62,-1)
    # Side upper windows rotate a façade detail set about its center, leaving ground-floor source glass visible.
    for z in (4.53,7.61):
        for y in [-2.7,.1,2.8]:
            before=set(bpy.data.objects);framed_window('ApartmentEastUpper',0,0,z,1.10,1.65,-1)
            for o in set(bpy.data.objects)-before:o.location=Vector((3.43-o.location.y,y+o.location.x,o.location.z));o.rotation_euler.z=math.pi/2
    roof('ApartmentRoof',-10,3.38,-4.23,4.43,9.35,rise=1.45)
    for x in [-7.7,1.3]:box('ApartmentChimney',(x,-.6,10.37),(.65,.75,1.30),'BrownBrick')
    box('ApartmentEntryCanopy',(-2.1,4.80,2.60),(2.42,1.03,.14),'Metal',bevel=.015)
    sign('Unit03','03',-2.1,4.53,2.88,.56,.36,1,'Trim',.24)
    # The copied corridor remains the actual 2.09 m clear opening.
    for x in [-3.26,-.94]:box('ApartmentPortalJamb',(x,4.43,1.3),(.20,.20,2.6),'StoneTrim',collide=True)
    box('ApartmentPortalHeader',(-2.1,4.43,2.76),(2.52,.20,.30),'StoneTrim',collide=True)
    sign('ApartmentService','SOUTHBANK HOUSE',-7.05,4.515,2.56,4.40,.52,1,'Trim',.24)
    framed_window('PaymentCounter',-7.05,4.46,1.53,2.4,1.3,1,lit=True);text('RentCounterLabel','BUILDING OFFICE',(-7.05,4.57,.68),.17,'Lettering',1)
    box('NoticeCase',(-4.65,4.54,1.43),(.60,.07,.82),'Metal');box('NoticePaper',(-4.65,4.58,1.43),(.49,.007,.70),'Paper');text('NoticeHeader','RESIDENTS',(-4.65,4.589,1.65),.07,'Trim',1)
    beam('ApartmentEastDrain',(3.45,3.7,.15),(3.45,3.7,9.30),.047,'Metal')
    # Modest service frontage east of passage provides enclosure without covering the real window.
    facade_mass('NeighborHome',6.8,21.5,-4.65,4.15,6.05,'PaleBrick',1,2)
    sign('NeighborTrade','KENT & SON • ALTERATIONS',14.1,4.26,2.65,6.8,.58,1,'Trim',.20)
    for x in [7.2,8.1,9.0]:box('RearDeliveryLocker',(x,-5.30,.74),(.72,.38,1.42),'ShopGreen',bevel=.018,collide=True)
    sign('RearAddress','RESIDENT DELIVERIES',8.1,-5.505,1.67,2.7,.32,-1,'Trim',.12)
    anchor('ApartmentStreetExit',(-2.1,4.45,.02));anchor('ApartmentWindowExterior',(3.55,-.15,1.61),'view');anchor('ResidentDelivery',(8.1,-6.0,.02));anchor('HomeEastReturn',(5,-5.75,.02));anchor('HomeWestReturn',(-12.5,-5.75,.02))


def glazing(name,x0,x1,y,z0,z1,ma='ShopGreen'):
    box(name+'Glass',((x0+x1)/2,y,(z0+z1)/2),(x1-x0,.012,z1-z0),'ClearGlass',collide=True)
    for x in (x0,x1):box(name+'Frame',(x,y,(z0+z1)/2),(.07,.10,z1-z0+.07),ma)
    for z in (z0,z1):box(name+'Rail',((x0+x1)/2,y,z),(x1-x0+.07,.10,.07),ma)


def shelf(name,x,y,w=1.3,depth=.40,facing=-1):
    for side in [-1,1]:box(name+'Upright',(x+side*(w/2-.035),y,.92),(.065,depth,1.8),'Metal','ShopInterior',collide=True)
    for z in [.20,.70,1.20,1.72]:box(name+'Deck',(x,y,z),(w-.135,depth,.045),'LightWood','ShopInterior')
    for j,z in enumerate([.35,.85,1.35]):
        for i in range(3):
            xx=x-w*.32+i*w*.32;box(name+'UsedStock',(xx,y-.015,z),(.28,.26,.26),'Cardboard' if (i+j)%2 else 'Plastic','ShopInterior',bevel=.016)
            if (i+j)%2==0:box(name+'Vent',(xx,y-depth/2-.01,z),(.18,.012,.08),'Silver','ShopInterior')


def tower(name,x,y,z,height=.50):
    co='ShopInterior';w=height*.47;d=height*.76
    box(name+'Case',(x,y,z+height/2),(w,d,height),'Plastic',co,bevel=.003)
    front=y-d/2-.004
    box(name+'VentMesh',(x,front,z+height*.44),(w*.72,.007,height*.48),'Shadow',co)
    for j in range(8):box(name+'VentSlit',(x,front-.005,z+height*(.25+j*.05)),(w*.65,.004,.008),'Metal',co)
    for dx in [-.034,.034]:box(name+'USB',(x+dx,front-.006,z+height*.84),(.025,.006,.012),'Silver',co)
    o=cyl(name+'Power',(x+w*.28,front-.01,z+height*.91),.008,.006,'Brass',co);o.rotation_euler.x=math.pi/2
    box(name+'SidePanel',(x+w/2+.002,y,z+height*.51),(.003,d*.88,height*.86),'Metal',co)
    for dx in [-w*.30,w*.30]:
        for dy in [-d*.30,d*.30]:box(name+'Foot',(x+dx,y+dy,z-.008),(.035,.035,.025),'Plastic',co)


def monitor(name,x,y,z,w=.48):
    co='ShopInterior';box(name+'Base',(x,y,z+.013),(w*.40,.21,.026),'Metal',co,bevel=.006);box(name+'Neck',(x,y+.04,z+.17),(.043,.042,.30),'Metal',co)
    box(name+'Housing',(x,y+.04,z+.35),(w,.049,w*.61),'Plastic',co,bevel=.006);box(name+'Screen',(x,y+.013,z+.35),(w-.026,.003,w*.61-.03),'Screen',co)
    for i in range(3):box(name+'DesktopLine',(x-w*.12,y+.010,z+.36-i*.026),(w*.48,.002,.009),'PoliceBlue',co)


def side_shelf(name,x,y,side):
    before=set(bpy.data.objects);shelf(name,0,0,1.6,.36)
    # Rotate the entire shelf, including stock, so its open face looks into the aisle.
    for o in set(bpy.data.objects)-before:
        ox,oy=o.location.x,o.location.y;o.location=Vector((x-side*oy,y+side*ox,o.location.z));o.rotation_euler.z+=side*math.pi/2
    before=set(bpy.data.objects);tower(name+'MiniDesktop',0,0,.235,.37)
    for o in set(bpy.data.objects)-before:
        ox,oy=o.location.x,o.location.y;o.location=Vector((x-side*oy,y+side*ox,o.location.z));o.rotation_euler.z+=side*math.pi/2


def repair_identity():
    co='ShopInterior'
    # Two recognizable used towers and a monitor in the storefront, at real desktop dimensions.
    box('JonRightDisplayBase',(-.32,17.88,.46),(1.55,.52,.055),'LightWood',co,bevel=.008,collide=True)
    for x in [-.95,.27]:box('JonRightDisplayLeg',(x,17.88,.24),(.055,.44,.43),'Metal',co)
    tower('UsedSFF',-.84,17.86,.505,.43);tower('UsedTower',.20,17.92,.505,.55);monitor('WindowMonitor',-.29,17.84,.505,.40)
    # Hardware inspection sample: board, two fans and a metal bracket, distinct from its package.
    box('AsterGPUBoard',(-3.65,20.06,1.067),(.28,.12,.015),'ShopGreen',co,bevel=.001)
    box('AsterGPUShroud',(-3.65,20.06,1.083),(.27,.11,.023),'Plastic',co,bevel=.003)
    for x in [-3.715,-3.585]:
        cyl('AsterFan',(x,20.06,1.100),.041,.012,'Shadow',co,24);cyl('AsterFanHub',(x,20.06,1.109),.012,.006,'Silver',co)
        for j in range(7):
            a=math.tau*j/7;beam('FanBlade',(x+.012*math.cos(a),20.06+.012*math.sin(a),1.108),(x+.035*math.cos(a+.25),20.06+.035*math.sin(a+.25),1.108),.003,'Metal',co)
    box('AsterGoldContacts',(-3.65,19.993,1.064),(.20,.018,.006),'Brass',co);box('AsterIOBracket',(-3.792,20.06,1.079),(.009,.145,.052),'Silver',co)
    # Real POS silhouette with stand, readable display and a separate card terminal.
    before=set(bpy.data.objects);monitor('JonPOS',-1.87,20.29,1.023,.32)
    for o in set(bpy.data.objects)-before:o.location.x=-3.74-o.location.x;o.location.y=40.58-o.location.y;o.rotation_euler.z+=math.pi
    reader=box('CardReader',(-1.43,20.01,1.071),(.115,.19,.075),'Plastic',co,bevel=.006);reader.rotation_euler.x=math.radians(12)
    box('CardReaderDisplay',(-1.43,20.025,1.115),(.075,.065,.003),'Screen',co)
    for ix in range(3):
        for iy in range(3):box('CardReaderKey',(-1.46+ix*.03,19.935+iy*.022,1.112),(.019,.014,.004),'Silver',co,bevel=.002)
    # Work identity: practical tool board and repair tickets, leaving the clerk aisle open.
    box('ToolBoard',(-3.52,21.563,1.97),(2.15,.036,.99),'Wood',co,bevel=.005)
    for ix in range(13):
        for iz in range(5):
            o=cyl('PegHole',(-4.45+ix*.155,21.540,1.58+iz*.18),.007,.003,'Shadow',co,8);o.rotation_euler.x=math.pi/2
    for i,x in enumerate([-4.25,-4.02,-3.79]):
        box('DriverHandle',(x,21.48,2.19),(.046,.055,.19),'PrintRed' if i==0 else 'Plastic',co,bevel=.010);beam('DriverShaft',(x,21.48,2.10),(x,21.48,1.88),.009,'Silver',co)
    beam('PliersA',(-3.53,21.47,2.23),(-3.41,21.47,1.95),.024,'Metal',co);beam('PliersB',(-3.38,21.47,2.23),(-3.52,21.47,1.95),.024,'Metal',co)
    for x in [-3.12,-2.82]:
        box('PartsBin',(x,21.445,1.62),(.24,.20,.17),'PoliceBlue',co,bevel=.005);box('PartsBinLabel',(x,21.34,1.62),(.14,.003,.055),'Paper',co)
    box('RepairTicketRail',(-3.49,21.51,2.60),(2.1,.03,.06),'Metal',co)
    for i in range(4):
        x=-4.2+i*.46;box('RepairTicket',(x,21.49,2.45),(.30,.004,.24),'Paper',co);text('RepairTicketID',f'R-{103+i}',(x,21.485,2.49),.048,'Trim')
    text('RepairQueueLabel','REPAIR QUEUE',(-3.49,21.47,2.75),.125,'Trim')


def workstation(name,x,y,screenface=-1):
    before=set(bpy.data.objects)
    box(name+'Desk',(x,y,.77),(1.12,.65,.07),'LightWood','ShopInterior',bevel=.025,collide=True)
    for xx in [x-.46,x+.46]:
        for yy in [y-.25,y+.25]:box(name+'Leg',(xx,yy,.40),(.05,.05,.73),'Metal','ShopInterior')
    box(name+'Stand',(x,y+.09,1.00),(.065,.065,.42),'Metal','ShopInterior');box(name+'Monitor',(x,y+.09,1.23),(.52,.075,.34),'Plastic','ShopInterior',bevel=.018);box(name+'Display',(x,y+.09-.04,1.23),(.47,.004,.285),'Screen','ShopInterior');box(name+'Keyboard',(x,y-.19,.82),(.44,.16,.026),'Plastic','ShopInterior',bevel=.009);box(name+'Tower',(x+.39,y+.10,1.04),(.20,.36,.45),'Plastic','ShopInterior',bevel=.014)
    if screenface>0:
        for o in set(bpy.data.objects)-before:o.location.x=2*x-o.location.x;o.location.y=2*y-o.location.y;o.rotation_euler.z+=math.pi


def parts_shop():
    x0,x1=-5.1,.9;y0,y1=17.3,25.5
    slab('JonEntryPaving',x0,x1,17.15,17.3,.02,'Paving','PartsShop');slab('JonFloor',x0,x1,y0,y1,.02,'ShopFloor','PartsShop')
    for x in [x0,x1]:box('JonSideWall',(x,(y0+y1)/2,1.57),(.22,y1-y0,3.1),'RedBrick','PartsShop',collide=True)
    box('JonBackWall',(-2.1,y1,1.57),(6,.22,3.1),'RedBrick','PartsShop',collide=True)
    box('JonFrontHeader',(-2.1,y0,2.83),(6,.22,.58),'RedBrick','PartsShop',collide=True)
    # 1.18 m open shop threshold; nearby glass displays are distinct from the doorway.
    for a,b in [(-4.99,-2.77),(-1.43,.79)]:glazing('JonDisplay',a,b,y0,.37,2.49);box('JonDisplayPlinth',((a+b)/2,y0,.20),(b-a,.20,.4),'ShopGreen','PartsShop',collide=True)
    front_door('JonEntry',-2.1,y0,1.18,2.49,open=True)
    sign('JonMainSign','PARTS & REPAIR',-2.1,17.08,2.90,5.70,.70,-1,'ShopGreen',.46);text('JonName','JON’S USED COMPUTERS',(-2.1,17.011,3.16),.11)
    box('JonAwning',(-2.1,16.86,2.62),(6.10,1.10,.065),'ShopGreen','PartsShop');box('JonAwningValance',(-2.1,16.34,2.52),(6.10,.06,.22),'ShopGreen','PartsShop');text('JonTradeLine','REPAIRS   /   REFURBISHED PCs   /   COMPONENTS',(-2.1,16.303,2.53),.115)
    # Upper residential story keeps the storefront from being a flat detached cube.
    box('JonUpper',(-2.1,21.4,4.59),(6,8.2,2.95),'RedBrick','PartsShop',collide=True)
    for x in [-3.8,-.45]:framed_window('JonUpperWindow',x,17.17,4.53,1.2,1.6,-1,'Cream')
    roof('JonRoof',x0,x1,y0,y1,6.08,'RoofSlate',1.15)
    box('JonCeiling',(-2.1,21.4,3.02),(5.80,8,.10),'Cream','PartsShop')
    # Sales 5 x 4.0 m clear. Rear partition door gives a real staff route, never a public passage.
    box('JonStockPartitionA',(-3.185,21.65,1.495),(3.61,.12,2.95),'Cream','PartsShop',collide=True);box('JonStockPartitionB',(.335,21.65,1.495),(.91,.12,2.95),'Cream','PartsShop',collide=True);box('JonStockPartitionLintel',(-.75,21.65,2.66),(1.26,.12,.62),'Cream','PartsShop',collide=True);front_door('JonStockDoor',-.75,21.65,1.08,2.2,ma='Trim');sign('JonPrivate','STAFF',-.75,21.55,2.46,.8,.22,-1,'Trim',.12)
    # Counter spans west/center; east end remains a 1.2 m bypass into staff work zone.
    box('JonCounterCabinet',(-2.95,20.25,.48),(3.55,.65,.91),'ShopGreen','ShopInterior',bevel=.004,collide=True);box('JonCounterTop',(-2.95,20.25,.98),(3.65,.77,.085),'LightWood','ShopInterior',bevel=.012)
    for x in [-4.4,-3.55,-2.70,-1.85]:box('JonCounterRecess',(x,19.918,.50),(.70,.015,.65),'DoorPanel','ShopInterior',bevel=.003)
    side_shelf('JonWestShelf',-4.71,18.90,1);side_shelf('JonEastShelf',.50,18.90,-1)
    # Browsable display workstation, tagged GPU package and repair tower visible at ordinary eye height.
    box('JonWindowTable',(-3.85,17.85,.60),(1.65,.55,.07),'Wood','ShopInterior',collide=True)
    for x in [-4.45,-3.20]:box('JonWindowTableLeg',(x,17.85,.31),(.065,.46,.60),'Metal','ShopInterior')
    box('RefurbTower',(-4.25,17.90,.94),(.25,.39,.61),'Plastic','ShopInterior',bevel=.025)
    for z in [.80,.87,.94,1.01,1.08]:box('RefurbVent',(-4.25,17.699,z),(.16,.016,.017),'Silver','ShopInterior')
    box('RefurbLaptopBase',(-3.53,17.81,.67),(.40,.28,.028),'Silver','ShopInterior',bevel=.015);box('RefurbLaptopScreen',(-3.53,17.96,.83),(.40,.03,.28),'Plastic','ShopInterior',bevel=.012);box('RefurbLaptopDisplay',(-3.53,17.942,.83),(.35,.005,.235),'Screen','ShopInterior')
    box('AsterS8Box',(-2.90,20.21,1.14),(.43,.22,.22),'Cardboard','ShopInterior',bevel=.009);text('AsterPackage','ASTER S8',(-2.90,20.094,1.15),.053,'Trim');text('AsterPrice','USED • 90 CREDITS',(-2.90,19.858,1.00),.048,'Trim')
    box('JonTill',(-1.73,20.23,1.11),(.38,.31,.17),'Plastic','ShopInterior',bevel=.025);box('JonReceiptPrinter',(-1.46,20.10,1.095),(.105,.14,.13),'Plastic','ShopInterior',bevel=.004);box('JonReceiptPaper',(-1.46,20.025,1.15),(.073,.003,.11),'Paper','ShopInterior')
    workstation('JonWorkstation',-2.40,23.10,-1);shelf('JonPickup',-4.0,24.7,1.7,.45);sign('JonPickupSign','COLLECTIONS',-4.0,24.42,2.12,1.85,.32,-1,'Trim',.14)
    for x in [-4.2,-3.6]:box('RepairOrderBox',(x,24.6,.92),(.46,.42,.48),'Cardboard','ShopInterior',bevel=.01)
    repair_identity()
    area('JonShopWarm',(-2.1,19.15,2.85),(-2.1,19.9,.1),230,2.4);area('JonCounterSoft',(-2.5,20.8,2.75),(-2.5,20.1,.8),170,1.7)
    for name,p,k in [('ShopEntrance',(-2.1,17.3,.02),'feet'),('ShopCustomerTalk',(-2.8,19.30,.02),'feet'),('ShopClerkStand',(-2.8,21.02,.02),'feet'),('ShopItemInspect',(-2.9,20.21,1.26),'target'),('ShopPackageHandoff',(-2.5,20.05,1.05),'target'),('ShopReceiptDelivery',(-1.45,20.1,1.18),'target'),('ShopStaffDoor',(-.75,21.65,.02),'private_threshold')]:anchor(name,p,k)


def copy_shop():
    x0,x1=5,16;y0,y1=17.15,26
    slab('CopyFloor',x0,x1,y0,y1,.02,'ShopFloor','CopyShop')
    for x in [x0,x1]:box('CopySide',(x,21.575,1.64),(.22,8.85,3.24),'PaleBrick','CopyShop',collide=True)
    box('CopyBackWest',(6.31,26,1.64),(2.40,.22,3.24),'PaleBrick','CopyShop',collide=True);box('CopyBackEast',(12.39,26,1.64),(7,.22,3.24),'PaleBrick','CopyShop',collide=True);box('CopyBackLintel',(8.2,26,2.83),(1.78,.22,.82),'PaleBrick','CopyShop',collide=True);box('CopyHeader',(10.5,17.15,2.83),(11,.22,.77),'Cream','CopyShop',collide=True)
    glazing('CopyWestWindow',5.12,7.56,y0,.28,2.44,'PrintRed');glazing('CopyEastWindow',8.84,15.88,y0,.28,2.44,'PrintRed');front_door('CopyEntry',8.2,y0,1.14,2.44,open=True,ma='PrintRed')
    for x in [11.2,13.5]:box('CopyFrontMullion',(x,y0,1.34),(.065,.10,2.20),'PrintRed')
    sign('CopyBusiness','SOUTHBANK COPY SHOP',10.5,16.94,2.92,10.3,.53,-1,'PrintRed',.39)
    text('CopyWindowServices','PRINT • COPY • BIND', (12.25,17.125,1.85),.20,'Trim')
    box('CopyCeiling',(10.5,21.575,3.23),(10.78,8.63,.12),'Cream','CopyShop');box('CopyParapet',(10.5,17.10,3.53),(11.2,.3,.55),'Cream','CopyShop');slab('CopyRoof',4.9,16.1,17.05,26.1,3.65,'RoofSlate','CopyShop',False)
    box('CopyRoofLanternBase',(11.8,23,3.96),(3.5,2.2,.55),'Metal','CopyShop');box('CopyRoofLanternGlass',(11.8,23,4.27),(3.25,2.0,.075),'WindowOpaque','CopyShop')
    # Customer order desk faces street; the authoritative future workstation screen faces its worker, not the window.
    box('CopyOrderCounter',(11.55,20.36,.49),(5.4,.72,.94),'PrintRed','ShopInterior',bevel=.018,collide=True);box('CopyCounterTop',(11.55,20.36,1.0),(5.55,.85,.075),'LightWood','ShopInterior',bevel=.025)
    for i in range(6):box('CopyOrderTray',(9.35+i*.48,20.42,1.075),(.40,.30,.09),'Paper','ShopInterior',bevel=.008)
    workstation('CopyAuthoritativeDeviceFuture',14.45,22.8,1)
    for x in [7,8.8]:
        box('CopierCabinet',(x,23.4,.50),(1.18,.85,.98),'Cream','ShopInterior',bevel=.06,collide=True);box('CopierTop',(x,23.4,1.06),(1.22,.91,.16),'Plastic','ShopInterior',bevel=.025);box('CopierScanner',(x-.16,23.39,1.18),(.82,.66,.08),'Silver','ShopInterior');box('CopierPanel',(x+.46,23.09,1.16),(.20,.24,.09),'Screen','ShopInterior')
    shelf('CopyPaperStock',11.4,25.45,3.3,.55)
    for y in [18.55,19.35]:box('CopyWaitingBench',(6.0,y,.46),(.55,.65,.12),'Wood','ShopInterior',bevel=.03)
    sign('CopyOrders','ORDER COLLECTION',11.55,20.0,.62,3.1,.31,-1,'PrintRed',.16)
    box('CopyStaffBoundaryWest',(6.3975,20.36,.51),(2.575,.24,.98),'PrintRed','ShopInterior',collide=True);box('CopyStaffBoundaryEast',(15.07,20.36,.51),(1.64,.24,.98),'PrintRed','ShopInterior',collide=True);box('CopyStaffGateJamb',(8.7425,20.36,.51),(.215,.16,.98),'PrintRed','ShopInterior',collide=True);box('CopyStaffGate',(8.16,20.36,.51),(.95,.10,.96),'LightWood','ShopInterior',collide=True);text('CopyStaffLabel','STAFF',(8.16,20.295,.62),.10,'Trim')
    area('CopyFrontLight',(10.5,19.3,3.0),(10.5,20.3,.1),370,3);area('CopyWorkLight',(11,23.9,3.0),(11,23.9,.5),250,3)
    front_door('CopyRearDelivery',8.2,26.14,1.2,2.3,1,ma='PrintRed');sign('CopyDelivery','DELIVERIES',8.2,26.22,2.65,2.1,.36,1,'PrintRed',.18)
    anchor('CopyShopEntrance',(8.2,17.15,.02));anchor('CopyCustomerWaiting',(10.25,19.22,.02));anchor('CopyClerkWork',(13.9,23.65,.02));anchor('CopyDeviceFuture',(14.45,22.8,1.23),'device_target');anchor('CopyDeliveryBay',(8.2,27.4,.02))


def police():
    # Exterior and public/staff distinction only. No fabricated working custody rooms.
    slab('StationApron',24,43,16.85,18.55,.02,'Paving','Police')
    box('StationEastOffice',(36.85,25.35,2.11),(8.30,14,4.18),'CivicStone','Police',collide=True)
    box('StationWestRear',(28.85,28.05,2.11),(7.70,8.60,4.18),'CivicStone','Police',collide=True)
    box('StationWestWing',(25.2,21.475,2.11),(.4,4.55,4.18),'CivicStone','Police',collide=True)
    box('StationPublicCeiling',(28.85,21.15,3.40),(7.7,5.6,.14),'Cream','Police')
    slab('StationLobbyFloor',25,32.7,18.35,23.75,.02,'Paving','Police')
    glazing('StationFrontWest',25.4,28.74,18.35,.18,2.90,'PoliceBlue');glazing('StationFrontEast',30.26,32.60,18.35,.18,2.90,'PoliceBlue');front_door('StationPublicEntry',29.5,18.35,1.4,2.9,open=True,ma='PoliceBlue')
    box('StationCanopy',(29.0,17.85,3.08),(8.45,1.90,.20),'PoliceBlue','Police',bevel=.025)
    text('StationName','SOUTHBANK POLICE',(29,16.86,3.07),.29)
    box('StationCanopyTower',(25.50,18.65,3.1),(1.0,1.1,6.2),'CivicStone','Police',collide=True);sign('StationPoliceID','POLICE',25.5,18.03,4.25,.89,.75,-1,'PoliceBlue',.17)
    cyl('StationClockFace',(25.5,18.039,5.21),.35,.055,'Lettering','Police',32).rotation_euler.x=math.pi/2
    beam('ClockMinute',(25.5,17.998,5.21),(25.5,17.998,5.46),.009,'Trim','Police');beam('ClockHour',(25.5,17.995,5.21),(25.32,17.995,5.12),.012,'Trim','Police')
    for x in [34.2,36.6,39.0]:framed_window('StationOffice',x,18.20,2.02,1.5,1.55,-1,'PoliceBlue')
    for x in [32.7,40.8]:box('StationStonePier',(x,18.2,2.1),(.35,.20,4.2),'StoneTrim','Police')
    slab('StationFlatRoof',24.8,41.2,18.15,32.55,4.25,'RoofSlate','Police',False)
    box('StationReception',(28.0,22.35,.57),(3.8,.72,1.1),'LightWood','Police',bevel=.025,collide=True);box('StationReceptionTop',(28,22.35,1.16),(3.92,.82,.065),'PoliceBlue','Police',bevel=.02);front_door('StationStaffControlled',31.55,23.70,1.05,2.25,ma='PoliceBlue');sign('StationStaffSign','STAFF ONLY',31.55,23.59,2.56,1.7,.3,-1,'PoliceBlue',.14)
    for x in [26,26.7]:box('StationWaitingSeat',(x,20.6,.47),(.52,.52,.13),'PoliceBlue','Police',bevel=.03)
    sign('StationReceptionLabel','RECEPTION',28.0,21.969,.75,2.6,.30,-1,'PoliceBlue',.19);area('StationPublicLight',(29,21.0,3.25),(28,22.0,.4),300,3)
    # Actual visible opaque secure yard perimeter; public common lane lies entirely west of X22.5.
    for name,loc,size in [('West',(24.9,35.2,1.25),(.2,5.8,2.5)),('East',(43.1,35.2,1.25),(.2,5.8,2.5)),('Rear',(34,38.1,1.25),(18.4,.2,2.5))]:box('SecureYard'+name,loc,size,'CivicStone','Police',collide=True)
    slab('SecureYardGround',25,43,32.3,38,.02,'BackPaving','Police');box('StationStaffGate',(41.9,32.45,1.2),(2.3,.13,2.4),'PoliceBlue','Police',collide=True)
    anchor('PolicePublicEntrance',(29.5,18.35,.02));anchor('PoliceReception',(28,21.23,.02));anchor('PoliceStaffControlled',(31.55,23.70,.02),'private_threshold');anchor('PoliceRearStaffGate',(41.9,32.45,.02),'private_threshold');anchor('InterviewReserve',(28.2,27.05,.02),'unmodeled_room');anchor('CustodySouthDoorReserve',(36.6,26.35,.02),'unmodeled_private_threshold')


def cafe_and_civic():
    facade_mass('CafeBuilding',-26,-15,-4.65,4.35,6.1,'Ochre',1,2,'RoofTile')
    # Replace front-most lower windows with deep framed business glazing details and a specific awning.
    sign('CafeName','CORNER TABLE',-20.5,4.49,2.68,9.3,.56,1,'ShopGreen',.39)
    box('CafeAwning',(-20.5,5.04,2.55),(10.8,1.5,.07),'Cream');
    for i in range(14):box('CafeAwningStripe',(-25.55+i*.78,5.04,2.59),(.38,1.5,.006),'ShopGreen')
    box('CafeValance',(-20.5,5.76,2.45),(10.8,.05,.22),'ShopGreen');text('CafeTag','COFFEE  •  BREAKFAST  •  LATE LUNCH',(-20.5,5.79,2.45),.14,facing=1)
    for x in [-24.4,-16.7]:
        cyl('CafeTable',(x,4.98,.73),.36,.05,'Wood');cyl('CafeTableLeg',(x,4.98,.38),.05,.70,'Metal')
        for xx in [x-.50,x+.50]:box('CafeSeat',(xx,4.98,.47),(.30,.34,.06),'Wood',collide=True)
    anchor('CafeMeeting',(-22.5,5.0,.02));anchor('BusStop',(-31.1,4.05,.02))
    # Local setback provides 4.5 m pedestrian depth at the bus shelter.
    facade_mass('WestCornerHomes',-35.5,-27,-5,2.85,8.95,'RedBrick',1,3,'RoofSlate')
    slab('BusSetbackPaving',-35.5,-27,2.85,4.35,.02,'Paving')
    for x in [-33,-29]:box('BusShelterPost',(x,3.45,1.20),(.07,.07,2.36),'Metal')
    box('BusShelterRoof',(-31,3.40,2.41),(4.4,1.35,.13),'Metal',bevel=.025);glazing('BusShelterBack',-33,-29,2.86,.4,2.28,'Metal');box('BusBench',(-31,3.08,.46),(3.2,.44,.08),'Wood',collide=True);sign('BusRoute','SOUTHBANK / 04',-31,3.48,2.12,3.1,.32,1,'PoliceBlue',.16)
    cyl('BusStopPole',(-28.8,4.00,1.3),.045,2.6,'Metal');sign('BusStopID','BUS',-28.8,4.01,2.44,.62,.52,1,'PoliceBlue',.20)
    # Separate civic frontage is visibly closed/reserved. It has no fake interaction prompt.
    facade_mass('CivicHall',23,40,-4.65,4.35,6.2,'CivicStone',1,2,'RoofSlate')
    for x in [26.0,29.4,32.8,36.2]:box('CivicPilaster',(x,4.5,2.6),(.45,.32,5.2),'StoneTrim',collide=True)
    sign('CivicIdentity','SOUTHBANK CIVIC HALL',31.5,4.71,4.7,12.0,.75,1,'CivicStone',.43)
    sign('CivicClosedNotice','PUBLIC ENTRANCE • APPOINTMENTS',31.5,4.66,1.30,3.2,.31,1,'Trim',.12)


def supplementary_frontage():
    # Complete focal-frontage neighbors; backgrounds use varied heights/roof lines rather than isolated cubes.
    facade_mass('NorthWestBaker',-35,-27.4,17.35,26.8,6.3,'PaleBrick',-1,2,'RoofTile');sign('BakerShop','MILLER’S BAKERY',-31.2,17.19,2.61,6.9,.6,-1,'PrintRed',.24)
    facade_mass('NorthWestHousing',-27,-16.3,17.20,27.5,9.3,'Plaster',-1,3)
    facade_mass('NorthHardwareNeighbor',-16,-5.35,17.30,26.0,6.15,'PaleBrick',-1,2);sign('NeighborClosedShop','SOUTHBANK SUPPLIES',-10.7,17.14,2.56,9.0,.57,-1,'Trim',.28)
    # Closed narrow terrace between Copy Shop and public eastern return, not a shortcut into premises.
    facade_mass('PrintNeighbor',16.25,19.20,17.20,26.0,6.2,'RedBrick',-1,2,detail=False)
    # A low planted plot provides civic setback and sightline without leaving a void at the bend.
    slab('CivicEastPlanting',42,47,17.15,30,.02,'Soil')
    for y in [18.4,23.1,27.8]:
        cyl('CivicTreeTrunk',(44.5,y,1.25),.13,2.5,'Wood')
        for dx,dy,dz,r in [(-.4,0,3.3,1.05),(.45,.25,3.7,1.05),(0,-.40,4.05,.8)]:
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=r,location=(44.5+dx,y+dy,dz));put(bpy.context.object,'CivicTreeCrown','Leaves','StreetDetails')
    # Close the private back-edge strips of residential plots with garden boundaries that leave both public passages open.
    for x0,x1 in [(-35.5,-15),(-10,3.38),(6.8,40)]:box('ResidentialBackBoundary',((x0+x1)/2,-4.95,.62),(x1-x0,.18,1.25),'BrownBrick',collide=True)
    # Common delivery lane is legible from the street, with a real return after the Copy Shop.
    before=set(bpy.data.objects);sign('DeliveryWaySign','COMMON YARD',0,0,2.30,2.20,.36,-1,'Trim',.17)
    for o in set(bpy.data.objects)-before:o.location=Vector((4.86+o.location.y,25-o.location.x,o.location.z));o.rotation_euler.z-=math.pi/2
    for x0,x1 in [(-36,1.3),(22.5,24.8)]:box('CommercialRearPlotWall',((x0+x1)/2,29.7,1.15),(x1-x0,.20,2.3),'BrownBrick',collide=True)
    box('NorthDeliveryBoundary',(11.9,29.67,1.15),(21.2,.20,2.3),'BrownBrick',collide=True)
    anchor('DeliveryCommonWest',(2.8,27.7,.02));anchor('DeliveryCommonEast',(21,27.7,.02));anchor('DeliveryReturnStreet',(21,16.85,.02));anchor('StreetCrossingHome',(-2.1,10.6,-.10));anchor('StreetCrossingCivic',(30,10.6,-.10))


def make_cameras():
    eye=1.63405
    camera('ApartmentExit',(-2.1,4.95,eye),(-2.1,18.8,1.75))
    camera('ShopAcrossStreet',(-5.0,6.45,eye),(-.7,18.0,1.95))
    camera('ShopOblique',(-7.5,15.40,eye),(-1.25,19.25,1.55))
    camera('ShopInterior',(-1.95,17.95,eye),(-2.8,20.4,1.10))
    camera('CopyInterior',(8.25,18.15,eye),(12.1,21.55,1.25))
    camera('PoliceApproach',(30,11.0,1.51405),(29.5,20.5,2.1))
    camera('WindowPassage',(5.0,3.40,eye),(3.5,-2.1,1.45))
    camera('WestBend',(-36.5,5.1,eye),(-41.2,-2.4,1.60))
    camera('BackReturn',(-9.5,-6.60,eye),(-11.6,-.3,1.85))
    camera('DeliveryReturn',(3.0,27.5,eye),(18.5,27.5,1.55))
    camera('ApartmentWindow',(2.90,-.15,1.61),(6,-.15,1.55))
    camera('StreetReverse',(12.0,15.30,eye),(-4.5,4.4,2.2))


def environment(a):
    s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=a.samples;s.cycles.use_denoising=True;s.cycles.max_bounces=7;s.cycles.transparent_max_bounces=8;s.render.resolution_x=a.width;s.render.resolution_y=a.height;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.threads_mode='FIXED';s.render.threads=4;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=.35
    s.world=bpy.data.worlds.new('SouthbankOvercast');s.world.use_nodes=True;n=s.world.node_tree.nodes;l=s.world.node_tree.links;bg=n.get('Background');bg.inputs['Color'].default_value=(.55,.68,.85,1);bg.inputs['Strength'].default_value=.80
    d=bpy.data.lights.new('SoftAfternoonSun','SUN');d.energy=2.0;d.angle=math.radians(7);o=bpy.data.objects.new('SoftAfternoonSun',d);group('Lighting').objects.link(o);o.rotation_euler=(math.radians(28),math.radians(-22),math.radians(-28))
    s.unit_settings.system='METRIC';s.unit_settings.length_unit='METERS';s.unit_settings.scale_length=1


def main():
    a=args();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True);(out/'renders').mkdir(exist_ok=True)
    if not a.render_only:
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);setup_mats();street();perimeter();print('SITE_AND_BOUNDARIES_READY',flush=True);source=Path(a.apartment_source).resolve();sourcehash=existing_apartment(source);apartment_wrapper();parts_shop();print('APARTMENT_AND_SHOP_READY',flush=True);copy_shop();police();cafe_and_civic();supplementary_frontage();print('FIRST_STAGE_MASSING_READY',flush=True)
        for name,p in [('Ref_Origin',(0,0,0)),('Ref_East',(2,0,0)),('Ref_North',(0,3,0)),('Ref_Up',(0,0,4))]:anchor(name,p,'basis')
        make_cameras();environment(a);bpy.context.view_layer.update()
        for level,rects in SLAB_FOOTPRINTS.items():
            for i,(x0,x1,y0,y1) in enumerate(rects):
                for a0,a1,b0,b1 in rects[i+1:]:
                    overlap=max(0,min(x1,a1)-max(x0,a0))*max(0,min(y1,b1)-max(y0,b0));assert overlap<1e-8,('coplanar slabs',level,overlap)
        # Pack copied apartment textures so this external editable file is self-contained.
        bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(out/'Southbank_Neighborhood_Prototype.blend'))
        manifest={'status':'External neighborhood prototype, NOT final art or Unity runtime accepted','design_coordinate_system':'Blender (x,y,z) -> desired Unity (x,z,y); raw FBX must be calibrated with four basis references','immutable_apartment_source':str(source),'immutable_apartment_sha256':sourcehash,'apartment_copy_changes':'180deg Z basis rotation; copied Corridor_End removed; copied fuse and 03 label moved; source never edited','basis_reference_names':['Ref_Origin','Ref_East','Ref_North','Ref_Up'],'anchors':ANCH,'colliders':COLL,'cameras':VIEWS,'generated_closed_boxes_verified_positive_signed_volume':len(BOX_CHECKS),'horizontal_slabs_same_height_overlap_area':0,'same_height_slab_fragments':sum(len(v) for v in SLAB_FOOTPRINTS.values()),'geometry_checks_pending':['Actual controller traversal and collision cooking','Imported FBX basis solve','Performance and final art review'],'floor_levels':{'sidewalk':.02,'road':-.10,'apartment_source_floor_preserved':True},'ground_support_bounds_xz':[-72,80,-40,60],'public_commercial_loop':[[2.8,16.85],[2.8,28],[21,28],[21,16.85]],'minimum_public_passage_m':3,'minimum_sidewalk_after_props_m':2,'shop_status':'Furnished navigation/visual study; no clerk, purchase, NPC or device gameplay implemented'}
        (out/'neighborhood_collision_anchors.json').write_text(json.dumps(manifest,indent=2)+'\n')
        if a.export:
            sys.path.insert(0,str(ROOT));from export_neighborhood import export_scene;export_scene(out)
    else:
        bpy.ops.wm.open_mainfile(filepath=str(out/'Southbank_Neighborhood_Prototype.blend'));s=bpy.context.scene;s.cycles.samples=a.samples;s.render.resolution_x=a.width;s.render.resolution_y=a.height;s.render.threads_mode='FIXED';s.render.threads=4
    if not a.no_render:
        for name in a.views.split(','):
            s=bpy.context.scene;s.camera=bpy.data.objects[name];s.render.filepath=str(out/'renders'/f'{name}.png');bpy.ops.render.render(write_still=True)
    print('NEIGHBORHOOD_PROTOTYPE_COMPLETE',out)

if __name__=='__main__':main()
