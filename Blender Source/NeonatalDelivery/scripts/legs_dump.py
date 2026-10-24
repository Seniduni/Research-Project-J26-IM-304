import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, json
sys.path.insert(0,ND+"/scripts")
from poselib import *
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m3_canal.blend")
arm=bpy.data.objects["ND_MotherRig"]; base=json.load(open(ND+"/blend/settle.json"))
out=lithotomy(arm,dict(base))
json.dump({k:list(v) for k,v in out.items()},open(ND+"/blend/legs.json","w"))
print(out)
