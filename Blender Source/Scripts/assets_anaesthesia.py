"""Surgical zone + anaesthetic area equipment (real-world dimensions in metres)."""
from otlib import *

def build_operating_table():
    """Electro-hydraulic universal operating table.
    Tabletop 2.05 m x 0.52 m (without rails), height adjustable 0.62 - 1.05 m (modelled at 0.85 m),
    sections: head, back (2), seat, leg (split), radiolucent, side rails 25 x 10 mm,
    viscoelastic pads 80 mm, column with stainless cover, base 0.9 x 0.6 m, arm boards."""
    root = begin_asset("OperatingTable")
    top_z = 0.85
    pad = 0.07
    # base (T-shaped footprint, stainless cover)
    box("Base", (0.58, 0.95, 0.10), (0, -0.05, 0.06), "M_Steel_Brushed", 0.02)
    box("Base_Plinth", (0.50, 0.85, 0.012), (0, -0.05, 0.004), "M_Rubber_Black", 0.004)
    box("Base_Floorlock", (0.10, 0.10, 0.02), (0, 0.40, 0.012), "M_Rubber_Black", 0.004)
    # column with telescopic covers
    box("Column_Lower", (0.30, 0.38, 0.38), (0, 0.0, 0.30), "M_Steel_Brushed", 0.02)
    box("Column_Upper", (0.27, 0.35, 0.28), (0, 0.0, 0.58), "M_Steel_Polished", 0.02)
    box("Column_Head", (0.40, 0.60, 0.06), (0, 0.0, top_z - 0.17), "M_Steel_Dark", 0.01)
    # tabletop frame (radiolucent carbon tops on stainless frame)
    sections = [("Head", 0.26, 0.98), ("Back", 0.78, 0.47), ("Seat", 0.44, -0.14), ("Leg", 0.62, -0.68)]
    for nm, L, cy in sections:
        w = 0.52 if nm != "Head" else 0.28
        box(f"Frame_{nm}", (w, L - 0.01, 0.07), (0, cy, top_z - 0.105), "M_Steel_Brushed", 0.008)
        box(f"Pad_{nm}", (w - 0.005, L - 0.02, pad), (0, cy, top_z - 0.035), "M_Upholstery_Black", 0.025)
    # leg split line
    box("Pad_LegSplit", (0.004, 0.60, pad + 0.002), (0, -0.68, top_z - 0.035), "M_Rubber_Black", 0.0)
    # side accessory rails (both sides of back, seat, leg)
    for s in (-1, 1):
        for nm, L, cy in sections[1:]:
            box(f"Rail_{nm}_{s}", (0.01, L - 0.08, 0.025), (s*0.285, cy, top_z - 0.10), "M_Steel_Polished", 0.003)
            for k in (-1, 1):
                box(f"RailPost_{nm}_{s}_{k}", (0.03, 0.02, 0.02), (s*0.27, cy + k*(L/2 - 0.06), top_z - 0.10), "M_Steel_Polished", 0.003)
    # arm boards (left & right, out at 80 degrees)
    for s in (-1, 1):
        bx = s*(0.29 + 0.33)
        box(f"ArmBoard_{s}", (0.62, 0.15, 0.035), (bx, 0.55, top_z - 0.06), "M_Steel_Brushed", 0.01)
        box(f"ArmBoard_Pad_{s}", (0.60, 0.145, 0.05), (bx, 0.55, top_z - 0.02), "M_Upholstery_Black", 0.018)
        box(f"ArmBoard_Clamp_{s}", (0.06, 0.08, 0.06), (s*0.31, 0.55, top_z - 0.09), "M_Steel_Dark", 0.008)
        # arm strap
        box(f"ArmBoard_Strap_{s}", (0.05, 0.155, 0.055), (bx + s*0.12, 0.55, top_z - 0.018), "M_Plastic_Black", 0.01)
    # anaesthetic screen holder posts at head-end rail (screen is separate asset)
    # wired hand control pendant hanging on rail
    box("HandControl", (0.075, 0.03, 0.17), (0.31, -0.25, top_z - 0.24), "M_Plastic_White", 0.012)
    box("HandControl_Keys", (0.055, 0.005, 0.10), (0.31, -0.267, top_z - 0.23), "M_Plastic_Blue", 0.003)
    tube("HandControl_Cable", [(0.31, -0.25, top_z - 0.155), (0.30, -0.18, top_z - 0.12), (0.27, -0.05, top_z - 0.15), (0.12, 0.0, top_z - 0.20)], 0.004, "M_Plastic_DarkGrey")
    # body strap across thighs
    box("BodyStrap", (0.60, 0.07, 0.008), (0, -0.40, top_z + 0.002), "M_Plastic_Black", 0.002)
    # head ring (gel) on head section
    torus("HeadRing_Gel", 0.075, 0.025, (0, 1.0, top_z + 0.022), "M_Gel_Pad", 48, 16)
    e = bpy.data.objects.new("ANCHOR_PatientHead", None); link(e); e.location = (0, 1.0, top_z + 0.05)
    e = bpy.data.objects.new("ANCHOR_SurgicalSite", None); link(e); e.location = (0, 0.0, top_z + 0.15)
    finalize_asset(root)
    return end_asset()

def build_anaesthetic_screen():
    """Anaesthetic screen (ether screen / drape bar): stainless bow, 2 rail clamps.
    Separates the sterile field from the anaesthetist's area at the head of the table."""
    root = begin_asset("AnaestheticScreen")
    h = 0.55
    pts = []
    for i in range(13):
        a = math.pi * i / 12
        pts.append((0.42*math.cos(a), 0, 0.85 + 0.20 + h*0.35*math.sin(a) + (0.0)))
    tube("Bow", [(-0.42, 0, 0.75)] + pts[1:-1] + [(0.42, 0, 0.75)], 0.0095, "M_Steel_Polished", 12)
    for s in (-1, 1):
        box(f"Clamp_{s}", (0.05, 0.07, 0.08), (s*0.42, 0, 0.75), "M_Steel_Dark", 0.008)
        cyl(f"ClampKnob_{s}", 0.02, 0.04, (s*0.46, 0, 0.75), "M_Plastic_Black", 16, rot=(0, R(90), 0))
    # green sterile drape hung over the screen (simplified cloth)
    bm = bmesh.new()
    nu, nv = 24, 14
    verts = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            u = i/nu; v = j/nv
            x = -0.80 + 1.60*u
            ang = math.pi*u
            topz = 0.85 + 0.20 + h*0.35*math.sin(ang)*min(1, (0.42/max(abs(x), 0.42)))
            # drape falls toward the patient (-y) and down
            z = topz - v*0.62
            y = -0.02 - 0.18*v*v + 0.015*math.sin(u*math.pi*6)*v
            row.append(bm.verts.new((x*(1 + 0.05*v), y, z)))
        verts.append(row)
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((verts[j][i], verts[j][i+1], verts[j+1][i+1], verts[j+1][i]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.003) if hasattr(bmesh.ops, "solidify") else None
    mesh_obj("SterileDrape", bm, "M_Drape_Green", smooth_angle=80)
    finalize_asset(root)
    return end_asset()

def _knob(name, loc, r=0.012, d=0.02, mat_="M_Plastic_Black", rot=(R(90), 0, 0)):
    return cyl(name, r, d, loc, mat_, 20, bevel=0.002, rot=rot)

def build_anaesthesia_machine():
    """Anaesthesia workstation (e.g. GE Aisys / Drager Fabius class):
    W 0.80 x D 0.75 x H 1.40 m; ventilator display, flowmeters, two vaporisers
    (sevoflurane yellow, isoflurane purple), bellows, CO2 absorber, APL valve,
    breathing circuit with Y-piece & face mask, reservoir bag, O2 flush,
    drawers, writing tray, cylinder yokes at back (O2, N2O)."""
    root = begin_asset("AnaesthesiaMachine")
    W, D = 0.80, 0.70
    # chassis
    box("Chassis", (W, D, 0.62), (0, 0, 0.42), "M_Plastic_White", 0.03)
    box("Chassis_Side_L", (0.02, D - 0.04, 0.58), (-W/2 - 0.005, 0, 0.42), "M_Plastic_Grey", 0.006)
    box("Chassis_Side_R", (0.02, D - 0.04, 0.58), (W/2 + 0.005, 0, 0.42), "M_Plastic_Grey", 0.006)
    for i in range(3):
        box(f"Drawer_{i}", (W - 0.10, 0.02, 0.16), (0, -D/2 - 0.005, 0.25 + i*0.18), "M_Plastic_OffWhite", 0.01)
        box(f"Drawer_{i}_Handle", (0.30, 0.025, 0.02), (0, -D/2 - 0.02, 0.31 + i*0.18), "M_Plastic_Grey", 0.008)
    box("BaseFrame", (W + 0.05, D + 0.05, 0.08), (0, 0, 0.11), "M_Plastic_Grey", 0.02)
    for x in (-0.36, 0.36):
        for y in (-0.30, 0.30):
            caster(f"Castor_{x}_{y}", (x, y, 0.07), wheel_r=0.05, brake=(y < 0))
    # work surface / writing tray
    box("WorkSurface", (W + 0.06, D - 0.10, 0.04), (0, -0.04, 0.75), "M_Plastic_OffWhite", 0.015)
    box("WritingTray", (0.50, 0.25, 0.02), (-0.05, -D/2 - 0.10, 0.73), "M_Plastic_OffWhite", 0.008)
    # upper module (flowmeters + vaporiser bar)
    box("Upper_Body", (W, 0.35, 0.50), (0, 0.15, 1.02), "M_Plastic_White", 0.03)
    box("Upper_Front", (W - 0.06, 0.02, 0.44), (0, -0.03, 1.02), "M_Plastic_OffWhite", 0.01)
    # electronic flowmeter display + total flow tube
    box("Flow_Display", (0.22, 0.012, 0.16), (-0.24, -0.045, 1.12), "M_Screen_Off", 0.004)
    for i, (gm, label) in enumerate((("M_Gas_O2_White", "O2"), ("M_Gas_Air_Black", "AIR"), ("M_Gas_N2O_Blue", "N2O"))):
        _knob(f"Flow_Knob_{label}", (-0.10 + i*0.07, -0.06, 0.92), 0.017, 0.03, gm)
        text_label(f"Flow_Lab_{label}", label, 0.014, (-0.10 + i*0.07, -0.046, 0.955), (R(90), 0, 0), "M_Plastic_Black")
    # O2 knob is fluted & larger (safety feature)
    cyl("Flow_TotalTube", 0.012, 0.18, (0.10, -0.06, 1.10), "M_Plastic_Clear", 16)
    sphere("Flow_TotalBobbin", 0.008, (0.10, -0.06, 1.06), "M_Plastic_Black")
    # vaporiser bar with two vaporisers
    box("VapBar", (0.50, 0.08, 0.04), (0.10, 0.05, 1.29), "M_Steel_Brushed", 0.01)
    for i, (vm, lab) in enumerate((("M_Vapor_Sevo", "SEVO"), ("M_Vapor_Iso", "ISO"))):
        vx = -0.02 + i*0.24
        box(f"Vap_{lab}_Body", (0.14, 0.18, 0.22), (vx, 0.0, 1.42), vm, 0.015)
        cyl(f"Vap_{lab}_Dial", 0.06, 0.035, (vx, 0.0, 1.55), "M_Plastic_White", 40, bevel=0.004)
        box(f"Vap_{lab}_Window", (0.03, 0.005, 0.08), (vx + 0.04, -0.093, 1.40), "M_Plastic_Clear", 0.002)
        box(f"Vap_{lab}_Filler", (0.04, 0.03, 0.05), (vx - 0.04, -0.10, 1.36), "M_Plastic_DarkGrey", 0.006)
        text_label(f"Vap_{lab}_Txt", lab, 0.022, (vx, -0.0915, 1.47), (R(90), 0, 0), "M_Plastic_Black")
    # top shelf + main ventilator screen on arm
    box("TopShelf", (W + 0.04, 0.50, 0.03), (0, 0.08, 1.60), "M_Plastic_OffWhite", 0.012)
    for sx in (-1, 1):
        box(f"TopShelf_Post{sx}", (0.04, 0.30, 0.34), (sx*(W/2 - 0.03), 0.18, 1.43), "M_Plastic_White", 0.01)
    box("TopShelf_Rail", (W + 0.04, 0.02, 0.04), (0, -0.18, 1.62), "M_Steel_Brushed", 0.006)
    tube("Screen_Arm", [(-0.30, 0.10, 1.27), (-0.42, 0.0, 1.30), (-0.45, -0.18, 1.30)], 0.02, "M_Plastic_Grey")
    box("Vent_Screen_Bezel", (0.40, 0.05, 0.30), (-0.48, -0.22, 1.30), "M_Plastic_DarkGrey", 0.015, rot=(R(-10), 0, R(15)))
    p = plane("Vent_Screen", 0.36, 0.26, (-0.473, -0.247, 1.302), "M_Screen_Anaesthesia", rot=(R(-10), 0, R(15)))
    mark(p, screen="anaesthesia")
    # O2 flush button
    cyl("O2_Flush", 0.016, 0.02, (0.32, -0.06, 0.92), "M_Gas_O2_White", 24, bevel=0.003, rot=(R(90), 0, 0))
    text_label("O2_Flush_Txt", "O2+", 0.012, (0.32, -0.046, 0.955), (R(90), 0, 0), "M_Plastic_Black")
    # breathing system block on the left side: bellows + absorber + APL
    bx = W/2 + 0.12
    box("BreathingBlock", (0.20, 0.30, 0.10), (bx, -0.05, 0.84), "M_Plastic_Grey", 0.015)
    cyl("Bellows_Housing", 0.085, 0.28, (bx, 0.05, 1.03), "M_Plastic_Clear", 40)
    for k in range(7):
        torus(f"Bellows_Fold_{k}", 0.065, 0.012, (bx, 0.05, 0.92 + k*0.03), "M_Plastic_LightBlue", 32, 8)
    cyl("Bellows_Cap", 0.088, 0.02, (bx, 0.05, 1.18), "M_Plastic_Grey", 40)
    cyl("Absorber_Canister", 0.07, 0.20, (bx, -0.12, 0.68), "M_Plastic_Clear", 40)
    cyl("Absorber_SodaLime", 0.065, 0.17, (bx, -0.12, 0.67), "M_Soda_Lime", 32)
    cyl("Absorber_Lid", 0.075, 0.02, (bx, -0.12, 0.79), "M_Plastic_Grey", 40)
    cyl("APL_Valve", 0.02, 0.04, (bx + 0.05, -0.17, 0.91), "M_Plastic_Yellow", 24, bevel=0.003)
    # unidirectional valves (domes)
    for k, yy in enumerate((-0.02, 0.07)):
        lathe(f"UniValve_{k}", [(0, 0), (0.025, 0), (0.025, 0.015), (0.02, 0.03), (0, 0.035)], (bx - 0.05, yy - 0.10, 0.89), "M_Plastic_Clear", 24)
    # reservoir bag (2 L) on bag arm
    tube("BagArm", [(bx + 0.08, -0.10, 0.88), (bx + 0.18, -0.12, 0.92), (bx + 0.22, -0.18, 0.92)], 0.008, "M_Steel_Brushed")
    lathe("ReservoirBag", [(0, 0), (0.05, 0.01), (0.075, 0.06), (0.08, 0.13), (0.07, 0.20), (0.03, 0.25), (0.012, 0.27), (0.012, 0.30)],
          (bx + 0.22, -0.20, 0.60), "M_Rubber_Black", 32)
    # corrugated breathing circuit (limbs to Y-piece and mask)
    pts_i = [(bx - 0.02, -0.18, 0.88), (bx + 0.05, -0.40, 0.95), (bx + 0.10, -0.70, 0.95), (bx + 0.05, -0.95, 0.92)]
    pts_e = [(bx - 0.08, -0.18, 0.88), (bx - 0.02, -0.42, 0.97), (bx + 0.02, -0.72, 0.97), (bx + 0.03, -0.95, 0.92)]
    tube("Circuit_Insp", pts_i, 0.011, "M_Plastic_LightBlue", 10)
    tube("Circuit_Exp", pts_e, 0.011, "M_Plastic_Clear", 10)
    box("Circuit_YPiece", (0.06, 0.03, 0.03), (bx + 0.04, -0.97, 0.92), "M_Plastic_Clear", 0.008)
    tube("Circuit_HMEFilter", [(bx + 0.04, -0.99, 0.92), (bx + 0.04, -1.04, 0.92)], 0.022, "M_Plastic_White", 16)
    lathe("FaceMask", [(0.0, 0.0), (0.012, 0.0), (0.014, 0.02), (0.045, 0.05), (0.06, 0.075), (0.058, 0.085)],
          (bx + 0.04, -1.06, 0.92), "M_Plastic_Clear", 32, rot=(R(90), 0, 0))
    torus("FaceMask_Cushion", 0.055, 0.008, (bx + 0.04, -1.145, 0.92), "M_Plastic_Clear", 32, 8, rot=(R(90), 0, 0))
    # back: cylinder yokes with E-size cylinders (O2: black body / white shoulder; N2O: blue)
    for i, (body, sh, lab) in enumerate((("M_Cylinder_Black", "M_Cylinder_O2_Shoulder", "O2"), ("M_Gas_N2O_Blue", "M_Gas_N2O_Blue", "N2O"))):
        cx = -0.20 + i*0.25
        cyl(f"Cyl_{lab}_Body", 0.05, 0.62, (cx, D/2 + 0.07, 0.55), body, 32, bevel=0.01)
        lathe(f"Cyl_{lab}_Shoulder", [(0.05, 0), (0.05, 0.02), (0.035, 0.06), (0.015, 0.075), (0.0, 0.075)],
              (cx, D/2 + 0.07, 0.86), sh, 32)
        box(f"Cyl_{lab}_Yoke", (0.07, 0.06, 0.06), (cx, D/2 + 0.07, 0.96), "M_Steel_Dark", 0.008)
        box(f"Cyl_{lab}_Holder", (0.12, 0.03, 0.03), (cx, D/2 + 0.04, 0.40), "M_Steel_Brushed", 0.006)
    # pipeline hoses to pendant (colour-coded)
    for i, gm in enumerate(("M_Gas_O2_White", "M_Gas_Air_Black", "M_Gas_N2O_Blue")):
        tube(f"PipelineHose_{i}", [(0.25 + i*0.04, D/2, 1.10), (0.30 + i*0.04, D/2 + 0.20, 1.20), (0.35, D/2 + 0.45, 1.6), (0.40, D/2 + 0.7, 1.9)], 0.007, gm, 8)
    # mains & status LEDs
    box("Status_LED", (0.03, 0.005, 0.01), (0.33, -0.046, 1.20), "M_LED_Green", 0.0)
    text_label("Brand", "ANAESTHESIA WORKSTATION", 0.016, (0.12, -0.046, 1.21), (R(90), 0, 0), "M_Plastic_DarkGrey")
    finalize_asset(root)
    return end_asset()

def build_patient_monitor():
    """Multi-parameter patient monitor (15" display) on a pole/arm mount:
    ECG, SpO2, NIBP, IBP, EtCO2, Temp. Cables: ECG trunk, SpO2 probe, BP cuff hose."""
    root = begin_asset("PatientMonitor")
    # wall/rail mount column (stands on top shelf of anaesthesia machine, adjustable)
    cyl("Mount_Pole", 0.02, 0.35, (0, 0.10, 0.175), "M_Steel_Brushed", 20)
    box("Mount_Base", (0.18, 0.18, 0.02), (0, 0.10, 0.01), "M_Plastic_Grey", 0.006)
    box("Mount_Tilt", (0.08, 0.06, 0.08), (0, 0.06, 0.36), "M_Plastic_DarkGrey", 0.01)
    box("Housing", (0.40, 0.12, 0.31), (0, 0.0, 0.40), "M_Plastic_White", 0.02)
    box("Bezel", (0.38, 0.01, 0.29), (0, -0.063, 0.40), "M_Plastic_DarkGrey", 0.01)
    p = plane("Screen", 0.33, 0.25, (0, -0.069, 0.41), "M_Screen_Monitor")
    mark(p, screen="vitals")
    box("Handle", (0.22, 0.03, 0.02), (0, 0.0, 0.565), "M_Plastic_Grey", 0.008)
    box("AlarmLight", (0.10, 0.03, 0.012), (0, -0.03, 0.56), "M_LED_Green", 0.004)
    for i in range(5):
        box(f"Key_{i}", (0.03, 0.006, 0.012), (-0.12 + i*0.06, -0.066, 0.268), "M_Plastic_Grey", 0.002)
    cyl("Knob", 0.015, 0.012, (0.16, -0.066, 0.27), "M_Plastic_Black", 20, rot=(R(90), 0, 0))
    # parameter module rack on the side
    box("ModuleRack", (0.05, 0.10, 0.25), (0.225, 0.0, 0.40), "M_Plastic_OffWhite", 0.008)
    for i, (lab, m) in enumerate((("ECG", "M_Plastic_Green"), ("SpO2", "M_Plastic_Blue"), ("NIBP", "M_Plastic_Red"), ("CO2", "M_Plastic_Yellow"))):
        cyl(f"Port_{lab}", 0.008, 0.01, (0.252, -0.02, 0.48 - i*0.05), m, 12, rot=(0, R(90), 0))
    tube("Cable_ECG", [(0.255, -0.02, 0.48), (0.30, -0.05, 0.40), (0.33, -0.10, 0.10), (0.31, -0.25, -0.20)], 0.004, "M_Plastic_DarkGrey")
    tube("Cable_SpO2", [(0.255, -0.02, 0.43), (0.31, -0.03, 0.35), (0.34, -0.12, 0.05), (0.30, -0.30, -0.25)], 0.003, "M_Plastic_Blue")
    tube("Hose_NIBP", [(0.255, -0.02, 0.38), (0.29, 0.0, 0.30), (0.32, -0.06, 0.0), (0.36, -0.20, -0.30)], 0.004, "M_Plastic_Grey")
    finalize_asset(root)
    return end_asset()

def build_anaesthetic_trolley():
    """Anaesthetic drug trolley (5 drawers, colour-coded), top work surface with
    airway tray: laryngoscope, ETT, syringes; side-mounted sharps bin; glove box holder."""
    root = begin_asset("AnaestheticTrolley")
    W, D, Hh = 0.75, 0.50, 0.95
    box("Body", (W, D, 0.78), (0, 0, 0.52), "M_Plastic_White", 0.025)
    box("Base", (W + 0.04, D + 0.04, 0.06), (0, 0, 0.10), "M_Plastic_Grey", 0.02)
    for x in (-0.31, 0.31):
        for y in (-0.19, 0.19):
            caster(f"Castor_{x}_{y}", (x, y, 0.07), wheel_r=0.05, brake=(y < 0))
    drawer_mats = ["M_Plastic_Blue", "M_Plastic_Blue", "M_Plastic_Green", "M_Plastic_Yellow", "M_Plastic_Red"]
    hs = [0.10, 0.10, 0.14, 0.16, 0.18]
    z = 0.89
    labels = ["AIRWAY", "IV ACCESS", "DRUGS", "FLUIDS", "EMERGENCY"]
    for i, h in enumerate(hs):
        z -= h + 0.01
        box(f"Drawer_{i}", (W - 0.04, 0.02, h), (0, -D/2 - 0.005, z + h/2), "M_Plastic_OffWhite", 0.008)
        box(f"Drawer_{i}_Strip", (W - 0.06, 0.022, 0.015), (0, -D/2 - 0.008, z + h - 0.015), drawer_mats[i], 0.003)
        box(f"Drawer_{i}_Handle", (0.40, 0.03, 0.018), (0, -D/2 - 0.02, z + h*0.45), "M_Plastic_Grey", 0.006)
        text_label(f"Drawer_{i}_Lab", labels[i], 0.016, (0.25, -D/2 - 0.0165, z + h*0.45), (R(90), 0, 0), "M_Plastic_DarkGrey")
    box("Top", (W + 0.04, D + 0.04, 0.03), (0, 0, Hh - 0.015), "M_Plastic_OffWhite", 0.012)
    for s in (-1, 1):
        box(f"TopLip_{s}", (W + 0.04, 0.012, 0.03), (0, s*(D/2 + 0.014), Hh + 0.01), "M_Plastic_Grey", 0.004)
    box("PushHandle", (0.02, 0.40, 0.02), (W/2 + 0.06, 0, Hh - 0.05), "M_Steel_Brushed", 0.008)
    for k in (-1, 1):
        box(f"PushHandle_Post{k}", (0.06, 0.02, 0.02), (W/2 + 0.03, k*0.19, Hh - 0.05), "M_Steel_Brushed", 0.006)
    # airway tray on top: kidney dish, laryngoscope, ETT, syringes, ampoules
    tz = Hh
    box("Tray", (0.45, 0.32, 0.015), (-0.05, 0.02, tz + 0.008), "M_Steel_Brushed", 0.006)
    # laryngoscope handle + Macintosh blade
    cyl("Laryngoscope_Handle", 0.014, 0.13, (-0.20, 0.10, tz + 0.03), "M_Chrome", 20, rot=(0, R(90), 0))
    for k in range(6):
        torus(f"Laryngoscope_Knurl_{k}", 0.0142, 0.0012, (-0.25 + k*0.02, 0.10, tz + 0.03), "M_Steel_Dark", 16, 6, rot=(0, R(90), 0))
    tube("Laryngoscope_Blade", [(-0.135, 0.10, tz + 0.03), (-0.10, 0.10, tz + 0.045), (-0.04, 0.10, tz + 0.04), (0.0, 0.10, tz + 0.025)], 0.007, "M_Chrome", 8)
    # endotracheal tubes (7.0 & 7.5) in packets
    tube("ETT_75", [(-0.25, -0.05, tz + 0.02), (-0.10, -0.06, tz + 0.025), (0.0, -0.03, tz + 0.03), (0.08, 0.03, tz + 0.025)], 0.0055, "M_Plastic_Clear", 10)
    sphere("ETT_Cuff", 0.012, (0.04, 0.00, tz + 0.028), "M_Plastic_Clear", 16, 8, scale=(1.6, 1, 1))
    tube("ETT_PilotLine", [(-0.18, -0.055, tz + 0.025), (-0.17, -0.10, tz + 0.02), (-0.14, -0.12, tz + 0.02)], 0.0012, "M_Plastic_Clear", 6)
    box("ETT_PilotBalloon", (0.02, 0.01, 0.01), (-0.13, -0.122, tz + 0.02), "M_Plastic_Blue", 0.004)
    box("ETT_Connector", (0.025, 0.017, 0.017), (-0.26, -0.05, tz + 0.02), "M_Plastic_White", 0.004)
    # syringes (10 ml, 5 ml, 2 ml) with coloured drug labels
    for i, (L, r, lm) in enumerate(((0.10, 0.008, "M_Plastic_Yellow"), (0.085, 0.0065, "M_Plastic_Blue"), (0.07, 0.005, "M_Plastic_Red"))):
        y = -0.08 - i*0.035
        cyl(f"Syringe_{i}_Barrel", r, L, (0.10, y, tz + 0.02), "M_Plastic_Clear", 16, rot=(0, R(90), 0))
        cyl(f"Syringe_{i}_Plunger", r*0.7, L*0.6, (0.10 - L*0.6, y, tz + 0.02), "M_Plastic_White", 12, rot=(0, R(90), 0))
        cyl(f"Syringe_{i}_Label", r + 0.0004, L*0.4, (0.11, y, tz + 0.02), lm, 16, rot=(0, R(90), 0))
        cyl(f"Syringe_{i}_Tip", 0.002, 0.012, (0.10 + L/2 + 0.006, y, tz + 0.02), "M_Plastic_Clear", 8, rot=(0, R(90), 0))
    # glove box holder on side
    box("GloveBox", (0.25, 0.13, 0.07), (-0.10, D/2 + 0.08, 0.80), "M_Cardboard", 0.004)
    box("GloveBox_Holder", (0.27, 0.14, 0.01), (-0.10, D/2 + 0.08, 0.76), "M_Steel_Brushed", 0.003)
    box("GloveBox_Slot", (0.10, 0.005, 0.04), (-0.10, D/2 + 0.08, 0.837), "M_Glove_Blue", 0.003, rot=(R(90), 0, 0))
    finalize_asset(root)
    return end_asset()

def build_sharps_bin():
    """Yellow rigid sharps container (WHO / UN3291), 7 L, with temporary closure lid.
    Fill line at 3/4. Must never be filled above the line."""
    root = begin_asset("SharpsBin")
    lathe("Body", [(0.0, 0.0), (0.095, 0.0), (0.10, 0.01), (0.11, 0.22), (0.112, 0.23), (0.0, 0.23)], (0, 0, 0), "M_Plastic_Yellow", 48)
    lathe("Lid", [(0.0, 0.0), (0.115, 0.0), (0.116, 0.02), (0.08, 0.04), (0.0, 0.045)], (0, 0, 0.23), "M_Plastic_Yellow", 48)
    box("Aperture", (0.10, 0.06, 0.012), (0, -0.02, 0.272), "M_Plastic_Black", 0.004)
    box("TempClosure", (0.11, 0.03, 0.01), (0, 0.06, 0.275), "M_Plastic_Yellow", 0.004)
    cyl("Label_Band", 0.1065, 0.08, (0, 0, 0.12), "M_Label_Print", 48)
    text_label("Label_T1", "SHARPS", 0.025, (0, -0.108, 0.135), (R(90), 0, 0), "M_Plastic_Black")
    text_label("Label_T2", "DO NOT FILL ABOVE LINE", 0.009, (0, -0.108, 0.10), (R(90), 0, 0), "M_Plastic_Red")
    torus("FillLine", 0.1085, 0.0012, (0, 0, 0.175), "M_Plastic_Red", 48, 6)
    finalize_asset(root)
    return end_asset()

def build_iv_stand():
    """Mobile IV drip stand: 5-castor base (dia 0.60 m), telescopic stainless pole
    (1.5 - 2.3 m), 4 hooks, holding a 1 L saline bag with giving set (drip chamber,
    roller clamp)."""
    root = begin_asset("IVDripStand")
    for i in range(5):
        a = 2*math.pi*i/5
        x, y = 0.28*math.cos(a), 0.28*math.sin(a)
        tube(f"Leg_{i}", [(0, 0, 0.12), (x*0.5, y*0.5, 0.10), (x, y, 0.09)], 0.012, "M_Steel_Brushed", 10)
        caster(f"Castor_{i}", (x, y, 0.08), wheel_r=0.04)
    cyl("Hub", 0.04, 0.06, (0, 0, 0.12), "M_Steel_Dark", 24)
    cyl("Pole_Lower", 0.016, 1.10, (0, 0, 0.70), "M_Steel_Polished", 20)
    cyl("Pole_Collar", 0.022, 0.05, (0, 0, 1.26), "M_Plastic_Black", 20, bevel=0.003)
    cyl("Pole_Upper", 0.012, 0.80, (0, 0, 1.68), "M_Chrome", 20)
    sphere("Pole_Top", 0.016, (0, 0, 2.09), "M_Plastic_Black")
    for i in range(4):
        a = math.pi/2*i
        dx, dy = math.cos(a), math.sin(a)
        tube(f"Hook_{i}", [(0, 0, 2.04), (dx*0.08, dy*0.08, 2.06), (dx*0.13, dy*0.13, 2.05), (dx*0.14, dy*0.14, 2.02)], 0.005, "M_Chrome", 8)
    # saline bag 1 L (approx 0.13 x 0.22 m) on hook 0
    bx = 0.14
    bm = bmesh.new()
    prof = []
    bag = box("SalineBag", (0.12, 0.03, 0.20), (bx, 0, 1.88), "M_Fluid_Saline", 0.012)
    box("SalineBag_Label", (0.08, 0.032, 0.10), (bx, 0, 1.89), "M_Label_Print", 0.004)
    text_label("SalineBag_Txt", "0.9% NaCl\n1000 ml", 0.012, (bx, -0.017, 1.89), (R(90), 0, 0), "M_Plastic_Blue")
    box("SalineBag_Hanger", (0.03, 0.004, 0.02), (bx, 0, 2.0), "M_Plastic_Clear", 0.003)
    cyl("SalineBag_Port", 0.006, 0.03, (bx, 0, 1.765), "M_Plastic_White", 12)
    # giving set
    lathe("DripChamber", [(0, 0), (0.008, 0.0), (0.011, 0.01), (0.011, 0.055), (0.006, 0.065), (0.003, 0.08)], (bx, 0, 1.66), "M_Plastic_Clear", 20, rot=(R(180), 0, 0))
    cyl("DripChamber_Fluid", 0.0095, 0.02, (bx, 0, 1.60), "M_Fluid_Saline", 16)
    tube("GivingSet_Line", [(bx, 0, 1.58), (bx + 0.02, -0.02, 1.30), (bx - 0.02, -0.05, 1.00), (bx - 0.10, -0.15, 0.95)], 0.0018, "M_Plastic_Clear", 6)
    box("RollerClamp", (0.018, 0.012, 0.05), (bx + 0.012, -0.03, 1.25), "M_Plastic_White", 0.004)
    cyl("RollerClamp_Wheel", 0.008, 0.006, (bx + 0.012, -0.037, 1.26), "M_Plastic_Blue", 12, rot=(R(90), 0, 0))
    finalize_asset(root)
    return end_asset()

def build_infusion_pump():
    """Volumetric infusion pump + syringe driver stacked on a pole clamp
    (B. Braun / Alaris class). Pump 0.22 x 0.12 x 0.18 m; syringe driver 0.30 x 0.11 x 0.09 m."""
    root = begin_asset("InfusionPumps")
    cyl("Pole", 0.014, 1.0, (0, 0.10, 0.5), "M_Steel_Polished", 16)
    # volumetric pump
    box("VP_Body", (0.22, 0.13, 0.19), (0, 0, 0.75), "M_Plastic_White", 0.02)
    box("VP_Door", (0.08, 0.01, 0.16), (0.06, -0.068, 0.75), "M_Plastic_LightBlue", 0.008)
    box("VP_Screen_Bezel", (0.10, 0.006, 0.07), (-0.045, -0.067, 0.79), "M_Plastic_DarkGrey", 0.004)
    p = plane("VP_Screen", 0.09, 0.06, (-0.045, -0.071, 0.79), "M_Screen_Pump"); mark(p, screen="pump")
    for i in range(6):
        box(f"VP_Key_{i}", (0.022, 0.006, 0.014), (-0.07 + (i % 3)*0.025, -0.068, 0.725 - (i // 3)*0.02), "M_Plastic_Grey", 0.003)
    box("VP_Clamp", (0.05, 0.06, 0.05), (0, 0.08, 0.75), "M_Plastic_Grey", 0.008)
    box("VP_LED", (0.015, 0.004, 0.006), (-0.09, -0.068, 0.835), "M_LED_Green", 0.0)
    # syringe driver
    box("SD_Body", (0.30, 0.11, 0.09), (0, 0, 0.55), "M_Plastic_White", 0.015)
    cyl("SD_Syringe", 0.014, 0.16, (0.01, -0.065, 0.575), "M_Plastic_Clear", 20, rot=(0, R(90), 0))
    cyl("SD_SyringeFluid", 0.0125, 0.10, (0.04, -0.065, 0.575), "M_Fluid_Saline", 16, rot=(0, R(90), 0))
    box("SD_Plunger", (0.03, 0.03, 0.04), (-0.09, -0.065, 0.575), "M_Plastic_Grey", 0.006)
    box("SD_Clamp", (0.02, 0.03, 0.04), (0.10, -0.065, 0.60), "M_Plastic_Grey", 0.004)
    p = plane("SD_Screen", 0.07, 0.03, (-0.08, -0.0555, 0.53), "M_Screen_Pump"); mark(p, screen="pump")
    box("SD_PoleClamp", (0.05, 0.06, 0.05), (0, 0.075, 0.55), "M_Plastic_Grey", 0.008)
    finalize_asset(root)
    return end_asset()
