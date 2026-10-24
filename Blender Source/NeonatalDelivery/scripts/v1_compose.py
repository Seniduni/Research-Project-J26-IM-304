import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json
sys.path.insert(0,ND+"/scripts")
from mathutils import Vector
from poselib import dilate
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m5_drape.blend")
sc=bpy.context.scene
arm=bpy.data.objects["ND_MotherRig"]
bpy.data.objects["ND_GreenCloth_Src"].hide_render=True
for lib,pref in ((ND+"/blend/p1_props.blend",("ND_DeliveryBed",)),(ND+"/blend/b4_baked.blend",("ND_BabyRig","B_Body"))):
    with bpy.data.libraries.load(lib) as (src,dst):
        dst.objects=[n for n in src.objects if n.startswith(pref)]
        dst.actions=[n for n in src.actions if n.startswith('ND_B_')]
    for o in dst.objects: sc.collection.objects.link(o)
barm=bpy.data.objects["ND_BabyRig"]
if barm.animation_data is None: barm.animation_data_create()
barm.animation_data.action=bpy.data.actions["ND_B_Birth"]
arm.animation_data.action=bpy.data.actions["ND_M_Base"]
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
sc.display.shading.show_shadows=True; sc.display.shading.show_cavity=True
sc.render.resolution_x=900; sc.render.resolution_y=700
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
views={'trainee':((0.0,-1.05,1.62),(0,0.05,1.05),32),'three4':((1.55,-1.35,1.75),(0,0.25,1.0),35),'side':((2.2,0.25,1.25),(0,0.3,1.05),35)}
for t,dil in ((0.45,0.05),(1.0,0.0)):
    sc.frame_set(int(t*300)); bpy.context.view_layer.update()
    arm.data.pose_position='POSE'
    dilate(arm,dil,0.02 if dil>0 else 0)
    for nm,(loc,tg,lens) in views.items():
        cam.data.lens=lens; cam.location=loc; cam.rotation_euler=(Vector(tg)-Vector(loc)).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=f"{ND}/renders/comp_{int(t*100)}_{nm}.png"; bpy.ops.render.render(write_still=True)
