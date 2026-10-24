import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json
sys.path.insert(0,ND+"/scripts")
from poselib import *
from babylib import *
from bakelib import *
from mathutils import Vector, Matrix
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m3_canal.blend")
marm=bpy.data.objects["ND_MotherRig"]
base=json.load(open(ND+"/blend/settle.json"))
c=dict(base); lithotomy(marm,c)
AM=marm.pose.bones["BirthAnchor"].matrix.copy()
# remove mother, bring in baby
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
with bpy.data.libraries.load(ND+"/blend/b2_rig.blend") as (src,dst): dst.objects=[n for n in src.objects]
for o in dst.objects: bpy.context.scene.collection.objects.link(o)
arm=bpy.data.objects["ND_BabyRig"]
FPS=30; bpy.context.scene.render.fps=FPS
def bake(name,seconds,fn,step=2,root=False,bones=None):
    reset_prev(); act=new_action(arm,name); nf=max(1,int(seconds*FPS))
    for f in range(0,nf+1,step):
        fn(f/FPS); key_pose(arm,f,exclude=(() if root else ("Root",)),loc_bones=(('Root',) if root else ()),scale=True,bones=bones)
    act.frame_range=(0,nf); print("baked",name,nf)
bake("ND_B_Birth",10.0,lambda t: birth_pose(arm,t/10.0,AM),root=True)
def calm(t):
    posture(arm,flex=0.6); pb=arm.pose.bones['Chest']; b=0.5-0.5*math.cos(2*math.pi*t/1.2)
    pb.scale=(1+0.03*b,1,1+0.04*b); bpy.context.view_layer.update()
bake("ND_B_Calm",3.6,calm)
def cry(t):
    ph=t/0.8; posture(arm,flex=0.55,cry=1.0,phase=ph)
    pb=arm.pose.bones['Chest']; b=0.5+0.5*math.sin(ph*2*math.pi); pb.scale=(1+0.05*b,1,1+0.06*b); bpy.context.view_layer.update()
bake("ND_B_Cry",2.4,cry)
bake("ND_B_Limp",0.1,lambda t: posture(arm,flex=0.05))
bake("ND_B_ErbsPalsy",0.1,lambda t: posture(arm,flex=0.6,erb=1.0),bones=["RightUpperArm","RightLowerArm","RightHand","RightFingers"])
posture(arm,flex=0.6)
arm.animation_data.action=bpy.data.actions["ND_B_Calm"]
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/b4_baked.blend")
