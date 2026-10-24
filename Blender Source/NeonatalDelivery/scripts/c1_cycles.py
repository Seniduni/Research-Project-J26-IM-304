# Usage: blender -b -P scripts/c1_cycles.py   (env: ND_RES=0.75 ND_SAMPLES=64 ND_EXP=-1.8 ND_ONLY=01)
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
# Cycles path-traced stills of the delivery inside the team's OT ward (presentation / report figures).
import bpy, sys, math, os
sys.path.insert(0,ND+"/scripts")
from mathutils import Vector
from poselib import dilate
W=_os.path.join(ND,"..","MR_OperatingTheatre_Ward.blend")   # the team ward (Blender Source/)

res=float(os.environ.get("ND_RES","1.0")); samples=int(os.environ.get("ND_SAMPLES","96")); only=os.environ.get("ND_ONLY","")
bpy.ops.wm.open_mainfile(filepath=W)
sc=bpy.context.scene
# ---- hide ward furniture occupying the delivery area (render only)
hide={"OperatingTable","AnaestheticScreen","PatientMannequin","MayoStand","StepStool","SurgeonStool"}
for o in bpy.data.objects:
    r=o
    while r.parent: r=r.parent
    if r.name in hide or o.name.startswith("Instr_") or o.name=="PreviewCamera": o.hide_render=True
# ---- append our assets
col=bpy.data.collections.new("NeonatalDelivery"); sc.collection.children.link(col)
def append(path,pred,acts=()):
    with bpy.data.libraries.load(path) as (src,dst):
        dst.objects=[n for n in src.objects if pred(n)]
        dst.actions=[n for n in src.actions if any(n.startswith(a) for a in acts)]
    for o in dst.objects:
        if o is not None: col.objects.link(o)
    return dst.objects
mom=append(ND+"/ND_Mother_Rigged.blend",lambda n: n!="ND_GreenCloth_Src" and not n.startswith("cam"),("ND_M_",))
bed=append(ND+"/ND_DeliveryProps.blend",lambda n: n.startswith("ND_DeliveryBed") or n.startswith("ND_DeliveryTray") or n.startswith("ND_CordClamp") or n.startswith("ND_CordScissors") or n.startswith("ND_Towel"))
bab=append(ND+"/ND_Baby_Rigged.blend",lambda n: n.startswith("ND_BabyRig") or n.startswith("B_Body"),("ND_B_",))
for o in list(col.objects):
    if o.type in ('CAMERA','LIGHT'): o.hide_render=True
# props at their positions on a trolley-free layout: tray/tools on the bed's right side table spot, towel on the seat
off=bpy.data.objects.new("ND_Offset",None); col.objects.link(off); off.location=(0,-0.27,0)
for o in col.objects:
    if o.parent is None and o is not off: o.parent=off
for n,loc in (("ND_DeliveryTray",(0.68,-0.88,0.932)),("ND_Towel",(0.29,0.17,0.926))):
    o=bpy.data.objects.get(n)
    if o: o.location=loc
for n,loc in (("ND_CordClamp",(0.56,-0.95,0.944)),("ND_CordScissors",(0.74,-0.82,0.944))):
    o=bpy.data.objects.get(n)
    if o: o.location=loc
# simple Mayo-style stand under the tray
bpy.ops.mesh.primitive_cylinder_add(radius=0.014,depth=0.92,location=(0,0,0)); p=bpy.context.object; p.parent=off; p.location=(0.68,-0.88,0.46)
bpy.ops.mesh.primitive_cylinder_add(radius=0.23,depth=0.024,location=(0,0,0)); p2=bpy.context.object; p2.parent=off; p2.location=(0.68,-0.88,0.012); p2.data.materials.append(bpy.data.materials.get('M_Plastic_Grey') or bpy.data.materials.new('grey'))
p.data.materials.append(bpy.data.materials.get("M_Steel_Brushed") or bpy.data.materials.new("steel"))
arm=bpy.data.objects["ND_MotherRig"]; barm=bpy.data.objects["ND_BabyRig"]
arm.animation_data_create(); arm.animation_data.action=bpy.data.actions["ND_M_Base"]
barm.animation_data_create(); barm.animation_data.action=bpy.data.actions["ND_B_Birth"]
# ---- skin: subsurface + sheen-free wet look for the newborn
for m in bpy.data.materials:
    if not m.use_nodes: continue
    bs=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if not bs: continue
    if m.name.startswith("Std_Skin") or m.name=="ND_BabySkin":
        bs.inputs["Subsurface Weight"].default_value=0.22 if m.name!="ND_BabySkin" else 0.3
        bs.inputs["Subsurface Radius"].default_value=(1.0,0.35,0.2)
        bs.inputs["Subsurface Scale"].default_value=0.006
        bs.inputs["Roughness"].default_value=0.48 if m.name!="ND_BabySkin" else 0.32
        try: bs.inputs["Coat Weight"].default_value=0.0 if m.name!="ND_BabySkin" else 0.35; bs.inputs["Coat Roughness"].default_value=0.15
        except KeyError: pass
    if m.name=="ND_Mucosa": bs.inputs["Roughness"].default_value=0.15; bs.inputs["Subsurface Weight"].default_value=0.4; bs.inputs["Subsurface Scale"].default_value=0.004
# ---- lighting: surgical spots aimed at the perineum, softened for skin
focus=Vector((0,-0.03-0.27,0.98))
for o in bpy.data.objects:
    if o.type=='LIGHT' and o.name.startswith("LIGHT_Surgical"):
        o.data.energy=420 if "Main" in o.name else 260; o.data.shadow_soft_size=0.25; o.data.spot_size=math.radians(70); o.data.spot_blend=0.6
        o.rotation_euler=(focus-o.matrix_world.translation).to_track_quat('-Z','Y').to_euler()
# ---- render settings
sc.render.engine='CYCLES'; cy=sc.cycles; cy.device='CPU'; cy.samples=samples; cy.use_adaptive_sampling=True; cy.adaptive_threshold=0.03
cy.use_denoising=True; cy.denoiser='OPENIMAGEDENOISE'; cy.max_bounces=6; cy.diffuse_bounces=3; cy.glossy_bounces=3; cy.transmission_bounces=4; cy.transparent_max_bounces=8
cy.caustics_reflective=False; cy.caustics_refractive=False; cy.blur_glossy=1.0; sc.render.use_persistent_data=True
sc.render.resolution_x=int(1600*res); sc.render.resolution_y=int(900*res); sc.render.film_transparent=False
sc.view_settings.view_transform='AgX'; sc.view_settings.exposure=float(os.environ.get('ND_EXP','-1.4'))
try: sc.view_settings.look='AgX - Medium High Contrast'
except Exception: pass
cam=bpy.data.objects.new("ND_Cam",bpy.data.cameras.new("ND_Cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.dof.use_dof=True; cam.data.dof.aperture_fstop=4.0
shots=[ # (name, birth t, dilation, cam loc, target, lens)
 ("01_crowning_trainee",0.45,0.05,(0.0,-1.62,1.78),(0,-0.12,1.08),27),
 ("02_head_born_threequarter",0.62,0.03,(1.10,-1.55,1.55),(0,-0.25,1.0),38),
 ("03_shoulders_side",0.80,0.02,(-1.30,-0.95,1.50),(0,-0.32,1.0),34),
 ("04_born_trainee",1.0,0.0,(0.25,-1.70,1.70),(0,-0.40,1.0),28),
]
for name,t,dil,loc,tg,lens in shots:
    if only and only not in name: continue
    sc.frame_set(int(t*300)); bpy.context.view_layer.update()
    arm.data.pose_position='POSE'; dilate(arm,dil,0.02 if dil>0 else 0); bpy.context.view_layer.update()
    cam.data.lens=lens; cam.location=loc; cam.rotation_euler=(Vector(tg)-Vector(loc)).to_track_quat('-Z','Y').to_euler()
    cam.data.dof.focus_distance=(Vector(tg)-Vector(loc)).length
    sc.render.filepath=f"{ND}/renders_cycles/{name}.png"; bpy.ops.render.render(write_still=True)
    print("RENDERED",name,flush=True)
