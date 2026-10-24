# v2: use the user's new torso texture (tone-matched to the head/arm/leg maps) in the Blender mother.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy
SRC=ND+"/blend/m5b_drape.blend"; NEW=ND_OUT+"/Art/Textures/Mother/Std_Skin_Body_base.jpg"
bpy.ops.wm.open_mainfile(filepath=SRC)
m=bpy.data.materials["Std_Skin_Body"]; img=bpy.data.images.load(NEW,check_existing=True); n=0
for nd in m.node_tree.nodes:
    if nd.type=='TEX_IMAGE' and nd.image and nd.image.colorspace_settings.name!='Non-Color': nd.image=img; n+=1
img.pack(); print("replaced",n)
bpy.ops.wm.save_as_mainfile(filepath=SRC)
