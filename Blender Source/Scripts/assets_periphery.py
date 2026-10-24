"""Trolleys, surgical machines, bins, emergency & peripheral infrastructure."""
from otlib import *

def build_mayo_stand():
    """Mayo instrument stand: single-column, height adjustable 0.85-1.35 m,
    U-shaped base with 3 castors slides under the operating table; tray 0.48 x 0.36 m."""
    root = begin_asset("MayoStand")
    # U base
    tube("Base_U", [(-0.25, -0.30, 0.09), (-0.25, 0.20, 0.09), (0.0, 0.25, 0.09), (0.25, 0.20, 0.09), (0.25, -0.30, 0.09)], 0.018, "M_Steel_Brushed", 10, smooth_curve=False)
    for i, (x, y) in enumerate(((-0.25, -0.30), (0.25, -0.30), (0.0, 0.25))):
        caster(f"Castor_{i}", (x, y, 0.07), wheel_r=0.035)
    cyl("Column", 0.022, 1.00, (0, 0.25, 0.60), "M_Steel_Polished", 20)
    cyl("Column_Inner", 0.016, 0.30, (0, 0.25, 1.15), "M_Chrome", 20)
    cyl("Clamp", 0.03, 0.06, (0, 0.25, 1.08), "M_Steel_Dark", 20)
    cyl("Clamp_Knob", 0.012, 0.05, (0.04, 0.25, 1.08), "M_Plastic_Black", 16, rot=(0, R(90), 0))
    tube("TrayArm", [(0, 0.25, 1.28), (0, 0.10, 1.29), (0, 0.0, 1.29)], 0.015, "M_Steel_Polished", 10)
    # tray (stainless, removable) with sterile drape
    box("TrayFrame", (0.50, 0.38, 0.02), (0, -0.02, 1.30), "M_Steel_Brushed", 0.006)
    box("Tray", (0.48, 0.36, 0.015), (0, -0.02, 1.315), "M_Steel_Polished", 0.004)
    box("TrayDrape", (0.56, 0.44, 0.004), (0, -0.02, 1.325), "M_Drape_Green", 0.002)
    for s in (-1, 1):
        box(f"DrapeFall_{s}", (0.005, 0.44, 0.18), (s*0.282, -0.02, 1.235), "M_Drape_Green", 0.002)
    box("DrapeFall_F", (0.56, 0.005, 0.18), (0, -0.242, 1.235), "M_Drape_Green", 0.002)
    e = bpy.data.objects.new("ANCHOR_TrayTop", None); link(e); e.location = (0, -0.02, 1.33)
    finalize_asset(root)
    return end_asset()

def build_instrument_trolley():
    """Instrument (back) trolley - stainless steel, 1.2 x 0.6 m top, 0.90 m high,
    lower shelf, guard rails; top covered with a sterile drape and laid instruments."""
    root = begin_asset("InstrumentTrolley")
    W, D, H = 1.20, 0.60, 0.90
    box("Top", (W, D, 0.03), (0, 0, H - 0.015), "M_Steel_Brushed", 0.006)
    box("Shelf", (W, D, 0.025), (0, 0, 0.30), "M_Steel_Brushed", 0.006)
    for x in (-W/2 + 0.02, W/2 - 0.02):
        for y in (-D/2 + 0.02, D/2 - 0.02):
            cyl(f"Leg_{x}_{y}", 0.016, H - 0.12, (x, y, (H - 0.12)/2 + 0.10), "M_Steel_Polished", 16)
            caster(f"Castor_{x}_{y}", (x, y, 0.10), wheel_r=0.05, brake=(x < 0))
    for s in (-1, 1):
        box(f"ShelfRail_{s}", (W, 0.012, 0.05), (0, s*(D/2 - 0.006), 0.34), "M_Steel_Brushed", 0.004)
    # sterile drape covering top, falling 300 mm on all sides
    box("Drape_Top", (W + 0.10, D + 0.10, 0.004), (0, 0, H + 0.002), "M_Drape_Green", 0.002)
    for s in (-1, 1):
        box(f"Drape_Side_{s}", (W + 0.10, 0.004, 0.32), (0, s*(D/2 + 0.05), H - 0.16), "M_Drape_Green", 0.002)
        box(f"Drape_End_{s}", (0.004, D + 0.10, 0.32), (s*(W/2 + 0.05), 0, H - 0.16), "M_Drape_Green", 0.002)
    # instrument tray (stainless) on drape
    box("InstrTray", (0.48, 0.30, 0.04), (-0.25, 0.05, H + 0.024), "M_Steel_Polished", 0.006)
    # bowls (lotion bowl), towels, gown pack
    lathe("LotionBowl", [(0, 0), (0.07, 0), (0.075, 0.005), (0.10, 0.06), (0.104, 0.065), (0.098, 0.064), (0.07, 0.008), (0, 0.006)], (0.30, 0.10, H + 0.004), "M_Steel_Polished", 48)
    box("Towels", (0.25, 0.18, 0.05), (0.33, -0.16, H + 0.03), "M_Drape_Green", 0.012)
    e = bpy.data.objects.new("ANCHOR_TrolleyTop", None); link(e); e.location = (-0.25, 0.05, H + 0.045)
    finalize_asset(root)
    return end_asset()

def build_diathermy():
    """Electrosurgical unit (diathermy) on its trolley with monopolar pencil,
    bipolar forceps lead, dual-pedal footswitch and patient return electrode."""
    root = begin_asset("DiathermyUnit")
    # trolley
    box("Trolley_Top", (0.50, 0.45, 0.02), (0, 0, 0.80), "M_Plastic_Grey", 0.006)
    box("Trolley_Shelf", (0.50, 0.45, 0.02), (0, 0, 0.30), "M_Plastic_Grey", 0.006)
    for x in (-0.22, 0.22):
        for y in (-0.19, 0.19):
            cyl(f"Leg_{x}_{y}", 0.014, 0.72, (x, y, 0.46), "M_Aluminium", 12)
            caster(f"Castor_{x}_{y}", (x, y, 0.10), wheel_r=0.04)
    # generator
    box("ESU_Body", (0.40, 0.42, 0.17), (0, 0.0, 0.895), "M_Plastic_White", 0.02)
    box("ESU_Front", (0.38, 0.01, 0.15), (0, -0.215, 0.895), "M_Plastic_LightBlue", 0.006)
    p = plane("ESU_Screen", 0.20, 0.06, (0, -0.221, 0.925), "M_Screen_Diathermy"); mark(p, screen="diathermy")
    for i, (lab, m) in enumerate((("CUT", "M_Plastic_Yellow"), ("COAG", "M_Plastic_Blue"))):
        box(f"ESU_Btn_{lab}", (0.04, 0.008, 0.02), (-0.08 + i*0.16, -0.222, 0.87), m, 0.003)
    for i, (lab, m) in enumerate((("MONO", "M_Plastic_Blue"), ("BIPOLAR", "M_Plastic_Grey"), ("RETURN", "M_Plastic_Grey"))):
        cyl(f"ESU_Port_{lab}", 0.012, 0.01, (-0.12 + i*0.12, -0.222, 0.845), m, 16, rot=(R(90), 0, 0))
    box("ESU_Power", (0.02, 0.008, 0.02), (0.165, -0.222, 0.845), "M_LED_Green", 0.003)
    # monopolar pencil lead & pencil hanging in holster
    tube("Lead_Mono", [(-0.12, -0.23, 0.845), (-0.15, -0.35, 0.80), (-0.20, -0.45, 0.85), (-0.25, -0.60, 1.0)], 0.0035, "M_Plastic_LightBlue", 6)
    cyl("Pencil", 0.008, 0.15, (-0.26, -0.62, 1.0), "M_Plastic_LightBlue", 12, rot=(R(30), 0, 0))
    box("Pencil_BtnY", (0.006, 0.004, 0.015), (-0.26, -0.625, 1.01), "M_Plastic_Yellow", 0.001, rot=(R(30), 0, 0))
    box("Pencil_BtnB", (0.006, 0.004, 0.015), (-0.26, -0.63, 0.99), "M_Plastic_Blue", 0.001, rot=(R(30), 0, 0))
    # patient return electrode (grounding pad) lead
    tube("Lead_Return", [(0.12, -0.23, 0.845), (0.14, -0.35, 0.60), (0.18, -0.45, 0.10), (0.30, -0.70, 0.01)], 0.003, "M_Plastic_Grey", 6)
    box("ReturnPad", (0.16, 0.10, 0.003), (0.35, -0.80, 0.002), "M_Gel_Pad", 0.002)
    # footswitch (dual pedal: yellow CUT, blue COAG) with cable on floor - trip hazard
    fx, fy = -0.20, -0.90
    box("Foot_Base", (0.32, 0.22, 0.04), (fx, fy, 0.02), "M_Plastic_Grey", 0.012)
    box("Foot_PedalCut", (0.12, 0.15, 0.02), (fx - 0.075, fy - 0.01, 0.048), "M_Plastic_Yellow", 0.008, rot=(R(-6), 0, 0))
    box("Foot_PedalCoag", (0.12, 0.15, 0.02), (fx + 0.075, fy - 0.01, 0.048), "M_Plastic_Blue", 0.008, rot=(R(-6), 0, 0))
    box("Foot_Guard", (0.33, 0.02, 0.07), (fx, fy + 0.10, 0.06), "M_Plastic_Grey", 0.008)
    tube("Foot_Cable", [(fx, fy + 0.11, 0.02), (fx + 0.05, fy + 0.40, 0.01), (fx + 0.10, -0.40, 0.01), (0.0, 0.0, 0.30), (0.0, 0.18, 0.86)], 0.004, "M_Plastic_DarkGrey", 6)
    finalize_asset(root)
    return end_asset()

def build_suction_unit():
    """Mobile surgical suction unit: vacuum regulator, 2 x 2 L disposable canisters
    with liners, Yankauer sucker on tubing. (Pipeline suction from the pendant VAC outlet.)"""
    root = begin_asset("SuctionUnit")
    box("Base", (0.45, 0.40, 0.08), (0, 0, 0.12), "M_Plastic_Grey", 0.02)
    for x in (-0.18, 0.18):
        for y in (-0.15, 0.15):
            caster(f"Castor_{x}_{y}", (x, y, 0.08), wheel_r=0.04)
    cyl("Pole", 0.018, 1.00, (0, 0.12, 0.66), "M_Steel_Polished", 16)
    box("Regulator", (0.10, 0.08, 0.14), (0, 0.06, 1.05), "M_Plastic_White", 0.012)
    cyl("Regulator_Gauge", 0.04, 0.02, (0, 0.015, 1.07), "M_Plastic_White", 32, rot=(R(90), 0, 0))
    cyl("Regulator_GaugeFace", 0.035, 0.003, (0, 0.004, 1.07), "M_Label_Print", 32, rot=(R(90), 0, 0))
    box("Regulator_Needle", (0.002, 0.002, 0.03), (0.005, 0.002, 1.08), "M_Plastic_Red", 0.0, rot=(0, R(30), 0))
    cyl("Regulator_Knob", 0.015, 0.02, (0, 0.06, 1.13), "M_Gas_Vac_Yellow", 16)
    box("CanisterBracket", (0.32, 0.06, 0.03), (0, 0.10, 0.75), "M_Steel_Brushed", 0.006)
    for i, x in enumerate((-0.09, 0.09)):
        cyl(f"Canister_{i}", 0.065, 0.22, (x, 0.04, 0.63), "M_Plastic_Clear", 40)
        cyl(f"Canister_{i}_Liner", 0.06, 0.20, (x, 0.04, 0.63), "M_Canister_Liner", 32)
        cyl(f"Canister_{i}_Fluid", 0.058, 0.06 + i*0.04, (x, 0.04, 0.55 + (0.03 + i*0.02)), "M_Fluid_Blood", 32)
        cyl(f"Canister_{i}_Lid", 0.07, 0.025, (x, 0.04, 0.755), "M_Plastic_Blue", 40)
        cyl(f"Canister_{i}_Port", 0.008, 0.03, (x + 0.03, 0.04, 0.78), "M_Plastic_Orange", 12)
        for k in range(4):
            torus(f"Canister_{i}_Grad_{k}", 0.0652, 0.0007, (x, 0.04, 0.56 + k*0.04), "M_Plastic_DarkGrey", 32, 4)
    tube("VacHose", [(0, 0.06, 1.12), (0.0, 0.10, 1.40), (-0.2, 0.3, 1.9), (-0.4, 0.5, 2.0)], 0.007, "M_Gas_Vac_Yellow", 8)
    tube("PatientTubing", [(0.12, 0.04, 0.79), (0.20, -0.10, 0.95), (0.25, -0.40, 1.05), (0.20, -0.70, 0.95)], 0.005, "M_Plastic_Clear", 8)
    tube("Yankauer", [(0.20, -0.70, 0.95), (0.20, -0.82, 0.96), (0.19, -0.92, 0.94), (0.17, -0.97, 0.90)], 0.006, "M_Plastic_Clear", 10)
    finalize_asset(root)
    return end_asset()

def build_waste_bin(kind="Clinical"):
    """Pedal-operated (hands-free) waste bins, 50 L.
    Clinical/infectious = yellow, General = black (WHO colour coding)."""
    m = {"Clinical": ("M_Plastic_Yellow", "CLINICAL WASTE", "M_Plastic_Black"),
         "General": ("M_Plastic_Black", "GENERAL WASTE", "M_Sign_White")}[kind]
    root = begin_asset(f"WasteBin_{kind}")
    box("Body", (0.40, 0.33, 0.58), (0, 0, 0.32), m[0], 0.04)
    box("Lid", (0.41, 0.34, 0.04), (0, 0.0, 0.63), m[0], 0.02)
    box("Hinge", (0.35, 0.02, 0.03), (0, 0.175, 0.62), "M_Plastic_DarkGrey", 0.008)
    box("Pedal", (0.18, 0.10, 0.025), (0, -0.20, 0.05), "M_Steel_Brushed", 0.008)
    box("PedalArm", (0.03, 0.05, 0.02), (0, -0.155, 0.05), "M_Steel_Dark", 0.004)
    box("Feet", (0.38, 0.31, 0.03), (0, 0, 0.015), "M_Plastic_DarkGrey", 0.008)
    box("Label", (0.30, 0.004, 0.08), (0, -0.166, 0.45), "M_Label_Print" if kind == "Clinical" else m[0], 0.002)
    text_label("LabelTxt", m[1], 0.025, (0, -0.169, 0.45), (R(90), 0, 0), m[2])
    box("BagFold", (0.38, 0.31, 0.03), (0, 0, 0.60), "M_Plastic_Yellow" if kind == "Clinical" else "M_Plastic_Black", 0.008)
    finalize_asset(root)
    return end_asset()

def build_kick_bucket():
    """Swab (kick) bucket on castor ring - for discarded swabs; swabs are counted
    from here (swab count). 13 L stainless bowl."""
    root = begin_asset("KickBucket")
    torus("Ring", 0.17, 0.01, (0, 0, 0.09), "M_Steel_Polished", 48, 10)
    for i in range(3):
        a = 2*math.pi*i/3
        x, y = 0.17*math.cos(a), 0.17*math.sin(a)
        caster(f"Castor_{i}", (x, y, 0.08), wheel_r=0.03)
        tube(f"Strut_{i}", [(x, y, 0.09), (x*0.8, y*0.8, 0.22), (x*0.85, y*0.85, 0.30)], 0.008, "M_Steel_Polished", 8)
    lathe("Bucket", [(0, 0.18), (0.10, 0.18), (0.16, 0.25), (0.175, 0.36), (0.18, 0.365), (0.172, 0.365), (0.155, 0.255), (0.095, 0.188), (0, 0.186)],
          (0, 0, 0), "M_Steel_Polished", 48)
    lathe("Liner", [(0, 0.19), (0.09, 0.19), (0.15, 0.25), (0.168, 0.355), (0.0, 0.355)], (0, 0, 0), "M_Plastic_Yellow", 32)
    finalize_asset(root)
    return end_asset()

def build_linen_skip():
    """Linen skip (used linen hamper) - pedal lid, white/clear bag for used linen."""
    root = begin_asset("LinenSkip")
    for x in (-0.20, 0.20):
        for y in (-0.20, 0.20):
            cyl(f"Leg_{x}_{y}", 0.012, 0.85, (x, y, 0.47), "M_Steel_Polished", 12)
            caster(f"Castor_{x}_{y}", (x, y, 0.06), wheel_r=0.035)
    torus("Hoop", 0.22, 0.012, (0, 0, 0.88), "M_Steel_Polished", 48, 10)
    lathe("Bag", [(0, 0.20), (0.18, 0.22), (0.21, 0.4), (0.22, 0.86), (0.23, 0.90), (0, 0.90)], (0, 0, 0), "M_Plastic_White", 32)
    cyl("Lid", 0.235, 0.02, (0, 0, 0.92), "M_Plastic_Blue", 48, bevel=0.006)
    box("Pedal", (0.15, 0.08, 0.02), (0, -0.27, 0.08), "M_Steel_Brushed", 0.006)
    finalize_asset(root)
    return end_asset()

def build_crash_cart():
    """Emergency (crash) cart: red, 5 drawers, defibrillator/monitor on top,
    O2 cylinder holder, CPR board on back, sharps bin, sealed with tag."""
    root = begin_asset("CrashCart")
    W, D, H = 0.76, 0.56, 1.0
    box("Body", (W, D, 0.80), (0, 0, 0.52), "M_Painted_Red", 0.02)
    box("Base", (W + 0.04, D + 0.04, 0.06), (0, 0, 0.10), "M_Plastic_DarkGrey", 0.02)
    for x in (-0.32, 0.32):
        for y in (-0.22, 0.22):
            caster(f"Castor_{x}_{y}", (x, y, 0.07), wheel_r=0.05, brake=(y < 0))
    z = 0.90
    for i, h in enumerate((0.10, 0.10, 0.14, 0.18, 0.22)):
        z -= h + 0.008
        box(f"Drawer_{i}", (W - 0.04, 0.02, h), (0, -D/2 - 0.005, z + h/2), "M_Painted_Red", 0.008)
        box(f"Drawer_{i}_Handle", (0.45, 0.03, 0.02), (0, -D/2 - 0.02, z + h*0.55), "M_Plastic_DarkGrey", 0.006)
        box(f"Drawer_{i}_Label", (0.10, 0.004, 0.03), (0.25, -D/2 - 0.016, z + h*0.55), "M_Label_Print", 0.002)
    box("Top", (W + 0.06, D + 0.04, 0.03), (0, 0, 0.94), "M_Plastic_DarkGrey", 0.012)
    box("SealTag", (0.02, 0.005, 0.04), (W/2 - 0.03, -D/2 - 0.03, 0.85), "M_Plastic_Yellow", 0.002)
    box("CPR_Board", (0.50, 0.02, 0.60), (0, D/2 + 0.03, 0.62), "M_Plastic_Orange", 0.01)
    box("PushHandle", (0.02, 0.45, 0.02), (-W/2 - 0.06, 0, 0.92), "M_Steel_Brushed", 0.008)
    # defibrillator (AED/manual biphasic) on top
    box("Defib_Body", (0.32, 0.26, 0.22), (-0.12, 0.05, 1.065), "M_Plastic_DarkGrey", 0.02)
    box("Defib_Front", (0.30, 0.01, 0.20), (-0.12, -0.085, 1.065), "M_Plastic_Grey", 0.008)
    p = plane("Defib_Screen", 0.14, 0.10, (-0.15, -0.091, 1.09), "M_Screen_Defib"); mark(p, screen="defib")
    for i, (lab, m) in enumerate((("1", "M_Plastic_Green"), ("2", "M_Plastic_Yellow"), ("3", "M_Plastic_Orange"))):
        cyl(f"Defib_Btn_{lab}", 0.014, 0.01, (0.0, -0.09, 1.13 - i*0.04), m, 20, rot=(R(90), 0, 0))
    cyl("Defib_Knob", 0.03, 0.02, (-0.22, -0.09, 1.0), "M_Plastic_Black", 24, bevel=0.003, rot=(R(90), 0, 0))
    for s in (-1, 1):
        box(f"Defib_Paddle_{s}", (0.06, 0.10, 0.08), (0.16 + s*0.05, 0.05, 1.0), "M_Plastic_Black", 0.015)
        box(f"Defib_PaddlePlate_{s}", (0.07, 0.11, 0.008), (0.16 + s*0.05, 0.05, 0.96), "M_Steel_Polished", 0.003)
    text_label("Defib_Txt", "DEFIBRILLATOR", 0.018, (-0.12, -0.0915, 1.165), (R(90), 0, 0), "M_Sign_White")
    # O2 cylinder holder
    cyl("O2Cyl", 0.05, 0.55, (W/2 + 0.07, 0.15, 0.55), "M_Cylinder_Black", 32, bevel=0.01)
    lathe("O2Cyl_Shoulder", [(0.05, 0), (0.05, 0.02), (0.03, 0.06), (0.012, 0.07), (0, 0.07)], (W/2 + 0.07, 0.15, 0.825), "M_Cylinder_O2_Shoulder", 32)
    box("O2Cyl_Regulator", (0.04, 0.06, 0.05), (W/2 + 0.07, 0.15, 0.92), "M_Steel_Polished", 0.008)
    box("O2Cyl_Holder", (0.12, 0.12, 0.02), (W/2 + 0.07, 0.15, 0.40), "M_Steel_Brushed", 0.004)
    text_label("Txt", "EMERGENCY", 0.04, (0, -D/2 - 0.0165, 0.86), (R(90), 0, 0), "M_Sign_White")
    finalize_asset(root)
    return end_asset()

def build_patient_warmer():
    """Forced-air patient warming unit (e.g. Bair Hugger) on IV-pole clamp with hose -
    prevents peri-operative hypothermia (core temp kept >= 36 C)."""
    root = begin_asset("PatientWarmer")
    for i in range(5):
        a = 2*math.pi*i/5
        x, y = 0.25*math.cos(a), 0.25*math.sin(a)
        tube(f"Leg_{i}", [(0, 0, 0.12), (x, y, 0.09)], 0.012, "M_Steel_Brushed", 8, smooth_curve=False)
        caster(f"Castor_{i}", (x, y, 0.08), wheel_r=0.035)
    cyl("Pole", 0.016, 1.0, (0, 0, 0.6), "M_Steel_Polished", 16)
    box("Unit", (0.28, 0.24, 0.30), (0, -0.05, 0.85), "M_Plastic_White", 0.03)
    box("Unit_Panel", (0.20, 0.01, 0.10), (0, -0.175, 0.92), "M_Plastic_Blue", 0.006)
    for i, m in enumerate(("M_LED_Green", "M_LED_Amber", "M_LED_Amber", "M_LED_Red")):
        box(f"TempLED_{i}", (0.025, 0.004, 0.015), (-0.06 + i*0.04, -0.181, 0.94), m, 0.0)
    text_label("Txt", "38 C", 0.02, (0, -0.181, 0.90), (R(90), 0, 0), "M_Sign_White")
    hose = [(0.14, -0.05, 0.80), (0.30, -0.10, 0.82), (0.45, -0.25, 0.90), (0.55, -0.45, 0.95)]
    tube("Hose", hose, 0.032, "M_Plastic_White", 16)
    for k in range(14):
        t = k / 13.0
        x = 0.14 + t*0.41; y = -0.05 - 0.40*t*t; z = 0.80 + 0.15*t
        torus(f"HoseRib_{k}", 0.033, 0.004, (x, y, z), "M_Plastic_OffWhite", 20, 6, rot=(0, R(90), math.atan2(y + 0.05, x - 0.14)))
    box("Hose_Blanket", (0.40, 0.25, 0.02), (0.60, -0.58, 0.95), "M_Paper_White", 0.01)
    finalize_asset(root)
    return end_asset()

def build_fluid_warmer_cabinet():
    """Blood / IV fluid warming cabinet (37 C) with glass door and shelves."""
    root = begin_asset("FluidWarmerCabinet")
    box("Body", (0.60, 0.55, 0.85), (0, 0, 0.425), "M_Painted_White", 0.015)
    box("Door", (0.52, 0.02, 0.70), (0, -0.28, 0.45), "M_Glass_Clear", 0.004)
    box("DoorFrame", (0.56, 0.018, 0.74), (0, -0.27, 0.45), "M_Steel_Brushed", 0.004)
    box("Handle", (0.02, 0.04, 0.25), (0.23, -0.30, 0.45), "M_Steel_Brushed", 0.006)
    for i in range(2):
        box(f"Shelf_{i}", (0.52, 0.48, 0.01), (0, 0, 0.25 + i*0.25), "M_Steel_Brushed", 0.002)
        for k in range(4):
            box(f"FluidBag_{i}_{k}", (0.10, 0.04, 0.16), (-0.18 + k*0.12, 0.0, 0.34 + i*0.25), "M_Fluid_Saline", 0.01)
    box("Display", (0.12, 0.01, 0.05), (0, -0.276, 0.80), "M_Screen_Off", 0.003)
    text_label("DisplayTxt", "37.0 C", 0.018, (0, -0.282, 0.80), (R(90), 0, 0), "M_LED_Red")
    finalize_asset(root)
    return end_asset()

def build_sterile_cabinet(width=1.2):
    """Wall-recessed stainless sterile supply cabinet with glass doors,
    shelved sterile packs (wrapped sets), gowns, gloves, drapes."""
    root = begin_asset("SterileSupplyCabinet")
    H, D = 2.0, 0.45
    box("Carcass", (width, D, H), (0, 0, H/2 + 0.1), "M_Steel_Brushed", 0.008)
    box("Plinth", (width - 0.02, D - 0.05, 0.1), (0, 0.02, 0.05), "M_Steel_Dark", 0.004)
    for i in range(4):
        z = 0.35 + i*0.42
        box(f"Shelf_{i}", (width - 0.04, D - 0.04, 0.012), (0, 0, z), "M_Steel_Polished", 0.002)
        # sterile packs (blue wrapped sets with indicator tape), glove boxes
        nx = int((width - 0.1) / 0.24)
        for k in range(nx):
            x = -width/2 + 0.14 + k*0.24
            if i == 0:
                box(f"Pack_{i}_{k}", (0.20, 0.32, 0.14), (x, 0.0, z + 0.077), "M_Drape_Blue", 0.02)
                box(f"PackTape_{i}_{k}", (0.205, 0.03, 0.142), (x, -0.05, z + 0.077), "M_Paper_White", 0.0)
            elif i == 1:
                box(f"Gown_{i}_{k}", (0.20, 0.30, 0.10), (x, 0.0, z + 0.057), "M_Paper_White", 0.015)
            elif i == 2:
                box(f"Gloves_{i}_{k}", (0.20, 0.10, 0.12), (x, -0.05, z + 0.066), "M_Paper_White", 0.004)
                box(f"GlovesBand_{i}_{k}", (0.205, 0.102, 0.03), (x, -0.05, z + 0.066), "M_Plastic_Green" if k % 2 else "M_Plastic_Blue", 0.0)
            else:
                box(f"Drape_{i}_{k}", (0.20, 0.30, 0.12), (x, 0.0, z + 0.067), "M_Drape_Green", 0.015)
    nd = 2 if width > 0.9 else 1
    dw = width / nd
    for k in range(nd):
        cx = -width/2 + dw/2 + k*dw
        box(f"DoorFrame_{k}", (dw - 0.01, 0.02, H - 0.04), (cx, -D/2 - 0.01, H/2 + 0.1), "M_Steel_Brushed", 0.004)
        box(f"DoorGlass_{k}", (dw - 0.08, 0.022, H - 0.12), (cx, -D/2 - 0.011, H/2 + 0.1), "M_Glass_Clear", 0.002)
        box(f"DoorHandle_{k}", (0.02, 0.04, 0.20), (cx + (dw/2 - 0.05)*(1 if k == 0 else -1), -D/2 - 0.04, 1.1), "M_Steel_Brushed", 0.006)
    box("Sign", (0.5, 0.005, 0.08), (0, -D/2 - 0.025, H + 0.05), "M_Sign_White", 0.002)
    text_label("SignTxt", "STERILE SUPPLIES", 0.03, (0, -D/2 - 0.028, H + 0.05), (R(90), 0, 0), "M_Sign_Blue")
    finalize_asset(root)
    return end_asset()

def build_workstation():
    """Theatre documentation workstation: wall bench with two PCs -
    (1) Nursing PC: patient record / WHO checklist / swab count; (2) Doctor PC: op notes, labs."""
    root = begin_asset("Workstation_TwoPC")
    box("Bench", (1.60, 0.60, 0.04), (0, 0, 0.76), "M_Plastic_OffWhite", 0.012)
    for x in (-0.76, 0.76):
        box(f"Bracket_{x}", (0.04, 0.55, 0.04), (x, 0.0, 0.72), "M_Steel_Brushed", 0.006)
    box("Modesty", (1.60, 0.02, 0.30), (0, 0.29, 0.60), "M_Plastic_OffWhite", 0.006)
    for i, (scr, lab) in enumerate((("M_Screen_PC_Nurse", "NURSING"), ("M_Screen_PC_Doctor", "SURGICAL / MEDICAL"))):
        x = -0.40 + i*0.80
        box(f"MonStand_{i}", (0.20, 0.16, 0.012), (x, 0.10, 0.786), "M_Plastic_Black", 0.004)
        box(f"MonNeck_{i}", (0.04, 0.03, 0.22), (x, 0.14, 0.89), "M_Plastic_Black", 0.006)
        box(f"Monitor_{i}", (0.56, 0.04, 0.34), (x, 0.10, 1.05), "M_Plastic_Black", 0.008)
        p = plane(f"Screen_{i}", 0.53, 0.30, (x, 0.079, 1.055), scr); mark(p, screen=("pc_nurse" if i == 0 else "pc_doctor"))
        # sealed medical keyboard (wipeable) & mouse
        box(f"Keyboard_{i}", (0.44, 0.15, 0.015), (x, -0.12, 0.787), "M_Plastic_White", 0.006)
        box(f"KeyboardKeys_{i}", (0.40, 0.11, 0.004), (x, -0.12, 0.796), "M_Plastic_LightBlue", 0.002)
        lathe(f"Mouse_{i}", [(0, 0), (0.03, 0), (0.032, 0.012), (0.02, 0.03), (0, 0.032)], (x + 0.28, -0.12, 0.78), "M_Plastic_White", 24)
        box(f"Label_{i}", (0.24, 0.004, 0.035), (x, 0.078, 1.245), "M_Sign_White", 0.002)
        text_label(f"LabelTxt_{i}", lab, 0.016, (x, 0.0755, 1.245), (R(90), 0, 0), "M_Sign_Blue")
    box("PC_Tower", (0.20, 0.45, 0.42), (0.55, 0.0, 0.22), "M_Plastic_DarkGrey", 0.01)
    box("Barcode_Scanner", (0.06, 0.10, 0.04), (0.0, 0.10, 0.80), "M_Plastic_DarkGrey", 0.01)
    # stool
    cyl("Stool_Seat", 0.18, 0.06, (-0.40, -0.55, 0.62), "M_Upholstery_Blue", 32, bevel=0.02)
    cyl("Stool_Gas", 0.025, 0.40, (-0.40, -0.55, 0.38), "M_Chrome", 16)
    for i in range(5):
        a = 2*math.pi*i/5
        x, y = -0.40 + 0.25*math.cos(a), -0.55 + 0.25*math.sin(a)
        tube(f"Stool_Leg_{i}", [(-0.40, -0.55, 0.15), (x, y, 0.08)], 0.012, "M_Plastic_Black", 8, smooth_curve=False)
        caster(f"Stool_Castor_{i}", (x, y, 0.07), wheel_r=0.03)
    finalize_asset(root)
    return end_asset()

def build_wall_monitor(name="PACS_Monitor", w=1.10, h=0.65, screen_mat="M_Screen_PACS", screen_key="pacs"):
    """Wall-mounted medical-grade display (55" PACS/imaging or teaching screen)."""
    root = begin_asset(name)
    box("Bezel", (w + 0.04, 0.05, h + 0.04), (0, 0, 0), "M_Plastic_Black", 0.01)
    p = plane("Screen", w, h, (0, -0.0255, 0), screen_mat); mark(p, screen=screen_key)
    box("WallMount", (0.40, 0.05, 0.30), (0, 0.05, 0), "M_Steel_Dark", 0.006)
    box("LED", (0.01, 0.004, 0.004), (w/2 - 0.02, -0.026, -h/2 - 0.012), "M_LED_Blue", 0.0)
    finalize_asset(root)
    return end_asset()

def build_wall_clock_panel():
    """Theatre control panel: digital clock, elapsed (anaesthesia/surgery) timers,
    temperature 18-23 C and humidity 40-60 % display, laminar flow & lighting controls,
    room pressure indicator (positive pressure)."""
    root = begin_asset("TheatreControlPanel")
    box("Frame", (1.20, 0.04, 0.55), (0, 0, 0), "M_Steel_Brushed", 0.006)
    box("Glass", (1.16, 0.004, 0.51), (0, -0.022, 0), "M_Glass_Clear", 0.002)
    p = plane("Screen_Clock", 0.52, 0.22, (-0.28, -0.021, 0.12), "M_Screen_Clock"); mark(p, screen="clock")
    p = plane("Screen_Vent", 0.52, 0.22, (0.28, -0.021, 0.12), "M_Screen_Ventilation"); mark(p, screen="ventilation")
    for i in range(8):
        box(f"Btn_{i}", (0.10, 0.006, 0.07), (-0.49 + i*0.14, -0.024, -0.15), "M_Plastic_White", 0.006)
        box(f"BtnLED_{i}", (0.03, 0.004, 0.006), (-0.49 + i*0.14, -0.028, -0.125), "M_LED_Green" if i % 3 else "M_LED_Amber", 0.0)
    labels = ["LIGHTS", "DIM", "LAMINAR", "TEMP+", "TEMP-", "TIMER", "RESET", "INTERCOM"]
    for i, l in enumerate(labels):
        text_label(f"BtnTxt_{i}", l, 0.011, (-0.49 + i*0.14, -0.0275, -0.155), (R(90), 0, 0), "M_Plastic_DarkGrey")
    text_label("Title", "THEATRE CONTROL PANEL", 0.02, (0, -0.0245, -0.235), (R(90), 0, 0), "M_Plastic_DarkGrey")
    finalize_asset(root)
    return end_asset()

def build_whiteboard():
    """Swab / instrument / needle count board (whiteboard) - completed by the
    scrub and circulating nurse before, during and at the end of surgery."""
    root = begin_asset("SwabCountBoard")
    box("Frame", (1.20, 0.03, 0.90), (0, 0, 0), "M_Aluminium", 0.006)
    box("Board", (1.16, 0.032, 0.86), (0, 0, 0), "M_Whiteboard", 0.002)
    text_label("Title", "SWAB / INSTRUMENT / NEEDLE COUNT", 0.032, (0, -0.0165, 0.37), (R(90), 0, 0), "M_Sign_Blue")
    cols = ["ITEM", "INITIAL", "ADDED", "1st", "FINAL"]
    for i, c in enumerate(cols):
        text_label(f"Col_{i}", c, 0.024, (-0.45 + i*0.23, -0.0165, 0.29), (R(90), 0, 0), "M_Marker_Black")
    rows = ["Swabs", "Packs", "Needles", "Blades", "Instruments"]
    for j, r in enumerate(rows):
        text_label(f"Row_{j}", r, 0.024, (-0.45, -0.0165, 0.20 - j*0.10), (R(90), 0, 0), "M_Marker_Black")
        box(f"Line_{j}", (1.10, 0.0335, 0.003), (0, 0, 0.245 - j*0.10), "M_Plastic_Grey", 0.0)
    for i in range(1, 5):
        box(f"VLine_{i}", (0.003, 0.0335, 0.52), (-0.45 + i*0.23 - 0.115, 0, -0.01), "M_Plastic_Grey", 0.0)
    box("MarkerTray", (0.60, 0.06, 0.02), (0, -0.04, -0.46), "M_Aluminium", 0.004)
    for k, m in enumerate(("M_Plastic_Blue", "M_Plastic_Red", "M_Plastic_Black")):
        cyl(f"Marker_{k}", 0.008, 0.13, (-0.10 + k*0.1, -0.045, -0.445), m, 12, rot=(0, R(90), 0))
    finalize_asset(root)
    return end_asset()

def build_hand_rub_dispenser():
    """Wall-mounted elbow / touch-free alcohol hand rub dispenser (WHO 5 moments)."""
    root = begin_asset("HandRubDispenser")
    box("Backplate", (0.12, 0.02, 0.28), (0, 0, 0), "M_Plastic_White", 0.008)
    box("Body", (0.11, 0.09, 0.22), (0, -0.05, 0.02), "M_Plastic_White", 0.02)
    box("Window", (0.05, 0.004, 0.08), (0, -0.096, 0.05), "M_Plastic_Clear", 0.002)
    box("ElbowLever", (0.025, 0.012, 0.26), (0, -0.115, -0.06), "M_Steel_Brushed", 0.004, rot=(R(-10), 0, 0))
    box("ElbowLever_Pad", (0.07, 0.02, 0.05), (0, -0.13, -0.18), "M_Plastic_Blue", 0.008, rot=(R(-10), 0, 0))
    box("Nozzle", (0.02, 0.02, 0.01), (0, -0.08, -0.10), "M_Plastic_Grey", 0.003)
    box("Label", (0.08, 0.004, 0.04), (0, -0.142, 0.0), "M_Label_Print", 0.002)
    finalize_asset(root)
    return end_asset()

def build_fire_safety():
    """CO2 fire extinguisher (black band - safe on electrical equipment) on wall bracket
    + red fire-alarm manual call point + sign."""
    root = begin_asset("FireSafetyPoint")
    lathe("Ext_Body", [(0, 0), (0.07, 0), (0.075, 0.01), (0.075, 0.55), (0.06, 0.62), (0.02, 0.65), (0, 0.65)], (0, -0.10, 0.25), "M_Painted_Red", 40)
    cyl("Ext_Band", 0.076, 0.10, (0, -0.10, 0.73), "M_Plastic_Black", 40)
    box("Ext_Valve", (0.05, 0.04, 0.06), (0, -0.10, 0.93), "M_Steel_Polished", 0.008)
    box("Ext_Handle", (0.14, 0.02, 0.015), (0.04, -0.10, 0.97), "M_Plastic_Black", 0.004)
    tube("Ext_Horn", [(0.02, -0.10, 0.93), (0.10, -0.14, 0.90), (0.12, -0.16, 0.70), (0.08, -0.16, 0.55)], 0.012, "M_Plastic_Black")
    box("Ext_Bracket", (0.12, 0.03, 0.06), (0, -0.03, 0.80), "M_Steel_Dark", 0.006)
    box("Sign", (0.20, 0.005, 0.25), (0, -0.01, 1.20), "M_Sign_Red", 0.003)
    text_label("SignTxt", "CO2\nFIRE\nEXT.", 0.035, (0, -0.0135, 1.20), (R(90), 0, 0), "M_Sign_White")
    box("CallPoint", (0.09, 0.05, 0.09), (0.30, -0.025, 1.40), "M_Painted_Red", 0.006)
    box("CallPoint_Glass", (0.06, 0.006, 0.06), (0.30, -0.052, 1.40), "M_Plastic_White", 0.002)
    text_label("CallTxt", "FIRE", 0.012, (0.30, -0.0565, 1.43), (R(90), 0, 0), "M_Sign_White")
    finalize_asset(root)
    return end_asset()

def build_wall_gas_panel():
    """Wall-mounted medical gas outlet panel (backup to pendants) + medical gas
    alarm panel (pressure status O2 / N2O / AIR / VAC)."""
    root = begin_asset("WallGasPanel")
    from assets_room import _gas_outlet
    box("Plate", (0.50, 0.03, 0.18), (0, 0, 0), "M_Plastic_White", 0.008)
    for i, g in enumerate(("O2", "N2O", "AIR", "VAC")):
        _gas_outlet(f"Gas_{i}_{g}", (-0.18 + i*0.12, -0.016, -0.02), g)
    box("AlarmPanel", (0.30, 0.04, 0.20), (0, 0, 0.40), "M_Plastic_White", 0.008)
    for i, (g, m) in enumerate((("O2", "M_Gas_O2_White"), ("N2O", "M_Gas_N2O_Blue"), ("AIR", "M_Gas_Air_Black"), ("VAC", "M_Gas_Vac_Yellow"))):
        box(f"Alarm_{g}_Band", (0.05, 0.004, 0.02), (-0.105 + i*0.07, -0.022, 0.45), m, 0.002)
        box(f"Alarm_{g}_LED", (0.02, 0.004, 0.012), (-0.105 + i*0.07, -0.022, 0.41), "M_LED_Green", 0.0)
    text_label("AlarmTxt", "MEDICAL GAS ALARM", 0.014, (0, -0.0225, 0.355), (R(90), 0, 0), "M_Plastic_DarkGrey")
    finalize_asset(root)
    return end_asset()

def build_step_stool():
    """Stainless steel step-up stool (single step, anti-slip rubber top) - used by
    surgeons/scrub nurse to reach the field."""
    root = begin_asset("StepStool")
    box("Top", (0.50, 0.30, 0.02), (0, 0, 0.22), "M_Steel_Brushed", 0.006)
    box("Mat", (0.46, 0.26, 0.006), (0, 0, 0.233), "M_Rubber_Black", 0.002)
    for x in (-0.23, 0.23):
        for y in (-0.13, 0.13):
            cyl(f"Leg_{x}_{y}", 0.012, 0.21, (x, y, 0.105), "M_Steel_Polished", 12)
            cyl(f"Foot_{x}_{y}", 0.018, 0.012, (x, y, 0.006), "M_Rubber_Black", 12)
    finalize_asset(root)
    return end_asset()

def build_surgeon_stool():
    root = begin_asset("SurgeonStool")
    cyl("Seat", 0.19, 0.07, (0, 0, 0.62), "M_Upholstery_Black", 40, bevel=0.025)
    cyl("Gas", 0.025, 0.45, (0, 0, 0.36), "M_Chrome", 16)
    torus("FootRing", 0.20, 0.01, (0, 0, 0.30), "M_Chrome", 40, 8)
    for i in range(5):
        a = 2*math.pi*i/5
        x, y = 0.26*math.cos(a), 0.26*math.sin(a)
        tube(f"Leg_{i}", [(0, 0, 0.14), (x, y, 0.08)], 0.014, "M_Steel_Polished", 8, smooth_curve=False)
        caster(f"Castor_{i}", (x, y, 0.07), wheel_r=0.03)
    finalize_asset(root)
    return end_asset()

def build_positioning_aids():
    """Patient positioning aids on a shelf trolley: gel head ring, gel chest roll,
    heel pads, arm/leg supports, safety straps - prevent pressure injury."""
    root = begin_asset("PositioningAidsTrolley")
    for z in (0.30, 0.70):
        box(f"Shelf_{z}", (0.80, 0.50, 0.02), (0, 0, z), "M_Steel_Brushed", 0.006)
    for x in (-0.38, 0.38):
        for y in (-0.23, 0.23):
            cyl(f"Leg_{x}_{y}", 0.014, 0.66, (x, y, 0.41), "M_Steel_Polished", 12)
            caster(f"Castor_{x}_{y}", (x, y, 0.08), wheel_r=0.04)
    torus("GelHeadRing", 0.08, 0.03, (-0.25, 0.0, 0.74), "M_Gel_Pad", 40, 16)
    cyl("GelChestRoll", 0.07, 0.45, (0.12, 0.10, 0.78), "M_Gel_Pad", 32, bevel=0.03, rot=(0, R(90), 0))
    for k in range(2):
        lathe(f"HeelPad_{k}", [(0, 0), (0.08, 0), (0.09, 0.03), (0.06, 0.05), (0, 0.052)], (0.15 + k*0.18, -0.13, 0.71), "M_Gel_Pad", 32)
    box("ArmSupport", (0.45, 0.14, 0.06), (0, 0.05, 0.34), "M_Upholstery_Black", 0.02)
    box("SafetyStraps", (0.30, 0.15, 0.04), (0, -0.15, 0.33), "M_Plastic_Black", 0.01)
    finalize_asset(root)
    return end_asset()

def build_lead_apron_rack():
    """Radiation protection: lead apron & thyroid collar hanger (for intra-operative imaging)."""
    root = begin_asset("LeadApronRack")
    box("Rail", (1.30, 0.08, 0.06), (0, 0, 1.70), "M_Steel_Brushed", 0.01)
    for i, m in enumerate(("M_Plastic_Blue", "M_Plastic_Purple", "M_Plastic_Green")):
        x = -0.42 + i*0.42
        tube(f"Hanger_{i}", [(x - 0.17, -0.05, 1.62), (x, -0.06, 1.66), (x + 0.17, -0.05, 1.62)], 0.006, "M_Plastic_Black")
        # tapered lead apron (shoulders wider than hem) built from a lathe-free extruded outline
        bm = bmesh.new()
        outline = [(-0.17, 0.0), (0.17, 0.0), (0.19, 0.55), (0.16, 0.75), (0.09, 0.80), (0.05, 0.72), (-0.05, 0.72), (-0.09, 0.80), (-0.16, 0.75), (-0.19, 0.55)]
        vs = [bm.verts.new((px, 0, pz)) for px, pz in outline]
        f = bm.faces.new(vs)
        rr = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, vec=(0, 0.025, 0), verts=[g for g in rr['geom'] if isinstance(g, bmesh.types.BMVert)])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh_obj(f"Apron_{i}", bm, m, loc=(x, -0.09, 0.82))
        torus(f"Collar_{i}", 0.06, 0.018, (x, -0.11, 1.50), m, 24, 8, rot=(R(90), 0, 0))
    box("Sign", (0.30, 0.005, 0.08), (0, -0.045, 1.80), "M_Sign_Yellow", 0.002)
    text_label("SignTxt", "RADIATION PPE", 0.018, (0, -0.048, 1.80), (R(90), 0, 0), "M_Plastic_Black")
    finalize_asset(root)
    return end_asset()

def build_telephone():
    root = begin_asset("WallPhoneIntercom")
    box("Body", (0.10, 0.05, 0.20), (0, 0, 0), "M_Plastic_White", 0.012)
    box("Handset", (0.05, 0.04, 0.20), (0, -0.045, 0.0), "M_Plastic_Grey", 0.015)
    for i in range(12):
        box(f"Key_{i}", (0.015, 0.004, 0.012), (-0.02 + (i % 3)*0.02, -0.027, 0.05 - (i // 3)*0.018), "M_Plastic_DarkGrey", 0.002)
    box("Emergency", (0.06, 0.006, 0.025), (0, -0.027, -0.08), "M_Plastic_Red", 0.004)
    finalize_asset(root)
    return end_asset()

def build_specimen_station():
    """Specimen collection: specimen pots (formalin), request forms, biohazard bags -
    'Dispatch and label specimens' (NS2072 LO3)."""
    root = begin_asset("SpecimenStation")
    box("Shelf", (0.60, 0.25, 0.02), (0, 0, 0), "M_Steel_Brushed", 0.006)
    box("Bracket", (0.60, 0.02, 0.10), (0, 0.12, -0.05), "M_Steel_Brushed", 0.004)
    for i in range(4):
        lathe(f"Pot_{i}", [(0, 0), (0.035, 0), (0.036, 0.07), (0.0, 0.07)], (-0.22 + i*0.09, -0.03, 0.01), "M_Plastic_Clear", 24)
        cyl(f"PotLid_{i}", 0.038, 0.02, (-0.22 + i*0.09, -0.03, 0.09), "M_Plastic_Red" if i < 2 else "M_Plastic_Yellow", 24)
        box(f"PotLabel_{i}", (0.035, 0.002, 0.03), (-0.22 + i*0.09, -0.066, 0.045), "M_Label_Print", 0.0)
    box("Forms", (0.21, 0.15, 0.02), (0.18, 0.02, 0.02), "M_Paper_White", 0.002)
    box("BioBags", (0.15, 0.12, 0.02), (0.18, 0.02, 0.04), "M_Plastic_Yellow", 0.004)
    finalize_asset(root)
    return end_asset()

def build_scrub_sink():
    """Surgical scrub trough (stainless, 3 stations) with infra-red sensor taps,
    elbow-operated antiseptic soap dispensers (chlorhexidine / povidone-iodine),
    nail-brush dispenser, clock/timer for the 3-5 minute scrub, mirror splashback."""
    root = begin_asset("ScrubSink")
    W, D = 2.10, 0.55
    # trough
    box("Trough_Front", (W, 0.02, 0.35), (0, -D/2, 0.86), "M_Sink_Steel", 0.006)
    box("Trough_Back", (W, 0.02, 0.60), (0, D/2, 0.98), "M_Sink_Steel", 0.006)
    box("Trough_Bottom", (W, D, 0.02), (0, 0, 0.70), "M_Sink_Steel", 0.006)
    for s in (-1, 1):
        box(f"Trough_End_{s}", (0.02, D, 0.35), (s*W/2, 0, 0.86), "M_Sink_Steel", 0.006)
    box("Trough_Lip", (W + 0.02, 0.06, 0.03), (0, -D/2 - 0.02, 1.04), "M_Sink_Steel", 0.01)
    box("Skirt", (W, 0.02, 0.68), (0, -D/2 + 0.02, 0.34), "M_Sink_Steel", 0.004)
    for i in range(3):
        x = -0.70 + i*0.70
        cyl(f"Drain_{i}", 0.035, 0.005, (x, 0.05, 0.712), "M_Chrome", 24)
        # sensor tap (gooseneck) from back panel
        tube(f"Tap_{i}", [(x, D/2 - 0.01, 1.25), (x, D/2 - 0.12, 1.30), (x, D/2 - 0.24, 1.25), (x, D/2 - 0.26, 1.18)], 0.012, "M_Chrome", 12)
        cyl(f"Tap_Sensor_{i}", 0.02, 0.03, (x, D/2 - 0.015, 1.12), "M_Plastic_Black", 20, rot=(R(90), 0, 0))
        box(f"Tap_SensorLED_{i}", (0.006, 0.004, 0.006), (x + 0.012, D/2 - 0.032, 1.135), "M_LED_Red", 0.0)
        # elbow-lever soap dispenser (left of tap)
        box(f"Soap_Body_{i}", (0.10, 0.10, 0.25), (x - 0.22, D/2 - 0.06, 1.40), "M_Plastic_White", 0.012)
        box(f"Soap_Lever_{i}", (0.20, 0.03, 0.02), (x - 0.22, D/2 - 0.13, 1.33), "M_Steel_Brushed", 0.006)
        box(f"Soap_Label_{i}", (0.07, 0.004, 0.04), (x - 0.22, D/2 - 0.112, 1.45), "M_Plastic_Red" if i == 1 else "M_Plastic_Purple", 0.002)
        # nail brush dispenser (right)
        box(f"Brush_Disp_{i}", (0.10, 0.08, 0.16), (x + 0.22, D/2 - 0.05, 1.45), "M_Plastic_Clear", 0.01)
        for k in range(3):
            box(f"Brush_{i}_{k}", (0.07, 0.04, 0.02), (x + 0.22, D/2 - 0.05, 1.40 + k*0.03), "M_Plastic_Orange", 0.006)
    # splash-back mirror & digital scrub timer
    box("Splashback", (W, 0.01, 0.50), (0, D/2 + 0.02, 1.55), "M_Steel_Polished", 0.004)
    box("ScrubTimer", (0.30, 0.03, 0.12), (0, D/2 + 0.0, 1.88), "M_Plastic_DarkGrey", 0.01)
    p = plane("ScrubTimer_Screen", 0.26, 0.08, (0, D/2 - 0.016, 1.88), "M_Screen_Clock"); mark(p, screen="scrub_timer")
    box("Sign", (0.6, 0.005, 0.24), (-0.65, D/2 + 0.0, 1.98), "M_Sign_White", 0.002)
    text_label("SignTxt", "SURGICAL SCRUB\n3 - 5 MIN\nHANDS ABOVE ELBOWS", 0.028, (-0.65, D/2 - 0.004, 1.98), (R(90), 0, 0), "M_Sign_Blue")
    finalize_asset(root)
    return end_asset()

def build_ppe_trolley():
    """Scrub-up / PPE trolley: theatre caps, fluid-resistant surgical masks,
    eye protection / visors, sterile gown packs, sterile gloves (sizes 6-8.5)."""
    root = begin_asset("PPETrolley")
    W, D = 0.80, 0.50
    for z in (0.30, 0.62, 0.92):
        box(f"Shelf_{z}", (W, D, 0.02), (0, 0, z), "M_Steel_Brushed", 0.006)
        box(f"ShelfLip_{z}", (W, 0.012, 0.04), (0, -D/2, z + 0.02), "M_Steel_Brushed", 0.004)
    for x in (-W/2 + 0.02, W/2 - 0.02):
        for y in (-D/2 + 0.02, D/2 - 0.02):
            cyl(f"Leg_{x}_{y}", 0.014, 0.84, (x, y, 0.52), "M_Steel_Polished", 12)
            caster(f"Castor_{x}_{y}", (x, y, 0.10), wheel_r=0.04)
    # top: caps, masks, visors
    box("Caps_Box", (0.22, 0.14, 0.12), (-0.25, 0.0, 0.99), "M_Gown_Blue", 0.006)
    box("Caps_Opening", (0.10, 0.004, 0.06), (-0.25, -0.072, 1.00), "M_Plastic_LightBlue", 0.002)
    box("Masks_Box", (0.20, 0.12, 0.10), (0.0, 0.0, 0.98), "M_Paper_White", 0.006)
    box("Masks_Band", (0.202, 0.122, 0.025), (0.0, 0.0, 0.98), "M_Plastic_Blue", 0.0)
    for k in range(3):
        box(f"Visor_{k}", (0.20, 0.02, 0.18), (0.26, -0.05 + k*0.04, 1.02), "M_Plastic_Clear", 0.01)
        box(f"VisorBand_{k}", (0.20, 0.025, 0.025), (0.26, -0.05 + k*0.04, 1.10), "M_Plastic_Blue", 0.006)
    # middle: sterile gown packs
    for k in range(3):
        box(f"GownPack_{k}", (0.22, 0.32, 0.08), (-0.25 + k*0.25, 0.0, 0.67 + (k % 2)*0.0), "M_Paper_White", 0.012)
        box(f"GownPack_Ind_{k}", (0.05, 0.322, 0.082), (-0.25 + k*0.25, 0.0, 0.67), "M_Plastic_Green", 0.0)
    # bottom: sterile glove boxes by size
    for k, m in enumerate(("M_Plastic_Green", "M_Plastic_Blue", "M_Plastic_Purple", "M_Plastic_Orange")):
        box(f"Gloves_{k}", (0.16, 0.12, 0.10), (-0.27 + k*0.18, 0.0, 0.36), "M_Paper_White", 0.006)
        box(f"GlovesBand_{k}", (0.162, 0.122, 0.025), (-0.27 + k*0.18, 0.0, 0.36), m, 0.0)
    box("Sign", (0.40, 0.005, 0.08), (0, -D/2 - 0.005, 0.82), "M_Sign_White", 0.002)
    text_label("SignTxt", "PPE / THEATRE ATTIRE", 0.02, (0, -D/2 - 0.008, 0.82), (R(90), 0, 0), "M_Sign_Blue")
    finalize_asset(root)
    return end_asset()

def build_shoe_rack():
    """Theatre clogs rack (dedicated theatre footwear) at the corridor."""
    root = begin_asset("TheatreClogsRack")
    for z in (0.05, 0.30, 0.55):
        box(f"Shelf_{z}", (0.90, 0.32, 0.02), (0, 0, z), "M_Steel_Brushed", 0.004)
    for x in (-0.44, 0.44):
        for y in (-0.15, 0.15):
            cyl(f"Leg_{x}_{y}", 0.01, 0.6, (x, y, 0.3), "M_Steel_Polished", 10)
    cols = ("M_Plastic_Blue", "M_Plastic_Green", "M_Plastic_White", "M_Plastic_Blue")
    for z in (0.06, 0.31):
        for k in range(4):
            x = -0.33 + k*0.22
            for s in (-1, 1):
                lathe(f"Clog_{z}_{k}_{s}", [(0, 0), (0.04, 0), (0.045, 0.02), (0.04, 0.06), (0.0, 0.065)], (x + s*0.05, 0, z + 0.01), cols[k], 16).scale = (1.0, 2.6, 1.0)
    finalize_asset(root)
    return end_asset()

def build_patient_mannequin():
    """Simple anaesthetised patient mannequin (supine) under green drapes - a shared
    'virtual patient dummy' placeholder used by other modules (IV cannula, etc.)."""
    root = begin_asset("PatientMannequin")
    top = 0.85
    sphere("Head", 0.095, (0, 0.98, top + 0.10), "M_Skin", 32, 16, scale=(0.85, 1.0, 0.9))
    lathe("Neck", [(0.05, 0), (0.05, 0.08)], (0, 0.86, top + 0.08), "M_Skin", 24, rot=(R(90), 0, 0))
    cap = sphere("Cap", 0.098, (0, 1.0, top + 0.12), "M_Gown_Blue", 32, 16, scale=(0.86, 1.02, 0.7))
    sphere("Torso", 0.20, (0, 0.50, top + 0.10), "M_Drape_Blue", 32, 16, scale=(0.9, 1.6, 0.45))
    sphere("Pelvis", 0.19, (0, 0.05, top + 0.09), "M_Drape_Green", 32, 16, scale=(0.95, 1.0, 0.45))
    for s in (-1, 1):
        sphere(f"Leg_{s}", 0.08, (s*0.09, -0.50, top + 0.07), "M_Drape_Green", 24, 12, scale=(1.0, 5.2, 0.8))
        sphere(f"Foot_{s}", 0.05, (s*0.09, -0.92, top + 0.11), "M_Drape_Green", 16, 8, scale=(0.8, 0.6, 1.4))
        # arms out on arm boards with IV cannula site
        sphere(f"Arm_{s}", 0.045, (s*0.50, 0.55, top + 0.06), "M_Skin", 24, 12, scale=(6.0, 1.0, 0.9))
        sphere(f"Hand_{s}", 0.04, (s*0.86, 0.55, top + 0.06), "M_Skin", 16, 8, scale=(1.6, 1.0, 0.6))
    box("Drape_Sheet", (0.95, 1.55, 0.01), (0, -0.25, top + 0.15), "M_Drape_Green", 0.004)
    for s in (-1, 1):
        box(f"Drape_Side_{s}", (0.01, 1.55, 0.45), (s*0.47, -0.25, top - 0.07), "M_Drape_Green", 0.004)
    box("SpO2_Probe", (0.025, 0.035, 0.02), (-0.89, 0.55, top + 0.08), "M_Plastic_Blue", 0.006)
    box("BP_Cuff", (0.12, 0.10, 0.10), (0.30, 0.55, top + 0.07), "M_Plastic_Blue", 0.03)
    finalize_asset(root)
    return end_asset()
