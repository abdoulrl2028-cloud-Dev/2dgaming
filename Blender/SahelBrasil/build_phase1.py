"""Sahel & Brasil — phase 1 kit.

Builds original Burkina Faso-inspired meshes in Blender (not Unity primitives):
round banco huts, flat-roof compounds, granary, well, market, acacia, baobab,
dirt road, and a rigged explorer with Idle / Walk / Run clips.
"""

import json
import math
import os

import bpy
import bmesh
from mathutils import Vector

ROOT = "/home/delll/2Dgaming/2Dgaming/Blender/SahelBrasil/export"
TEX_DIR = os.path.join(ROOT, "Textures")
MOD_DIR = os.path.join(ROOT, "Models")
PREVIEW = "/tmp/sahel_preview"
os.makedirs(TEX_DIR, exist_ok=True)
os.makedirs(MOD_DIR, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

MANIFEST = {"materials": {}, "files": []}
MATERIALS = {}
TEXTURES = {}


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30


def hash_noise(ix, iy):
    n = math.sin(ix * 127.1 + iy * 311.7) * 43758.5453
    return n - math.floor(n)


def value_noise(x, y):
    x0 = math.floor(x)
    y0 = math.floor(y)
    fx = x - x0
    fy = y - y0
    sx = fx * fx * (3.0 - 2.0 * fx)
    sy = fy * fy * (3.0 - 2.0 * fy)
    n00 = hash_noise(x0, y0)
    n10 = hash_noise(x0 + 1, y0)
    n01 = hash_noise(x0, y0 + 1)
    n11 = hash_noise(x0 + 1, y0 + 1)
    nx0 = n00 * (1 - sx) + n10 * sx
    nx1 = n01 * (1 - sx) + n11 * sx
    return nx0 * (1 - sy) + nx1 * sy


def fbm(x, y, octaves=4):
    total = 0.0
    amplitude = 1.0
    frequency = 1.0
    norm = 0.0
    for _ in range(octaves):
        total += value_noise(x * frequency, y * frequency) * amplitude
        norm += amplitude
        amplitude *= 0.5
        frequency *= 2.05
    return total / norm


def save_image(name, width, height, pixels):
    image = bpy.data.images.new(name, width, height, alpha=True)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels)
    image.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    image.file_format = "PNG"
    image.save()
    TEXTURES[name] = image
    return image


def save_normal(name, width, height, pixels):
    image = bpy.data.images.new(name, width, height, alpha=True)
    try:
        image.colorspace_settings.name = "Non-Color"
    except TypeError:
        pass
    image.pixels.foreach_set(pixels)
    image.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    image.file_format = "PNG"
    image.save()
    TEXTURES[name] = image
    return image


def height_to_normal(height, width, size, strength):
    pixels = [0.0] * (width * size * 4)
    for y in range(size):
        for x in range(width):
            left = height[y * width + (x - 1) % width]
            right = height[y * width + (x + 1) % width]
            down = height[((y - 1) % size) * width + x]
            up = height[((y + 1) % size) * width + x]
            nx = (left - right) * strength
            ny = (down - up) * strength
            nz = 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            i = (y * width + x) * 4
            pixels[i] = nx / length * 0.5 + 0.5
            pixels[i + 1] = ny / length * 0.5 + 0.5
            pixels[i + 2] = nz / length * 0.5 + 0.5
            pixels[i + 3] = 1.0
    return pixels


def make_color_texture(name, width, base, variation, streak=0.0, grain=0.0):
    height = [0.0] * (width * width)
    pixels = [0.0] * (width * width * 4)
    for y in range(width):
        for x in range(width):
            u = x / width
            v = y / width
            n = fbm(u * 6.0, v * 6.0, 5)
            n2 = fbm(u * 18.0 + 3.0, v * 18.0, 3)
            stripe = 0.5 + 0.5 * math.sin((u * 2.0 + v * 46.0 + n * 2.0) * math.pi)
            g = 0.5 + 0.5 * math.sin((u * 40.0 + n2 * 3.0) * math.pi)
            h = n * 0.75 + n2 * 0.25 + streak * (stripe - 0.5) + grain * (g - 0.5) * 0.35
            height[y * width + x] = h
            tone = (h - 0.5) * 2.0
            color = [base[c] + variation[c] * tone for c in range(3)]
            if streak > 0.2:
                band = 0.5 + 0.5 * math.sin(u * math.pi * 8.0)
                color = [color[c] * (0.75 + 0.35 * band) for c in range(3)]
            i = (y * width + x) * 4
            pixels[i] = min(1.0, max(0.0, color[0]))
            pixels[i + 1] = min(1.0, max(0.0, color[1]))
            pixels[i + 2] = min(1.0, max(0.0, color[2]))
            pixels[i + 3] = 1.0
    save_image(name, width, width, pixels)
    save_normal("N_" + name, width, width, height_to_normal(height, width, width, 3.2))


def build_textures():
    size = 256
    specs = [
        ("T_Laterite", (0.58, 0.30, 0.15), (0.10, 0.06, 0.04), 0.0, 0.15),
        ("T_DryGrass", (0.52, 0.46, 0.22), (0.08, 0.08, 0.04), 0.35, 0.1),
        ("T_Sand", (0.74, 0.64, 0.44), (0.06, 0.05, 0.04), 0.0, 0.2),
        ("T_Thatch", (0.66, 0.48, 0.20), (0.08, 0.06, 0.03), 0.85, 0.05),
        ("T_Wood", (0.38, 0.24, 0.12), (0.07, 0.04, 0.02), 0.55, 0.4),
        ("T_ClothOchre", (0.70, 0.40, 0.16), (0.05, 0.04, 0.03), 0.25, 0.15),
        ("T_ClothDark", (0.18, 0.16, 0.24), (0.04, 0.03, 0.04), 0.2, 0.1),
        ("T_ClothIndigo", (0.16, 0.22, 0.38), (0.03, 0.04, 0.05), 0.2, 0.1),
        ("T_Skin", (0.38, 0.24, 0.16), (0.04, 0.03, 0.02), 0.0, 0.25),
        ("T_Leaf", (0.30, 0.40, 0.14), (0.06, 0.07, 0.03), 0.15, 0.2),
        ("T_Road", (0.48, 0.28, 0.14), (0.08, 0.05, 0.03), 0.1, 0.35),
        ("T_Leather", (0.30, 0.17, 0.09), (0.04, 0.03, 0.02), 0.15, 0.3),
        ("T_Hair", (0.07, 0.05, 0.04), (0.02, 0.015, 0.01), 0.4, 0.2),
        ("T_Stone", (0.48, 0.44, 0.38), (0.06, 0.05, 0.04), 0.0, 0.45),
        ("T_Awning", (0.72, 0.22, 0.12), (0.08, 0.05, 0.03), 0.9, 0.05),
        ("T_Sandal", (0.36, 0.22, 0.11), (0.04, 0.03, 0.02), 0.3, 0.2),
    ]
    for name, base, variation, streak, grain in specs:
        make_color_texture(name, size, base, variation, streak, grain)


def make_material(name, texture_name, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (1, 1, 1, 1)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    image = TEXTURES[texture_name]
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    normal_name = "N_" + texture_name
    if normal_name in TEXTURES:
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = TEXTURES[normal_name]
        try:
            ntex.image.colorspace_settings.name = "Non-Color"
        except TypeError:
            pass
        nmap = nodes.new("ShaderNodeNormalMap")
        nmap.inputs["Strength"].default_value = 0.7
        links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        if "Normal" in bsdf.inputs:
            links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    MATERIALS[name] = mat
    return mat


def build_materials():
    pairs = [
        ("M_Laterite", "T_Laterite", 0.88),
        ("M_DryGrass", "T_DryGrass", 0.9),
        ("M_Sand", "T_Sand", 0.82),
        ("M_Thatch", "T_Thatch", 0.92),
        ("M_Wood", "T_Wood", 0.72),
        ("M_ClothOchre", "T_ClothOchre", 0.78),
        ("M_ClothDark", "T_ClothDark", 0.8),
        ("M_ClothIndigo", "T_ClothIndigo", 0.8),
        ("M_Skin", "T_Skin", 0.55),
        ("M_Leaf", "T_Leaf", 0.7),
        ("M_Road", "T_Road", 0.9),
        ("M_Leather", "T_Leather", 0.58),
        ("M_Hair", "T_Hair", 0.7),
        ("M_Stone", "T_Stone", 0.75),
        ("M_Awning", "T_Awning", 0.62),
        ("M_Sandal", "T_Sandal", 0.7),
        ("M_Eye", "T_Hair", 0.25),
    ]
    for name, tex, rough in pairs:
        make_material(name, tex, rough)


class MeshData:
    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []
        self.mats = []
        self.face_uvs = []

    def add(self, pts, mat, uvs):
        base = len(self.verts)
        for point in pts:
            self.verts.append(Vector(point))
        self.faces.append(tuple(base + i for i in range(len(pts))))
        self.mats.append(mat)
        self.face_uvs.append(uvs)
        return base

    def to_object(self, material_names):
        mesh = bpy.data.meshes.new(self.name + "_mesh")
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for name in material_names:
            mesh.materials.append(MATERIALS[name])
        uv_layer = mesh.uv_layers.new(name="UVMap")
        for poly, mat_index, uvs in zip(mesh.polygons, self.mats, self.face_uvs):
            poly.material_index = mat_index
            poly.use_smooth = True
            for loop_index, uv in zip(poly.loop_indices, uvs):
                uv_layer.data[loop_index].uv = uv
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        return obj


def uv_xz(points, scale):
    return [(p.x * scale, p.z * scale) for p in points]


def uv_yz(points, scale):
    return [(p.y * scale, p.z * scale) for p in points]


def uv_xy(points, scale):
    return [(p.x * scale, p.y * scale) for p in points]


def quad(md, a, b, c, d, mat, uvs):
    md.add((a, b, c, d), mat, uvs)


def box(md, origin, size, mat, uv_scale=0.5, taper=0.0):
    ox, oy, oz = origin
    sx, sy, sz = size
    top = 1.0 - taper
    p = [
        Vector((ox, oy, oz)),
        Vector((ox + sx, oy, oz)),
        Vector((ox + sx, oy + sy, oz)),
        Vector((ox, oy + sy, oz)),
        Vector((ox + sx * (1 - top) * 0.5, oy + sy * (1 - top) * 0.5, oz + sz)),
        Vector((ox + sx - sx * (1 - top) * 0.5, oy + sy * (1 - top) * 0.5, oz + sz)),
        Vector((ox + sx - sx * (1 - top) * 0.5, oy + sy - sy * (1 - top) * 0.5, oz + sz)),
        Vector((ox + sx * (1 - top) * 0.5, oy + sy - sy * (1 - top) * 0.5, oz + sz)),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ]
    uv_sets = [uv_xy, uv_xy, uv_xz, uv_yz, uv_xz, uv_yz]
    for (ia, ib, ic, id_), uv_fn in zip(faces, uv_sets):
        pts = [p[ia], p[ib], p[ic], p[id_]]
        md.add(pts, mat, uv_fn(pts, uv_scale))


def muddy_walls(obj, amount=0.035):
    mesh = obj.data
    for vertex in mesh.vertices:
        if vertex.co.z < 0.05:
            continue
        n = fbm(vertex.co.x * 1.7, vertex.co.y * 1.7 + vertex.co.z, 3) - 0.5
        vertex.co.x += n * amount
        vertex.co.y += (fbm(vertex.co.y * 2.2, vertex.co.z * 1.4, 2) - 0.5) * amount
    mesh.update()


def ring(center, radius, sides, sx=1.0, sy=1.0, z_wave=0.0):
    points = []
    for i in range(sides):
        angle = (i / sides) * math.tau
        wave = math.sin(angle * 5.0) * z_wave
        points.append(
            Vector(
                (
                    center.x + math.cos(angle) * radius * sx,
                    center.y + math.sin(angle) * radius * sy,
                    center.z + wave,
                )
            )
        )
    return points


def loft(md, rings, mat, u_repeat=1.0, v_repeat=1.0):
    sides = len(rings[0])
    steps = len(rings) - 1
    base = len(md.verts)
    for loop in rings:
        for point in loop:
            md.verts.append(Vector(point))
    for step in range(steps):
        for i in range(sides):
            j = (i + 1) % sides
            a = base + step * sides + i
            b = base + step * sides + j
            c = base + (step + 1) * sides + j
            d = base + (step + 1) * sides + i
            u0 = (i / sides) * u_repeat
            u1 = ((i + 1) / sides) * u_repeat
            v0 = (step / max(1, steps)) * v_repeat
            v1 = ((step + 1) / max(1, steps)) * v_repeat
            md.faces.append((a, b, c, d))
            md.mats.append(mat)
            md.face_uvs.append([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])


def tube(md, start, end, r0, r1, mat, sides=8, steps=1, sx=1.0, sy=1.0):
    start = Vector(start)
    end = Vector(end)
    rings = []
    for step in range(steps + 1):
        t = step / max(1, steps)
        center = start.lerp(end, t)
        radius = r0 * (1 - t) + r1 * t
        rings.append(ring(center, radius, sides, sx, sy))
    loft(md, rings, mat, 1.0, 1.0)


def build_hut():
    md = MeshData("SM_HutRound")
    laterite = 0
    thatch = 1
    wood = 2
    sides = 28
    radius = 1.85
    thick = 0.28
    wall_h = 2.25
    door_h = 1.7
    door_angle = 0.42
    levels = [0.0, 0.45, 0.95, 1.4, 1.85, wall_h]

    def loop_at(z, rad):
        points = []
        for i in range(sides):
            angle = (i / sides) * math.tau
            bulge = 0.05 * math.sin((z / wall_h) * math.pi)
            jitter = 1.0 + 0.018 * math.sin(angle * 4.0 + z)
            points.append(
                Vector((math.cos(angle) * (rad + bulge) * jitter, math.sin(angle) * (rad + bulge) * jitter, z))
            )
        return points

    outer = [loop_at(z, radius) for z in levels]
    inner = [loop_at(z, radius - thick) for z in levels]

    def in_door(index, z0, z1):
        angle = ((index + 0.5) / sides) * math.tau
        delta = (angle - (-math.pi / 2) + math.pi) % math.tau - math.pi
        return abs(delta) < door_angle and z1 <= door_h + 0.02 and z0 < door_h

    for level in range(len(levels) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            z0 = levels[level]
            z1 = levels[level + 1]
            if in_door(i, z0, z1):
                continue
            quad(md, outer[level][i], outer[level][j], outer[level + 1][j], outer[level + 1][i], laterite, uv_xz([outer[level][i], outer[level][j], outer[level + 1][j], outer[level + 1][i]], 0.45))
            quad(md, inner[level][j], inner[level][i], inner[level + 1][i], inner[level + 1][j], laterite, uv_xz([inner[level][j], inner[level][i], inner[level + 1][i], inner[level + 1][j]], 0.45))

    # Lip and door jambs.
    top_o = outer[-1]
    top_i = inner[-1]
    for i in range(sides):
        j = (i + 1) % sides
        if in_door(i, wall_h - 0.1, wall_h):
            continue
        quad(md, top_o[i], top_o[j], top_i[j], top_i[i], laterite, uv_xy([top_o[i], top_o[j], top_i[j], top_i[i]], 0.4))

    for level in range(len(levels) - 1):
        if levels[level + 1] > door_h:
            continue
        for edge in range(sides):
            angle = ((edge) / sides) * math.tau
            delta = (angle - (-math.pi / 2) + math.pi) % math.tau - math.pi
            if abs(abs(delta) - door_angle) > (math.tau / sides) * 0.6:
                continue
            quad(
                md,
                outer[level][edge],
                inner[level][edge],
                inner[level + 1][edge],
                outer[level + 1][edge],
                laterite,
                [(0, 0), (0.3, 0), (0.3, 0.5), (0, 0.5)],
            )

    # Door planks, slightly recessed.
    door_center = -math.pi / 2
    for plank in range(-2, 3):
        a0 = door_center + (plank - 0.45) * 0.14
        a1 = door_center + (plank + 0.45) * 0.14
        r = radius - thick + 0.04
        p0 = Vector((math.cos(a0) * r, math.sin(a0) * r, 0.02))
        p1 = Vector((math.cos(a1) * r, math.sin(a1) * r, 0.02))
        p2 = Vector((math.cos(a1) * r, math.sin(a1) * r, door_h - 0.08))
        p3 = Vector((math.cos(a0) * r, math.sin(a0) * r, door_h - 0.08))
        quad(md, p0, p1, p2, p3, wood, [(0, 0), (0.2, 0), (0.2, 1), (0, 1)])

    # Lintel.
    lintel_y = math.sin(door_center) * (radius - 0.05)
    box(md, (-0.55, lintel_y - 0.08, door_h - 0.02), (1.1, 0.16, 0.12), wood, 1.2)

    # Relief band.
    band = []
    band_top = []
    for i in range(sides):
        angle = (i / sides) * math.tau
        band.append(Vector((math.cos(angle) * (radius + 0.06), math.sin(angle) * (radius + 0.06), 1.55)))
        band_top.append(Vector((math.cos(angle) * (radius + 0.02), math.sin(angle) * (radius + 0.02), 1.72)))
    loft(md, [band, band_top], laterite, 2.0, 0.3)

    # Thatch cone, three drooping layers.
    peak = Vector((0, 0, 4.05))
    for layer, rad0, z0, droop in ((0, radius + 0.55, 2.05, 0.16), (1, radius + 0.18, 2.55, 0.1), (2, 0.85, 3.15, 0.05)):
        rings = []
        for step in range(7):
            t = step / 6.0
            radius_l = rad0 * (1 - t) + 0.08 * t
            z = z0 * (1 - t) + peak.z * t - droop * (1 - t)
            rings.append(ring(Vector((0, 0, z)), radius_l, sides, 1, 1, z_wave=0.045 * (1 - t)))
        loft(md, rings, thatch, 3.0, 1.4)
        # Underside lip so the roof has thickness.
        lip = ring(Vector((0, 0, z0 - 0.08)), rad0 * 0.92, sides)
        loft(md, [rings[0], lip], thatch, 2.0, 0.2)

    # Rafters.
    for i in range(8):
        angle = (i / 8.0) * math.tau
        start = Vector((math.cos(angle) * (radius - 0.05), math.sin(angle) * (radius - 0.05), wall_h - 0.05))
        end = Vector((math.cos(angle) * 0.25, math.sin(angle) * 0.25, 3.55))
        tube(md, start, end, 0.035, 0.02, wood, sides=5, steps=1)

    obj = md.to_object(["M_Laterite", "M_Thatch", "M_Wood"])
    MANIFEST["materials"][obj.name] = ["M_Laterite", "M_Thatch", "M_Wood"]
    return obj


def build_house():
    md = MeshData("SM_HouseBanco")
    mud = 0
    wood = 1
    # Main volume, wider at the base. Subdivided by stacked sections so the
    # silhouette is a banco compound, not a single box.
    widths = [5.4, 5.28, 5.16, 5.05]
    depths = [4.15, 4.05, 3.95, 3.86]
    zs = [0.0, 0.8, 1.6, 2.45]
    for i in range(3):
        ox = (5.4 - widths[i]) * 0.5
        oy = (4.15 - depths[i]) * 0.5
        box(md, (ox, oy, zs[i]), (widths[i], depths[i], zs[i + 1] - zs[i]), mud, 0.42)
    # Parapet.
    box(md, (0.15, 0.15, 2.45), (5.1, 0.28, 0.55), mud, 0.5)
    box(md, (0.15, 3.72, 2.45), (5.1, 0.28, 0.55), mud, 0.5)
    box(md, (0.15, 0.15, 2.45), (0.28, 3.85, 0.55), mud, 0.5)
    box(md, (4.97, 0.15, 2.45), (0.28, 3.85, 0.5), mud, 0.5)
    for merlon in range(5):
        box(md, (0.45 + merlon * 0.95, -0.02, 2.95), (0.42, 0.2, 0.28), mud, 0.8)
    # Buttresses.
    for x in (0.15, 4.85):
        box(md, (x, 1.7, 0.0), (0.42, 0.55, 2.2), mud, 0.4, taper=0.18)
    # Recessed door.
    box(md, (2.25, -0.04, 0.0), (0.9, 0.18, 1.85), wood, 1.1)
    box(md, (2.15, -0.08, 1.78), (1.1, 0.22, 0.14), wood, 1.0)
    # Shutters.
    box(md, (0.7, -0.05, 1.25), (0.55, 0.08, 0.6), wood, 1.3)
    box(md, (4.05, -0.05, 1.25), (0.55, 0.08, 0.6), wood, 1.3)
    # Torons — short hardwood beams cast into the mud.
    for row, z in enumerate((0.7, 1.25, 1.8, 2.3)):
        for i in range(7):
            length = 0.28 + 0.08 * math.sin(i * 2.1 + row)
            box(md, (0.45 + i * 0.68, -length, z), (0.07, length + 0.05, 0.07), wood, 2.0)
    # Roof crust.
    box(md, (0.35, 0.35, 2.42), (4.7, 3.45, 0.08), mud, 0.35)
    obj = md.to_object(["M_Laterite", "M_Wood"])
    muddy_walls(obj, 0.028)
    MANIFEST["materials"][obj.name] = ["M_Laterite", "M_Wood"]
    return obj


def build_granary():
    md = MeshData("SM_Granary")
    mud = 0
    thatch = 1
    wood = 2
    stone = 3
    for i, x in enumerate((-0.45, 0.35)):
        for y in (-0.45, 0.35):
            box(md, (x, y, 0.0), (0.22, 0.22, 0.35), stone, 1.5)
    # Stem and bulb.
    rings = []
    profile = [
        (0.28, 0.35),
        (0.22, 0.7),
        (0.55, 1.05),
        (0.85, 1.45),
        (0.78, 1.9),
        (0.4, 2.25),
    ]
    for radius, z in profile:
        rings.append(ring(Vector((0, 0, z)), radius, 16, 1.0, 1.0, 0.01))
    loft(md, rings, mud, 2.0, 1.5)
    cap = []
    for step in range(5):
        t = step / 4.0
        cap.append(ring(Vector((0, 0, 2.2 + t * 0.85)), 0.55 * (1 - t) + 0.05, 14, z_wave=0.03 * (1 - t)))
    loft(md, cap, thatch, 1.5, 1.0)
    tube(md, (-0.15, -0.7, 1.55), (0.15, -0.95, 1.7), 0.08, 0.05, wood, sides=6)
    obj = md.to_object(["M_Laterite", "M_Thatch", "M_Wood", "M_Stone"])
    MANIFEST["materials"][obj.name] = ["M_Laterite", "M_Thatch", "M_Wood", "M_Stone"]
    return obj


def build_well():
    md = MeshData("SM_Well")
    stone = 0
    wood = 1
    rings = []
    for z, rad in ((0.0, 0.72), (0.45, 0.78), (0.85, 0.7)):
        rings.append(ring(Vector((0, 0, z)), rad, 18))
    loft(md, rings, stone, 2.0, 0.6)
    inner = [ring(Vector((0, 0, z)), rad - 0.18, 18) for z, rad in ((0.0, 0.72), (0.85, 0.7))]
    loft(md, inner, stone, 2.0, 0.4)
    loft(md, [rings[-1], inner[-1]], stone, 1.0, 0.2)
    box(md, (-0.08, -0.15, 0.85), (0.16, 0.16, 1.15), wood, 1.4)
    box(md, (-0.55, -0.08, 1.9), (1.1, 0.12, 0.1), wood, 1.2)
    tube(md, (0.15, 0.0, 1.85), (0.15, 0.0, 0.95), 0.015, 0.015, wood, sides=5)
    obj = md.to_object(["M_Stone", "M_Wood"])
    MANIFEST["materials"][obj.name] = ["M_Stone", "M_Wood"]
    return obj


def build_market():
    md = MeshData("SM_MarketStall")
    wood = 0
    cloth = 1
    mud = 2
    posts = [(-1.35, -0.8), (1.35, -0.8), (1.35, 0.8), (-1.35, 0.8)]
    for x, y in posts:
        box(md, (x - 0.06, y - 0.06, 0.0), (0.12, 0.12, 2.15), wood, 1.5)
    box(md, (-1.45, -0.9, 2.05), (2.9, 0.12, 0.1), wood, 0.8)
    box(md, (-1.45, 0.78, 2.05), (2.9, 0.12, 0.1), wood, 0.8)
    box(md, (-1.45, -0.9, 2.05), (0.12, 1.8, 0.1), wood, 0.8)
    box(md, (1.33, -0.9, 2.05), (0.12, 1.8, 0.1), wood, 0.8)
    # Sagging awning.
    cols, rows = 10, 6
    grid = []
    for row in range(rows):
        v = row / (rows - 1)
        line = []
        for col in range(cols):
            u = col / (cols - 1)
            sag = math.sin(u * math.pi) * math.sin(v * math.pi) * 0.28
            line.append(Vector((-1.4 + u * 2.8, -0.85 + v * 1.7, 2.02 - sag)))
        grid.append(line)
    for row in range(rows - 1):
        for col in range(cols - 1):
            a = grid[row][col]
            b = grid[row][col + 1]
            c = grid[row + 1][col + 1]
            d = grid[row + 1][col]
            quad(md, a, b, c, d, cloth, [(col / cols, row / rows), ((col + 1) / cols, row / rows), ((col + 1) / cols, (row + 1) / rows), (col / cols, (row + 1) / rows)])
    # Counter and pots.
    box(md, (-1.1, -0.35, 0.7), (2.2, 0.7, 0.08), wood, 0.7)
    for x in (-1.1, 1.1):
        box(md, (x, -0.15, 0.0), (0.08, 0.08, 0.7), wood, 1.0)
        box(md, (x, 0.25, 0.0), (0.08, 0.08, 0.7), wood, 1.0)
    pot_profile = [(0.16, 0.78), (0.22, 0.95), (0.18, 1.15), (0.1, 1.28)]
    for index, x in enumerate((-0.45, 0.35)):
        rings = [ring(Vector((x, 0.05, z)), radius, 10) for radius, z in pot_profile]
        loft(md, rings, mud, 1.0, 0.6)
    obj = md.to_object(["M_Wood", "M_Awning", "M_Laterite"])
    MANIFEST["materials"][obj.name] = ["M_Wood", "M_Awning", "M_Laterite"]
    return obj


def build_acacia():
    md = MeshData("SM_Acacia")
    wood = 0
    leaf = 1
    # Leaning trunk.
    points = [
        Vector((0, 0, 0)),
        Vector((0.05, 0.02, 0.8)),
        Vector((0.12, 0.0, 1.7)),
        Vector((0.28, -0.05, 2.6)),
        Vector((0.45, 0.05, 3.5)),
    ]
    radii = [0.22, 0.18, 0.14, 0.1, 0.07]
    for i in range(len(points) - 1):
        tube(md, points[i], points[i + 1], radii[i], radii[i + 1], wood, sides=7, steps=1)
    branches = [
        (Vector((0.4, 0.05, 3.45)), Vector((2.4, 0.4, 3.7)), 0.05, 0.02),
        (Vector((0.4, 0.05, 3.5)), Vector((-1.8, 0.6, 3.85)), 0.045, 0.018),
        (Vector((0.35, 0.0, 3.4)), Vector((0.6, 2.1, 3.75)), 0.04, 0.016),
        (Vector((0.35, 0.0, 3.4)), Vector((0.2, -1.9, 3.65)), 0.04, 0.016),
        (Vector((0.42, 0.02, 3.48)), Vector((1.6, -1.2, 3.8)), 0.035, 0.014),
        (Vector((0.42, 0.02, 3.48)), Vector((-1.2, -1.3, 3.9)), 0.035, 0.014),
    ]
    for start, end, r0, r1 in branches:
        tube(md, start, end, r0, r1, wood, sides=5, steps=1)
        # Flat canopy pads.
        canopy = end + Vector((0, 0, 0.05))
        pad = ring(canopy, 0.85, 8, 1.3, 0.7)
        top = ring(canopy + Vector((0, 0, 0.18)), 0.45, 8, 1.1, 0.6)
        loft(md, [pad, top], leaf, 1.0, 0.4)
    obj = md.to_object(["M_Wood", "M_Leaf"])
    MANIFEST["materials"][obj.name] = ["M_Wood", "M_Leaf"]
    return obj


def build_baobab():
    md = MeshData("SM_Baobab")
    wood = 0
    leaf = 1
    profile = [
        (1.35, 0.0),
        (1.55, 0.6),
        (1.45, 1.6),
        (1.15, 3.0),
        (0.72, 4.6),
        (0.42, 6.2),
        (0.28, 7.4),
    ]
    rings = []
    for radius, z in profile:
        loop = []
        for i in range(18):
            angle = (i / 18) * math.tau
            bulge = 1.0 + 0.06 * math.sin(angle * 3.0 + z) + 0.03 * math.sin(angle * 7.0)
            loop.append(Vector((math.cos(angle) * radius * bulge, math.sin(angle) * radius * bulge * 0.92, z)))
        rings.append(loop)
    loft(md, rings, wood, 2.0, 2.5)
    branches = [
        (Vector((0.1, 0.05, 6.6)), Vector((2.3, 0.4, 7.6))),
        (Vector((-0.1, 0.0, 6.7)), Vector((-2.1, 0.5, 7.8))),
        (Vector((0.0, 0.1, 6.8)), Vector((0.4, 2.0, 7.5))),
        (Vector((0.0, -0.05, 6.5)), Vector((0.3, -1.8, 7.3))),
        (Vector((0.15, 0.0, 6.9)), Vector((1.4, -1.2, 8.0))),
        (Vector((-0.12, 0.05, 6.9)), Vector((-1.3, -1.1, 7.9))),
    ]
    for start, end in branches:
        tube(md, start, end, 0.16, 0.06, wood, sides=6, steps=2)
        puff = ring(end, 0.55, 8, 1.2, 0.8)
        puff_top = ring(end + Vector((0, 0, 0.35)), 0.22, 8)
        loft(md, [puff, puff_top], leaf, 1.0, 0.5)
    obj = md.to_object(["M_Wood", "M_Leaf"])
    MANIFEST["materials"][obj.name] = ["M_Wood", "M_Leaf"]
    return obj


def build_road():
    md = MeshData("SM_RoadSegment")
    cols, rows = 18, 10
    length = 8.0
    width = 5.6
    grid = []
    for row in range(rows):
        v = row / (rows - 1)
        line = []
        for col in range(cols):
            u = col / (cols - 1)
            x = (u - 0.5) * width
            z = (v - 0.5) * length
            rut = math.exp(-((abs(x) - 0.85) ** 2) / 0.08) * 0.07
            edge = max(0.0, abs(x) - 2.2) * 0.08
            noise = (fbm(u * 5.0, v * 4.0, 3) - 0.5) * 0.04
            y = -rut + edge + noise
            line.append(Vector((x, z, y)))
        grid.append(line)
    road = 0
    for row in range(rows - 1):
        for col in range(cols - 1):
            a = grid[row][col]
            b = grid[row][col + 1]
            c = grid[row + 1][col + 1]
            d = grid[row + 1][col]
            quad(md, a, b, c, d, road, [(a.x * 0.2, a.y * 0.2), (b.x * 0.2, b.y * 0.2), (c.x * 0.2, c.y * 0.2), (d.x * 0.2, d.y * 0.2)])
            # Thin underside so the mesh has a side at the shoulders.
    # Shoulder berms.
    for side in (-1, 1):
        berm = []
        berm_top = []
        for row in range(rows):
            z = (row / (rows - 1) - 0.5) * length
            x = side * 2.55
            berm.append(Vector((x, z, 0.0)))
            berm_top.append(Vector((x + side * 0.35, z, 0.12 + 0.02 * math.sin(row))))
        loft(md, [berm, berm_top], road, 2.0, 0.3)
    obj = md.to_object(["M_Road"])
    MANIFEST["materials"][obj.name] = ["M_Road"]
    return obj


def build_wall():
    md = MeshData("SM_CompoundWall")
    mud = 0
    sections = [0.0, 0.6, 1.2, 1.8, 2.15]
    for i in range(len(sections) - 1):
        taper = 0.04 * i
        box(md, (taper, taper, sections[i]), (6.0 - taper * 2, 0.42 - taper, sections[i + 1] - sections[i]), mud, 0.45)
    for x in (0.3, 2.8, 5.3):
        box(md, (x, -0.08, 0.0), (0.38, 0.58, 1.7), mud, 0.4, taper=0.12)
    obj = md.to_object(["M_Laterite"])
    muddy_walls(obj, 0.02)
    MANIFEST["materials"][obj.name] = ["M_Laterite"]
    return obj


def build_pot():
    md = MeshData("SM_ClayPot")
    profile = [(0.12, 0.0), (0.16, 0.08), (0.2, 0.22), (0.16, 0.38), (0.09, 0.48)]
    rings = [ring(Vector((0, 0, z)), radius, 12) for radius, z in profile]
    loft(md, rings, 0, 1.0, 1.0)
    obj = md.to_object(["M_Laterite"])
    MANIFEST["materials"][obj.name] = ["M_Laterite"]
    return obj


def build_rock():
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.55, location=(0, 0, 0.28))
    obj = bpy.context.active_object
    obj.name = "SM_Rock"
    mesh = obj.data
    for vertex in mesh.vertices:
        vertex.co.z *= 0.62
        push = (fbm(vertex.co.x * 3.0, vertex.co.y * 3.0, 3) - 0.35) * 0.22
        vertex.co += vertex.co.normalized() * push
    mesh.materials.append(MATERIALS["M_Stone"])
    for poly in mesh.polygons:
        poly.use_smooth = True
    MANIFEST["materials"][obj.name] = ["M_Stone"]
    return obj


def build_grass():
    md = MeshData("SM_GrassTuft")
    for i in range(7):
        angle = (i / 7.0) * math.tau
        lean = Vector((math.cos(angle) * 0.18, math.sin(angle) * 0.18, 0.42))
        base = Vector((math.cos(angle) * 0.05, math.sin(angle) * 0.05, 0))
        side = Vector((-math.sin(angle) * 0.03, math.cos(angle) * 0.03, 0))
        a = base - side
        b = base + side
        c = base + lean + side * 0.3
        d = base + lean - side * 0.3
        quad(md, a, b, c, d, 0, [(0, 0), (1, 0), (1, 1), (0, 1)])
    obj = md.to_object(["M_DryGrass"])
    MANIFEST["materials"][obj.name] = ["M_DryGrass"]
    return obj


def export_objects(objects, filename, anim=False):
    view_layer = bpy.context.view_layer
    view_layer.objects.active = objects[0]
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = os.path.join(MOD_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "ARMATURE"},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=anim,
        bake_anim_use_all_actions=anim,
        bake_anim_use_nla_strips=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )
    MANIFEST["files"].append(filename)
    print("EXPORTED", filename, os.path.getsize(path))


def hide(obj):
    obj.hide_set(True)
    obj.hide_render = True


# --- Character ----------------------------------------------------------------

def build_armature():
    arm_data = bpy.data.armatures.new("SK_Explorer_Rig")
    arm = bpy.data.objects.new("SK_Explorer_Rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    spec = [
        ("Hips", (0, 0, 0.96), (0, 0, 1.08), None),
        ("Spine", (0, 0, 1.08), (0, 0, 1.28), "Hips"),
        ("Chest", (0, 0, 1.28), (0, 0, 1.48), "Spine"),
        ("Neck", (0, 0, 1.48), (0, 0, 1.56), "Chest"),
        ("Head", (0, 0, 1.56), (0, 0, 1.78), "Neck"),
        ("LeftShoulder", (0.05, 0, 1.44), (0.16, 0, 1.44), "Chest"),
        ("LeftUpperArm", (0.16, 0, 1.44), (0.40, 0.02, 1.18), "LeftShoulder"),
        ("LeftLowerArm", (0.40, 0.02, 1.18), (0.58, 0.04, 0.94), "LeftUpperArm"),
        ("LeftHand", (0.58, 0.04, 0.94), (0.67, 0.05, 0.84), "LeftLowerArm"),
        ("RightShoulder", (-0.05, 0, 1.44), (-0.16, 0, 1.44), "Chest"),
        ("RightUpperArm", (-0.16, 0, 1.44), (-0.40, 0.02, 1.18), "RightShoulder"),
        ("RightLowerArm", (-0.40, 0.02, 1.18), (-0.58, 0.04, 0.94), "RightUpperArm"),
        ("RightHand", (-0.58, 0.04, 0.94), (-0.67, 0.05, 0.84), "RightLowerArm"),
        ("LeftUpperLeg", (0.09, 0, 0.94), (0.10, 0.01, 0.50), "Hips"),
        ("LeftLowerLeg", (0.10, 0.01, 0.50), (0.10, 0.0, 0.08), "LeftUpperLeg"),
        ("LeftFoot", (0.10, 0.0, 0.08), (0.10, 0.16, 0.03), "LeftLowerLeg"),
        ("RightUpperLeg", (-0.09, 0, 0.94), (-0.10, 0.01, 0.50), "Hips"),
        ("RightLowerLeg", (-0.10, 0.01, 0.50), (-0.10, 0.0, 0.08), "RightUpperLeg"),
        ("RightFoot", (-0.10, 0.0, 0.08), (-0.10, 0.16, 0.03), "RightLowerLeg"),
    ]
    bones = {}
    for name, head, tail, parent in spec:
        bone = arm_data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.roll = 0.0
        if parent:
            bone.parent = bones[parent]
            bone.use_connect = parent not in ("Chest",) or "Shoulder" not in name
            if "Shoulder" in name or "UpperLeg" in name:
                bone.use_connect = False
        bones[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def ellipse_ring(center, rx, ry, sides=12):
    return ring(Vector(center), 1.0, sides, rx, ry)


def build_character_meshes(arm):
    body = MeshData("SK_Body")
    clothes = MeshData("SK_Clothes")
    gear = MeshData("SK_Gear")

    # Head.
    head_profile = [
        (0.055, 0.07, 1.56),
        (0.075, 0.086, 1.60),
        (0.086, 0.098, 1.66),
        (0.09, 0.10, 1.71),
        (0.078, 0.09, 1.76),
        (0.04, 0.045, 1.80),
    ]
    head_rings = []
    for rx, ry, z in head_profile:
        loop = ellipse_ring((0, 0.01, z), rx, ry, 14)
        for vertex in loop:
            if vertex.y > 0.04 and 1.62 < vertex.z < 1.72:
                vertex.y += 0.02  # nose
        head_rings.append(loop)
    loft(body, head_rings, 0, 1.0, 1.0)
    tube(body, (0, 0, 1.48), (0, 0.01, 1.58), 0.055, 0.06, 0, sides=10)

    # Forearms and hands (sleeves stop near the elbow).
    for sign in (1, -1):
        tube(body, (sign * 0.40, 0.02, 1.18), (sign * 0.58, 0.04, 0.94), 0.045, 0.038, 0, sides=8, steps=2)
        tube(body, (sign * 0.58, 0.04, 0.94), (sign * 0.68, 0.06, 0.84), 0.04, 0.028, 0, sides=6)
        # Feet.
        tube(body, (sign * 0.10, 0.0, 0.08), (sign * 0.10, 0.12, 0.04), 0.04, 0.035, 0, sides=6)

    # Tunic with a slight flare.
    tunic_rings = [
        ellipse_ring((0, 0.02, 0.78), 0.16, 0.12, 16),
        ellipse_ring((0, 0.015, 1.05), 0.18, 0.13, 16),
        ellipse_ring((0, 0.01, 1.28), 0.20, 0.14, 16),
        ellipse_ring((0, 0.0, 1.48), 0.22, 0.15, 16),
    ]
    loft(clothes, tunic_rings, 0, 1.0, 1.2)
    for sign in (1, -1):
        tube(clothes, (sign * 0.16, 0.0, 1.44), (sign * 0.40, 0.02, 1.18), 0.07, 0.055, 0, sides=8, steps=2)
        # Pants.
        tube(clothes, (sign * 0.09, 0.0, 0.94), (sign * 0.10, 0.01, 0.50), 0.09, 0.07, 1, sides=8, steps=2)
        tube(clothes, (sign * 0.10, 0.01, 0.50), (sign * 0.10, 0.0, 0.08), 0.065, 0.05, 1, sides=8, steps=2)

    # Hair cap.
    hair = [
        ellipse_ring((0, 0.0, 1.70), 0.092, 0.10, 12),
        ellipse_ring((0, -0.005, 1.78), 0.07, 0.075, 12),
        ellipse_ring((0, 0.0, 1.82), 0.03, 0.03, 12),
    ]
    loft(gear, hair, 0, 1.0, 0.6)
    # Backpack.
    box(gear, (-0.13, -0.22, 1.12), (0.26, 0.12, 0.32), 1, 1.6)
    box(gear, (-0.11, -0.2, 1.38), (0.22, 0.08, 0.08), 1, 1.4)
    # Straps.
    for sign in (1, -1):
        tube(gear, (sign * 0.08, -0.02, 1.46), (sign * 0.08, -0.16, 1.18), 0.015, 0.015, 1, sides=5)
    # Sandals.
    for sign in (1, -1):
        box(gear, (sign * 0.10 - 0.045, -0.02, 0.015), (0.09, 0.2, 0.018), 2, 2.0)

    # Eyes.
    eyes = MeshData("SK_Eyes")
    for sign in (1, -1):
        tube(eyes, (sign * 0.035, 0.075, 1.70), (sign * 0.035, 0.09, 1.70), 0.012, 0.012, 0, sides=6)
    eye_obj = eyes.to_object(["M_Eye"])

    body_obj = body.to_object(["M_Skin"])
    cloth_obj = clothes.to_object(["M_ClothOchre", "M_ClothDark"])
    gear_obj = gear.to_object(["M_Hair", "M_Leather", "M_Sandal"])
    for obj in (body_obj, cloth_obj, gear_obj, eye_obj):
        bind_nearest(obj, arm)
        modifier = obj.modifiers.new("Armature", "ARMATURE")
        modifier.object = arm
        obj.parent = arm
    MANIFEST["materials"]["SK_Explorer"] = ["M_Skin", "M_ClothOchre", "M_ClothDark", "M_Hair", "M_Leather", "M_Sandal", "M_Eye"]
    return [arm, body_obj, cloth_obj, gear_obj, eye_obj]


def bone_segments(arm):
    segments = {}
    for bone in arm.data.bones:
        segments[bone.name] = (Vector(bone.head_local), Vector(bone.tail_local))
    return segments


def point_segment_distance(point, start, end):
    axis = end - start
    length_sq = axis.length_squared
    if length_sq < 1e-8:
        return (point - start).length
    t = max(0.0, min(1.0, (point - start).dot(axis) / length_sq))
    closest = start + axis * t
    return (point - closest).length


def bind_nearest(obj, arm):
    segments = bone_segments(arm)
    groups = {name: obj.vertex_groups.new(name=name) for name in segments}
    for vertex in obj.data.vertices:
        ranked = []
        for name, (start, end) in segments.items():
            ranked.append((point_segment_distance(vertex.co, start, end), name))
        ranked.sort()
        chosen = ranked[:3]
        weights = []
        for distance, name in chosen:
            weights.append((name, 1.0 / (distance * distance + 0.0006)))
        total = sum(weight for _, weight in weights)
        for name, weight in weights:
            groups[name].add([vertex.index], weight / total, "REPLACE")


def axis_that_moves(arm, bone_name, direction):
    pose = arm.pose.bones[bone_name]
    pose.rotation_mode = "XYZ"
    pose.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    rest = pose.tail.copy()
    best = None
    for axis in range(3):
        for sign in (1, -1):
            euler = [0.0, 0.0, 0.0]
            euler[axis] = sign * math.radians(35)
            pose.rotation_euler = euler
            bpy.context.view_layer.update()
            score = (pose.tail - rest).dot(direction) - abs((pose.tail - rest).x) * 0.15
            if best is None or score > best[0]:
                best = (score, axis, sign)
    pose.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    return best[1], best[2]


def location_axis(arm, bone_name, direction):
    pose = arm.pose.bones[bone_name]
    pose.location = (0, 0, 0)
    bpy.context.view_layer.update()
    rest = pose.matrix.translation.copy()
    best = None
    for axis in range(3):
        for sign in (1, -1):
            loc = [0.0, 0.0, 0.0]
            loc[axis] = sign * 0.1
            pose.location = loc
            bpy.context.view_layer.update()
            score = (pose.matrix.translation - rest).dot(direction)
            if best is None or score > best[0]:
                best = (score, axis, sign)
    pose.location = (0, 0, 0)
    return best[1], best[2]


def clear_pose(arm):
    for pose in arm.pose.bones:
        pose.location = (0, 0, 0)
        pose.rotation_mode = "XYZ"
        pose.rotation_euler = (0, 0, 0)
        pose.scale = (1, 1, 1)


def set_euler(pose, axis, degrees):
    euler = [0.0, 0.0, 0.0]
    euler[axis[0]] = math.radians(degrees) * axis[1]
    pose.rotation_euler = euler


def key_pose(arm, frame):
    for pose in arm.pose.bones:
        pose.keyframe_insert("rotation_euler", frame=frame)
        pose.keyframe_insert("location", frame=frame)


def begin_action(arm, name, start, end):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = start
    action.frame_end = end
    arm.animation_data_create()
    arm.animation_data.action = action
    if arm.animation_data.action_slot is None and len(action.slots):
        arm.animation_data.action_slot = action.slots[0]
    clear_pose(arm)
    return action


def animate_character(arm):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for pose in arm.pose.bones:
        pose.rotation_mode = "XYZ"
    forward = Vector((0, 1, 0))
    up = Vector((0, 0, 1))
    leg_l = axis_that_moves(arm, "LeftUpperLeg", forward)
    leg_r = axis_that_moves(arm, "RightUpperLeg", forward)
    arm_l = axis_that_moves(arm, "LeftUpperArm", forward)
    arm_r = axis_that_moves(arm, "RightUpperArm", forward)
    knee_l = axis_that_moves(arm, "LeftLowerLeg", Vector((0, -1, 0)))
    knee_r = axis_that_moves(arm, "RightLowerLeg", Vector((0, -1, 0)))
    elbow_l = axis_that_moves(arm, "LeftLowerArm", Vector((0, -1, 0)))
    elbow_r = axis_that_moves(arm, "RightLowerArm", Vector((0, -1, 0)))
    bob = location_axis(arm, "Hips", up)
    print("CALIB", leg_l, knee_l, arm_l, elbow_l, bob)

    def apply_cycle(frame, swing, knee, arm_swing, elbow, bob_amount, phase):
        # phase 0: left leg forward. phase 0.5: right leg forward.
        left = math.sin(phase * math.tau)
        right = -left
        passing = abs(math.cos(phase * math.tau))
        clear_pose(arm)
        set_euler(arm.pose.bones["LeftUpperLeg"], leg_l, swing * left)
        set_euler(arm.pose.bones["RightUpperLeg"], leg_r, swing * right)
        set_euler(arm.pose.bones["LeftLowerLeg"], knee_l, knee * max(0.0, -left) + knee * 0.35 * passing)
        set_euler(arm.pose.bones["RightLowerLeg"], knee_r, knee * max(0.0, -right) + knee * 0.35 * passing)
        set_euler(arm.pose.bones["LeftUpperArm"], arm_l, arm_swing * right)
        set_euler(arm.pose.bones["RightUpperArm"], arm_r, arm_swing * left)
        set_euler(arm.pose.bones["LeftLowerArm"], elbow_l, elbow)
        set_euler(arm.pose.bones["RightLowerArm"], elbow_r, elbow)
        set_euler(arm.pose.bones["Spine"], leg_l, 4.0 * left)
        loc = [0.0, 0.0, 0.0]
        loc[bob[0]] = bob[1] * bob_amount * (0.35 + 0.65 * passing)
        arm.pose.bones["Hips"].location = loc
        key_pose(arm, frame)

    begin_action(arm, "Idle", 1, 48)
    for frame in (1, 16, 32, 48):
        clear_pose(arm)
        breath = math.sin((frame / 48.0) * math.tau)
        set_euler(arm.pose.bones["Chest"], arm_l, 1.5 * breath)
        set_euler(arm.pose.bones["LeftUpperArm"], arm_l, 2.0 * breath)
        set_euler(arm.pose.bones["RightUpperArm"], arm_r, -2.0 * breath)
        loc = [0.0, 0.0, 0.0]
        loc[bob[0]] = bob[1] * 0.008 * breath
        arm.pose.bones["Hips"].location = loc
        key_pose(arm, frame)

    begin_action(arm, "Walk", 1, 32)
    for frame, phase in ((1, 0.0), (9, 0.25), (17, 0.5), (25, 0.75), (33, 1.0)):
        apply_cycle(frame, 26.0, 32.0, 18.0, 12.0, 0.025, phase)
    arm.animation_data.action.frame_end = 32

    begin_action(arm, "Run", 1, 24)
    for frame, phase in ((1, 0.0), (7, 0.25), (13, 0.5), (19, 0.75), (25, 1.0)):
        apply_cycle(frame, 40.0, 48.0, 32.0, 22.0, 0.045, phase)
    arm.animation_data.action.frame_end = 24
    bpy.ops.object.mode_set(mode="OBJECT")


def render_preview(character_root):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.image_settings.file_format = "PNG"
    if hasattr(scene, "eevee"):
        for attr in ("taa_render_samples", "taa_samples"):
            if hasattr(scene.eevee, attr):
                setattr(scene.eevee, attr, 8)

    world = bpy.data.worlds.new("SahelPreview")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background:
        background.inputs[0].default_value = (0.62, 0.72, 0.82, 1)
        background.inputs[1].default_value = 0.9

    bpy.ops.object.light_add(type="SUN", location=(0, 0, 10))
    sun = bpy.context.active_object
    sun.data.energy = 3.5
    sun.data.color = (1.0, 0.9, 0.75)
    sun.rotation_euler = (math.radians(52), math.radians(8), math.radians(30))

    ground = MeshData("PreviewGround")
    ground.add(
        [(-18, -18, -0.02), (18, -18, -0.02), (18, 18, -0.02), (-18, 18, -0.02)],
        0,
        [(0, 0), (4, 0), (4, 4), (0, 4)],
    )
    ground_obj = ground.to_object(["M_Laterite"])

    bpy.ops.object.camera_add(location=(11, -13, 6.5))
    camera = bpy.context.active_object
    camera.rotation_euler = (math.radians(68), 0, math.radians(42))
    scene.camera = camera
    scene.frame_set(1)
    scene.render.filepath = os.path.join(PREVIEW, "village.png")
    bpy.ops.render.render(write_still=True)

    # Character close-up on the walk pose.
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH" and obj.name.startswith("SM_"):
            obj.hide_render = True
    ground_obj.hide_render = True
    scene.frame_set(9)
    camera.location = (2.2, -2.4, 1.3)
    camera.rotation_euler = (math.radians(82), 0, math.radians(48))
    scene.render.filepath = os.path.join(PREVIEW, "explorer_walk.png")
    bpy.ops.render.render(write_still=True)
    print("PREVIEWS", PREVIEW)


def main():
    reset_scene()
    print("TEXTURES")
    build_textures()
    build_materials()
    print("ENVIRONMENT")
    hut = build_hut()
    export_objects([hut], "SM_HutRound.fbx")
    hide(hut)
    house = build_house()
    export_objects([house], "SM_HouseBanco.fbx")
    hide(house)
    granary = build_granary()
    export_objects([granary], "SM_Granary.fbx")
    hide(granary)
    well = build_well()
    export_objects([well], "SM_Well.fbx")
    hide(well)
    market = build_market()
    export_objects([market], "SM_MarketStall.fbx")
    hide(market)
    acacia = build_acacia()
    export_objects([acacia], "SM_Acacia.fbx")
    hide(acacia)
    baobab = build_baobab()
    export_objects([baobab], "SM_Baobab.fbx")
    hide(baobab)
    road = build_road()
    export_objects([road], "SM_RoadSegment.fbx")
    hide(road)
    wall = build_wall()
    export_objects([wall], "SM_CompoundWall.fbx")
    hide(wall)
    pot = build_pot()
    export_objects([pot], "SM_ClayPot.fbx")
    hide(pot)
    rock = build_rock()
    export_objects([rock], "SM_Rock.fbx")
    hide(rock)
    grass = build_grass()
    export_objects([grass], "SM_GrassTuft.fbx")
    hide(grass)

    print("CHARACTER")
    arm = build_armature()
    character = build_character_meshes(arm)
    animate_character(arm)
    export_objects(character, "SK_Explorer.fbx", anim=True)

    # Unhide a small set for the preview render.
    for obj, loc in (
        (hut, (-4.2, 1.5, 0)),
        (house, (3.5, 2.0, 0)),
        (granary, (-2.2, 4.5, 0)),
        (market, (0.2, -1.5, 0)),
        (acacia, (6.5, -2.0, 0)),
        (baobab, (-6.0, 6.0, 0)),
        (road, (0, -6, 0)),
        (well, (2.4, 5.2, 0)),
    ):
        obj.hide_set(False)
        obj.hide_render = False
        obj.location = loc
    arm.location = (0, -4.5, 0)
    render_preview(arm)

    manifest_path = os.path.join(ROOT, "manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(MANIFEST, handle, indent=2)
    print("DONE", manifest_path)


if __name__ == "__main__":
    main()
