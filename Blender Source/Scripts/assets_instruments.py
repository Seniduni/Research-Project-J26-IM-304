"""Surgical instruments (basic general surgery set) - stainless steel, real sizes.
These are laid out on the Mayo stand / instrument trolley. Each instrument is
also exported individually so the Surgical Instrument Identification module
(Seniduni H.M.D.P.) can reuse them.
"""
from otlib import *

def _ring(name, loc, R_=0.0125, r=0.0022, rot=(0, 0, 0)):
    return torus(name, R_, r, loc, "M_Instrument_Steel", 32, 8, rot=rot)

def ring_instrument(name, total_len, jaw="straight_serrated", curved=False, tip_w=0.003, jaw_len=None, ratchet=True, gold=False):
    """Generic ring-handled hinged instrument (scissors, artery forceps, needle holders).
    Built closed, lying flat on XY plane, tip toward +X, origin at box-lock."""
    root = begin_asset(name)
    L = total_len
    jl = jaw_len or L*0.30
    hl = L - jl                      # handle length (box joint to ring end)
    # two shanks from box joint to rings
    for s in (-1, 1):
        pts = [(0, 0, 0), (-hl*0.45, s*0.004, 0.0), (-hl*0.85, s*0.012, 0.0)]
        tube(f"Shank_{s}", pts, 0.0022, "M_Instrument_Steel", 8)
        _ring(f"Ring_{s}", (-hl*0.85 - 0.012, s*0.017, 0), 0.0115, 0.0023)
        if gold:
            _ring(f"RingGold_{s}", (-hl*0.85 - 0.012, s*0.017, 0), 0.0116, 0.0012)
    if ratchet:
        for s in (-1, 1):
            box(f"Ratchet_{s}", (0.012, 0.003, 0.002), (-hl*0.78, s*0.0045, 0), "M_Instrument_Steel", 0.0005)
            for k in range(3):
                box(f"RatchetTooth_{s}_{k}", (0.002, 0.0015, 0.0022), (-hl*0.78 - 0.004 + k*0.004, s*0.0028, 0), "M_Instrument_Steel", 0.0)
    # box lock
    box("BoxLock", (0.010, 0.008, 0.004), (0, 0, 0), "M_Instrument_Steel", 0.001)
    cyl("Screw", 0.0018, 0.0045, (0, 0, 0), "M_Steel_Dark", 12)
    # jaws
    if curved:
        pts = [(0.004, 0, 0), (jl*0.5, 0.0, 0), (jl*0.85, 0.004, 0), (jl, 0.009, 0)]
    else:
        pts = [(0.004, 0, 0), (jl*0.5, 0, 0), (jl, 0, 0)]
    if jaw == "scissor_blades":
        # two flat, tapered blades (slightly offset vertically, overlapping when closed)
        for sgn in (-1, 1):
            bm = bmesh.new()
            n = 10
            top, bot = [], []
            for k in range(n + 1):
                t = k / n
                x = 0.004 + t*(jl - 0.004)
                yc = (pts[-1][1]*(t**2)) if curved else 0.0
                w = 0.0045*(1 - t) + 0.0012*t
                top.append(bm.verts.new((x, yc + w*0.5 + sgn*0.0006, sgn*0.0007 + 0.0006)))
                bot.append(bm.verts.new((x, yc - w*0.5 + sgn*0.0006, sgn*0.0007 + 0.0006)))
            top2 = [bm.verts.new((v.co.x, v.co.y, v.co.z - 0.0012)) for v in top]
            bot2 = [bm.verts.new((v.co.x, v.co.y, v.co.z - 0.0012)) for v in bot]
            for k in range(n):
                bm.faces.new((top[k], top[k+1], bot[k+1], bot[k]))
                bm.faces.new((top2[k], bot2[k], bot2[k+1], top2[k+1]))
                bm.faces.new((top[k], top2[k], top2[k+1], top[k+1]))
                bm.faces.new((bot[k], bot[k+1], bot2[k+1], bot2[k]))
            bm.faces.new((top[0], bot[0], bot2[0], top2[0]))
            bm.faces.new((top[n], top2[n], bot2[n], bot[n]))
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            mesh_obj(f"Blade_{sgn}", bm, "M_Instrument_Steel", smooth_angle=30)
    elif jaw == "sponge":
        for sgn in (-1, 1):
            p2 = [(x, y, z + sgn*0.0011) for x, y, z in pts]
            tube(f"Jaw_{sgn}", p2[:-1] + [(jl - 0.03, 0, sgn*0.0011)], 0.002, "M_Instrument_Steel", 6, res=6)
        torus("JawRing", 0.012, 0.0025, (jl - 0.016, 0, 0), "M_Instrument_Steel", 24, 8)
        torus("JawRing2", 0.0115, 0.0018, (jl - 0.016, 0, 0.002), "M_Instrument_Steel", 24, 8)
        for k in range(6):
            box(f"JawSerr_{k}", (0.001, 0.02, 0.0012), (jl - 0.024 + k*0.003, 0, 0.003), "M_Steel_Dark", 0.0)
    elif jaw == "allis":
        for sgn in (-1, 1):
            p2 = [(x, y, z + sgn*0.0011) for x, y, z in pts]
            tube(f"Jaw_{sgn}", p2, 0.0016, "M_Instrument_Steel", 6, res=6)
        # flared atraumatic tip with interlocking teeth
        box("AllisTip", (0.004, 0.009, 0.004), (jl, 0, 0), "M_Instrument_Steel", 0.0008)
        for k in range(5):
            box(f"AllisTooth_{k}", (0.0012, 0.0012, 0.0016), (jl + 0.0022, -0.0036 + k*0.0018, 0), "M_Steel_Dark", 0.0)
    else:
        for s_ in (-1, 1):
            p2 = [(x, y, z + s_*0.0011) for x, y, z in pts]
            tube(f"Jaw_{s_}", p2, max(0.0015, tip_w*0.6), "M_Instrument_Steel", 6, res=6)
        # serrations
        n = int(jl/0.003)
        for k in range(n):
            t = k/(n)
            x = 0.006 + t*(jl - 0.008)
            y = pts[-1][1]*(t**2) if curved else 0
            box(f"Serr_{k}", (0.0007, 0.0034, 0.0026), (x, y, 0), "M_Steel_Dark", 0.0)
    finalize_asset(root)
    return end_asset()

def build_scalpel():
    """No.3 scalpel handle (125 mm) with No.10 blade fitted."""
    root = begin_asset("Instr_Scalpel_No3_Blade10")
    box("Handle", (0.105, 0.010, 0.0025), (-0.05, 0, 0), "M_Instrument_Steel", 0.0008)
    for k in range(10):
        box(f"Grip_{k}", (0.0012, 0.0102, 0.0027), (-0.085 + k*0.004, 0, 0), "M_Steel_Dark", 0.0)
    box("Neck", (0.02, 0.005, 0.0022), (0.012, 0, 0), "M_Instrument_Steel", 0.0006)
    # No.10 blade: curved cutting edge
    bm = bmesh.new()
    pts = [(0.020, -0.0025), (0.042, -0.0045), (0.055, -0.003), (0.061, 0.0), (0.058, 0.0035), (0.040, 0.0045), (0.020, 0.0025)]
    vs = [bm.verts.new((x, y, -0.0002)) for x, y in pts]
    f = bm.faces.new(vs)
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0, 0.0004), verts=[g for g in r['geom'] if isinstance(g, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh_obj("Blade", bm, "M_Steel_Polished")
    finalize_asset(root)
    return end_asset()

def build_tissue_forceps(name, length=0.145, toothed=False):
    """Dissecting forceps (thumb forceps): spring tweezers, 145 mm."""
    root = begin_asset(name)
    for s in (-1, 1):
        pts = [(-length*0.5, 0, 0), (0.0, s*0.006, 0), (length*0.35, s*0.004, 0), (length*0.5, s*0.0008, 0)]
        tube(f"Arm_{s}", pts, 0.0018, "M_Instrument_Steel", 6)
        for k in range(8):
            box(f"Grip_{s}_{k}", (0.0015, 0.004, 0.0045), (-length*0.05 + k*0.004, s*0.0058, 0), "M_Steel_Dark", 0.0)
    box("Weld", (0.010, 0.004, 0.0035), (-length*0.5, 0, 0), "M_Instrument_Steel", 0.001)
    if toothed:
        for s in (-1, 1):
            box(f"Tooth_{s}", (0.0015, 0.0012, 0.0012), (length*0.5 + 0.0005, s*0.0005, 0), "M_Instrument_Steel", 0.0)
    finalize_asset(root)
    return end_asset()

def build_towel_clip():
    """Backhaus towel clip (110 mm) - sharp curved points."""
    root = ring_instrument("Instr_TowelClip_Backhaus", 0.11, jaw="straight_serrated", jaw_len=0.025)
    return root

def build_retractor():
    """Langenbeck retractor (220 mm) - flat handle with right-angle blade."""
    root = begin_asset("Instr_Retractor_Langenbeck")
    box("Handle", (0.16, 0.016, 0.003), (-0.04, 0, 0), "M_Instrument_Steel", 0.001)
    for k in range(5):
        cyl(f"Hole_{k}", 0.0035, 0.0032, (-0.10 + k*0.025, 0, 0), "M_Steel_Dark", 12)
    box("Shaft", (0.05, 0.010, 0.003), (0.065, 0, 0), "M_Instrument_Steel", 0.001)
    box("Blade", (0.003, 0.016, 0.04), (0.09, 0, 0.018), "M_Instrument_Steel", 0.001)
    finalize_asset(root)
    return end_asset()

def build_kidney_dish(name="Instr_KidneyDish", L=0.25):
    """Stainless kidney dish (250 mm) - receives used swabs/instruments."""
    root = begin_asset(name)
    bm = bmesh.new()
    n = 48
    outline = []
    for i in range(n):
        t = 2*math.pi*i/n
        x = 0.5*L*math.cos(t)
        y = 0.22*L*math.sin(t) - 0.06*L*math.cos(2*t) + 0.03*L
        outline.append((x, y))
    rings = []
    for k, (sc, z) in enumerate(((0.80, 0.0), (0.86, 0.004), (0.95, 0.03), (1.0, 0.045), (1.02, 0.047))):
        rings.append([bm.verts.new((x*sc, y*sc, z)) for x, y in outline])
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i+1) % n], b[(i+1) % n], b[i]))
    bm.faces.new(rings[0][::-1])
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0012)
    mesh_obj("Dish", bm, "M_Steel_Polished", smooth_angle=60)
    finalize_asset(root)
    return end_asset()

def build_gallipot(name="Instr_Gallipot"):
    root = begin_asset(name)
    lathe("Pot", [(0, 0), (0.035, 0), (0.038, 0.005), (0.042, 0.05), (0.045, 0.052), (0.044, 0.054), (0.040, 0.052), (0.037, 0.008), (0.0, 0.006)], (0, 0, 0), "M_Steel_Polished", 40)
    finalize_asset(root)
    return end_asset()

def build_gauze_swabs():
    """Stack of 10 x 10 cm gauze swabs with radio-opaque (X-ray detectable) blue thread."""
    root = begin_asset("Instr_GauzeSwabs")
    for i in range(5):
        box(f"Swab_{i}", (0.10, 0.10, 0.004), (0.002*(i % 2), 0.001*i, 0.002 + i*0.004), "M_Gauze", 0.0015)
        box(f"Thread_{i}", (0.10, 0.003, 0.0042), (0.002*(i % 2), 0.02 + 0.001*i, 0.002 + i*0.004), "M_Plastic_Blue", 0.0)
    finalize_asset(root)
    return end_asset()

def build_suture_pack():
    root = begin_asset("Instr_SuturePack")
    box("Pack", (0.13, 0.05, 0.004), (0, 0, 0.002), "M_Paper_White", 0.002)
    box("Window", (0.06, 0.035, 0.0045), (0.025, 0, 0.002), "M_Plastic_Clear", 0.001)
    box("Stripe", (0.13, 0.008, 0.0042), (0, -0.018, 0.002), "M_Plastic_Purple", 0.0)
    text_label("Txt", "3-0 Polyglactin", 0.006, (-0.03, 0.005, 0.0045), (0, 0, 0), "M_Plastic_Black")
    finalize_asset(root)
    return end_asset()

INSTRUMENT_BUILDERS = [
    ("Instr_Scalpel_No3_Blade10", build_scalpel),
    ("Instr_Scissors_Mayo_Curved", lambda: ring_instrument("Instr_Scissors_Mayo_Curved", 0.17, "scissor_blades", curved=True, ratchet=False, jaw_len=0.055)),
    ("Instr_Scissors_Metzenbaum", lambda: ring_instrument("Instr_Scissors_Metzenbaum", 0.18, "scissor_blades", curved=True, ratchet=False, jaw_len=0.05)),
    ("Instr_ArteryForceps_Kelly_Curved", lambda: ring_instrument("Instr_ArteryForceps_Kelly_Curved", 0.14, curved=True, jaw_len=0.04)),
    ("Instr_ArteryForceps_Mosquito", lambda: ring_instrument("Instr_ArteryForceps_Mosquito", 0.125, curved=True, jaw_len=0.03, tip_w=0.002)),
    ("Instr_NeedleHolder_MayoHegar", lambda: ring_instrument("Instr_NeedleHolder_MayoHegar", 0.16, jaw_len=0.03, tip_w=0.005, gold=True)),
    ("Instr_AllisTissueForceps", lambda: ring_instrument("Instr_AllisTissueForceps", 0.155, jaw="allis", jaw_len=0.055, tip_w=0.003)),
    ("Instr_SpongeHolder_Rampley", lambda: ring_instrument("Instr_SpongeHolder_Rampley", 0.24, jaw="sponge", jaw_len=0.09, tip_w=0.004)),
    ("Instr_TowelClip_Backhaus", build_towel_clip),
    ("Instr_DissectingForceps_Toothed", lambda: build_tissue_forceps("Instr_DissectingForceps_Toothed", toothed=True)),
    ("Instr_DissectingForceps_NonToothed", lambda: build_tissue_forceps("Instr_DissectingForceps_NonToothed", toothed=False)),
    ("Instr_Retractor_Langenbeck", build_retractor),
    ("Instr_KidneyDish", build_kidney_dish),
    ("Instr_Gallipot", build_gallipot),
    ("Instr_GauzeSwabs", build_gauze_swabs),
    ("Instr_SuturePack", build_suture_pack),
]
