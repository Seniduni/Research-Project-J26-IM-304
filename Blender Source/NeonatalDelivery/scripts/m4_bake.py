# Bake mother animation clips (bed space). Ring bones are script-driven in Unity and are NOT keyed.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json
sys.path.insert(0,ND+"/scripts")
from poselib import *
from bakelib import *
def smooth(a,b,t):
    t=max(0,min(1,t)); t=t*t*(3-2*t); return a+(b-a)*t
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m3_canal.blend")
arm=bpy.data.objects["ND_MotherRig"]; body=bpy.data.objects["M_Body"]
base=json.load(open(ND+"/blend/settle.json"))
FPS=30; bpy.context.scene.render.fps=FPS
EXC=("Introitus_","CanalA_","CanalB_","Perineum","BirthAnchor")
def P(**kw):
    c=dict(base); c.update(kw); return c
def bake(name,seconds,fn,step=2):
    reset_prev(); act=new_action(arm,name)
    nf=int(seconds*FPS)
    for f in range(0,nf+1,step):
        t=f/FPS; lithotomy(arm,fn(t)); key_pose(arm,f,exclude=EXC,scale=True)
    act.frame_range=(0,nf)
    print("baked",name,nf)
def breath(t,period=4.0): return 0.5-0.5*math.cos(2*math.pi*t/period)
bake("ND_M_Base",0.1,lambda t:P())
bake("ND_M_Idle",4.0,lambda t:P(breath=breath(t),head=(0.0+0.03*math.sin(t*math.pi/2),0.40,0.92)))
def push(t):
    # 0-1 inhale, 1-2 curl up & grab knees, 2-6 sustained push (tremble), 6-7 release, 7-8 recover
    e=lambda a,b: smooth(0,1,(t-a)/(b-a))
    curl=e(1.0,2.0)*(1-e(6.0,7.0))
    tremble=0.9*math.sin(t*2*math.pi*5.5)*curl
    return P(breath=e(0,1)*(1-e(2.0,2.4))+0.3*e(6.2,7.0)*(1-e(7.2,8.0)),
             arms={'handles':1-curl+1e-3,'knees':curl+1e-3},curlspine=26*curl+tremble,chin=40*curl,
             thighflex=72-12*curl,grip=0.85+0.1*curl)
bake("ND_M_Push",8.0,push)
bake("ND_M_Pant",2.0,lambda t:P(breath=0.35+0.25*math.sin(t*2*math.pi*2.0),head=(0,0.45,0.89),chin=-5))
bake("ND_M_Reach",2.0,lambda t:P(arms={'handles':1-smooth(0,1,t/1.6)+1e-3,'reach':smooth(0,1,t/1.6)+1e-3},curlspine=16*smooth(0,1,t/1.6),chin=22*smooth(0,1,t/1.6)))
bake("ND_M_Cradle",4.0,lambda t:P(arms={'cradle':1.0},breath=breath(t),chin=18,head=(0.03*math.sin(t*math.pi/2),0.40,0.92)))
# reference data for Unity
lithotomy(arm,P())
out={"settle":base,"seat":SEAT}
for b in ("Hips","Spine","Chest","UpperChest","Head","Perineum","BirthAnchor","LeftUpperArm","RightUpperArm"):
    pb=arm.pose.bones[b]; out[b]={"head":list(pb.head),"tail":list(pb.tail)}
json.dump(out,open(ND+"/blend/mother_ref.json","w"),indent=1)
arm.animation_data.action=bpy.data.actions["ND_M_Base"]
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m4_baked.blend")
