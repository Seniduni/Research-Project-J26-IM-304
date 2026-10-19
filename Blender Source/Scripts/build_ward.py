"""
build_ward.py  -  builds the complete MR Operating Theatre in Blender and exports:
  <out>/FBX/Ward/MR_OperatingTheatre_Ward.fbx      (whole ward, one hierarchy, for Unity)
  <out>/FBX/Equipment/<Asset>.fbx                  (each asset on its own, for reuse)
  <out>/FBX/Instruments/<Instrument>.fbx
  <out>/Blender/MR_OperatingTheatre_Ward.blend     (master scene, collections per zone)
  <out>/Blender/Equipment/<Asset>.blend            (each asset in its own .blend)
  <out>/Blender/Instruments/<Instrument>.blend
  <out>/layout.json                                (asset ids + positions, used by Unity)

Run inside Blender (Scripting tab -> Open -> Run Script), or headless:
  blender -b --factory-startup -P build_ward.py -- <output_folder>
Author: H.M.H. Lakshan (IT23404564).  Works with Blender 4.2+.
"""
import bpy, sys, os, json, math, importlib

def _script_dir():
    for t in bpy.data.texts:                       # run from Blender's Text Editor
        if t.filepath and os.path.basename(t.filepath) == "build_ward.py":
            return os.path.dirname(bpy.path.abspath(t.filepath))
    f = globals().get("__file__")
    if f and os.path.isfile(f):
        return os.path.dirname(os.path.abspath(f))
    return os.getcwd()
HERE = _script_dir()
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import otlib, assets_room, assets_anaesthesia, assets_instruments, assets_periphery
for m in (otlib, assets_room, assets_anaesthesia, assets_instruments, assets_periphery):
    importlib.reload(m)
from otlib import *
from assets_room import *
from assets_anaesthesia import *
from assets_periphery import *
from assets_instruments import INSTRUMENT_BUILDERS

argv = sys.argv
OUT = argv[argv.index("--") + 1] if "--" in argv else os.path.join(HERE, "..")   # default: the 'Blender Source' folder
OUT = os.path.abspath(OUT)
EXPORT_FBX = ("--fbx" in argv) or (bpy.app.background and "--no-fbx" not in argv)
EXPORT_BLEND = "--no-blend" not in argv

REG = {}

def reset():
    if bpy.app.background:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        return
    # interactive Blender: clear the scene data without unloading the running script
    for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras,
                 bpy.data.lights, bpy.data.collections, bpy.data.images, bpy.data.worlds):
        for item in list(coll):
            try: coll.remove(item)
            except Exception: pass
    for sc in list(bpy.data.scenes)[1:]:
        bpy.data.scenes.remove(sc)

def new_coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(c)
    return c

def with_coll(coll, fn, *a, **k):
    otlib._current_coll[0] = coll
    r = fn(*a, **k)
    # move root + all children into coll
    for o in [r] + list(r.children_recursive):
        for c in list(o.users_collection):
            if c != coll: c.objects.unlink(o)
        if coll not in o.users_collection: coll.objects.link(o)
    otlib._current_coll[0] = None
    return r

def dup_into(src_root, parent, loc, rot=(0, 0, 0), name=None, coll=None):
    """Linked duplicate of an asset (shares mesh data) parented to another asset."""
    nr = bpy.data.objects.new(name or src_root.name + "_Copy", None)
    nr.empty_display_size = 0.05
    (coll or src_root.users_collection[0]).objects.link(nr)
    nr.parent = parent
    nr.location = loc
    nr.rotation_euler = rot
    for ch in src_root.children:
        c = ch.copy()
        (coll or src_root.users_collection[0]).objects.link(c)
        c.parent = nr
    return nr

def main():
    reset()
    sc = bpy.context.scene
    sc.name = "MR_OperatingTheatre"
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    world = bpy.data.worlds.new("World"); sc.world = world
    world.color = (0.05, 0.06, 0.07)

    top = new_coll("MR_OperatingTheatre")
    C = {k: new_coll(k, top) for k in ("01_Architecture", "02_Ceiling_Systems", "03_Surgical_Zone",
                                       "04_Anaesthetic_Area", "05_Peripheral_Equipment", "06_Wall_Equipment",
                                       "07_Scrub_And_Corridor", "08_Instruments", "09_Patient_Shared")}
    layout = []
    REG.clear()
    def put(coll_key, fn, loc, rot=0.0, zone="", fn_args=(), hidden=False):
        r = with_coll(C[coll_key], fn, *fn_args)
        REG[r.name] = (fn, fn_args)
        place(r, loc, rot)
        layout.append({"id": r.name, "zone": zone, "pos": list(loc), "rotZ": rot, "hidden": hidden})
        if hidden:
            r.hide_viewport = False
        return r

    D2 = ROOM_D/2
    # ---------- architecture
    put("01_Architecture", build_room, (0, 0, 0), 0, "Architecture")
    put("01_Architecture", build_main_door, (0, -D2 - WT/2, 0), 0, "Doors")
    put("01_Architecture", build_swing_door, (-2.75, D2 + WT/2, 0), 0, "Doors", ("Door_AnaestheticRoom", 1.2, "ANAESTHETIC ROOM"))
    put("01_Architecture", build_swing_door, (2.5, D2 + WT/2, 0), 0, "Doors", ("Door_DirtyUtility", 1.0, "DIRTY UTILITY", "M_Sign_Red"))
    put("01_Architecture", build_swing_door, (-ROOM_W/2 - WT/2, -2.0, 0), 90, "Doors", ("Door_PrepRoom", 1.2, "PREP / LAY-UP ROOM"))
    put("01_Architecture", build_swing_door, (ROOM_W/2 + WT/2, -1.9, 0), -90, "Doors", ("Door_ScrubRoom", 1.0, "SCRUB ROOM"))
    # ---------- ceiling systems
    put("02_Ceiling_Systems", build_laminar_canopy, (0, 0.2, 0), 0, "Ceiling")
    put("02_Ceiling_Systems", build_surgical_lights, (0, 0.2, 0), 0, "Ceiling")
    put("02_Ceiling_Systems", build_pendant, (-1.85, 1.70, 0), 90, "Anaesthetic", ("Anaesthesia",))
    put("02_Ceiling_Systems", build_pendant, (1.75, 1.40, 0), -90, "Surgical", ("Surgical",))
    # ---------- surgical zone
    table = put("03_Surgical_Zone", build_operating_table, (0, 0.2, 0), 0, "Surgical")
    put("03_Surgical_Zone", build_anaesthetic_screen, (0, 0.90, 0), 0, "Surgical")
    mayo = put("03_Surgical_Zone", build_mayo_stand, (0, -0.90, 0), 180, "Surgical")
    trolley = put("03_Surgical_Zone", build_instrument_trolley, (1.45, -1.55, 0), 90, "Surgical")
    put("03_Surgical_Zone", build_diathermy, (-1.40, -0.45, 0), 90, "Surgical")
    put("03_Surgical_Zone", build_suction_unit, (1.30, 1.55, 0), -90, "Surgical")
    put("03_Surgical_Zone", build_kick_bucket, (0.80, -0.25, 0), 0, "Surgical")
    put("03_Surgical_Zone", build_step_stool, (-0.62, -0.55, 0), 90, "Surgical")
    put("03_Surgical_Zone", build_surgeon_stool, (0.75, 0.55, 0), 0, "Surgical")
    # ---------- anaesthetic area
    mach = put("04_Anaesthetic_Area", build_anaesthesia_machine, (-0.90, 2.65, 0), 0, "Anaesthetic")
    put("04_Anaesthetic_Area", build_patient_monitor, (-0.75, 2.75, 1.615), 20, "Anaesthetic")
    put("04_Anaesthetic_Area", build_anaesthetic_trolley, (0.75, 2.70, 0), 0, "Anaesthetic")
    put("04_Anaesthetic_Area", build_sharps_bin, (0.98, 2.80, 0.95), 0, "Anaesthetic")
    put("04_Anaesthetic_Area", build_iv_stand, (-0.85, 1.35, 0), 0, "Anaesthetic")
    ivp = put("04_Anaesthetic_Area", build_iv_stand, (-1.15, 0.80, 0), 0, "Anaesthetic")
    ivp.name = "IVDripStand_Pumps"; layout[-1]["id"] = ivp.name; REG.pop("IVDripStand.001", None)
    for ch in ivp.children: ch.name = "IVDripStand_Pumps_Mesh"
    put("04_Anaesthetic_Area", build_infusion_pump, (-1.15, 0.70, 0.25), 0, "Anaesthetic")
    put("04_Anaesthetic_Area", build_patient_warmer, (1.55, 2.35, 0), -60, "Anaesthetic")
    # ---------- peripheral equipment
    put("05_Peripheral_Equipment", build_crash_cart, (-2.20, -3.12, 0), 180, "Peripheral")
    put("05_Peripheral_Equipment", build_waste_bin, (1.15, 3.25, 0), 180, "Peripheral", ("Clinical",))
    put("05_Peripheral_Equipment", build_waste_bin, (0.65, 3.25, 0), 180, "Peripheral", ("General",))
    put("05_Peripheral_Equipment", build_linen_skip, (3.15, 3.05, 0), 0, "Peripheral")
    put("05_Peripheral_Equipment", build_workstation, (-3.30, -0.10, 0), 90, "Peripheral")
    put("05_Peripheral_Equipment", build_sterile_cabinet, (-3.36, 1.65, 0), 90, "Peripheral")
    put("05_Peripheral_Equipment", build_fluid_warmer_cabinet, (3.31, -0.90, 0), -90, "Peripheral")
    put("05_Peripheral_Equipment", build_positioning_aids, (2.85, -2.95, 0), 0, "Peripheral")
    put("05_Peripheral_Equipment", build_lead_apron_rack, (2.05, -3.43, 0), 180, "Peripheral")
    # ---------- wall equipment
    put("06_Wall_Equipment", build_wall_monitor, (3.57, 0.95, 1.65), -90, "Wall", ("PACS_ImagingMonitor", 1.10, 0.65, "M_Screen_PACS", "pacs"))
    put("06_Wall_Equipment", build_wall_monitor, (3.57, -0.85, 1.55), -90, "Wall", ("TeachingMonitor", 0.80, 0.46, "M_Screen_Teaching", "teaching"))
    put("06_Wall_Equipment", build_wall_clock_panel, (0.55, ROOM_D/2 - 0.02, 1.85), 0, "Wall")
    put("06_Wall_Equipment", build_whiteboard, (3.58, 2.35, 1.45), -90, "Wall")
    put("06_Wall_Equipment", build_wall_gas_panel, (-0.15, ROOM_D/2 - 0.015, 1.25), 0, "Wall")
    put("06_Wall_Equipment", build_hand_rub_dispenser, (1.15, -ROOM_D/2 + 0.01, 1.25), 180, "Wall")
    put("06_Wall_Equipment", build_fire_safety, (2.95, -ROOM_D/2 + 0.0, 0.0), 180, "Wall")
    put("06_Wall_Equipment", build_telephone, (1.75, ROOM_D/2 - 0.025, 1.40), 0, "Wall")
    put("06_Wall_Equipment", build_specimen_station, (3.47, 1.85, 1.05), -90, "Wall")
    # ---------- scrub area & corridor (outside the theatre door)
    cy = -ROOM_D/2 - WT
    put("07_Scrub_And_Corridor", build_scrub_sink, (-2.30, cy - CORR_D + 0.30, 0), 180, "Corridor")
    put("07_Scrub_And_Corridor", build_ppe_trolley, (3.30, cy - 1.10, 0), -90, "Corridor")
    put("07_Scrub_And_Corridor", build_shoe_rack, (3.40, cy - 2.40, 0), -90, "Corridor")
    hr = put("07_Scrub_And_Corridor", build_hand_rub_dispenser, (1.25, cy - 0.01, 1.25), 0, "Corridor")
    hr.name = "HandRubDispenser_Corridor"; layout[-1]["id"] = hr.name; REG.pop("HandRubDispenser.001", None)
    # ---------- patient (shared virtual patient dummy - hidden in orientation)
    put("09_Patient_Shared", build_patient_mannequin, (0, 0.2, 0), 0, "Patient", hidden=True)
    # ---------- instruments (library row, then duplicates on Mayo tray & trolley)
    lib = {}
    for i, (nm, fn) in enumerate(INSTRUMENT_BUILDERS):
        r = with_coll(C["08_Instruments"], fn)
        REG[nm] = (fn, ())
        lib[nm] = r
    mayo_layout = [("Instr_Scalpel_No3_Blade10", -0.15), ("Instr_Scissors_Mayo_Curved", -0.11), ("Instr_Scissors_Metzenbaum", -0.07),
                   ("Instr_DissectingForceps_Toothed", -0.035), ("Instr_DissectingForceps_NonToothed", -0.005),
                   ("Instr_ArteryForceps_Kelly_Curved", 0.03), ("Instr_ArteryForceps_Mosquito", 0.065),
                   ("Instr_NeedleHolder_MayoHegar", 0.10), ("Instr_TowelClip_Backhaus", 0.135)]
    # Mayo tray anchor (local) = (0, -0.02, 1.33)
    for nm, yy in mayo_layout:
        dup_into(lib[nm], mayo, (0.02, -0.02 + yy, 1.334), (0, 0, math.radians(90)), nm + "_OnMayo", C["03_Surgical_Zone"])
    trolley_layout = [("Instr_SpongeHolder_Rampley", (-0.25, 0.14)), ("Instr_AllisTissueForceps", (-0.27, 0.09)),
                      ("Instr_Retractor_Langenbeck", (-0.25, 0.03)), ("Instr_ArteryForceps_Kelly_Curved", (-0.28, -0.02)),
                      ("Instr_ArteryForceps_Mosquito", (-0.29, -0.06)), ("Instr_KidneyDish", (0.05, 0.10)),
                      ("Instr_Gallipot", (0.10, -0.12)), ("Instr_GauzeSwabs", (-0.05, -0.17)), ("Instr_SuturePack", (-0.30, -0.19))]
    for nm, (x, y) in trolley_layout:
        z = 0.947 if nm.startswith(("Instr_Kidney", "Instr_Gallipot", "Instr_Gauze", "Instr_Suture")) else 0.948
        z = 0.905 if nm in ("Instr_KidneyDish", "Instr_Gallipot", "Instr_GauzeSwabs", "Instr_SuturePack") else 0.947
        dup_into(lib[nm], trolley, (x, y, z), (0, 0, 0), nm + "_OnTrolley", C["03_Surgical_Zone"])
    # library row (catalogue) placed out of the way on the sterile cabinet? keep at origin far below (hidden in ward)
    for i, (nm, r) in enumerate(lib.items()):
        place(r, (-2.0 + 0.30*(i % 8), -8.6 - 0.25*(i // 8), 0.0), 0)
        layout.append({"id": nm, "zone": "InstrumentLibrary", "pos": list(r.location), "rotZ": 0, "hidden": True})

    # camera + sun for previews in Blender
    cam = bpy.data.cameras.new("Cam"); cam.lens = 18
    co = bpy.data.objects.new("PreviewCamera", cam); sc.collection.objects.link(co)
    co.location = (3.0, -3.2, 2.4); co.rotation_euler = (math.radians(68), 0, math.radians(42))
    sc.camera = co

    for e in [o for o in bpy.data.objects if o.name.startswith("LIGHT_")]:
        surgical = e.name.startswith("LIGHT_Surgical")
        L = bpy.data.lights.new(e.name + "_Lamp", 'SPOT' if surgical else 'AREA')
        if surgical:
            L.energy = 900; L.spot_size = math.radians(40); L.shadow_soft_size = 0.3
        else:
            L.energy = 180 if "Corridor" not in e.name else 120; L.size = 0.6
        lo = bpy.data.objects.new(e.name + "_Lamp", L)
        e.users_collection[0].objects.link(lo)
        lo.parent = e
        if surgical:
            tgt = bpy.data.objects.get("OperatingTable")
            aim = (tgt.location if tgt else e.matrix_world.translation) + Vector((0, 0, 0.95))
            d = aim - e.matrix_world.translation
            lo.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
            lo.matrix_parent_inverse = e.matrix_world.inverted()
            lo.location = e.matrix_world.translation
    world.color = (0.35, 0.37, 0.40)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "layout.json"), "w") as f:
        json.dump(layout, f, indent=1)
    return lib, layout

def select_tree(root):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in [root] + list(root.children_recursive):
        try: o.select_set(True)
        except RuntimeError: pass

def export_fbx(path, selection=True):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=selection, object_types={'EMPTY', 'MESH'},
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', use_custom_props=True,
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

def export_all(lib, layout):
    if EXPORT_FBX:
        # whole ward (everything except preview camera)
        for o in bpy.context.view_layer.objects: o.select_set(o.type in ('MESH', 'EMPTY'))
        export_fbx(os.path.join(OUT, "FBX", "Ward", "MR_OperatingTheatre_Ward.fbx"))
        # each asset individually at the origin
        roots = [bpy.data.objects[e["id"]] for e in layout if e["id"] in bpy.data.objects]
        for r in roots:
            if r.parent is not None: continue
            old_loc, old_rot = r.location.copy(), r.rotation_euler.copy()
            r.location = (0, 0, 0); r.rotation_euler = (0, 0, 0)
            bpy.context.view_layer.update()
            select_tree(r)
            sub = "Instruments" if r.name.startswith("Instr_") else "Equipment"
            export_fbx(os.path.join(OUT, "FBX", sub, r.name + ".fbx"))
            r.location, r.rotation_euler = old_loc, old_rot
        bpy.context.view_layer.update()
    if EXPORT_BLEND:
        bdir = OUT if not bpy.app.background else os.path.join(OUT, "Blender")
        os.makedirs(os.path.join(bdir, "Equipment"), exist_ok=True)
        os.makedirs(os.path.join(bdir, "Instruments"), exist_ok=True)
        master = os.path.join(bdir, "MR_OperatingTheatre_Ward.blend")
        bpy.ops.wm.save_as_mainfile(filepath=master, compress=True)
        # one clean .blend per asset (rebuilt alone at the origin)
        for nm, (fn, args) in list(REG.items()):
            reset()
            sc = bpy.context.scene; sc.name = nm
            col = bpy.data.collections.new(nm); sc.collection.children.link(col)
            r = with_coll(col, fn, *args)
            r.name = nm
            sub = "Instruments" if nm.startswith("Instr_") else "Equipment"
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(bdir, sub, nm + ".blend"), compress=True)
        if not bpy.app.background:
            bpy.ops.wm.open_mainfile(filepath=master)

if __name__ == "__main__":
    lib, layout = main()
    export_all(lib, layout)
    print("MR Operating Theatre build complete ->", OUT)
