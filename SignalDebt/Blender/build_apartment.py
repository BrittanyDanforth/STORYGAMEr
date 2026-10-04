"""Signal Debt apartment. Run in Blender: blender -b --python build_apartment.py.

Reproducible geometry, original baked color textures, a fully editable .blend,
Unity-oriented FBX, GLB, OBJ fallback, collision/anchor manifest, five CPU renders.
No downloaded assets or image-generated stand-ins. Units are metres, Blender Z up.
"""
from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import random
import sys
from array import array

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
BLEND_DIR = ROOT / "Blender"
ART_DIR = ROOT / "UnityProject" / "Assets" / "Art" / "Apartment"
TEX_DIR = ART_DIR / "Textures"
PREVIEW_DIR = BLEND_DIR / "Previews"
SEED = 190426
random.seed(SEED)
M = {}
COL = {}
COLLIDERS = []


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--samples", type=int, default=256)
    p.add_argument("--adaptive-threshold", type=float, default=.015)
    p.add_argument("--no-render", action="store_true")
    p.add_argument("--render-only", action="store_true")
    p.add_argument("--view", default=None, help="Optional named Preview camera to render alone")
    p.add_argument("--width", type=int, default=1280)
    p.add_argument("--height", type=int, default=720)
    return p.parse_args(argv)


def collection(name):
    if name not in COL:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
        COL[name] = c
    return COL[name]


def move_to(obj, group):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection(group).objects.link(obj)
    return obj


def mat(name, color, rough=0.6, metal=0.0, emission=0.0, alpha=1.0, texture=None):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, alpha)
    material.use_nodes = True
    p = material.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*color, alpha)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    p.inputs["Alpha"].default_value = alpha
    if "Emission Color" in p.inputs:
        p.inputs["Emission Color"].default_value = (*color, 1)
        p.inputs["Emission Strength"].default_value = emission
    else:
        p.inputs["Emission"].default_value = (*color, 1)
    if alpha < 1:
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        elif hasattr(material, "blend_method"):
            material.blend_method = "BLEND"
    if texture:
        t = material.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(str(texture), check_existing=True)
        t.label = "Original exported base color texture"
        material.node_tree.links.new(t.outputs["Color"], p.inputs["Base Color"])
    M[name] = material
    return material


def create_texture(name, kind, base, size=512):
    """Bake restrained material color variation to actual portable PNG files."""
    rng = random.Random(SEED + sum(ord(c) for c in name))
    rgba = array('f')
    for iy in range(size):
        yy = iy / size
        for ix in range(size):
            xx = ix / size
            noise = rng.uniform(-.014,.014)
            if kind == "wood":
                warp = xx + .010*math.sin(yy*9) + .004*math.sin(yy*37+xx*8)
                grain = math.sin(warp*220+1.4*math.sin(yy*3))
                pores = math.sin(warp*710+.3*math.sin(yy*13))
                broad = .055*math.sin(warp*31)+.022*grain+.011*pores
                knots = math.exp(-(((xx-.3)/.09)**2+((yy-.64)/.22)**2))
                value = broad+noise-knots*(.025+.018*math.sin(warp*280))
                if name == "Walnut_Worktop_BaseColor":
                    value *= .38
            elif kind == "fabric":
                weave = .009*(1 if ix%2 else -1)+.009*(1 if iy%2 else -1)
                value = weave+noise*1.5+.012*math.sin(xx*13)*math.cos(yy*17)
            else:
                value = noise*.6+.008*math.sin(xx*30)*math.cos(yy*29)
            rgba.extend((*[max(0.,min(1.,c+value)) for c in base],1.0))
    im = bpy.data.images.new(name, width=size, height=size, alpha=False)
    im.pixels.foreach_set(rgba)
    im.filepath_raw = str(TEX_DIR / f"{name}.png")
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    return TEX_DIR / f"{name}.png"


def box_uv(obj):
    """Deterministic box UVs, all meshes keep usable nonzero-area mapping."""
    mesh = obj.data
    layer = mesh.uv_layers.get("UVMap") or mesh.uv_layers.new(name="UVMap")
    dims = [max(obj.dimensions[i], 0.001) for i in range(3)]
    for face in mesh.polygons:
        axis = max(range(3), key=lambda a: abs(face.normal[a]))
        axes = (0, 1) if axis == 2 else ((0, 2) if axis == 1 else (1, 2))
        for li in face.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co
            layer.data[li].uv = (v[axes[0]] / dims[axes[0]] + .5,
                                 v[axes[1]] / dims[axes[1]] + .5)


def finish(obj, name, material, group, bevel=0.0, smooth=False):
    obj.name = name
    move_to(obj, group)
    if material:
        obj.data.materials.append(M[material] if isinstance(material, str) else material)
    if obj.type == "MESH":
        if hasattr(obj.data, "use_auto_smooth"):
            obj.data.use_auto_smooth = True
            obj.data.auto_smooth_angle = math.radians(60)
        for face in obj.data.polygons:
            face.use_smooth = smooth
        if bevel > 0:
            m = obj.modifiers.new("Manufactured edge bevel", "BEVEL")
            m.width = bevel
            m.segments = 3 if bevel > .025 else 2
            m.limit_method = "ANGLE"
            m.harden_normals = True
            n = obj.modifiers.new("Face weighted normals", "WEIGHTED_NORMAL")
            n.keep_sharp = True
    return obj


def box(name, loc, dims, material, group="Architecture", bevel=.008, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    box_uv(o)
    if rot:
        o.rotation_euler = rot
    return finish(o, name, material, group, bevel)


def cylinder(name, a, b, radius, material, group="Details", vertices=16, bevel=.002):
    a, b = Vector(a), Vector(b)
    delta = b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
                                       depth=delta.length, location=(a+b)/2)
    o = bpy.context.object
    o.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(o, name, material, group, bevel, smooth=True)


def sphere(name, loc, scale, material, group="Details", segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=loc)
    o = bpy.context.object
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, name, material, group, smooth=True)


def curve(name, points, radius, material, group="Details"):
    data = bpy.data.curves.new(name, "CURVE")
    data.dimensions = "3D"
    data.resolution_u = 8
    data.bevel_depth = radius
    data.bevel_resolution = 3
    sp = data.splines.new("BEZIER")
    sp.bezier_points.add(len(points)-1)
    for p, co in zip(sp.bezier_points, points):
        p.co = co
        p.handle_left_type = "AUTO"
        p.handle_right_type = "AUTO"
    o = bpy.data.objects.new(name, data)
    collection(group).objects.link(o)
    o.data.materials.append(M[material])
    return o


def text(name, body, loc, size, material, rotation=(math.pi/2, 0, 0), group="Lettering"):
    data = bpy.data.curves.new(name, "FONT")
    data.body = body
    data.size = size
    data.extrude = .0003
    data.resolution_u = 2
    data.space_line = 1.10
    o = bpy.data.objects.new(name, data)
    collection(group).objects.link(o)
    o.location = loc
    o.rotation_euler = rotation
    o.data.materials.append(M[material])
    return o


def empty(name, location, display=.14):
    o = bpy.data.objects.new(name, None)
    collection("Gameplay_Anchors").objects.link(o)
    o.location = location
    o.empty_display_type = "ARROWS"
    o.empty_display_size = display
    o["coordinate_system"] = "Blender metres, Z up; Unity imported FBX Y up"
    return o


def parent_local(obj, parent):
    """Input transform is already expressed in parent coordinates."""
    obj.parent = parent
    return obj


def collider(name, center, size, kind="Box", dynamic=False):
    COLLIDERS.append({"name":name, "kind":kind, "center_blender":list(center),
                      "size_blender":list(size), "dynamic":dynamic,
                      "center_unity":[center[0],center[2],center[1]],
                      "size_unity":[size[0],size[2],size[1]]})


def materials():
    wood = create_texture("Oak_Floor_BaseColor", "wood", (.30,.22,.145))
    desk = create_texture("Walnut_Desk_BaseColor", "wood", (.33,.215,.125))
    worktop = create_texture("Walnut_Worktop_BaseColor", "wood", (.33,.215,.125))
    fabric = create_texture("Cotton_Blanket_BaseColor", "fabric", (.185,.255,.265))
    plaster = create_texture("Plaster_BaseColor", "plaster", (.42,.455,.43))
    mat("Oak Floor", (.30,.22,.145), .58, texture=wood)
    mat("Walnut", (.33,.215,.125), .44, texture=desk)
    mat("Desk Walnut", (.33,.215,.125), .53, texture=worktop)
    mat("Key Legend", (.53,.56,.54), .86)
    mat("Mouse Shell", (.055,.064,.071), .43)
    mat("Mouse Buttons", (.068,.078,.087), .48)
    mat("Glide Pad", (.16,.18,.185), .27)
    mat("Chair Stitch", (.11,.135,.14), .97)
    mat("Cotton Teal", (.185,.255,.265), .95, texture=fabric)
    mat("Plaster Warm", (.42,.455,.43), .88, texture=plaster)
    mat("Teal Wall", (.105,.205,.215), .9)
    mat("Panel Teal", (.12,.225,.23), .7)
    mat("Ceiling", (.64,.655,.61), .95)
    mat("Trim", (.40,.44,.41), .63)
    mat("Paint Dark", (.046,.061,.062), .5)
    mat("Steel", (.075,.095,.105), .32, .72)
    mat("Aluminium", (.25,.28,.30), .36, .68)
    mat("Plastic Black", (.024,.029,.033), .58)
    mat("Rubber", (.013,.018,.021), .9)
    mat("Fabric Charcoal", (.06,.08,.085), .94)
    mat("Cotton Ivory", (.63,.61,.53), .96)
    mat("Blanket Edge", (.07,.115,.12), .91)
    mat("Brass", (.41,.285,.11), .29, .65)
    mat("Paper", (.70,.68,.57), .92)
    mat("Ink", (.035,.055,.06), .9)
    mat("Notice Red", (.49,.09,.055), .76)
    mat("Screen", (.015,.055,.065), .42, emission=1.4)
    mat("Screen Text", (.32,.75,.70), .5, emission=.9)
    mat("Screen White", (.68,.76,.70), .7, emission=.6)
    mat("Status Amber", (.90,.40,.10), .6, emission=1.2)
    mat("Bulb Warm", (.95,.68,.36), .4, emission=4)
    mat("Window Glass", (.12,.21,.28), .12, metal=.18, alpha=.24)
    mat("Facade", (.10,.14,.18), .88)
    mat("Facade Trim", (.075,.10,.13), .8)
    mat("City Window Dark", (.015,.035,.055), .25)
    mat("City Window Warm", (.58,.37,.17), .45, emission=.45)
    mat("City Window Cool", (.15,.28,.34), .5, emission=.25)
    mat("Circuit Board", (.025,.10,.075), .73)
    mat("Cardboard", (.33,.265,.17), .98)


def architecture():
    box("Foundation", (0,0,-.105), (6.65,5.45,.20), "Paint Dark", bevel=.005)
    # Individual staggered planks, with real seams and bevels; no oversized floor cube.
    columns = 36
    pw = 6.4/columns
    for ix in range(columns):
        x = -3.2 + (ix+.5)*pw
        y = -2.6
        first = .40 if ix % 3 == 0 else (.78 if ix % 3 == 1 else 1.14)
        j = 0
        while y < 2.6-.01:
            ln = min(first if j==0 else 1.14, 2.6-y)
            board = box(f"Floor_Oak_{ix:02d}_{j:02d}", (x,y+ln/2,.006),
                        (pw-.003,ln-.003,.025), "Oak Floor", "Floorboards", .0015)
            # Each narrow plank samples a different strip of the same wood map;
            # avoid stretching an entire dense grain image over every small board.
            grain_rng = random.Random(SEED + ix * 31 + j * 173)
            u_offset, v_offset = grain_rng.random()*.80, grain_rng.random()*.45
            for loop in board.data.uv_layers.active.data:
                loop.uv = (loop.uv.x*.18+u_offset, loop.uv.y*.55+v_offset)
            y += ln
            j += 1
    collider("Floor", (0,0,-.055), (6.4,5.2,.10))
    box("North_Wall", (0,2.69,1.4), (6.58,.18,2.8), "Teal Wall")
    box("East_Wall", (3.29,0,1.4), (.18,5.2,2.8), "Plaster Warm")
    # Real opening in south wall, 1.0 m wide and 2.2 m high.
    box("South_Wall_Left", (-.8,-2.69,1.4), (4.8,.18,2.8), "Plaster Warm")
    box("South_Wall_Right", (2.9,-2.69,1.4), (.6,.18,2.8), "Plaster Warm")
    box("South_Door_Lintel", (2.1,-2.69,2.50), (1.0,.18,.60), "Plaster Warm")
    collider("North_Wall", (0,2.69,1.4), (6.58,.18,2.8))
    collider("East_Wall", (3.29,0,1.4), (.18,5.2,2.8))
    collider("South_Wall_Left", (-.8,-2.69,1.4), (4.8,.18,2.8))
    collider("South_Wall_Right", (2.9,-2.69,1.4), (.6,.18,2.8))
    collider("South_Door_Lintel", (2.1,-2.69,2.50), (1.0,.18,.60))
    # Window opening is y -0.75 .. 1.05, z 0.90 .. 2.28.
    box("West_Below_Window", (-3.29,0,.45), (.18,5.2,.90), "Plaster Warm")
    box("West_Above_Window", (-3.29,0,2.54), (.18,5.2,.52), "Plaster Warm")
    box("West_Window_South", (-3.29,-1.675,1.59), (.18,1.85,1.38), "Plaster Warm")
    box("West_Window_North", (-3.29,1.825,1.59), (.18,1.55,1.38), "Plaster Warm")
    collider("West_Wall", (-3.29,0,1.4), (.18,5.2,2.8))
    box("Ceiling", (0,0,2.85), (6.58,5.38,.10), "Ceiling", bevel=.001)
    collider("Ceiling", (0,0,2.85), (6.58,5.38,.10))
    for z, h in ((.09,.16),(2.70,.14)):
        box(f"North_Trim_{z}", (0,2.572,z), (6.4,.045,h), "Trim", bevel=.005)
        box(f"West_Trim_{z}", (-3.172,0,z), (.045,5.2,h), "Trim", bevel=.005)
        box(f"East_Trim_{z}", (3.172,0,z), (.045,5.2,h), "Trim", bevel=.005)
        if z > 2:
            box("South_Crown", (0,-2.572,z), (6.4,.045,h), "Trim", bevel=.005)
        else:
            box("South_Base_Left", (-.8,-2.572,z), (4.8,.045,h), "Trim", bevel=.005)
            box("South_Base_Right", (2.9,-2.572,z), (.6,.045,h), "Trim", bevel=.005)
    # Quiet architectural panel rhythm, not decorative clutter.
    box("North_Chair_Rail", (0,2.563,.965), (6.4,.035,.055), "Panel Teal", bevel=.007)
    for x in (-3.12,-2.1,-1.05,0,1.05,2.1,3.12):
        box(f"North_Panel_Stile_{x}", (x,2.568,.515), (.035,.026,.79), "Panel Teal", bevel=.004)
    # Door jambs and visible reveal.
    for x in (1.57,2.63):
        box(f"Door_Casing_{x}", (x,-2.575,1.105), (.075,.072,2.21), "Trim", bevel=.007)
        box(f"Door_Reveal_{x}", (x,-2.70,1.105), (.045,.20,2.21), "Walnut", bevel=.003)
    box("Door_Header_Casing", (2.1,-2.575,2.225), (1.14,.072,.075), "Trim", bevel=.007)
    box("Door_Threshold", (2.1,-2.64,.035), (1.02,.26,.035), "Walnut", bevel=.006)
    # Actual short corridor outside the door.
    box("Corridor_Floor", (2.1,-3.50,-.02), (2.1,1.7,.08), "Oak Floor", "Corridor")
    box("Corridor_West", (.98,-3.50,1.4), (.15,1.85,2.8), "Plaster Warm", "Corridor")
    box("Corridor_East", (3.22,-3.50,1.4), (.15,1.85,2.8), "Plaster Warm", "Corridor")
    box("Corridor_End", (2.1,-4.35,1.4), (2.4,.15,2.8), "Plaster Warm", "Corridor")
    box("Corridor_Ceiling", (2.1,-3.50,2.83), (2.4,1.85,.08), "Ceiling", "Corridor")
    collider("Corridor_Floor", (2.1,-3.50,-.02), (2.1,1.7,.08))
    collider("Corridor_West", (.98,-3.50,1.4), (.15,1.85,2.8))
    collider("Corridor_East", (3.22,-3.50,1.4), (.15,1.85,2.8))
    collider("Corridor_End", (2.1,-4.35,1.4), (2.4,.15,2.8))
    collider("Corridor_Ceiling", (2.1,-3.50,2.83), (2.4,1.85,.08))
    box("Corridor_Fuse_Box", (2.73,-4.245,1.60), (.34,.10,.46), "Steel", "Corridor", .012)
    text("Corridor_Floor_Label", "03", (2.225,-4.255,1.83), .18, "Ink",
         rotation=(math.pi/2,0,math.pi), group="Corridor")


def door():
    pivot = empty("DoorHinge", (1.6,-2.63,.023))
    pivot["rotation_axis"] = "local Z (Unity local Y after import)"
    pivot["closed_degrees"] = 0.0
    pivot["open_degrees"] = 92.0
    leaf = box("DoorLeaf", (.485,0,1.072), (.97,.052,2.144), "Walnut", "Door", .011)
    parent_local(leaf, pivot)
    for z,h in ((.54,.68),(1.44,.73)):
        panel = box(f"Door_Inset_{z}", (.485,.030,z), (.73,.013,h), "Paint Dark", "Door", .010)
        parent_local(panel, pivot)
        for x in (.12,.85):
            parent_local(box(f"Door_Panel_Frame_{z}_{x}", (x,.043,z), (.025,.022,h+.05), "Walnut", "Door", .004),pivot)
    for z in (.30,1.02,1.85):
        parent_local(cylinder(f"Door_Hinge_Knuckle_{z}", (0,0,z-.045),(0,0,z+.045),.017,"Brass","Door",16), pivot)
    parent_local(box("Door_Handle_Plate", (.84,.041,1.04), (.055,.018,.19), "Brass", "Door", .009),pivot)
    parent_local(cylinder("Door_Handle_Spindle", (.84,.04,1.10),(.84,.085,1.10),.014,"Brass","Door"),pivot)
    parent_local(cylinder("Door_Handle_Lever", (.84,.085,1.10),(.70,.085,1.10),.016,"Brass","Door"),pivot)
    parent_local(sphere("Door_Keyhole",(.84,.056,1.005),(.007,.003,.014),"Plastic Black","Door"),pivot)
    for z in (.966,1.12):
        parent_local(sphere(f"Door_Plate_Screw_{z}",(.84,.053,z),(.004,.002,.004),"Steel","Door",12,6),pivot)
    collider("DoorLeaf", (2.085,-2.63,1.095), (.97,.052,2.144), dynamic=True)


def window_and_city():
    for y in (-.785,1.085):
        box(f"Window_Jamb_{y}", (-3.21,y,1.59), (.22,.072,1.48), "Trim", "Window", .007)
    for z in (.865,2.315):
        box(f"Window_Reveal_{z}", (-3.21,.15,z), (.22,1.94,.07), "Trim", "Window", .007)
    box("Window_Sill", (-3.035,.15,.88), (.38,2.04,.08), "Walnut", "Window", .011)
    for y in (-.72,.15,1.02):
        box(f"Window_Vertical_Mullion_{y}", (-3.235,y,1.59), (.085,.042,1.38), "Paint Dark", "Window", .003)
    for z in (.94,1.66,2.24):
        box(f"Window_Horizontal_Mullion_{z}", (-3.235,.15,z), (.085,1.78,.043), "Paint Dark", "Window", .003)
    for y in (-.285,.585):
        for z in (1.30,1.95):
            box(f"Window_Glass_{y}_{z}", (-3.245,y,z), (.008,.83,.65 if z>1.5 else .69), "Window Glass", "Window", 0)
    cylinder("Curtain_Rod",(-3.00,-1.02,2.43),(-3.00,1.32,2.43),.013,"Steel","Window")
    for y in (-1.05,1.35):
        sphere(f"Curtain_Rod_End_{y}",(-3.0,y,2.43),(.025,.032,.025),"Brass","Window")
    for side,y0 in (("South",-.98),("North",.98)):
        verts=[]; faces=[]; uv=[]
        ny,nz=20,30
        for iz in range(nz+1):
            zz=.76+1.62*iz/nz
            for iy in range(ny+1):
                yy=y0+.30*iy/ny
                xx=-2.99+.033*math.sin(iy/ny*math.pi*8)*(1.0-.18*iz/nz)
                verts.append((xx,yy,zz+.012*math.cos(iy/ny*math.pi*8)))
        for iz in range(nz):
            for iy in range(ny):
                a=iz*(ny+1)+iy
                faces.append((a,a+1,a+ny+2,a+ny+1))
        mesh=bpy.data.meshes.new(f"Curtain_{side}")
        mesh.from_pydata(verts,[],faces);mesh.update()
        o=bpy.data.objects.new(f"Curtain_{side}",mesh);collection("Window").objects.link(o)
        finish(o,o.name,"Cotton Teal","Window",smooth=True)
        box_uv(o)
        m=o.modifiers.new("Cloth thickness","SOLIDIFY");m.thickness=.002
    # Shallow radiator fins with visible pipes under the actual window.
    for i in range(15):
        box(f"Radiator_Fin_{i:02d}",(-3.005,-.44+i*.084,.435),(.16,.047,.52),"Trim","Window",.017)
    for z in (.215,.65):
        cylinder(f"Radiator_Header_{z}",(-3.005,-.50,z),(-3.005,.78,z),.027,"Trim","Window")
    curve("Radiator_Pipe",[(-3.01,.82,.50),(-2.98,.89,.45),(-2.98,.89,.10),(-3.17,.89,.08)],.018,"Trim","Window")
    cylinder("Radiator_Thermostat",(-2.91,.81,.54),(-2.83,.81,.54),.036,"Cotton Ivory","Window",24)
    # Exterior uses real simplified meshes and emissive window panes, no backdrop image.
    for bi,(x,y,w,h) in enumerate(((-6.8,-3.9,3.2,6.4),(-7.8,.15,3.8,8.5),(-6.3,4.4,3.2,5.6))):
        box(f"City_Building_{bi}",(x,y,h/2-1.5),(1.5,w,h),"Facade","Exterior",.02)
        for floor in range(int(h/.85)):
            zz=-1.0+floor*.85
            box(f"Facade_Stringcourse_{bi}_{floor}",(x+.76,y,zz-.13),(.035,w+.08,.047),"Facade Trim","Exterior",.001)
            for bay in range(4):
                yy=y-w/2+.40+bay*(w-.60)/4
                material=random.choices(["City Window Dark","City Window Warm","City Window Cool"],[.5,.28,.22])[0]
                box(f"City_Pane_{bi}_{floor}_{bay}",(x+.768,yy,zz+.20),(.019,.38,.51),material,"Exterior",.002)
    box("Exterior_Alley",(-5.15,0,-.9),(5.2,15,.1),"Paint Dark","Exterior",0)


def bed():
    group="Bed"
    for x in (-2.51,-1.19):
        for y in (.27,2.24):
            box(f"Bed_Leg_{x}_{y}",(x,y,.19),(.075,.075,.36),"Walnut",group,.012)
    for x in (-2.60,-1.10):
        box(f"Bed_Rail_{x}",(x,1.28,.36),(.085,2.28,.21),"Walnut",group,.013)
    for y in (.17,2.39):
        box(f"Bed_End_Rail_{y}",(-1.85,y,.36),(1.53,.07,.21),"Walnut",group,.012)
    for i in range(12):
        box(f"Bed_Slat_{i}",(-1.85,.27+i*.18,.415),(1.45,.062,.025),"Walnut",group,.005)
    box("Mattress",(-1.85,1.28,.55),(1.48,2.17,.27),"Cotton Ivory",group,.09)
    box("Mattress_Seam",(-1.85,1.28,.625),(1.49,2.18,.016),"Paper",group,.007)
    box("Headboard",(-1.85,2.405,.77),(1.70,.10,1.16),"Walnut",group,.03)
    box("Headboard_Upholstery",(-1.85,2.34,.91),(1.48,.066,.64),"Fabric Charcoal",group,.055)
    for x in (-2.3,-1.85,-1.4):
        sphere(f"Headboard_Button_{x}",(x,2.301,.91),(.014,.007,.014),"Plastic Black",group)
    for x,angle in ((-2.20,-.04),(-1.50,.06)):
        o=box(f"Pillow_{x}",(x,2.0,.748),(.64,.42,.16),"Cotton Ivory",group,.073)
        o.rotation_euler=(0,0,angle)
    # Draped blanket with geometric seams/folds instead of a flat cover slab.
    nx,ny=40,48
    verts=[];faces=[]
    for j in range(ny+1):
        y=.10+1.63*j/ny
        for i in range(nx+1):
            x=-2.68+1.66*i/nx
            edge=max(0,abs(x+1.85)-.675)/.155
            foot=max(0,.24-y)/.14
            z=.751-.23*(edge**1.5)-.16*(foot**1.5)
            z+=.012*math.sin((x+2.68)*24+y*4)+.010*math.sin(y*17+x*3)
            z+=.013*math.sin(y*8)*math.sin((x+2.68)*10)
            verts.append((x,y,z))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    mesh=bpy.data.meshes.new("Blanket_Draped_Grid");mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new("Blanket_Draped",mesh);collection(group).objects.link(o)
    finish(o,o.name,"Cotton Teal",group,smooth=True)
    box_uv(o)
    mod=o.modifiers.new("Fabric thickness","SOLIDIFY");mod.thickness=.004
    points=[]
    for i in range(nx+1):
        x,y,z=verts[ny*(nx+1)+i];points.append((x,y,z+.004))
    curve("Blanket_Bound_Edge",points,.006,"Blanket Edge",group)
    box("Bedside_Chest",(-2.89,2.10,.30),(.40,.44,.55),"Walnut",group,.012)
    for z in (.22,.44):
        box(f"Bedside_Drawer_{z}",(-2.89,1.865,z),(.35,.022,.185),"Walnut",group,.006)
        cylinder(f"Bedside_Handle_{z}",(-2.95,1.842,z),(-2.83,1.842,z),.01,"Brass",group)
    collider("Bed",(-1.85,1.28,.46),(1.67,2.36,.90))
    collider("BedsideChest",(-2.89,2.1,.3),(.40,.44,.60))



def formed_shell(name, origin, sections, material, group, span=(-1,1), resolution=20):
    """Manufactured shell surface from measured longitudinal profiles, not an ellipsoid."""
    verts=[];faces=[]
    for y,width,height in sections:
        for j in range(resolution+1):
            u=span[0]+(span[1]-span[0])*j/resolution
            verts.append((origin[0]+width*u,origin[1]+y,
                          origin[2]+height*max(0,1-u*u)**.55))
    for i in range(len(sections)-1):
        for j in range(resolution):
            a=i*(resolution+1)+j
            faces.append((a,a+1,a+resolution+2,a+resolution+1))
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    obj=bpy.data.objects.new(name,data);collection(group).objects.link(obj)
    box_uv(obj);finish(obj,name,material,group,smooth=True)
    solid=obj.modifiers.new("Molded shell thickness","SOLIDIFY");solid.thickness=.0013
    bevel=obj.modifiers.new("Soft manufactured edge","BEVEL");bevel.width=.0005;bevel.segments=2
    return obj


def tailored_cushion(name, loc, width, depth, thickness, material, group, rot=None):
    """Rounded rectangular upholstery with a crowned center and upholstered perimeter."""
    verts=[];faces=[];n=48
    rings=[(.91,-thickness*.45),(1.,-thickness*.12),(.97,thickness*.27),(.78,thickness*.48),(.35,thickness*.56)]
    for radius,z in rings:
        for i in range(n):
            a=math.tau*i/n
            xx=math.copysign(abs(math.cos(a))**.42,math.cos(a))*width*.5*radius
            yy=math.copysign(abs(math.sin(a))**.42,math.sin(a))*depth*.5*radius
            verts.append((xx,yy,z))
    for ring in range(len(rings)-1):
        for i in range(n):
            a=ring*n+i;b=ring*n+(i+1)%n
            faces.append((a,b,b+n,a+n))
    faces.append(tuple(reversed(tuple(range(n)))))
    faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    obj=bpy.data.objects.new(name,data);collection(group).objects.link(obj)
    obj.location=loc
    if rot:obj.rotation_euler=rot
    box_uv(obj);finish(obj,name,material,group,smooth=True)
    sub=obj.modifiers.new("Cushion contour refinement","SUBSURF");sub.levels=1;sub.render_levels=1
    bpy.context.view_layer.update()
    points=[]
    for i in range(49):
        a=math.tau*i/48
        xx=math.copysign(abs(math.cos(a))**.42,math.cos(a))*width*.5*.98
        yy=math.copysign(abs(math.sin(a))**.42,math.sin(a))*depth*.5*.98
        points.append(tuple(obj.matrix_world@Vector((xx,yy,thickness*.19))))
    curve(name+"_Piped_Seam",points,.0015,"Chair Stitch",group)
    return obj


def mouse_and_tools():
    group="Desk"
    box("Mouse_Pad",(2.18,1.72,.8155),(.31,.30,.005),"Fabric Charcoal",group,.012)
    # A 118 x 66 x 34 mm wired mouse, with separate injection-molded shell/button parts.
    x,y,z=2.18,1.72,.8195
    footprint=[(-.057,.005),(-.054,.016),(-.042,.026),(-.021,.0305),(.006,.0305),(.027,.028),(.045,.024),(.055,.016)]
    outline=[(-ww,yy) for yy,ww in footprint]+[(ww,yy) for yy,ww in reversed(footprint)]
    nn=len(outline)
    verts=[(x+xx,y+yy,z+zz) for zz in (0,.006) for xx,yy in outline]
    faces=[tuple(reversed(range(nn))),tuple(range(nn,2*nn))]
    faces += [(i,(i+1)%nn,(i+1)%nn+nn,i+nn) for i in range(nn)]
    faces=[tuple(reversed(face)) for face in faces]
    data=bpy.data.meshes.new("Mouse_Underside");data.from_pydata(verts,[],faces);data.update()
    base=bpy.data.objects.new("Mouse_Underside",data);collection(group).objects.link(base)
    box_uv(base);finish(base,"Mouse_Underside","Plastic Black",group,.0015,True)
    formed_shell("Mouse_Rear_Palm_Shell",(x,y,z+.005),
        [(-.057,.006,.002),(-.053,.018,.012),(-.041,.028,.023),(-.021,.032,.030),(-.001,.032,.027),(.006,.031,.024)],"Mouse Shell",group)
    button_sections=[(.008,.031,.023),(.020,.029,.020),(.037,.026,.014),(.049,.023,.007),(.055,.015,.002)]
    formed_shell("Mouse_Left_Button",(x,y,z+.005),button_sections,"Mouse Buttons",group,(-1,-.043),12)
    formed_shell("Mouse_Right_Button",(x,y,z+.005),button_sections,"Mouse Buttons",group,(.043,1),12)
    cylinder("Mouse_Scroll_Wheel",(x-.004,y+.028,z+.022),(x+.004,y+.028,z+.022),.008,"Rubber",group,32,.0007)
    for i in range(18):
        a=i*math.tau/18
        yy=y+.028+math.sin(a)*.008;zz=z+.022+math.cos(a)*.008
        cylinder(f"Mouse_Wheel_Grip_{i}",(x-.0042,yy,zz),(x+.0042,yy,zz),.00065,"Mouse Shell",group,6,0)
    box("Mouse_DPI_Button",(x,y+.003,z+.031),(.008,.009,.002),"Plastic Black",group,.002)
    for side in (-1,1):
        curve(f"Mouse_Lower_Shell_Seam_{side}",[(x+side*.021,y-.050,z+.007),(x+side*.030,y-.023,z+.007),(x+side*.030,y+.012,z+.007),(x+side*.022,y+.049,z+.007)],.00055,"Rubber",group)
    for yy in (-.038,.037):
        box(f"Mouse_Underside_Glide_{yy}",(x,y+yy,z-.001),(.032,.010,.001),"Glide Pad",group,.004)
    cylinder("Mouse_Optical_Aperture",(x,y,z-.001),(x,y,z+.0002),.005,"Plastic Black",group,20,0)
    for yy in (-.036,.035):
        cylinder(f"Mouse_Bottom_Screw_{yy}",(x,y+yy,z-.0012),(x,y+yy,z-.0005),.0018,"Steel",group,12,0)
    cylinder("Mouse_Cable_Core",(x,y+.048,z+.009),(x,y+.0675,z+.009),.0022,"Rubber",group,20,.0003)
    for i in range(4):
        cylinder(f"Mouse_Cable_Strain_Relief_{i}",(x,y+.055+i*.0025,z+.009),(x,y+.057+i*.0025,z+.009),.0032-i*.00025,"Rubber",group,12,.0002)
    curve("Mouse_Cable",[(x,y+.065,z+.009),(2.23,1.90,.817),(2.19,2.04,.817),(2.05,2.035,.817),(1.985,1.97,.818),(1.972,1.873,.837)],.0018,"Rubber",group)
    box("Mouse_USB_Connector",(1.965,1.873,.837),(.030,.014,.006),"Plastic Black",group,.002)
    # Real shallow magnetic parts tray, not a solid rectangular slab.
    box("Parts_Tray_Base",(1.25,1.70,.816),(.17,.13,.006),"Steel",group,.007)
    box("Parts_Tray_Liner",(1.25,1.70,.820),(.149,.109,.0015),"Rubber",group,.004)
    for xx in (1.169,1.331):box(f"Parts_Tray_Side_{xx}",(xx,1.70,.823),(.008,.119,.012),"Steel",group,.003)
    for yy in (1.639,1.761):box(f"Parts_Tray_End_{yy}",(1.25,yy,.823),(.162,.008,.012),"Steel",group,.003)
    for i in range(5):
        xx=1.199+i*.022
        cylinder(f"Spare_Screw_Shaft_{i}",(xx,1.68,.824),(xx,1.704,.824),.0024,"Aluminium",group,10,.0003)
        cylinder(f"Spare_Screw_Head_{i}",(xx,1.704,.824),(xx,1.708,.824),.004,"Steel",group,12,.0004)
    cylinder("Screwdriver_Handle",(1.235,1.56,.828),(1.343,1.56,.825),.014,"Notice Red",group,32,.003)
    for i in range(5):
        xx=1.248+i*.015
        cylinder(f"Screwdriver_Grip_Ring_{i}",(xx,1.56,.828),(xx+.005,1.56,.828),.0144,"Rubber",group,24,.001)
    cylinder("Screwdriver_Collar",(1.343,1.56,.825),(1.354,1.56,.824),.007,"Steel",group,20,.001)
    cylinder("Screwdriver_Shaft",(1.354,1.56,.824),(1.454,1.56,.8157),.0027,"Aluminium",group,16,.0005)
    box("Screwdriver_Flat_Tip",(1.461,1.56,.815),(.016,.005,.0024),"Steel",group,.0005,rot=(0,.075,0))


def desk_and_chair():
    group="Desk"
    top=box("Desk_Worktop",(1.83,1.87,.785),(2.14,.83,.055),"Desk Walnut",group,.010)
    for uv in top.data.uv_layers.active.data:uv.uv=(uv.uv.y,uv.uv.x)
    box("Desk_Front_Edge",(1.83,1.463,.756),(2.10,.019,.018),"Desk Walnut",group,.005)
    for x in (.87,2.79):
        for y in (1.54,2.19):
            box(f"Desk_Leg_{x}_{y}",(x,y,.39),(.045,.045,.74),"Steel",group,.005)
            box(f"Desk_Foot_{x}_{y}",(x,y,.032),(.064,.066,.04),"Rubber",group,.008)
        box(f"Desk_Side_Brace_{x}",(x,1.865,.22),(.033,.65,.033),"Steel",group,.004)
    box("Desk_Rear_Brace",(1.83,2.19,.51),(1.94,.027,.04),"Steel",group,.003)
    box("Desk_Drawer_Carcass",(2.60,1.89,.605),(.41,.67,.29),"Walnut",group,.01)
    for z in (.535,.675):
        box(f"Desk_Drawer_Front_{z}",(2.60,1.54,z),(.389,.031,.126),"Walnut",group,.006)
        curve(f"Desk_Drawer_Handle_{z}",[(2.53,1.519,z),(2.53,1.486,z),(2.67,1.486,z),(2.67,1.519,z)],.008,"Steel",group)
    mouse_and_tools()
    # Ergonomic articulated chair, visible spokes, suspension, caster wheels, armrests.
    cx,cy=1.68,.91
    box("Chair_Seat_Base",(cx,cy,.463),(.51,.48,.045),"Plastic Black","Chair",.04)
    tailored_cushion("Chair_Seat_Cushion",(cx,cy,.514),.49,.46,.082,"Fabric Charcoal","Chair")
    back=box("Chair_Back_Shell",(cx,cy-.255,.85),(.485,.046,.55),"Plastic Black","Chair",.04,rot=(math.radians(7),0,0))
    tailored_cushion("Chair_Back_Cushion",(cx,cy-.211,.86),.435,.485,.063,"Fabric Charcoal","Chair",rot=(math.radians(-83),0,0))
    box("Chair_Underseat_Mechanism",(cx,cy-.025,.416),(.22,.24,.044),"Steel","Chair",.014)
    curve("Chair_Adjustment_Lever",[(cx+.07,cy-.03,.415),(cx+.25,cy-.05,.411),(cx+.29,cy-.06,.431)],.007,"Steel","Chair")
    box("Chair_Lever_Grip",(cx+.29,cy-.06,.431),(.065,.038,.022),"Plastic Black","Chair",.009)
    for x in (cx-.155,cx+.155):
        curve(f"Chair_Back_Support_{x}",[(x,cy-.15,.40),(x,cy-.29,.53),(x,cy-.30,.72)],.019,"Steel","Chair")
    cylinder("Chair_Piston",(cx,cy,.135),(cx,cy,.438),.032,"Aluminium","Chair",24)
    cylinder("Chair_Piston_Cover",(cx,cy,.16),(cx,cy,.34),.044,"Plastic Black","Chair",24)
    for i in range(5):
        a=math.pi/10+i*math.tau/5
        wx,wy=cx+math.cos(a)*.31,cy+math.sin(a)*.31
        cylinder(f"Chair_Base_Spoke_{i}",(cx,cy,.14),(wx,wy,.085),.023,"Steel","Chair")
        cylinder(f"Caster_Stem_{i}",(wx,wy,.055),(wx,wy,.094),.013,"Steel","Chair")
        dx,dy=math.cos(a+math.pi/2)*.022,math.sin(a+math.pi/2)*.022
        for side in (-1,1):
            px,py=wx+dx*side,wy+dy*side
            cylinder(f"Caster_Wheel_{i}_{side}",(px-dx*.32,py-dy*.32,.046),(px+dx*.32,py+dy*.32,.046),.034,"Rubber","Chair",20)
    for x in (cx-.29,cx+.29):
        cylinder(f"Chair_Arm_Post_{x}",(x,cy-.02,.49),(x,cy-.02,.695),.015,"Steel","Chair")
        box(f"Chair_Armrest_{x}",(x,cy,.718),(.075,.32,.045),"Plastic Black","Chair",.019)
    for obj in COL["Chair"].objects:
        if obj.type=="MESH":
            for face in obj.data.polygons:face.use_smooth=True
            for mod in obj.modifiers:
                if mod.type=="BEVEL":mod.segments=6
    collider("DeskTop",(1.83,1.87,.775),(2.14,.83,.08))
    collider("DeskDrawer",(2.6,1.89,.61),(.41,.67,.30))
    collider("Chair",(cx,cy,.55),(.66,.65,1.10))


def laptop():
    group="Laptop"
    x,y,z=1.73,1.80,.837
    box("Laptop_Bottom_Case",(x,y,z-.009),(.437,.297,.009),"Steel",group,.005)
    box("Laptop_Case_Seam",(x,y,z-.002),(.441,.300,.002),"Plastic Black",group,.006)
    box("Laptop_Base",(x,y,z+.003),(.44,.30,.018),"Aluminium",group,.005)
    for dx in (-.168,.168):
        for dy in (-.105,.105):
            cylinder(f"Laptop_Rubber_Foot_{dx}_{dy}",(x+dx,y+dy,.8125),(x+dx,y+dy,.824),.008,"Rubber",group,24,.001)
            cylinder(f"Laptop_Base_Screw_{dx}_{dy}",(x+dx*.9,y+dy*.85,.823),(x+dx*.9,y+dy*.85,.824),.002,"Steel",group,12,0)
    box("Laptop_Keyboard_Recess",(x,y+.044,z+.0125),(.400,.164,.002),"Plastic Black",group,.005)
    unit=.019
    def key(name,label,kx,ky,width=.0165,height=.017):
        obj=box(name,(kx,ky,z+.0167),(width,height,.0042),"Plastic Black",group,.0012)
        for mod in obj.modifiers:
            if mod.type=="BEVEL":mod.segments=3
        size=.0055 if len(label)==1 else .0034
        if label:text(name+"_Legend",label,(kx-width*.33,ky-height*.18,z+.019),size,"Key Legend",rotation=(0,0,0),group=group)
    left=x-.187
    rows=[[(c,1) for c in "`1234567890-="]+[("BKSP",1.5)],
          [("TAB",1.5)]+[(c,1) for c in "QWERTYUIOP[]\\"],
          [("CAPS",1.75)]+[(c,1) for c in "ASDFGHJKL;'"]+[("ENTER",1.75)],
          [("SHIFT",2.25)]+[(c,1) for c in "ZXCVBNM,./"]+[("SHIFT",2.25)],
          [("CTRL",1.25),("FN",1),("WIN",1),("ALT",1.25),("",6),("ALT",1.25),("CTRL",1.25),("MENU",1.5)]]
    for row,keys in enumerate(rows):
        cursor=left
        for col,(label,units) in enumerate(keys):
            width=units*unit-.0025
            key(f"Keycap_{row}_{col}",label,cursor+units*unit*.5,y+.080-row*.023,width)
            cursor+=units*unit
    for i,label in enumerate(["ESC"]+["F"+str(j) for j in range(1,13)]+["DEL"]):
        key(f"Function_Key_{i}",label,left+.009+i*.020,y+.106,.0165,.012)
    num_left=x+.111
    padrows=[["NUM","/","*","-"],["7","8","9","+"],["4","5","6",None],["1","2","3","ENT"],["0",None,".",None]]
    for row,labels in enumerate(padrows):
        for col,label in enumerate(labels):
            if label is None:continue
            ww=.0165;hh=.017;kx=num_left+col*.019;ky=y+.080-row*.023
            if label in ("+","ENT"):hh=.040;ky-=.0115
            if label=="0":ww=.0355;kx+=.0095
            key(f"Numpad_{row}_{col}",label,kx,ky,ww,hh)
    box("Laptop_Trackpad_Seam",(x-.044,y-.096,z+.0127),(.146,.064,.0013),"Plastic Black",group,.005)
    box("Laptop_Trackpad",(x-.044,y-.096,z+.0136),(.142,.060,.0013),"Aluminium",group,.004)
    box("Laptop_Status_LED",(x+.175,y-.1505,z+.003),(.004,.001,.0015),"Status Amber",group,.0003)
    cylinder("Laptop_Hinge_Left",(x-.17,y+.14,z+.006),(x-.10,y+.14,z+.006),.014,"Steel",group)
    cylinder("Laptop_Hinge_Right",(x+.10,y+.14,z+.006),(x+.17,y+.14,z+.006),.014,"Steel",group)
    hinge=empty("LaptopLidPivot",(x,y+.145,z+.008),.035)
    hinge.rotation_euler[0]=math.radians(-12)
    parent_local(box("Laptop_Lid",(0,0,.139),(.445,.013,.286),"Aluminium",group,.008),hinge)
    parent_local(box("Laptop_Bezel",(0,-.009,.141),(.427,.009,.267),"Plastic Black",group,.007),hinge)
    parent_local(box("Laptop_Screen",(0,-.014,.145),(.397,.002,.229),"Screen",group,.001),hinge)
    parent_local(sphere("Laptop_Webcam",(0,-.016,.267),(.003,.002,.003),"Steel",group,12,6),hinge)
    parent_local(text("Screen_Brand","SIGNAL / OS",(-.180,-.017,.241),.014,"Screen Text",group=group),hinge)
    parent_local(text("Screen_Notification_Header","INCOMING MESSAGE",(-.174,-.017,.198),.010,"Status Amber",group=group),hinge)
    parent_local(text("Screen_Notification_Title","QUICK DIAGNOSTIC",(-.174,-.017,.171),.014,"Screen White",group=group),hinge)
    parent_local(text("Screen_Notification_Body","shop order folder won't open.\npays today. can you take a look?",(-.174,-.017,.145),.009,"Screen White",group=group),hinge)
    parent_local(box("Screen_Notification_Button",(-.103,-.017,.086),(.147,.001,.023),"Panel Teal",group,.002),hinge)
    parent_local(text("Screen_Button_Text","OPEN MESSAGE",(-.170,-.019,.081),.0085,"Screen Text",group=group),hinge)
    parent_local(text("Screen_Status","LOCAL / 00:23",(-.175,-.017,.046),.009,"Screen Text",group=group),hinge)
    for i in range(15):
        for side in (-1,1):
            box(f"Laptop_Speaker_Slot_{side}_{i}",(x+side*.205,y+.104-i*.009,z+.013),(.005,.0016,.0008),"Plastic Black",group,.0003)
    for yy in (-.073,.073):
        box(f"Laptop_USB_Port_Rim_{yy}",(x+.2204,y+yy,z),(.0015,.015,.006),"Steel",group,.001)
        box(f"Laptop_USB_Port_{yy}",(x+.2213,y+yy,z),(.001,.012,.004),"Plastic Black",group,.0007)
        box(f"Laptop_USB_Tongue_{yy}",(x+.222,y+yy,z-.0007),(.001,.008,.001),"Aluminium",group,.0002)
    for i in range(11):
        box(f"Laptop_Cooling_Slit_{i}",(x-.2204,y-.035+i*.008,z-.002),(.001,.004,.004),"Plastic Black",group,.0006)
    curve("Laptop_Power_Cable",[(x-.22,y+.09,z),(x-.35,y+.18,.824),(1.31,2.24,.80),(1.28,2.32,.26),(.89,2.49,.25)],.0035,"Rubber",group)
    bpy.context.view_layer.update()
    focus=hinge.matrix_world @ Vector((0,-.016,.15))
    empty("LaptopFocus",focus,.055)
    collider("Laptop",(x,y+.04,z+.14),(.46,.40,.29))


def client_pc():
    """A modeled customer tower: visible component shapes and removable side panel."""
    group="Client_PC"
    x,y,z=1.01,1.91,1.087
    w,d,h=.265,.36,.50
    box("ClientPC_Chassis_Bottom",(x,y,z-h/2), (w,d,.012),"Steel",group,.005)
    box("ClientPC_Chassis_Top",(x,y,z+h/2), (w,d,.012),"Steel",group,.005)
    for dx in (-.090,.090):
        for dy in (-.125,.125):
            cylinder(f"ClientPC_Rubber_Foot_{dx}_{dy}",(x+dx,y+dy,.8125),(x+dx,y+dy,.832),.014,"Rubber",group,24,.002)
    box("ClientPC_Back",(x,y+d/2,z), (w,.012,h),"Steel",group,.003)
    box("ClientPC_Front",(x,y-d/2,z), (w,.016,h),"Plastic Black",group,.008)
    box("ClientPC_Fixed_Side",(x-w/2,y,z), (.014,d,h),"Steel",group,.004)
    panel=box("ClientPC_SidePanel",(x+w/2+.002,y,z),(.009,d-.008,h-.012),"Window Glass",group,.003)
    panel["removable_panel"] = True
    for yy in (y-d/2+.025,y+d/2-.025):
        for zz in (z-h/2+.025,z+h/2-.025):
            cylinder(f"ClientPC_Panel_Screw_{yy}_{zz}",(x+w/2+.003,yy,zz),(x+w/2+.012,yy,zz),.006,"Aluminium",group,12,.001)
    box("ClientPC_Grille_Recess",(x,y-d/2-.009,z-.035),(.207,.002,.299),"Rubber",group,.006)
    for i in range(14):
        box(f"ClientPC_Front_Vent_{i}",(x,y-d/2-.0105,z-.165+i*.019),(.189,.002,.009),"Plastic Black",group,.0015)
    for dx in (-.094,.094):
        box(f"ClientPC_Front_Grille_Frame_{dx}",(x+dx,y-d/2-.012,z-.04),(.011,.006,.282),"Plastic Black",group,.002)
    box("ClientPC_Front_IO_Recess",(x,y-d/2-.010,z+.145),(.173,.002,.044),"Paint Dark",group,.004)
    for dx in (-.054,-.021):
        box(f"ClientPC_USB_Rim_{dx}",(x+dx,y-d/2-.012,z+.146),(.020,.003,.009),"Aluminium",group,.001)
        box(f"ClientPC_USB_Socket_{dx}",(x+dx,y-d/2-.014,z+.146),(.015,.001,.0055),"Plastic Black",group,.0006)
    cylinder("ClientPC_Audio_Jack",(x+.024,y-d/2-.012,z+.146),(x+.024,y-d/2-.016,z+.146),.004,"Aluminium",group,16,.0004)
    cylinder("ClientPC_Audio_Hole",(x+.024,y-d/2-.016,z+.146),(x+.024,y-d/2-.017,z+.146),.0026,"Rubber",group,16,0)
    for yy in (y-d/2+.015,y+d/2-.015):
        box(f"ClientPC_Glass_Vertical_Gasket_{yy}",(x+w/2+.007,yy,z),(.003,.008,h-.025),"Rubber",group,.001)
    for zz in (z-h/2+.01,z+h/2-.01):
        box(f"ClientPC_Glass_Horizontal_Gasket_{zz}",(x+w/2+.007,y,zz),(.003,d-.03,.009),"Rubber",group,.001)
    sphere("ClientPC_Power_LED",(x+.08,y-d/2-.01,z+.19),(.004,.002,.004),"Status Amber",group,12,6)
    box("ClientPC_Power_Button",(x-.02,y-d/2-.011,z+.19),(.041,.008,.013),"Steel",group,.004)
    box("ClientPC_Motherboard",(x-w/2+.025,y,z+.025),(.013,.29,.36),"Circuit Board",group,.002)
    box("ClientPC_CPU_Heatsink",(x-.058,y+.035,z+.085),(.095,.087,.085),"Aluminium",group,.003)
    for i in range(7):
        box(f"ClientPC_Heatsink_Fin_{i}",(x-.045,y-.001+i*.012,z+.09),(.086,.003,.09),"Steel",group,.001)
    for i in range(2):
        box(f"ClientPC_RAM_{i}",(x-.075,y-.105+i*.028,z+.095),(.015,.012,.14),"Circuit Board",group,.001)
    box("ClientPC_GPU",(x-.015,y-.005,z-.055),(.16,.23,.032),"Plastic Black",group,.004)
    box("ClientPC_PSU",(x,y+.095,z-.188),(.22,.15,.102),"Paint Dark",group,.004)
    curve("ClientPC_Internal_Cable",[(x+.06,y+.08,z-.13),(x+.075,y-.04,z-.03),(x+.04,y-.09,z+.07)],.005,"Rubber",group)
    empty("PCBenchTarget",(x,y-.23,z),.06)


def lamp_and_details():
    group="Lighting_Fixtures"
    x,y=2.59,2.08
    cylinder("Desk_Lamp_Base",(x,y,.814),(x,y,.835),.089,"Paint Dark",group,32,.004)
    cylinder("Lamp_Lower_Arm",(x,y,.842),(x-.03,y,.111+1),.012,"Steel",group)
    cylinder("Lamp_Upper_Arm",(x-.03,y,1.111),(x-.23,y-.04,1.30),.012,"Steel",group)
    sphere("Lamp_Elbow",(x-.03,y,1.111),(.025,.021,.025),"Brass",group)
    bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=.105, radius2=.051, depth=.108, location=(x-.23,y-.04,1.295))
    finish(bpy.context.object,"Desk_Lamp_Shade","Paint Dark",group,.004)
    cylinder("Desk_Lamp_Diffuser",(x-.23,y-.04,1.237),(x-.23,y-.04,1.242),.094,"Bulb Warm",group,32,0)
    curve("Desk_Lamp_Cord",[(x,y,.82),(x+.17,y+.1,.821),(2.88,2.28,.76),(2.97,2.51,.26)],.003,"Rubber",group)
    # Functional outlet plates; three visible sockets and connected plugs.
    for x in (.86,2.94):
        box(f"Outlet_Plate_{x}",(x,2.585,.27),(.12,.026,.19),"Trim","Details",.01)
        for z in (.235,.305):
            box(f"Outlet_Socket_{x}_{z}",(x,2.568,z),(.062,.006,.048),"Cotton Ivory","Details",.008)
            for dx in (-.014,.014):
                box(f"Outlet_Hole_{x}_{z}_{dx}",(x+dx,2.563,z),(.006,.003,.013),"Plastic Black","Details",.001)
        box(f"Outlet_Plug_{x}",(x,2.54,.235),(.049,.039,.038),"Plastic Black","Details",.008)
    # Rent paper is part of the actual scene and exports as geometry.
    box("Rent_Notice_Paper",(.47,2.572,1.48),(.30,.007,.40),"Paper","Story_Props",.003)
    text("Notice_Title","FINAL NOTICE",(.344,2.565,1.615),.025,"Notice Red",group="Story_Props")
    text("Notice_Rent","RENT OVERDUE",(.346,2.565,1.553),.021,"Ink",group="Story_Props")
    text("Notice_Body","Payment is still outstanding.\nPlease contact the manager.\n\nApartment 03",(.345,2.565,1.512),.011,"Ink",group="Story_Props")
    sphere("Notice_Pin",(.47,2.553,1.662),(.011,.008,.011),"Notice Red","Story_Props")
    # A few purposeful hacker-workspace props; no unrelated lifestyle clutter.
    book=empty("Service_Manual_Pivot",(2.49,1.84,.8125),.02);book.rotation_euler.z=-.15
    for zz in (.002,.020):
        parent_local(box(f"Manual_Cover_{zz}",(0,0,zz),(.144,.214,.003),"Panel Teal","Story_Props",.0015),book)
    parent_local(box("Manual_Page_Block",(.002,0,.011),(.137,.207,.015),"Paper","Story_Props",.001),book)
    parent_local(box("Manual_Spine",(-.071,0,.011),(.005,.214,.022),"Panel Teal","Story_Props",.0015),book)
    for i in range(4):
        parent_local(box(f"Manual_Page_Edge_{i}",(.071,0,.005+i*.003),(.0004,.204,.0005),"Cotton Ivory","Story_Props",0),book)
    parent_local(text("Manual_Title","SERVICE\nNOTES",(-.052,-.069,.022),.017,"Paper",rotation=(0,0,0),group="Story_Props"),book)
    box("Packing_Box",(2.71,.19,.23),(.43,.44,.45),"Cardboard","Story_Props",.007)
    for x in (2.50,2.92):
        box(f"Packing_Flap_{x}",(x,.19,.461),(.025,.44,.005),"Cardboard","Story_Props",.002)
    text("Box_Label","FRAGILE\nCOMPONENTS",(2.54,-.035,.31),.031,"Ink",group="Story_Props")
    collider("PackingBox",(2.71,.19,.23),(.43,.44,.46))
    # Ceiling fitting with physical diffuser and dark rim.
    cylinder("Ceiling_Rose",(0,-.3,2.76),(0,-.3,2.79),.085,"Paint Dark",group,32)
    cylinder("Pendant_Stem",(0,-.3,2.52),(0,-.3,2.76),.005,"Rubber",group)
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=.26, radius2=.09, depth=.15, location=(0,-.3,2.50))
    finish(bpy.context.object,"Ceiling_Shade","Paint Dark",group,.006)
    cylinder("Ceiling_Diffuser",(0,-.3,2.421),(0,-.3,2.428),.245,"Bulb Warm",group,40,0)


def area(name, loc, target, energy, color, size, size_y=None):
    data=bpy.data.lights.new(name,"AREA");data.energy=energy;data.color=color
    if size_y:
        data.shape="RECTANGLE";data.size=size;data.size_y=size_y
    else:
        data.shape="DISK";data.size=size
    o=bpy.data.objects.new(name,data);collection("Render_Lights").objects.link(o)
    o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat("-Z","Y").to_euler()
    return o


def lighting():
    area("Warm_Pendant",(0,-.3,2.405),(0,0,0),100,(1.0,.73,.46),.42)
    area("Desk_Lamp_Light",(2.36,2.04,1.23),(1.70,1.80,.82),22,(1.0,.70,.38),.17)
    area("Window_Blue_Bounce",(-3.13,.15,1.68),(-.1,.3,1.1),42,(.30,.55,1.0),1.6,1.25)
    area("Quiet_Ceiling_Bounce",(1.35,.4,2.73),(0,.8,.1),35,(.74,.84,1.0),2.0)
    area("Screen_Bounce",(1.73,1.91,1.005),(1.68,.80,1.05),1.4,(.35,.83,.80),.28,.18)
    area("Corridor_Light",(2.1,-3.40,2.62),(2.1,-3.3,0),22,(.94,.79,.56),.55)
    world=bpy.data.worlds.new("Night Sky")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs[0].default_value=(.055,.095,.16,1)
    world.node_tree.nodes["Background"].inputs[1].default_value=.17
    bpy.context.scene.world=world


def anchors():
    o=empty("PlayerSpawn",(2.05,-1.89,.022));o["anchor_kind"]="feet";o["camera_height_standing"]=1.68
    o=empty("DeskSeat",(1.68,.91,.022));o["anchor_kind"]="feet";o["camera_height_seated"]=1.18
    o["look_at"]="LaptopFocus"
    empty("DoorInteract",(2.32,-2.38,1.095))
    empty("BedInteract",(-.975,.86,.63))
    empty("BillInteract",(.47,2.50,1.48))


def camera(name, loc, target, lens):
    data=bpy.data.cameras.new(name);data.lens=lens;data.sensor_width=36
    data.clip_start=.03;data.clip_end=150
    o=bpy.data.objects.new(name,data);collection("Render_Cameras").objects.link(o)
    o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat("-Z","Y").to_euler()
    return o


def setup_render(args):
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=args.samples
    # Portable packaged Blender builds can lack OpenImageDenoiser entirely.
    scene.cycles.use_denoising=False
    scene.cycles.adaptive_threshold=args.adaptive_threshold
    scene.cycles.max_bounces=7
    scene.cycles.diffuse_bounces=4
    scene.cycles.glossy_bounces=4
    scene.cycles.transmission_bounces=4
    scene.render.resolution_x=args.width
    scene.render.resolution_y=args.height
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.film_transparent=False
    scene.render.threads_mode="FIXED"
    scene.render.threads=4
    scene.view_settings.view_transform="AgX" if bpy.app.version >= (4,0,0) else "Filmic"
    scene.view_settings.exposure=.45
    scene.view_settings.gamma=1.0
    scene.render.image_settings.color_mode="RGB"
    scene.render.image_settings.color_depth="8"
    camera("Preview_01_Room",(2.96,-2.10,1.64),(-.25,1.05,1.03),21)
    camera("Preview_02_Workstation",(2.85,.14,1.48),(1.61,1.91,.94),31)
    camera("Preview_03_Bed_Window",(-.15,-1.88,1.57),(-2.10,1.34,1.01),26)
    camera("Preview_04_Entrance",(2.97,-.20,1.61),(2.00,-3.15,1.17),25)
    camera("Preview_05_Desk_Detail",(2.75,.92,1.51),(1.92,1.81,.88),40)
    scene.camera=bpy.data.objects["Preview_01_Room"]


def mesh_for_export():
    # Actual editable file is saved before conversion. The interchange files contain
    # all visible geometry (including lettering and cables) plus named empty anchors.
    bpy.ops.object.select_all(action="DESELECT")
    convert=[o for o in bpy.context.scene.objects if o.type in {"CURVE","FONT"}]
    for o in convert:
        o.select_set(True)
    if convert:
        bpy.context.view_layer.objects.active=convert[0]
        bpy.ops.object.convert(target="MESH")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.context.scene.objects:
        if o.type in {"MESH","EMPTY"}:
            o.select_set(True)


def export_files():
    manifest={
        "asset":"SignalDebt_Apartment", "version":"0.2.0", "units":"metres",
        "room_internal_size_blender":[6.4,5.2,2.8],
        "axis_conversion_note":"FBX axis_forward=-Z, axis_up=Y; collision centres supplied as x,z,y for Unity room assembly.",
        "import_note":"Use supplied box collision plan rather than mesh colliders on every detailed part. Root asset scale 1.0.",
        "door":{"pivot":"DoorHinge","mesh":"DoorLeaf","blender_closed_rotation_z":0,"blender_open_rotation_z_degrees":92},
        "anchors":{},"colliders":COLLIDERS,
        "textures":"Original baked base color PNGs in Textures. Uniform roughness/metallic and emission are material values.",
        "materials_note":"FBX material translation varies by Unity pipeline. Diffuse colors/textures survive; rebuild transparency/emission as needed.",
        "scope":"Detailed first-pass apartment source and game geometry; not a claim of final AAA production readiness."
    }
    bpy.context.view_layer.update()
    for o in COL["Gameplay_Anchors"].objects:
        p=o.matrix_world.translation
        manifest["anchors"][o.name]={"blender":[round(v,6) for v in p],"unity":[round(p[0],6),round(p[2],6),round(p[1],6)]}
    (ART_DIR/"apartment_collision_plan.json").write_text(json.dumps(manifest,indent=2))
    (BLEND_DIR/"apartment_manifest.json").write_text(json.dumps(manifest,indent=2))
    mesh_for_export()
    result={}
    try:
        bpy.ops.export_scene.fbx(filepath=str(ART_DIR/"Apartment.fbx"),
            use_selection=True, object_types={"MESH","EMPTY"},
            axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS", use_space_transform=True,
            mesh_smooth_type="FACE", use_mesh_modifiers=True,
            bake_anim=False, path_mode="COPY", embed_textures=False)
        result["fbx"]="Apartment.fbx"
    except Exception as ex:
        result["fbx_error"]=str(ex)
        print("FBX export unavailable; exporting OBJ fallback",ex,flush=True)
    try:
        bpy.ops.export_scene.gltf(filepath=str(ART_DIR/"SignalDebt_Apartment.glb"),
            export_format="GLB",use_selection=True,export_cameras=False,
            export_lights=False,export_apply=True,export_yup=True)
        result["glb"]="SignalDebt_Apartment.glb"
    except Exception as ex:
        result["glb_error"]=str(ex)
    try:
        if hasattr(bpy.ops.wm,"obj_export"):
            bpy.ops.wm.obj_export(filepath=str(ART_DIR/"SignalDebt_Apartment.obj"),
                export_selected_objects=True,forward_axis="NEGATIVE_Z",up_axis="Y",
                export_materials=True,apply_modifiers=True)
        else:
            bpy.ops.export_scene.obj(filepath=str(ART_DIR/"SignalDebt_Apartment.obj"),
                use_selection=True,axis_forward="-Z",axis_up="Y",use_materials=True)
        result["obj"]="SignalDebt_Apartment.obj"
    except Exception as ex:
        result["obj_error"]=str(ex)
    (BLEND_DIR/"export_report.json").write_text(json.dumps(result,indent=2))
    return result


def render_previews(only=None):
    scene=bpy.context.scene
    views=("Preview_01_Room","Preview_02_Workstation","Preview_03_Bed_Window","Preview_04_Entrance","Preview_05_Desk_Detail")
    if only:
        if only not in views:
            raise ValueError(f"Unknown preview camera {only}")
        views=(only,)
    for i,name in enumerate(views,1):
        scene.camera=bpy.data.objects[name]
        hinge=bpy.data.objects["DoorHinge"]
        hinge.rotation_euler.z=math.radians(78) if name=="Preview_04_Entrance" else 0
        scene.render.filepath=str(PREVIEW_DIR/f"{name}.png")
        print(f"RENDER {i}/{len(views)} {name}",flush=True)
        bpy.ops.render.render(write_still=True)
        print(f"RENDER COMPLETE {scene.render.filepath}",flush=True)


def main():
    args=parse_args()
    for path in (BLEND_DIR,ART_DIR,TEX_DIR,PREVIEW_DIR):
        path.mkdir(parents=True,exist_ok=True)
    if args.render_only:
        bpy.ops.wm.open_mainfile(filepath=str(BLEND_DIR/"SignalDebt_Apartment.blend"), use_scripts=False)
        bpy.context.scene.cycles.samples=args.samples
        bpy.context.scene.cycles.adaptive_threshold=args.adaptive_threshold
        bpy.context.scene.render.resolution_x=args.width
        bpy.context.scene.render.resolution_y=args.height
        render_previews(args.view)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC";scene.unit_settings.scale_length=1.0
    print("BUILD original apartment geometry",flush=True)
    materials()
    architecture();door();window_and_city();bed();desk_and_chair()
    laptop();client_pc();lamp_and_details();anchors();lighting();setup_render(args)
    scene["asset_authoring_note"]="Original deterministic Blender construction; script and editable geometry included."
    scene["room_dimensions_metres"]="6.4 x 5.2 x 2.8"
    scene["seed"]=SEED
    # Make mesh textures relative to this .blend location, while keeping external
    # PNGs in the Unity asset folder. Also pack source file for portability.
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_DIR/"SignalDebt_Apartment.blend"))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_DIR/"SignalDebt_Apartment.blend"))
    print("BLEND SAVED",flush=True)
    report=export_files()
    print("EXPORTS "+json.dumps(report),flush=True)
    # Restore editable source before rendering, preserving curves, text, modifiers.
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_DIR/"SignalDebt_Apartment.blend"), use_scripts=False)
    if not args.no_render:
        render_previews(args.view)
    meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    stats={"objects":len(bpy.context.scene.objects),"mesh_objects":len(meshes),
           "base_polygons":sum(len(o.data.polygons) for o in meshes),
           "materials":len(bpy.data.materials),"renders":not args.no_render,
           "blender_version":bpy.app.version_string,"seed":SEED}
    (BLEND_DIR/"build_statistics.json").write_text(json.dumps(stats,indent=2))
    print("BUILD COMPLETE "+json.dumps(stats),flush=True)


if __name__=="__main__":
    main()
