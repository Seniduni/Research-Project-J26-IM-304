import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json
sys.path.insert(0,ND+"/scripts")
from poselib import *
from babylib import *
from mathutils import Vector, Matrix
# load mother (posed) + append baby
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m3_canal.blend")
marm=bpy.data.objects["ND_MotherRig"]; body=bpy.data.objects["M_Body"]
if bpy.data.objects.get("ND_GreenCloth_Src"): bpy.data.objects["ND_GreenCloth_Src"].hide_render=True
cfg=settle(marm,body,lambda c: lithotomy(marm,c))
json.dump({k:float(v) for k,v in cfg.items() if not isinstance(v,str)},open(ND+"/blend/settle.json","w"))
anc=marm.pose.bones["BirthAnchor"]; AM=anc.matrix.copy()
with bpy.data.libraries.load(ND+"/blend/b2_rig.blend") as (src,dst): dst.objects=[n for n in src.objects]
for o in dst.objects: bpy.context.scene.collection.objects.link(o)
barm=bpy.data.objects["ND_BabyRig"]
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'

sc.render.resolution_x=420; sc.render.resolution_y=420
sc.render.film_transparent=True
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=0.75
P0=AM.translation
ts=[0.0,0.30,0.45,0.55,0.62,0.70,0.80,0.88,1.0]
for t in ts:
    k=birth_pose(barm,t,AM)
    r=min(0.05,max(0.0,0.05*smooth(0,1,(t-0.25)/0.2)))*(1 if t<0.95 else 0.5)
    dilate(marm,r,0.02*smooth(0,1,(t-0.3)/0.15)*(1-smooth(0,1,(t-0.9)/0.1)))
    cam.location=P0+Vector((1.5,-0.12,0.05)); cam.rotation_euler=(P0+Vector((0,-0.12,0.05))-cam.location).to_track_quat('-Z','Y').to_euler()
    mothers=[o for o in bpy.data.objects if o.type=='MESH' and o.parent==marm]
    babies=[o for o in bpy.data.objects if o.type=='MESH' and o.parent==barm]
    for o in mothers: o.hide_render=True
    sc.render.filepath=f"{ND}/renders/bb_{int(t*100):03d}.png"; bpy.ops.render.render(write_still=True)
    for o in mothers: o.hide_render=(o.name!="M_Body")
    for o in babies: o.hide_render=True
    sc.render.filepath=f"{ND}/renders/bm_{int(t*100):03d}.png"; bpy.ops.render.render(write_still=True)
    for o in babies: o.hide_render=False
print("ANCHOR",list(AM.translation),[list(r) for r in AM.to_3x3()])
