"""
otlib.py - modelling helpers for the MR Operating Theatre asset library.
Author: H.M.H. Lakshan (IT23404564) - 3D Onboarding & Orientation Module
Compatible with Blender 4.2 LTS - 5.x  (units: metres, Z-up)

Every asset is built from real-world dimensions (see asset docstrings).
Geometry is generated with bmesh so the result is clean, quad-based and
fully editable in Blender afterwards.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler

# ----------------------------------------------------------------------------
# Materials  (name -> base colour, metallic, roughness, emission)
# The same names are used by Unity to build URP materials (MaterialLibrary).
# ----------------------------------------------------------------------------
MATS = {
    # metals
    "M_Steel_Brushed":   ((0.78, 0.79, 0.80, 1), 1.0, 0.32, None),
    "M_Steel_Polished":  ((0.90, 0.91, 0.92, 1), 1.0, 0.12, None),
    "M_Steel_Dark":      ((0.35, 0.36, 0.38, 1), 1.0, 0.40, None),
    "M_Chrome":          ((0.95, 0.95, 0.96, 1), 1.0, 0.05, None),
    "M_Aluminium":       ((0.82, 0.83, 0.85, 1), 1.0, 0.45, None),
    "M_Instrument_Steel":((0.86, 0.87, 0.89, 1), 1.0, 0.18, None),
    "M_Gold_Ring":       ((0.95, 0.75, 0.30, 1), 1.0, 0.25, None),
    # plastics / painted
    "M_Plastic_White":   ((0.92, 0.93, 0.93, 1), 0.0, 0.40, None),
    "M_Plastic_OffWhite":((0.86, 0.87, 0.85, 1), 0.0, 0.45, None),
    "M_Plastic_Grey":    ((0.55, 0.57, 0.60, 1), 0.0, 0.45, None),
    "M_Plastic_DarkGrey":((0.16, 0.17, 0.18, 1), 0.0, 0.50, None),
    "M_Plastic_Black":   ((0.03, 0.03, 0.035, 1), 0.0, 0.45, None),
    "M_Plastic_Blue":    ((0.10, 0.33, 0.62, 1), 0.0, 0.40, None),
    "M_Plastic_LightBlue":((0.55, 0.75, 0.90, 1), 0.0, 0.40, None),
    "M_Plastic_Green":   ((0.12, 0.55, 0.30, 1), 0.0, 0.40, None),
    "M_Plastic_Red":     ((0.75, 0.08, 0.07, 1), 0.0, 0.40, None),
    "M_Plastic_Yellow":  ((0.98, 0.80, 0.05, 1), 0.0, 0.40, None),
    "M_Plastic_Orange":  ((0.95, 0.45, 0.05, 1), 0.0, 0.40, None),
    "M_Plastic_Purple":  ((0.40, 0.20, 0.55, 1), 0.0, 0.40, None),
    "M_Painted_White":   ((0.90, 0.91, 0.90, 1), 0.1, 0.35, None),
    "M_Painted_Blue":    ((0.18, 0.40, 0.65, 1), 0.2, 0.35, None),
    "M_Painted_Red":     ((0.70, 0.07, 0.07, 1), 0.2, 0.30, None),
    "M_Rubber_Black":    ((0.02, 0.02, 0.02, 1), 0.0, 0.85, None),
    "M_Rubber_Grey":     ((0.25, 0.25, 0.26, 1), 0.0, 0.80, None),
    "M_Gel_Pad":         ((0.35, 0.10, 0.45, 1), 0.0, 0.25, None),
    "M_Upholstery_Black":((0.05, 0.055, 0.06, 1), 0.0, 0.60, None),
    "M_Upholstery_Blue": ((0.08, 0.22, 0.38, 1), 0.0, 0.60, None),
    # fabrics / disposables
    "M_Drape_Green":     ((0.10, 0.38, 0.30, 1), 0.0, 0.90, None),
    "M_Drape_Blue":      ((0.15, 0.35, 0.55, 1), 0.0, 0.90, None),
    "M_Gown_Blue":       ((0.30, 0.50, 0.70, 1), 0.0, 0.90, None),
    "M_Paper_White":     ((0.95, 0.95, 0.93, 1), 0.0, 0.80, None),
    "M_Gauze":           ((0.96, 0.96, 0.94, 1), 0.0, 0.95, None),
    "M_Cardboard":       ((0.70, 0.55, 0.38, 1), 0.0, 0.85, None),
    "M_Glove_Blue":      ((0.35, 0.55, 0.85, 1), 0.0, 0.50, None),
    "M_Skin":            ((0.62, 0.45, 0.35, 1), 0.0, 0.55, None),
    # glass / transparent
    "M_Glass_Clear":     ((0.85, 0.92, 0.95, 0.25), 0.0, 0.05, None),
    "M_Glass_Frosted":   ((0.85, 0.90, 0.92, 0.55), 0.0, 0.35, None),
    "M_Plastic_Clear":   ((0.90, 0.95, 0.98, 0.35), 0.0, 0.10, None),
    "M_Fluid_Saline":    ((0.85, 0.93, 1.00, 0.45), 0.0, 0.05, None),
    "M_Fluid_Blood":     ((0.45, 0.02, 0.03, 0.85), 0.0, 0.10, None),
    "M_Canister_Liner":  ((0.75, 0.88, 0.95, 0.50), 0.0, 0.20, None),
    # emissive / screens / lights
    "M_Light_LED":       ((1.0, 1.0, 1.0, 1), 0.0, 0.20, (1.0, 0.98, 0.95, 6.0)),
    "M_Light_Panel":     ((1.0, 1.0, 1.0, 1), 0.0, 0.30, (0.96, 0.98, 1.0, 3.0)),
    "M_Light_Diffuser":  ((0.95, 0.97, 1.0, 1), 0.0, 0.30, (0.90, 0.95, 1.0, 1.2)),
    "M_Screen_Off":      ((0.01, 0.012, 0.015, 1), 0.0, 0.08, None),
    "M_Screen_Monitor":  ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_Anaesthesia":((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_PACS":     ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_PC_Nurse": ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.4)),
    "M_Screen_PC_Doctor":((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.4)),
    "M_Screen_Diathermy":((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_Clock":    ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_Pump":     ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_Defib":    ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_Screen_Teaching": ((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.4)),
    "M_Screen_Ventilation":((0.0, 0.0, 0.0, 1), 0.0, 0.10, (1, 1, 1, 1.5)),
    "M_LED_Green":       ((0.1, 1.0, 0.2, 1), 0.0, 0.20, (0.1, 1.0, 0.2, 4.0)),
    "M_LED_Red":         ((1.0, 0.1, 0.1, 1), 0.0, 0.20, (1.0, 0.1, 0.1, 4.0)),
    "M_LED_Amber":       ((1.0, 0.6, 0.0, 1), 0.0, 0.20, (1.0, 0.6, 0.0, 4.0)),
    "M_LED_Blue":        ((0.2, 0.5, 1.0, 1), 0.0, 0.20, (0.2, 0.5, 1.0, 4.0)),
    # architecture
    "M_Floor_Vinyl":     ((0.58, 0.66, 0.66, 1), 0.0, 0.30, None),
    "M_Floor_Corridor":  ((0.62, 0.62, 0.60, 1), 0.0, 0.35, None),
    "M_Wall_Panel":      ((0.80, 0.86, 0.85, 1), 0.0, 0.40, None),
    "M_Wall_Corridor":   ((0.88, 0.88, 0.85, 1), 0.0, 0.55, None),
    "M_Ceiling":         ((0.93, 0.94, 0.94, 1), 0.0, 0.60, None),
    "M_Skirting":        ((0.40, 0.48, 0.48, 1), 0.0, 0.35, None),
    "M_Door_Panel":      ((0.72, 0.80, 0.82, 1), 0.3, 0.30, None),
    "M_Door_Kickplate":  ((0.78, 0.79, 0.80, 1), 1.0, 0.30, None),
    "M_Sign_Green":      ((0.0, 0.55, 0.25, 1), 0.0, 0.40, (0.0, 0.55, 0.25, 1.5)),
    "M_Sign_Red":        ((0.80, 0.05, 0.05, 1), 0.0, 0.40, (0.8, 0.05, 0.05, 1.5)),
    "M_Sign_White":      ((0.95, 0.95, 0.95, 1), 0.0, 0.40, None),
    "M_Sign_Blue":       ((0.05, 0.25, 0.60, 1), 0.0, 0.40, None),
    "M_Sign_Yellow":     ((0.98, 0.82, 0.05, 1), 0.0, 0.40, None),
    "M_Whiteboard":      ((0.97, 0.97, 0.97, 1), 0.0, 0.15, None),
    "M_Marker_Black":    ((0.02, 0.02, 0.05, 1), 0.0, 0.50, None),
    "M_Wood_Laminate":   ((0.62, 0.48, 0.34, 1), 0.0, 0.45, None),
    "M_Sink_Steel":      ((0.80, 0.81, 0.82, 1), 1.0, 0.22, None),
    # medical gas colour codes (ISO 32 / HTM 02-01, as used in Sri Lankan hospitals)
    "M_Gas_O2_White":    ((0.95, 0.95, 0.95, 1), 0.0, 0.35, None),
    "M_Gas_N2O_Blue":    ((0.05, 0.20, 0.60, 1), 0.0, 0.35, None),
    "M_Gas_Air_Black":   ((0.03, 0.03, 0.03, 1), 0.0, 0.35, None),
    "M_Gas_Vac_Yellow":  ((0.95, 0.80, 0.05, 1), 0.0, 0.35, None),
    "M_Gas_AGSS_Purple": ((0.45, 0.25, 0.55, 1), 0.0, 0.35, None),
    "M_Cylinder_Black":  ((0.03, 0.03, 0.03, 1), 0.3, 0.40, None),
    "M_Cylinder_O2_Shoulder":((0.95, 0.95, 0.95, 1), 0.3, 0.35, None),
    "M_Soda_Lime":       ((0.92, 0.92, 0.95, 1), 0.0, 0.90, None),
    "M_Vapor_Sevo":      ((0.95, 0.80, 0.10, 1), 0.2, 0.35, None),
    "M_Vapor_Iso":       ((0.55, 0.25, 0.55, 1), 0.2, 0.35, None),
    "M_Label_Print":     ((0.95, 0.95, 0.95, 1), 0.0, 0.50, None),
}

def mat(name):
    m = bpy.data.materials.get(name)
    if m: return m
    col, metal, rough, emis = MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = col
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if col[3] < 1.0:
        p.inputs["Alpha"].default_value = col[3]
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            try: m.blend_method = 'BLEND'
            except Exception: pass
    if emis:
        p.inputs["Emission Color"].default_value = (emis[0], emis[1], emis[2], 1)
        p.inputs["Emission Strength"].default_value = emis[3]
    m.diffuse_color = col
    m.metallic = metal
    m.roughness = rough
    return m

# ----------------------------------------------------------------------------
# Scene / collection helpers
# ----------------------------------------------------------------------------
_current_parent = [None]
_current_coll = [None]

def begin_asset(name, coll=None):
    """Start a new asset: creates an Empty root; all parts parented to it."""
    coll = coll or bpy.context.scene.collection
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.25
    coll.objects.link(root)
    _current_parent[0] = root
    _current_coll[0] = coll
    return root

def end_asset():
    r = _current_parent[0]
    _current_parent[0] = None
    return r

def _link(obj, parent=None):
    (_current_coll[0] or bpy.context.scene.collection).objects.link(obj)
    par = parent if parent is not None else _current_parent[0]
    if par is not None:
        obj.parent = par
    return obj

def mesh_obj(name, bm, material=None, smooth_angle=35, parent=None, loc=(0,0,0), rot=(0,0,0)):
    me = bpy.data.meshes.new(name)
    # smoothing: smooth faces, sharp edges above angle
    for f in bm.faces: f.smooth = True
    lim = math.radians(smooth_angle)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            try:
                if e.calc_face_angle() > lim: e.smooth = False
            except ValueError:
                pass
        else:
            e.smooth = False
    bm.normal_update()
    bm.to_mesh(me); bm.free()
    if material is not None:
        me.materials.append(mat(material) if isinstance(material, str) else material)
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    ob.rotation_euler = rot
    return _link(ob, parent)

# ----------------------------------------------------------------------------
# Primitive generators (all return objects)
# ----------------------------------------------------------------------------
def _bevel(bm, width, segs=2, edges=None):
    if width <= 0: return
    es = edges if edges is not None else bm.edges[:]
    try:
        bmesh.ops.bevel(bm, geom=es, offset=width, segments=segs, profile=0.5,
                        affect='EDGES', clamp_overlap=True)
    except TypeError:
        bmesh.ops.bevel(bm, geom=es, offset=width, segments=segs, profile=0.5,
                        vertex_only=False, clamp_overlap=True)

def box(name, size, loc=(0,0,0), material="M_Plastic_White", bevel=0.004, segs=2, rot=(0,0,0), parent=None):
    """Axis aligned box. size=(x,y,z) full extents, loc = centre."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    _bevel(bm, min(bevel, min(size)*0.45), segs)
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent)

def cyl(name, r, depth, loc=(0,0,0), material="M_Steel_Brushed", verts=32, bevel=0.0, rot=(0,0,0), r2=None, parent=None, cap=True):
    """Cylinder along local Z, centred at loc. r2 = top radius (cone)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=verts,
                          radius1=r, radius2=(r if r2 is None else r2), depth=depth)
    if bevel > 0:
        rim = [e for e in bm.edges if len(e.link_faces)==2 and e.calc_face_angle() > 0.6]
        _bevel(bm, bevel, 2, rim)
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent)

def sphere(name, r, loc=(0,0,0), material="M_Plastic_White", segs=32, rings=16, scale=(1,1,1), parent=None):
    bm = bmesh.new()
    try:
        bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r)
    except TypeError:
        bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, diameter=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    return mesh_obj(name, bm, material, loc=loc, parent=parent, smooth_angle=89)

def lathe(name, profile, loc=(0,0,0), material="M_Plastic_White", segs=48, rot=(0,0,0), parent=None, smooth=60):
    """Revolve a (radius, z) profile around Z.  profile from bottom to top."""
    bm = bmesh.new()
    vs = [bm.verts.new((r, 0, z)) for r, z in profile]
    es = [bm.edges.new((vs[i], vs[i+1])) for i in range(len(vs)-1)]
    res = bmesh.ops.spin(bm, geom=vs+es, cent=(0,0,0), axis=(0,0,1), angle=math.radians(360), steps=segs, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent, smooth_angle=smooth)

def torus(name, R, r, loc=(0,0,0), material="M_Steel_Brushed", segs=48, csegs=12, rot=(0,0,0), arc=360, parent=None):
    bm = bmesh.new()
    ring = []
    for i in range(csegs):
        a = 2*math.pi*i/csegs
        ring.append(bm.verts.new((R + r*math.cos(a), 0, r*math.sin(a))))
    es = [bm.edges.new((ring[i], ring[(i+1)%csegs])) for i in range(csegs)]
    bmesh.ops.spin(bm, geom=ring+es, cent=(0,0,0), axis=(0,0,1), angle=math.radians(arc), steps=max(3,int(segs*arc/360)), use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    if arc < 360:
        pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent, smooth_angle=80)

def tube(name, pts, radius, material="M_Steel_Brushed", segs=12, parent=None, smooth_curve=True, res=8, cap=True):
    """Tube following a polyline/spline (world-ish coordinates, relative to parent)."""
    cu = bpy.data.curves.new(name+"_crv", 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius
    cu.bevel_resolution = max(1, segs//4)
    cu.use_fill_caps = cap
    if smooth_curve:
        sp = cu.splines.new('BEZIER')
        sp.bezier_points.add(len(pts)-1)
        for i, p in enumerate(pts):
            bp = sp.bezier_points[i]
            bp.co = p
            bp.handle_left_type = bp.handle_right_type = 'AUTO'
        sp.resolution_u = res
    else:
        sp = cu.splines.new('POLY')
        sp.points.add(len(pts)-1)
        for i, p in enumerate(pts):
            sp.points[i].co = (p[0], p[1], p[2], 1)
    tmp = bpy.data.objects.new(name+"_tmp", cu)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = tmp.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev)
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    bm = bmesh.new(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    return mesh_obj(name, bm, material, parent=parent, smooth_angle=70)

def rounded_panel(name, w, h, depth, radius, loc=(0,0,0), material="M_Plastic_White", rot=(0,0,0), segs=6, parent=None, bevel=0.002, axis='Y'):
    """Rounded rectangle slab: w (x) by h (z) extruded depth along -Y (front face at y=0 -> faces -Y)."""
    bm = bmesh.new()
    radius = min(radius, w/2-1e-4, h/2-1e-4)
    pts = []
    corners = [( w/2-radius,  h/2-radius, 0), (-w/2+radius,  h/2-radius, 90),
               (-w/2+radius, -h/2+radius, 180), ( w/2-radius, -h/2+radius, 270)]
    for cx, cz, a0 in corners:
        for i in range(segs+1):
            a = math.radians(a0 + 90*i/segs)
            pts.append((cx + radius*math.cos(a), cz + radius*math.sin(a)))
    vs = [bm.verts.new((x, 0, z)) for x, z in pts]
    f = bm.faces.new(vs)
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    nv = [g for g in r['geom'] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, depth, 0), verts=nv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bevel > 0:
        rim = [e for e in bm.edges if len(e.link_faces)==2 and e.calc_face_angle() > 1.0]
        _bevel(bm, bevel, 2, rim)
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent)

def plane(name, w, h, loc=(0,0,0), material="M_Screen_Off", rot=(0,0,0), parent=None, uv=True):
    """Plane in XZ (facing -Y), with 0..1 UVs - used for screens, labels, signs."""
    bm = bmesh.new()
    v = [bm.verts.new(p) for p in ((-w/2,0,-h/2),(w/2,0,-h/2),(w/2,0,h/2),(-w/2,0,h/2))]
    f = bm.faces.new(v)
    if uv:
        lay = bm.loops.layers.uv.new("UVMap")
        for loop, co in zip(f.loops, ((0,0),(1,0),(1,1),(0,1))):
            loop[lay].uv = co
    # face points -Y
    f.normal_update()
    if f.normal.y > 0: f.normal_flip()
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent)

def text_label(name, txt, size, loc=(0,0,0), rot=(math.radians(90),0,0), material="M_Plastic_Black", depth=0.0005, parent=None, align='CENTER'):
    cu = bpy.data.curves.new(name+"_txt", 'FONT')
    cu.body = txt
    cu.size = size
    cu.extrude = depth
    cu.align_x = align
    cu.align_y = 'CENTER'
    tmp = bpy.data.objects.new(name+"_tmp", cu)
    bpy.context.scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp); bpy.data.curves.remove(cu)
    bm = bmesh.new(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    return mesh_obj(name, bm, material, loc=loc, rot=rot, parent=parent, smooth_angle=30)

def caster(name, loc, parent=None, wheel_r=0.05, brake=False, height=None):
    """Swivel castor: plate, fork, wheel (r = 50 mm or 75 mm typical)."""
    x, y, z = loc
    g = []
    g.append(cyl(name+"_Plate", 0.03, 0.008, (x, y, z), "M_Steel_Brushed", 16, parent=parent))
    g.append(cyl(name+"_Swivel", 0.012, 0.03, (x, y, z-0.02), "M_Steel_Dark", 16, parent=parent))
    fork_h = wheel_r + 0.02
    g.append(box(name+"_ForkL", (0.006, 0.04, fork_h), (x-0.022, y+0.012, z-0.035-fork_h/2+0.01), "M_Plastic_DarkGrey", 0.002, parent=parent))
    g.append(box(name+"_ForkR", (0.006, 0.04, fork_h), (x+0.022, y+0.012, z-0.035-fork_h/2+0.01), "M_Plastic_DarkGrey", 0.002, parent=parent))
    wz = wheel_r
    g.append(cyl(name+"_Wheel", wheel_r, 0.032, (x, y+0.018, wz), "M_Rubber_Grey", 24, bevel=0.006, rot=(0, math.radians(90), 0), parent=parent))
    g.append(cyl(name+"_Hub", wheel_r*0.45, 0.036, (x, y+0.018, wz), "M_Plastic_Grey", 16, rot=(0, math.radians(90), 0), parent=parent))
    if brake:
        g.append(box(name+"_Brake", (0.03, 0.035, 0.008), (x, y-0.03, wz+wheel_r*0.6), "M_Plastic_Red", 0.002, parent=parent))
    return g

def join(objs, name):
    """Join mesh objects that share the same parent into one multi-material mesh
    (pure bmesh - no operators, works headless and in every Blender version)."""
    objs = [o for o in objs if o is not None and o.type == 'MESH']
    if not objs: return None
    if len(objs) == 1:
        objs[0].name = name; objs[0].data.name = name
        return objs[0]
    target = objs[0]
    tinv = target.matrix_basis.inverted()
    mats = []
    bm = bmesh.new()
    for o in objs:
        me = o.data
        local_mats = [m for m in me.materials]
        remap = []
        for m in local_mats:
            if m not in mats: mats.append(m)
            remap.append(mats.index(m))
        tmp = bmesh.new(); tmp.from_mesh(me)
        tmp.transform(tinv @ o.matrix_basis)
        for f in tmp.faces:
            f.material_index = remap[f.material_index] if remap else 0
        me2 = bpy.data.meshes.new("__tmp"); tmp.to_mesh(me2); tmp.free()
        bm.from_mesh(me2)
        bpy.data.meshes.remove(me2)
    newme = bpy.data.meshes.new(name)
    bm.to_mesh(newme); bm.free()
    for m in mats: newme.materials.append(m)
    old = target.data
    target.data = newme
    for o in objs[1:]:
        d = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if d.users == 0: bpy.data.meshes.remove(d)
    if old.users == 0: bpy.data.meshes.remove(old)
    target.name = name
    return target

def children_meshes(root):
    return [o for o in root.children_recursive if o.type == 'MESH']

def finalize_asset(root, keep_separate=()):
    """Join all mesh parts of an asset into one multi-material mesh named <root>_Mesh,
    except parts whose name contains any token in keep_separate (moving parts)."""
    # screens (objects created by plane() and marked with a 'screen' property) stay separate so
    # Unity can animate them; they are renamed <Asset>_Screen_<key>
    for o in list(root.children):
        if o.type == 'MESH' and "screen" in o.keys():
            o.name = f"{root.name}_Screen_{o['screen']}"
    parts = [o for o in root.children if o.type == 'MESH' and "screen" not in o.keys() and not any(k in o.name for k in keep_separate)]
    if parts:
        j = join(parts, root.name + "_Mesh")
        # merge duplicate material slots
        _merge_slots(j)
    return root

def _merge_slots(ob):
    me = ob.data
    names = [m.name if m else None for m in me.materials]
    uniq = []
    for n in names:
        if n not in uniq: uniq.append(n)
    if len(uniq) == len(names): return
    remap = [uniq.index(n) for n in names]
    for p in me.polygons:
        p.material_index = remap[p.material_index]
    me.materials.clear()
    for n in uniq:
        me.materials.append(bpy.data.materials.get(n))

def add_uv_box(ob, scale=1.0):
    """Simple box-projection UVs in world metres (for tiling textures such as floor vinyl)."""
    me = ob.data
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    bm = bmesh.new(); bm.from_mesh(me)
    lay = bm.loops.layers.uv.active
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            co = l.vert.co
            if ax == 0: uv = (co.y, co.z)
            elif ax == 1: uv = (co.x, co.z)
            else: uv = (co.x, co.y)
            l[lay].uv = (uv[0]*scale, uv[1]*scale)
    bm.to_mesh(me); bm.free()

def place(root, loc=(0,0,0), rot_z_deg=0.0):
    root.location = loc
    root.rotation_euler = (0, 0, math.radians(rot_z_deg))
    return root

def mark(ob, **props):
    """Custom properties -> exported to FBX as user properties (Unity reads them)."""
    for k, v in props.items():
        ob[k] = v
    return ob

R = math.radians

link = _link

