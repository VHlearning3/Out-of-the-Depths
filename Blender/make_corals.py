# Out of the Depths - coral builder for Blender (tested in 4.2 and 5.0).
#
# Run it: Blender > Scripting tab > Open > this file > Run Script (or Alt+P in the text editor).
# It builds the four corals from the concept art side by side on the X axis, ready for the game:
#
#   Coral_Polyp  - a bouquet of thin teal stems fanning up from one foot, each ending in a flared cup: a fat
#                  scalloped yellow ring round a cyan middle with a mouth (placed so no cup touches another cup or stem)
#   Coral_Tube   - a mound of blue tubes with uneven, wavy lips, near-black inside, the middle ones tallest
#   Coral_Bush   - a chunky red bush: stout branches splitting into fat upturned lobes, widest at the top
#   Coral_Finger - like the bush but taller and more upright: magenta fingers with rounded tips
#
# How: each coral is first built in detail (a few tens of thousands of faces, with its colours, a gradient darker to
# the bottom, a slow wobble so nothing is perfectly round, and on the bush and fingers a real coral surface of packed
# polyp cups). Then a light copy is made for the game (TRIANGLES: about 2.5-4.5k triangles each), unwrapped, and the
# detailed one is baked onto it (Cycles): its colours into <name>_Color and its surface detail into <name>_Normal, on
# one matte material, M_<name>. The detailed one is then deleted (KEEP_DETAILED keeps it, hidden). So the game gets
# cheap meshes that still look detailed.
#
# Run it again to rebuild them (the old ones are replaced). Change SEED for another version of every shape, or the
# settings in each coral's function for just that one. Set EXPORT_DIR to also write one .fbx per coral plus its two
# .png textures, set up for Unity (Y up, scale 1): drop the folder into Assets, set each _Normal texture's Texture
# Type to Normal map (Unity offers "Fix now"), and keep the materials' Smoothness low (about 0.1) so they stay matte.


import bpy
import bmesh
import math
import os
import random
from mathutils import Vector, Matrix, noise

SEED = 7
DETAIL = 2             # how fine the detailed versions are (1 = coarser and quicker, 2 = finer)
SPACING = 2.5          # metres between the corals in the scene
EXPORT_DIR = ""        # e.g. r"C:\Users\you\...\Out-of-the-Depths\Assets\Art\Models\environment\corals"; "" = no export
BOTTOM_SHADE = 0.3     # how dark the bottom of each coral is (share of its colour: 0 = black, 1 = no gradient)
TIP_LIGHT = 0.12       # how much lighter the very tips get (0 = not at all)
TRIANGLES = {"Coral_Polyp": 4500, "Coral_Tube": 2600, "Coral_Bush": 2400, "Coral_Finger": 2400}   # the game meshes' size
TEXTURE_SIZE = 1024     # the baked colour and normal textures (pixels across)
KEEP_DETAILED = False  # keep the detailed versions too (hidden, as <name>_High), e.g. to bake again

NAMES = ["Coral_Polyp", "Coral_Tube", "Coral_Bush", "Coral_Finger"]

# The concept art's colours as they look on screen (sRGB, like a colour picker), the upper part of each gradient.
TEAL = (0.16, 0.7, 0.66)
YELLOW = (1.0, 0.86, 0.16)
CYAN = (0.3, 0.88, 0.85)
BLUE = (0.18, 0.28, 0.72)
TUBE_INSIDE = (0.03, 0.04, 0.12)
RED = (0.8, 0.2, 0.11)
MAGENTA = (0.78, 0.08, 0.78)


# ---------------------------------------------------------------------------------------------------------------
# helpers

def clear_old():
    for name in NAMES + [n + "_High" for n in NAMES] + ["Coral_Branch", "Coral_Brain", "Coral_Fan"]:   # (and the first version's)
        ob = bpy.data.objects.get(name)
        if ob is not None:
            mesh = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if mesh is not None and mesh.users == 0:
                bpy.data.meshes.remove(mesh)


def collection():
    col = bpy.data.collections.get("Corals")
    if col is None:
        col = bpy.data.collections.new("Corals")
        bpy.context.scene.collection.children.link(col)
    return col


def make_active(ob):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)


def apply_modifiers(ob):
    make_active(ob)
    for mod in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def subdivide(ob, levels=1):
    if levels <= 0:
        return
    sub = ob.modifiers.new("Subdivision", 'SUBSURF')
    sub.levels = levels
    sub.render_levels = levels
    apply_modifiers(ob)


def new_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    collection().objects.link(ob)
    return ob


def perpendicular(d, rng):
    # A random direction square to d.
    while True:
        r = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1)))
        p = d.cross(r)
        if p.length > 1e-3:
            return p.normalized()


def turned_to(direction):
    # A rotation that turns +Z to `direction`.
    return Vector((0, 0, 1)).rotation_difference(direction.normalized()).to_matrix().to_4x4()


def surface(ob, rng, wobble=0.0, wobble_scale=3.0, bumps=0.0, bump_size=0.05, skip_materials=()):
    # Pushes the surface in and out along its normals: a slow `wobble` (metres, over `wobble_scale` bumps a metre)
    # so nothing is perfectly round or straight, and small rounded `bumps` (metres high, `bump_size` apart: the
    # polyps). Faces of `skip_materials` keep their shape.
    mesh = ob.data
    mesh.update()
    skip = set()
    if skip_materials:
        for poly in mesh.polygons:
            if poly.material_index in skip_materials:
                skip.update(poly.vertices)
    shift = Vector((rng.uniform(0, 100), rng.uniform(0, 100), rng.uniform(0, 100)))
    moves = []
    for v in mesh.vertices:
        if v.index in skip:
            moves.append(None)
            continue
        d = 0.0
        if wobble:
            d += wobble * noise.noise(v.co * wobble_scale + shift)
        if bumps:
            distances, _ = noise.voronoi(v.co / bump_size + shift, distance_metric='DISTANCE')
            h = max(0.0, 1.0 - distances[0] * 1.15)
            d += bumps * h * h * (3 - 2 * h)
        moves.append(v.normal * d)
    for v, move in zip(mesh.vertices, moves):
        if move is not None:
            v.co += move
    mesh.update()


def keep_largest(ob, share=0.02):
    # Drops loose bits (less than `share` of the mesh) floating apart from the rest.
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    seen = set()
    islands = []
    for start in bm.verts:
        if start in seen:
            continue
        island, todo = [], [start]
        seen.add(start)
        while todo:
            v = todo.pop()
            island.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w not in seen:
                    seen.add(w)
                    todo.append(w)
        islands.append(island)
    total = len(bm.verts)
    loose = [v for island in islands if len(island) < total * share for v in island]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(ob.data)
    bm.free()


def corallites(ob, rng, size=0.045, depth=0.01):
    # A real coral's skin: packed polyp cups (corallites), each a small crater with a raised rim and a pit in the
    # middle, `size` apart (metres) and `depth` deep, with shallow grooves between the cups. Wants a fine mesh (a
    # few vertices across each cup).
    mesh = ob.data
    mesh.update()
    shift = Vector((rng.uniform(0, 100), rng.uniform(0, 100), rng.uniform(0, 100)))
    moves = []
    for v in mesh.vertices:
        distances, _ = noise.voronoi(v.co / size + shift, distance_metric='DISTANCE')
        f1, f2 = distances[0], distances[1]
        pit = max(0.0, 1.0 - f1 / 0.3)
        pit = pit * pit * (3 - 2 * pit)                                # the hollow in the middle of the cup
        rim = math.exp(-((f1 - 0.38) / 0.12) ** 2)                     # the raised ring round it
        groove = max(0.0, 1.0 - (f2 - f1) / 0.12)                      # between neighbouring cups
        moves.append(v.normal * (depth * (0.7 * rim - 1.0 * pit - 0.35 * groove)))
    for v, move in zip(mesh.vertices, moves):
        v.co += move
    mesh.update()


class Skeleton:
    # Points (with a thickness each) joined by lines, wrapped in tubes by the Skin modifier.
    def __init__(self):
        self.points = []
        self.radii = []
        self.lines = []
        self.tips = []      # (position, direction, radius) at the end of every last branch

    def add(self, position, radius, parent=None):
        self.points.append(position.copy())
        self.radii.append(radius)
        index = len(self.points) - 1
        if parent is not None:
            self.lines.append((parent, index))
        return index

    def grow(self, rng, parent, position, direction, length, radius, depth, spread, up_pull, wobble=0.25,
             children=(2, 3), shrink=(0.6, 0.8), step=0.06, tip=0.6, lump=0.0):
        # One branch from the point `parent`, `length` long, thinning to `tip` of its radius at the very ends (to
        # 0.85 where it forks), lumpy by `lump`; then 2-3 smaller branches from its end, `spread` degrees off,
        # down to `depth` more levels.
        steps = max(2, int(length / step))
        segment = length / steps
        d = direction.normalized()
        index = parent
        at = position.copy()
        r = radius
        for s in range(steps):
            bend = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * wobble
            d = (d + bend * 0.35 + Vector((0, 0, up_pull))).normalized()
            at = at + d * segment
            k = (s + 1) / steps
            r = radius * (1 - (1 - (tip if depth == 0 else 0.85)) * k)
            index = self.add(at, r * (1 + rng.uniform(-lump, lump)), index)
        if depth <= 0:
            self.tips.append((at.copy(), d.copy(), r))
            return
        for c in range(rng.randint(children[0], children[1])):
            angle = math.radians(rng.uniform(spread * 0.6, spread * 1.2))
            nd = Matrix.Rotation(angle, 3, perpendicular(d, rng)) @ d
            self.grow(rng, index, at, nd, length * rng.uniform(*shrink), r * rng.uniform(0.75, 0.92), depth - 1,
                      spread, up_pull, wobble, children, shrink, step, tip, lump)

    def to_blob_object(self, name, scale=2.0, resolution=0.025, thinnest=0.0):
        # The skeleton as metaballs, one at every point, melted into one smooth surface and made a mesh: branches
        # flow into each other and into round ends with no pinched necks (what the Skin modifier gives fat, short
        # branches). `scale` = how big each ball is for its point's thickness; `resolution` = mesh detail (metres). No point
        # thinner than `thinnest` (a ball too small does not join its neighbours), and any stray bit left over is dropped.
        balls = bpy.data.metaballs.new(name + "_Blob")
        balls.resolution = resolution
        balls.render_resolution = resolution
        balls.threshold = 0.6
        for p, r in zip(self.points, self.radii):
            element = balls.elements.new(type='BALL')
            element.co = p
            element.radius = max(r, thinnest) * scale
            element.stiffness = 2.0
        blob = bpy.data.objects.new(name + "_Blob", balls)
        collection().objects.link(blob)
        make_active(blob)
        bpy.context.view_layer.update()
        bpy.ops.object.convert(target='MESH')
        ob = bpy.context.view_layer.objects.active
        ob.name = name
        ob.data.name = name
        if balls.users == 0:
            bpy.data.metaballs.remove(balls)
        keep_largest(ob)
        for f in ob.data.polygons:
            f.use_smooth = True
        return ob

    def to_object(self, name, smoothing=0.5):
        # The skinned mesh, the Skin modifier applied.
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        skin = bm.verts.layers.skin.verify()
        verts = [bm.verts.new(p) for p in self.points]
        for a, b in self.lines:
            bm.edges.new((verts[a], verts[b]))
        for v, r in zip(verts, self.radii):
            v[skin].radius = (r, r)
        verts[0][skin].use_root = True
        bm.to_mesh(mesh)
        bm.free()
        ob = bpy.data.objects.new(name, mesh)
        collection().objects.link(ob)
        mod = ob.modifiers.new("Skin", 'SKIN')
        mod.branch_smoothing = smoothing
        mod.use_smooth_shade = True
        apply_modifiers(ob)
        return ob


# ---- materials: matte, a colour fading darker to the bottom, as a little texture mapped by height ---------------

def gradient_image(name, colour, shade, light):
    image = bpy.data.images.get(name)
    if image is not None:
        bpy.data.images.remove(image)
    height = 64
    image = bpy.data.images.new(name, width=4, height=height, alpha=False)
    pixels = []
    for y in range(height):
        t = y / (height - 1)
        rise = min(1.0, t / 0.75)
        rise = rise * rise * (3 - 2 * rise)                # dark at the foot, the full colour by three quarters up
        k = shade + (1 - shade) * rise
        tips = max(0.0, (t - 0.75) / 0.25) * light         # then a little lighter to the tips
        c = [min(1.0, colour[i] * k + (1 - colour[i]) * tips) for i in range(3)]
        for x in range(4):
            pixels += [c[0], c[1], c[2], 1.0]
    image.pixels = pixels
    return image


def linear(colour):
    # On-screen (sRGB) colour to the linear one Blender's colour inputs take.
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in colour[:3])


def set_input(node, names, value):
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return


def material(name, colour, shade=BOTTOM_SHADE, light=TIP_LIGHT, roughness=0.88):
    # `shade` = how dark its bottom is (1 = no gradient).
    mat = bpy.data.materials.get(name)
    if mat is not None:
        bpy.data.materials.remove(mat)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    set_input(bsdf, ["Roughness"], roughness)
    set_input(bsdf, ["Specular IOR Level", "Specular"], 0.15)          # matte: hardly any shine
    flat = linear(colour)
    mat.diffuse_color = (flat[0], flat[1], flat[2], 1.0)               # the solid viewport colour
    mat.roughness = roughness
    mat.specular_intensity = 0.15
    if shade >= 0.999 and light <= 0.0:
        set_input(bsdf, ["Base Color"], (flat[0], flat[1], flat[2], 1.0))
        return mat
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = gradient_image(name + "_Gradient", colour, shade, light)
    texture.extension = 'EXTEND'
    texture.location = (-450, 250)
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def height_uvs(ob):
    # U = 0.5, V = height from the bottom (0) to the top (1): the gradient textures run up the coral.
    mesh = ob.data
    zs = [v.co.z for v in mesh.vertices]
    low, span = min(zs), max(max(zs) - min(zs), 1e-4)
    uv = mesh.uv_layers.get("UVMap") or mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv.data[loop.index].uv = (0.5, (mesh.vertices[loop.vertex_index].co.z - low) / span)


def finish(ob, x, materials):
    # Materials on, height UVs, origin at the bottom middle, then set in its place along X.
    mesh = ob.data
    for i, mat in enumerate(materials):          # (clearing the slots would put every face back on the first)
        if i < len(mesh.materials):
            mesh.materials[i] = mat
        else:
            mesh.materials.append(mat)
    height_uvs(ob)
    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    offset = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    mesh.transform(Matrix.Translation(-offset))
    mesh.update()
    ob.location = (x, 0, 0)


# ---------------------------------------------------------------------------------------------------------------
# the four corals

def coral_polyp(rng):
    # A bouquet: thin teal stems rising from one foot and fanning out, of different lengths, each ending in a flared
    # cup facing up and out: a fat scalloped yellow ring round a sunken cyan middle with a little mouth. The stems
    # are placed one by one, and one whose cup would touch another cup or another stem is tried again elsewhere (or
    # left out), so nothing overlaps. Materials: 0 stems, 1 ring, 2 middle.
    s = Skeleton()
    root = s.add(Vector((0, 0, 0)), 0.07)
    foot = Vector((0, 0, 0.06))
    hub = s.add(foot, 0.055, root)
    cups, stems = [], []
    for k in range(12):                                           # how many stems it tries for
        for attempt in range(60):
            around = rng.uniform(0, math.tau)
            lean = math.radians(rng.uniform(5, 55))
            length = rng.uniform(0.38, 0.72)
            out = Vector((math.cos(around) * math.sin(lean), math.sin(around) * math.sin(lean), math.cos(lean)))
            flat = Vector((math.cos(around), math.sin(around), 0))
            # Up from the foot first, then bending out: a curve through these three points.
            p0, p1 = foot, foot + flat * 0.04 + Vector((0, 0, length * 0.45))
            p2 = foot + out * length + Vector((0, 0, length * 0.15))
            n = max(5, int(length / 0.04))
            pts = [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2 for t in (i / n for i in range(1, n + 1))]
            facing = ((pts[-1] - pts[-2]).normalized() + Vector((0, 0, 1.2))).normalized()
            radius = rng.uniform(0.09, 0.125)                     # cup size
            centre = pts[-1] + facing * radius * 0.3
            upper = pts[len(pts) // 3:]
            clear = all((centre - c).length > (radius + r) * 1.12 for c, f, r in cups)
            clear = clear and all(min((centre - q).length for q in other) > radius + 0.05 for other in stems)
            clear = clear and all(min((c - q).length for q in upper) > r + 0.05 for c, f, r in cups)
            if clear:
                break
        else:
            continue
        cups.append((centre, facing, radius))
        stems.append(upper)
        index = hub
        for i, p in enumerate(pts):
            index = s.add(p, 0.032 - 0.01 * i / len(pts), index)
    ob = s.to_object("Coral_Polyp", smoothing=0.4)
    subdivide(ob, 1)

    bm = bmesh.new()
    bm.from_mesh(ob.data)
    for f in bm.faces:
        f.material_index = 0
    for centre, facing, radius in cups:
        thick = radius * 0.6
        turn = turned_to(facing)
        at = centre - facing * radius * 0.3                       # the stem's end
        place = Matrix.Translation(at - facing * thick * 0.1) @ turn @ Matrix.Translation((0, 0, thick * 0.5))
        made = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=20,
                                     radius1=radius * 0.35, radius2=radius, depth=thick, matrix=place)
        faces = {f for v in made["verts"] for f in v.link_faces}
        bm.normal_update()
        top = max(faces, key=lambda f: f.normal.dot(facing))
        # Scallops: the ring's edge waves up and down.
        across, along = turn.to_3x3() @ Vector((1, 0, 0)), turn.to_3x3() @ Vector((0, 1, 0))
        middle = top.calc_center_median()
        waves, phase = rng.choice([7, 8, 9]), rng.uniform(0, math.tau)
        for v in top.verts:
            rel = v.co - middle
            angle = math.atan2(rel.dot(along), rel.dot(across))
            v.co += facing * (math.sin(angle * waves + phase) * radius * 0.08)
        rim = bmesh.ops.inset_individual(bm, faces=[top], thickness=radius * 0.55, depth=-radius * 0.15)   # a fat yellow ring
        mouth = bmesh.ops.inset_individual(bm, faces=[top], thickness=radius * 0.25, depth=radius * 0.08)
        dimple = bmesh.ops.inset_individual(bm, faces=[top], thickness=radius * 0.18, depth=-radius * 0.06)   # the mouth
        for f in faces | set(rim["faces"]):
            f.material_index = 1
        for f in set(mouth["faces"]) | set(dimple["faces"]) | {top}:
            f.material_index = 2
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(ob.data)
    bm.free()
    subdivide(ob, 1)
    surface(ob, rng, wobble=0.005, wobble_scale=9.0, skip_materials=(1, 2))
    return ob, [material("M_Coral_Polyp_Stem", TEAL),
                material("M_Coral_Polyp_Ring", YELLOW, shade=0.7),
                material("M_Coral_Polyp_Middle", CYAN, shade=0.75)]


def tube(bm, rng, base, axis, r, h):
    # One tube, built ring by ring: its outside swelling a little half way up and pinched just under the lip, an
    # uneven lip rising and dipping round it, a thin rounded rim, and the inside going down to a floor. Returns the
    # inside faces. (The bottom is left open: it is sunk in the mound.)
    segments, rings = 20, 6
    turn = turned_to(axis).to_3x3()
    waves, phase = rng.choice([2, 3]), rng.uniform(0, math.tau)

    def ring(t, scale, lift, sink=0.0):
        out = []
        for i in range(segments):
            a = i / segments * math.tau
            lip = math.sin(a * waves + phase) * r * 0.18 * lift     # the uneven lip (none at the foot)
            local = Vector((math.cos(a) * scale, math.sin(a) * scale, t * h + lip - sink))
            out.append(bm.verts.new(base + turn @ local))
        return out

    loops = []
    for k in range(rings + 1):
        t = k / rings
        scale = r * (0.85 + 0.2 * t) * (1 + 0.07 * math.sin(t * math.pi) - 0.05 * max(0.0, t - 0.8) / 0.2)
        loops.append(ring(t, scale, t * t))
    rim_top = ring(1.0, r * 0.95, 1.0, -r * 0.04)                 # the rim's rounded top
    lip_in = ring(1.0, r * 0.84, 1.0, r * 0.03)                   # its inner edge
    inner = [ring(1.0 - f, r * 0.82, (1.0 - f) ** 2, 0.0) for f in (0.2, 0.5, 0.8)]   # down the inside
    loops += [rim_top, lip_in] + inner
    inside = set()
    for k in range(len(loops) - 1):
        a, b = loops[k], loops[k + 1]
        for i in range(segments):
            j = (i + 1) % segments
            face = bm.faces.new((a[i], a[j], b[j], b[i]))
            if k >= rings + 1:                                    # from the rim's inner edge down
                inside.add(face)
    floor = bm.faces.new(loops[-1])                               # facing up, into the hole
    inside.add(floor)
    return inside


def coral_tube(rng):
    # A mound with a cluster of tubes, open at the top and hollow, the middle ones tallest, leaning out a little,
    # each lip uneven. Materials: 0 outside, 1 inside (dark).
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=0.5,
                              matrix=Matrix.Translation((0, 0, 0.04)) @ Matrix.Diagonal((1.0, 1.0, 0.32, 1.0)))
    tubes = []
    tries = 0
    while len(tubes) < 13 and tries < 600:                        # how many tubes
        tries += 1
        r = rng.uniform(0.1, 0.16)                                # tube radius
        a = rng.uniform(0, math.tau)
        out = math.sqrt(rng.random()) * 0.42
        x, y = math.cos(a) * out, math.sin(a) * out
        if all((Vector((x, y)) - Vector((tx, ty))).length > (r + tr) * 0.92 for tx, ty, tr in tubes):
            tubes.append((x, y, r))
    inside = set()
    for x, y, r in tubes:
        out = Vector((x, y, 0)).length
        h = 0.35 + 0.55 * (1 - out / 0.5) + rng.uniform(0, 0.15)  # tallest in the middle
        axis = Vector((0, 0, 1))
        if out > 0.05:                                            # leaning out from the middle
            tilt = math.radians(rng.uniform(4, 12))
            axis = Matrix.Rotation(tilt, 3, Vector((-y, x, 0)).normalized()) @ axis
        inside |= tube(bm, rng, Vector((x, y, 0.0)), axis, r, h)
    for f in bm.faces:
        f.smooth = True
        f.material_index = 1 if f in inside else 0
    ob = new_object("Coral_Tube", bm)
    subdivide(ob, 1)
    surface(ob, rng, wobble=0.012, wobble_scale=4.0, bumps=0.006, bump_size=0.035, skip_materials=(1,))
    return ob, [material("M_Coral_Tube_Paint", BLUE), material("M_Coral_Tube_Inside", TUBE_INSIDE, shade=1.0, light=0.0)]


def coral_bush(rng):
    # A chunky bush like the concept art: a short thick foot, a few stout branches leaning out, each splitting into
    # fat lobes that turn upwards with blunt rounded ends, so it is widest at the top. Its surface: packed polyp
    # cups, baked into the game mesh's normal map.
    s = Skeleton()
    root = s.add(Vector((0, 0, 0)), 0.15)
    stems = 4
    for k in range(stems):
        around = k / stems * math.tau + rng.uniform(-0.35, 0.35)
        lean = math.radians(rng.uniform(45, 62))
        d = Vector((math.cos(around) * math.sin(lean), math.sin(around) * math.sin(lean), math.cos(lean)))
        s.grow(rng, root, Vector((0, 0, 0)), d,
               length=0.3,
               radius=0.11,       # branch thickness
               depth=2,
               spread=34,
               up_pull=0.11,      # the lobes turn upwards
               wobble=0.2,
               children=(2, 3),
               shrink=(0.75, 0.95),
               step=0.05,
               tip=1.0,           # blunt, rounded ends
               lump=0.05)
    ob = s.to_blob_object("Coral_Bush", scale=1.35, resolution=0.03 if DETAIL > 1 else 0.045, thinnest=0.07)
    surface(ob, rng, wobble=0.01, wobble_scale=3.5)
    subdivide(ob, DETAIL - 1)                                     # fine enough for the polyp cups
    corallites(ob, rng, size=0.05, depth=0.007)
    return ob, [material("M_Coral_Bush_Paint", RED)]


def coral_finger(rng):
    # Like the bush but taller and more upright: a few stems from one foot, each splitting into upright fingers
    # with rounded, slightly knobbly tips, the whole thing wider at the top. Polyp cups baked into the normal map.
    s = Skeleton()
    root = s.add(Vector((0, 0, 0)), 0.13)
    stems = 4
    for k in range(stems):
        around = k / stems * math.tau + rng.uniform(-0.35, 0.35)
        lean = math.radians(rng.uniform(30, 45))
        d = Vector((math.cos(around) * math.sin(lean), math.sin(around) * math.sin(lean), math.cos(lean)))
        s.grow(rng, root, Vector((0, 0, 0)), d,
               length=0.32,
               radius=0.08,
               depth=2,
               spread=28,
               up_pull=0.12,      # upright fingers
               wobble=0.2,
               children=(2, 3),
               shrink=(0.65, 0.85),
               step=0.05,
               tip=1.0,
               lump=0.07)
    ob = s.to_blob_object("Coral_Finger", scale=1.35, resolution=0.025 if DETAIL > 1 else 0.038, thinnest=0.06)
    surface(ob, rng, wobble=0.008, wobble_scale=5.0)
    subdivide(ob, DETAIL - 1)
    corallites(ob, rng, size=0.04, depth=0.006)
    return ob, [material("M_Coral_Finger_Paint", MAGENTA)]


# ---- the game mesh: a light copy with the detailed one baked onto it -------------------------------------------

def game_mesh(high, triangles):
    # A copy of the detailed coral cut down to about `triangles` (Decimate keeps the shape), unwrapped for its
    # textures. The detailed one is renamed ..._High.
    name = high.name
    high.name = name + "_High"
    low = high.copy()
    low.data = high.data.copy()
    low.name = name
    low.data.name = name
    collection().objects.link(low)
    count = sum(len(p.vertices) - 2 for p in low.data.polygons)
    if count > triangles:
        mod = low.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = triangles / count
        apply_modifiers(low)
    mesh = low.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    mesh.uv_layers.new(name="UVMap")
    mesh.materials.clear()
    for p in mesh.polygons:
        p.use_smooth = True
    make_active(low)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    return low


def bake(low, high, size):
    # The detailed coral's colours (with its gradient) and its surface (the bumps and polyp cups) baked onto the
    # light one: a colour texture and a normal map, on one material (M_<name>). Cycles does the baking.
    name = low.name
    old = bpy.data.materials.get("M_" + name)
    if old is not None:
        bpy.data.materials.remove(old)
    images = []
    for suffix, colour_space in (("_Color", 'sRGB'), ("_Normal", 'Non-Color')):
        image = bpy.data.images.get(name + suffix)
        if image is not None:
            bpy.data.images.remove(image)
        image = bpy.data.images.new(name + suffix, width=size, height=size, alpha=False)
        image.colorspace_settings.name = colour_space
        images.append(image)
    colour, normal = images

    mat = bpy.data.materials.new("M_" + name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    set_input(bsdf, ["Roughness"], 0.88)
    set_input(bsdf, ["Specular IOR Level", "Specular"], 0.15)
    mat.roughness = 0.88
    mat.specular_intensity = 0.15
    colour_node = nodes.new("ShaderNodeTexImage")
    colour_node.image = colour
    colour_node.location = (-500, 300)
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_node.location = (-700, -150)
    bump = nodes.new("ShaderNodeNormalMap")
    bump.location = (-300, -150)
    links.new(colour_node.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal_node.outputs["Color"], bump.inputs["Color"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    low.data.materials.append(mat)

    scene = bpy.context.scene
    keep_engine = scene.render.engine
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 4
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    reach = max(high.dimensions) * 0.04                            # how far apart the two surfaces may be
    for node, kind in ((colour_node, 'DIFFUSE'), (normal_node, 'NORMAL')):
        for other in nodes:
            other.select = False
        node.select = True
        nodes.active = node
        settings = dict(type=kind, use_selected_to_active=True, cage_extrusion=reach, max_ray_distance=reach * 2,
                        margin=8)
        if kind == 'DIFFUSE':
            settings["pass_filter"] = {'COLOR'}
        bpy.ops.object.bake(**settings)
    scene.render.engine = keep_engine
    return [mat]


def remove(ob):
    mesh = ob.data
    bpy.data.objects.remove(ob, do_unlink=True)
    if mesh is not None and mesh.users == 0:
        bpy.data.meshes.remove(mesh)


# ---------------------------------------------------------------------------------------------------------------

def export(ob, materials):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    for mat in materials:
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image is not None:
                node.image.filepath_raw = os.path.join(EXPORT_DIR, node.image.name + ".png")
                node.image.file_format = 'PNG'
                node.image.save()
    keep = ob.location.copy()
    ob.location = (0, 0, 0)
    make_active(ob)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(EXPORT_DIR, ob.name + ".fbx"),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        path_mode='RELATIVE')
    ob.location = keep


def main():
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    clear_old()
    rng = random.Random(SEED)
    made = []
    for i, build in enumerate([coral_polyp, coral_tube, coral_bush, coral_finger]):
        high, paint = build(random.Random(rng.random()))
        finish(high, i * SPACING, paint)
        low = game_mesh(high, TRIANGLES[NAMES[i]])
        materials = bake(low, high, TEXTURE_SIZE)
        if KEEP_DETAILED:
            high.hide_set(True)
            high.hide_render = True
        else:
            remove(high)
            for mat in paint:
                for node in mat.node_tree.nodes:
                    if node.type == 'TEX_IMAGE' and node.image is not None and node.image.users <= 1:
                        bpy.data.images.remove(node.image)
                bpy.data.materials.remove(mat)
        made.append((low, materials))
    if EXPORT_DIR:
        for ob, materials in made:
            export(ob, materials)
    else:
        for ob, materials in made:
            for mat in materials:
                for node in mat.node_tree.nodes:
                    if node.type == 'TEX_IMAGE' and node.image is not None and not node.image.packed_file:
                        node.image.pack()      # kept in the .blend when you save it
    bpy.ops.object.select_all(action='DESELECT')
    print("Corals built:", ", ".join("%s (%d triangles)" % (ob.name, sum(len(p.vertices) - 2 for p in ob.data.polygons))
                                     for ob, _ in made))


main()
