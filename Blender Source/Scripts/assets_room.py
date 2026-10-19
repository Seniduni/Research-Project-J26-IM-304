"""Room shell, doors, ceiling systems, surgical lights, pendants.
Real-world references:
 - Operating room ~ 7.2 m x 7.0 m x 3.0 m clear height (HBN 26: >= 55 m2 general OT)
 - Hermetic sliding OT door, clear opening 1.6 m x 2.1 m, foot-operated sensor
 - Laminar (unidirectional) airflow canopy 2.8 m x 2.8 m centred over the table
 - Twin LED surgical lights (main ~ 700 mm dome, satellite ~ 550 mm), 160,000 lux class
 - Ceiling supply pendants: anaesthesia + surgical, gas outlets colour-coded (ISO 32)
"""
from otlib import *

from mathutils import Matrix
ROOM_W, ROOM_D, ROOM_H = 7.2, 7.0, 3.0
WT = 0.15                          # wall thickness
CORR_D = 3.2                       # corridor depth (south of the theatre)
TABLE_POS = (0.0, 0.2)
MAIN_DOOR_W, MAIN_DOOR_H = 1.6, 2.1

def _wall_with_openings(name, length, height, thick, openings, loc, rot_z, material, parent):
    """Wall along local X from -L/2..L/2, openings = [(centre_x, width, height)].
    Built from boxes (left/right/top segments)."""
    parts = []
    xs = sorted(openings, key=lambda o: o[0])
    cur = -length/2
    i = 0
    for cx, w, h in xs:
        a, b = cx - w/2, cx + w/2
        if a > cur + 1e-4:
            parts.append(((cur+a)/2, a-cur, height, height/2))
        parts.append((cx, w, height-h, h + (height-h)/2))
        cur = b
    if cur < length/2 - 1e-4:
        parts.append(((cur+length/2)/2, length/2-cur, height, height/2))
    objs = []
    for j, (cx, w, h, cz) in enumerate(parts):
        if h <= 1e-3: continue
        o = box(f"{name}_{j}", (w, thick, h), (cx, 0, cz), material, 0.0, parent=parent)
        objs.append(o)
    w = join(objs, name)
    # join() keeps the first segment's transform - bake every segment offset into the mesh
    # so the openings stay where they were designed (fixes the blocked main doorway)
    w.data.transform(w.matrix_basis.copy())
    w.matrix_basis = Matrix.Identity(4)
    w.location = loc
    w.rotation_euler = (0, 0, R(rot_z))
    add_uv_box(w, 1.0)
    return w

def build_room():
    root = begin_asset("OT_RoomShell")
    W, D, H = ROOM_W, ROOM_D, ROOM_H
    # --- floor: seamless anti-static vinyl, coved up the wall 100 mm
    fl = box("Floor", (W, D, 0.02), (0, 0, -0.01), "M_Floor_Vinyl", 0.0)
    add_uv_box(fl, 0.5)
    corr = box("CorridorFloor", (W, CORR_D, 0.02), (0, -D/2 - WT - CORR_D/2, -0.01), "M_Floor_Corridor", 0.0)
    add_uv_box(corr, 0.5)
    box("DoorThreshold", (MAIN_DOOR_W, WT, 0.02), (0, -D/2 - WT/2, -0.005), "M_Floor_Vinyl", 0.0)
    # coved skirting (quarter round) along the theatre walls
    for nm, L, loc, rz in (("CoveS", W, (0, -D/2+0.03, 0.03), 0), ("CoveN", W, (0, D/2-0.03, 0.03), 180),
                           ("CoveW", D, (-W/2+0.03, 0, 0.03), 270), ("CoveE", D, (W/2-0.03, 0, 0.03), 90)):
        c = cyl(nm, 0.03, L, loc, "M_Skirting", 8, rot=(0, R(90), R(rz)))
        c.rotation_euler = (0, R(90), 0) if rz in (0, 180) else (R(90), 0, 0)
    # --- walls (hygienic seamless wall panels)
    # south wall: main door
    _wall_with_openings("Wall_South", W + 2*WT, H, WT, [(0, MAIN_DOOR_W, MAIN_DOOR_H)],
                        (0, -D/2 - WT/2, 0), 0, "M_Wall_Panel", root)
    # north wall: anaesthetic room door (west), dirty utility door (east)
    _wall_with_openings("Wall_North", W + 2*WT, H, WT, [(-2.3, 1.2, 2.1), (2.5, 1.0, 2.1)],
                        (0, D/2 + WT/2, 0), 0, "M_Wall_Panel", root)
    # west wall: setup / prep room door
    _wall_with_openings("Wall_West", D, H, WT, [(-2.0, 1.2, 2.1)],
                        (-W/2 - WT/2, 0, 0), 90, "M_Wall_Panel", root)
    # east wall: scrub room door
    _wall_with_openings("Wall_East", D, H, WT, [(-1.9, 1.0, 2.1)],
                        (W/2 + WT/2, 0, 0), 90, "M_Wall_Corridor" if False else "M_Wall_Panel", root)
    # corridor walls
    cy = -D/2 - WT - CORR_D
    box("Corridor_WallS", (W + 2*WT, WT, H), (0, cy - WT/2, H/2), "M_Wall_Corridor", 0.0)
    box("Corridor_WallW", (WT, CORR_D, H), (-W/2 - WT/2, -D/2 - WT - CORR_D/2, H/2), "M_Wall_Corridor", 0.0)
    box("Corridor_WallE", (WT, CORR_D, H), (W/2 + WT/2, -D/2 - WT - CORR_D/2, H/2), "M_Wall_Corridor", 0.0)
    box("Corridor_Ceiling", (W, CORR_D, 0.02), (0, -D/2 - WT - CORR_D/2, 2.7), "M_Ceiling", 0.0)
    # corridor-side wall finish (painted) on the south wall face
    box("Corridor_WallFinishL", (W/2 - MAIN_DOOR_W/2, 0.005, H), (-(W/2 + MAIN_DOOR_W/2)/2, -D/2 - WT - 0.003, H/2), "M_Wall_Corridor", 0.0)
    box("Corridor_WallFinishR", (W/2 - MAIN_DOOR_W/2, 0.005, H), ((W/2 + MAIN_DOOR_W/2)/2, -D/2 - WT - 0.003, H/2), "M_Wall_Corridor", 0.0)
    # bump rails on corridor
    for s in (-1, 1):
        box(f"Corridor_BumpRail_{s}", (W/2 - MAIN_DOOR_W/2 - 0.4, 0.03, 0.12),
            (s*(W/2 + MAIN_DOOR_W/2)/2, -D/2 - WT - 0.02, 0.9), "M_Wood_Laminate", 0.01)
    # --- ceiling with opening for laminar flow canopy
    cx0, cy0, cs = TABLE_POS[0], TABLE_POS[1], 2.8
    ce = []
    ce.append(box("Ceil_N", (W, D/2 - (cy0 + cs/2), 0.04), (0, (D/2 + cy0 + cs/2)/2, H + 0.02), "M_Ceiling", 0.0))
    ce.append(box("Ceil_S", (W, (cy0 - cs/2) + D/2, 0.04), (0, (-D/2 + cy0 - cs/2)/2, H + 0.02), "M_Ceiling", 0.0))
    ce.append(box("Ceil_W", (W/2 - cs/2, cs, 0.04), (-(W/2 + cs/2)/2, cy0, H + 0.02), "M_Ceiling", 0.0))
    ce.append(box("Ceil_E", (W/2 - cs/2, cs, 0.04), ((W/2 + cs/2)/2, cy0, H + 0.02), "M_Ceiling", 0.0))
    j = join(ce, "Ceiling"); add_uv_box(j, 1.0)
    # --- recessed LED ceiling panels (600 x 600) around the canopy: ambient room lighting
    panel_pos = [(-2.6, 2.6), (0.0, 2.75), (2.6, 2.6), (-2.6, 0.2), (2.6, 0.2), (-2.6, -2.3), (0.0, -2.55), (2.6, -2.3)]
    for i, (x, y) in enumerate(panel_pos):
        box(f"CeilingPanel_{i}_Frame", (0.62, 0.62, 0.02), (x, y, H - 0.005), "M_Aluminium", 0.003)
        box(f"CeilingPanel_{i}_Diffuser", (0.58, 0.58, 0.01), (x, y, H - 0.016), "M_Light_Panel", 0.0)
        e = bpy.data.objects.new(f"LIGHT_Panel_{i:02d}", None); link(e)
        e.location = (x, y, H - 0.05)
        mark(e, light_type="area", intensity=1.0, range=6.0, color="F2F6FF")
    for i, (x, y) in enumerate([(-2.4, -D/2 - WT - 1.0), (0.0, -D/2 - WT - 1.6), (2.4, -D/2 - WT - 1.0)]):
        box(f"CorridorPanel_{i}_Frame", (0.62, 0.62, 0.02), (x, y, 2.695), "M_Aluminium", 0.003)
        box(f"CorridorPanel_{i}_Diffuser", (0.58, 0.58, 0.01), (x, y, 2.684), "M_Light_Panel", 0.0)
        e = bpy.data.objects.new(f"LIGHT_Corridor_{i:02d}", None); link(e)
        e.location = (x, y, 2.6)
        mark(e, light_type="area", intensity=0.8, range=5.0, color="FFF6EA")
    # pressure stabiliser / exhaust grilles low on walls (theatre air exits low)
    for i, (x, y, rz) in enumerate([(-3.59, 2.6, 90), (3.59, 2.6, 90), (-3.59, -2.9, 90), (3.59, 0.9, 90)]):
        g = box(f"ExhaustGrille_{i}", (0.6, 0.02, 0.3), (x, y, 0.35), "M_Aluminium", 0.003, rot=(0, 0, R(rz)))
        for k in range(6):
            box(f"ExhaustGrille_{i}_Slat{k}", (0.56, 0.03, 0.012), (x, y, 0.235 + k*0.045), "M_Steel_Dark", 0.0, rot=(0, 0, R(rz)))
    finalize_asset(root)
    return end_asset()

def build_laminar_canopy():
    """Unidirectional (laminar) airflow ceiling canopy with HEPA diffusers,
    perimeter glass skirts and integrated LED light strips."""
    root = begin_asset("LaminarFlowCanopy")
    s, H = 2.8, ROOM_H
    box("Canopy_Housing", (s, s, 0.12), (0, 0, H - 0.04), "M_Plastic_White", 0.01)
    # perforated diffuser panels (4 quadrants, leaving centre for light mount)
    for i, (x, y) in enumerate([(-0.68, -0.68), (0.68, -0.68), (-0.68, 0.68), (0.68, 0.68)]):
        box(f"Canopy_Diffuser_{i}", (1.18, 1.18, 0.01), (x, y, H - 0.105), "M_Light_Diffuser", 0.0)
        for k in range(7):
            box(f"Canopy_DiffRib_{i}_{k}", (1.18, 0.006, 0.006), (x, y - 0.54 + k*0.18, H - 0.112), "M_Aluminium", 0.0)
    # centre cross beam (light & pendant mounts pass through)
    box("Canopy_CentreBeamX", (s, 0.16, 0.14), (0, 0, H - 0.07), "M_Plastic_White", 0.01)
    box("Canopy_CentreBeamY", (0.16, s, 0.14), (0, 0, H - 0.07), "M_Plastic_White", 0.01)
    # perimeter LED strips
    for i, (x, y, sx, sy) in enumerate([(0, s/2 - 0.06, s - 0.1, 0.04), (0, -s/2 + 0.06, s - 0.1, 0.04),
                                         (s/2 - 0.06, 0, 0.04, s - 0.1), (-s/2 + 0.06, 0, 0.04, s - 0.1)]):
        box(f"Canopy_LEDStrip_{i}", (sx, sy, 0.012), (x, y, H - 0.106), "M_Light_LED", 0.0)
    # glass air-curtain skirts (300 mm)
    for i, (x, y, sx, sy) in enumerate([(0, s/2, s, 0.012), (0, -s/2, s, 0.012), (s/2, 0, 0.012, s), (-s/2, 0, 0.012, s)]):
        box(f"Canopy_Skirt_{i}", (sx, sy, 0.30), (x, y, H - 0.25), "M_Glass_Frosted", 0.0)
        box(f"Canopy_SkirtRail_{i}", (sx + (0.02 if sx > 0.1 else 0.02), sy + 0.02, 0.025), (x, y, H - 0.40), "M_Aluminium", 0.004)
    for i, (x, y) in enumerate([(-0.9, -0.9), (0.9, -0.9), (-0.9, 0.9), (0.9, 0.9)]):
        e = bpy.data.objects.new(f"LIGHT_Canopy_{i:02d}", None); link(e)
        e.location = (x, y, H - 0.15)
        mark(e, light_type="area", intensity=1.4, range=5.0, color="F4F8FF")
    finalize_asset(root)
    return end_asset()

def _light_dome(prefix, dia, loc, parent_tilt=0):
    """LED surgical light head: shallow dome with LED reflector array and sterile handle."""
    r = dia / 2
    x, y, z = loc
    prof = [(0.0, 0.0), (r*0.25, 0.0), (r*0.97, 0.005), (r, 0.02), (r*0.99, 0.05), (r*0.9, 0.085),
            (r*0.65, 0.11), (r*0.3, 0.125), (0.0, 0.13)]
    lathe(prefix + "_Shell", prof, (x, y, z), "M_Plastic_White", 64)
    cyl(prefix + "_Lens", r*0.93, 0.004, (x, y, z - 0.001), "M_Glass_Frosted", 64)
    # LED modules in concentric rings
    rings = [(0.0, 1), (r*0.28, 6), (r*0.52, 12), (r*0.75, 18)]
    for ri, (rr, n) in enumerate(rings):
        for k in range(n):
            a = 2*math.pi*k/n + (ri % 2)*math.pi/n
            if ri == 0 and k == 0:
                continue
            cx, cy = x + rr*math.cos(a), y + rr*math.sin(a)
            lathe(f"{prefix}_LEDCup_{ri}_{k}", [(0.0, 0.0), (0.028, 0.0), (0.030, 0.004), (0.018, 0.018), (0.0, 0.019)],
                  (cx, cy, z + 0.002), "M_Chrome", 16)
            cyl(f"{prefix}_LED_{ri}_{k}", 0.009, 0.003, (cx, cy, z + 0.001), "M_Light_LED", 12)
    # sterile handle (centre) - covered with a sterile cover during surgery
    cyl(prefix + "_HandleBase", 0.045, 0.02, (x, y, z - 0.01), "M_Plastic_Grey", 32)
    lathe(prefix + "_SterileHandle", [(0.0, 0.0), (0.016, 0.0), (0.02, 0.01), (0.019, 0.09), (0.022, 0.11), (0.0, 0.12)],
          (x, y, z - 0.13), "M_Plastic_LightBlue", 24)
    # outer handle ring
    torus(prefix + "_OuterHandle", r + 0.01, 0.012, (x, y, z + 0.03), "M_Plastic_Grey", 64, 12)
    # yoke mount on top
    cyl(prefix + "_Hub", 0.05, 0.06, (x, y, z + 0.16), "M_Plastic_White", 32)

def build_surgical_lights():
    """Ceiling-mounted twin LED surgical light (main + satellite) on spring arms."""
    root = begin_asset("SurgicalLight_Twin")
    H = ROOM_H
    # central ceiling mount & axis
    cyl("Mount_Flange", 0.22, 0.02, (0, 0, H - 0.01), "M_Plastic_White", 48)
    cyl("Mount_Axis", 0.07, 0.45, (0, 0, H - 0.245), "M_Plastic_White", 32)
    cyl("Mount_Collar1", 0.085, 0.05, (0, 0, H - 0.35), "M_Plastic_Grey", 32)
    cyl("Mount_Collar2", 0.085, 0.05, (0, 0, H - 0.44), "M_Plastic_Grey", 32)
    # Main light arm (towards +x/-y, above table)
    arms = [("Main", 0.72, H - 0.35, R(-35), 0.80, 0.55, 1.98),
            ("Satellite", 0.58, H - 0.44, R(145), 0.70, 0.50, 2.05)]
    for name, dia, z0, ang, L1, L2, zh in arms:
        ex, ey = L1*math.cos(ang), L1*math.sin(ang)
        tube(f"{name}_ExtensionArm", [(0, 0, z0), (ex*0.5, ey*0.5, z0), (ex, ey, z0)], 0.035, "M_Plastic_White", 16)
        cyl(f"{name}_Joint1", 0.055, 0.09, (ex, ey, z0 - 0.02), "M_Plastic_Grey", 32)
        # spring arm descending
        tz = zh + 0.42
        sx, sy = ex + L2*0.6*math.cos(ang), ey + L2*0.6*math.sin(ang)
        tube(f"{name}_SpringArm", [(ex, ey, z0 - 0.06), ((ex+sx)/2, (ey+sy)/2, (z0 - 0.06 + tz)/2 + 0.04), (sx, sy, tz)], 0.03, "M_Plastic_White", 16)
        cyl(f"{name}_Joint2", 0.045, 0.08, (sx, sy, tz), "M_Plastic_Grey", 32)
        # yoke (C-bracket) holding dome
        tube(f"{name}_Yoke", [(sx, sy, tz - 0.04), (sx, sy, tz - 0.16), (sx, sy, zh + 0.20)], 0.022, "M_Plastic_White", 12)
        _light_dome(name + "Head", dia, (sx, sy, zh))
        e = bpy.data.objects.new(f"LIGHT_Surgical_{name}", None); link(e)
        e.location = (sx, sy, zh - 0.02)
        mark(e, light_type="spot", intensity=6.0 if name == "Main" else 4.5, range=4.0, spot_angle=38, color="FFFAF0",
             aim_x=0.0, aim_y=0.0, aim_z=0.95)
    finalize_asset(root)
    return end_asset()

def _gas_outlet(prefix, loc, gas, rot_z=0, parent=None):
    """Medical gas terminal unit (BS 5682 / ISO 9170) with colour-coded collar and label."""
    colors = {"O2": "M_Gas_O2_White", "N2O": "M_Gas_N2O_Blue", "AIR": "M_Gas_Air_Black",
              "VAC": "M_Gas_Vac_Yellow", "AGSS": "M_Gas_AGSS_Purple"}
    x, y, z = loc
    rz = R(rot_z)
    o = []
    o.append(cyl(prefix + "_Plate", 0.032, 0.006, (x, y, z), "M_Plastic_White", 24, rot=(R(90), 0, rz)))
    dx, dy = -math.sin(rz)*0.008, -math.cos(rz)*0.008
    o.append(cyl(prefix + "_Collar", 0.022, 0.012, (x + dx, y + dy, z), colors[gas], 24, rot=(R(90), 0, rz)))
    o.append(cyl(prefix + "_Socket", 0.009, 0.016, (x + dx*1.6, y + dy*1.6, z), "M_Chrome", 16, rot=(R(90), 0, rz)))
    lab = {"O2": "O2", "N2O": "N2O", "AIR": "AIR", "VAC": "VAC", "AGSS": "AGS"}[gas]
    t = text_label(prefix + "_Txt", lab, 0.012, (x + dx*1.2, y + dy*1.2, z + 0.042), (R(90), 0, rz), "M_Plastic_Black")
    return o

def build_pendant(kind="Anaesthesia"):
    """Ceiling supply pendant (column type) with shelves, gas outlets, sockets.
    Anaesthesia pendant: O2, N2O, AIR, VAC x2, AGSS; Surgical pendant: AIR, VAC x2, CO2-free."""
    root = begin_asset(f"CeilingPendant_{kind}")
    H = ROOM_H
    cyl("Flange", 0.20, 0.02, (0, 0, H - 0.01), "M_Plastic_White", 48)
    cyl("Column", 0.08, 0.35, (0, 0, H - 0.19), "M_Plastic_White", 32)
    L = 0.9
    box("Arm", (L, 0.16, 0.12), (L/2, 0, H - 0.40), "M_Plastic_White", 0.03)
    cyl("Joint", 0.10, 0.12, (L, 0, H - 0.40), "M_Plastic_Grey", 32)
    cyl("Joint0", 0.10, 0.12, (0, 0, H - 0.40), "M_Plastic_Grey", 32)
    # supply head (vertical column)
    hx, top, bot = L, H - 0.46, 0.85
    box("Head", (0.30, 0.30, top - bot), (hx, 0, (top + bot)/2), "M_Plastic_White", 0.03)
    box("Head_Handle", (0.30, 0.02, 0.02), (hx, -0.20, 1.10), "M_Steel_Brushed", 0.008)
    for k in (-1, 1):
        box(f"Head_HandlePost{k}", (0.02, 0.05, 0.02), (hx + k*0.14, -0.175, 1.10), "M_Steel_Brushed", 0.006)
    # shelves
    for i, zz in enumerate((1.05, 1.45) if kind == "Anaesthesia" else (1.25,)):
        box(f"Shelf_{i}", (0.60, 0.48, 0.03), (hx, -0.10, zz), "M_Plastic_OffWhite", 0.01)
        box(f"Shelf_{i}_Rail", (0.60, 0.02, 0.035), (hx, -0.335, zz + 0.03), "M_Steel_Brushed", 0.006)
    # side rails (DIN rails for accessories)
    for k in (-1, 1):
        box(f"Rail_{k}", (0.025, 0.012, top - bot - 0.1), (hx + k*0.163, -0.10, (top + bot)/2), "M_Steel_Brushed", 0.003)
    gases = ["O2", "N2O", "AIR", "VAC", "VAC", "AGSS"] if kind == "Anaesthesia" else ["AIR", "VAC", "VAC", "O2"]
    for i, g in enumerate(gases):
        _gas_outlet(f"Gas_{i}_{g}", (hx - 0.10 + (i % 2)*0.1, -0.151, 1.95 - (i // 2)*0.11), g)
    # power sockets (15 A), equipotential studs, data
    for i in range(4):
        box(f"Socket_{i}", (0.07, 0.01, 0.07), (hx + 0.10, -0.152, 1.95 - i*0.09), "M_Plastic_White", 0.004)
        box(f"Socket_{i}_Face", (0.05, 0.005, 0.05), (hx + 0.10, -0.158, 1.95 - i*0.09), "M_Plastic_Grey", 0.003)
        box(f"Socket_{i}_LED", (0.008, 0.004, 0.004), (hx + 0.125, -0.161, 1.975 - i*0.09), "M_LED_Green", 0.0)
    for i in range(3):
        cyl(f"Equipotential_{i}", 0.008, 0.015, (hx - 0.11 + i*0.03, -0.153, 1.58), "M_Gold_Ring", 12, rot=(R(90), 0, 0))
    box("Data_RJ45", (0.08, 0.008, 0.05), (hx, -0.152, 1.58 + 0.08), "M_Plastic_White", 0.003)
    finalize_asset(root)
    return end_asset()

# --------------------------------------------------------------------------- doors
def build_main_door():
    """Hermetic sliding operating-theatre door (1.6 m clear) with foot sensor.
    Leaf slides to +X on the corridor side. Foot sensor on corridor wall at 150 mm."""
    root = begin_asset("MainTheatreDoor")
    W, Hd = MAIN_DOOR_W, MAIN_DOOR_H
    leaf_w, leaf_h = W + 0.10, Hd + 0.05
    # frame / architrave (stainless)
    box("Frame_L", (0.06, 0.20, Hd + 0.06), (-W/2 - 0.03, 0, (Hd + 0.06)/2), "M_Steel_Brushed", 0.004)
    box("Frame_R", (0.06, 0.20, Hd + 0.06), (W/2 + 0.03, 0, (Hd + 0.06)/2), "M_Steel_Brushed", 0.004)
    box("Frame_Top", (W + 0.12, 0.20, 0.06), (0, 0, Hd + 0.03), "M_Steel_Brushed", 0.004)
    # operator housing above door (corridor side)
    box("Operator_Housing", (2*W + 0.4, 0.16, 0.20), (W/2, -0.16, Hd + 0.17), "M_Aluminium", 0.01)
    box("Operator_Indicator", (0.10, 0.005, 0.03), (0, -0.242, Hd + 0.17), "M_LED_Green", 0.0)
    # door leaf (separate object - animated in Unity)
    leaf = begin_leaf = None
    parts = []
    parts.append(box("Leaf_Panel", (leaf_w, 0.05, leaf_h), (0, 0, 0), "M_Door_Panel", 0.01))
    parts.append(box("Leaf_Kickplate", (leaf_w - 0.02, 0.054, 0.35), (0, 0, -leaf_h/2 + 0.185), "M_Door_Kickplate", 0.003))
    parts.append(box("Leaf_VisionFrame", (0.46, 0.056, 0.66), (0, 0, 0.30), "M_Steel_Brushed", 0.006))
    parts.append(box("Leaf_Vision", (0.40, 0.058, 0.60), (0, 0, 0.30), "M_Glass_Clear", 0.002))
    parts.append(box("Leaf_PullHandle", (0.03, 0.06, 0.60), (-leaf_w/2 + 0.12, -0.06, 0.0), "M_Steel_Brushed", 0.01))
    for k in (-1, 1):
        parts.append(box(f"Leaf_HandlePost{k}", (0.025, 0.05, 0.025), (-leaf_w/2 + 0.12, -0.035, k*0.28), "M_Steel_Brushed", 0.005))
    parts.append(text_label("Leaf_Sign", "OPERATING THEATRE 1", 0.055, (0, -0.0265, 0.78), (R(90), 0, 0), "M_Sign_Blue"))
    parts.append(text_label("Leaf_Sign2", "RESTRICTED  -  THEATRE ATTIRE ONLY", 0.032, (0, -0.0265, 0.70), (R(90), 0, 0), "M_Plastic_DarkGrey"))
    lf = join(parts, "MainDoor_Leaf")
    lf.location = (0, -0.13, leaf_h/2)
    mark(lf, interactable="door_leaf", slide_x=W + 0.05)
    # foot sensor (corridor wall, left of door) - stainless kick box with sensor window
    fs = []
    fx = -W/2 - 0.45
    fs.append(box("FootSensor_Box", (0.22, 0.08, 0.16), (0, 0, 0), "M_Steel_Brushed", 0.01))
    fs.append(box("FootSensor_Window", (0.14, 0.004, 0.06), (0, -0.041, 0.02), "M_Plastic_Black", 0.002))
    fs.append(box("FootSensor_LED", (0.03, 0.004, 0.008), (0, -0.042, -0.045), "M_LED_Blue", 0.0))
    fs.append(text_label("FootSensor_Icon", "FOOT", 0.022, (0, -0.0425, -0.025), (R(90), 0, 0), "M_Plastic_Black"))
    f = join(fs, "MainDoor_FootSensor")
    f.location = (fx, -0.12, 0.15)
    mark(f, interactable="foot_sensor")
    # second sensor on the theatre side (to leave the theatre hands-free)
    fs2 = []
    fs2.append(box("FootSensor2_Box", (0.22, 0.08, 0.16), (0, 0, 0), "M_Steel_Brushed", 0.01))
    fs2.append(box("FootSensor2_Window", (0.14, 0.004, 0.06), (0, 0.041, 0.02), "M_Plastic_Black", 0.002))
    fs2.append(box("FootSensor2_LED", (0.03, 0.004, 0.008), (0, 0.042, -0.045), "M_LED_Blue", 0.0))
    f2 = join(fs2, "MainDoor_FootSensorInside")
    f2.location = (W/2 + 0.45, 0.12, 0.15)
    mark(f2, interactable="foot_sensor")
    # sign above the foot sensor (wall mounted, eye level)
    sg = []
    sg.append(box("FootSign_Plate", (0.30, 0.006, 0.20), (0, 0, 0), "M_Sign_White", 0.004))
    sg.append(text_label("FootSign_T1", "HANDS-FREE DOOR", 0.026, (0, -0.0035, 0.05), (R(90), 0, 0), "M_Sign_Blue"))
    sg.append(text_label("FootSign_T2", "Use foot sensor below", 0.02, (0, -0.0035, 0.0), (R(90), 0, 0), "M_Plastic_DarkGrey"))
    sg.append(text_label("FootSign_T3", "v", 0.05, (0, -0.0035, -0.06), (R(90), 0, 0), "M_Sign_Blue"))
    s = join(sg, "MainDoor_FootSign")
    s.location = (fx, -0.085, 1.35)
    # theatre status light above door (corridor side): "IN USE" / "VACANT"
    box("StatusLight_Box", (0.40, 0.06, 0.12), (0, -0.27, Hd + 0.40), "M_Plastic_DarkGrey", 0.01)
    box("StatusLight_InUse", (0.18, 0.006, 0.08), (-0.095, -0.302, Hd + 0.40), "M_Sign_Red", 0.002)
    box("StatusLight_Vacant", (0.18, 0.006, 0.08), (0.095, -0.302, Hd + 0.40), "M_Sign_Green", 0.002)
    text_label("StatusLight_T1", "IN USE", 0.03, (-0.095, -0.306, Hd + 0.40), (R(90), 0, 0), "M_Sign_White")
    text_label("StatusLight_T2", "VACANT", 0.03, (0.095, -0.306, Hd + 0.40), (R(90), 0, 0), "M_Sign_White")
    finalize_asset(root, keep_separate=("MainDoor_Leaf", "MainDoor_FootSensor"))
    return end_asset()

def build_swing_door(name, width, label, color_mat="M_Sign_Blue"):
    """Double-action / swing door with vision panel, push plate and kick plate (closed)."""
    root = begin_asset(name)
    Hd = 2.1
    box("Frame_L", (0.05, 0.18, Hd + 0.05), (-width/2 - 0.025, 0, (Hd + 0.05)/2), "M_Steel_Brushed", 0.004)
    box("Frame_R", (0.05, 0.18, Hd + 0.05), (width/2 + 0.025, 0, (Hd + 0.05)/2), "M_Steel_Brushed", 0.004)
    box("Frame_T", (width + 0.1, 0.18, 0.05), (0, 0, Hd + 0.025), "M_Steel_Brushed", 0.004)
    leaves = 2 if width > 1.1 else 1
    lw = width/leaves - 0.005
    for i in range(leaves):
        cx = -width/2 + lw/2 + i*(lw + 0.01)
        box(f"Leaf{i}", (lw, 0.045, Hd - 0.01), (cx, 0, Hd/2), "M_Door_Panel", 0.008)
        box(f"Leaf{i}_KickF", (lw - 0.02, 0.049, 0.30), (cx, 0, 0.16), "M_Door_Kickplate", 0.003)
        box(f"Leaf{i}_Vision", (min(0.3, lw*0.45), 0.05, 0.5), (cx, 0, 1.45), "M_Glass_Clear", 0.003)
        box(f"Leaf{i}_PushPlate", (0.10, 0.05, 0.35), (cx + (lw/2 - 0.12)*(1 if i == 0 else -1), 0, 1.10), "M_Steel_Polished", 0.003)
    box("Sign_Plate", (0.6, 0.008, 0.14), (0, -0.095, Hd + 0.20), "M_Sign_White", 0.004)
    text_label("Sign_Text", label, 0.045, (0, -0.1, Hd + 0.20), (R(90), 0, 0), color_mat)
    finalize_asset(root)
    return end_asset()
