import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, math
from mathutils import Quaternion
def new_action(obj,name):
    if obj.animation_data is None: obj.animation_data_create()
    obj.animation_data.action=None
    act=bpy.data.actions.new(name); act.use_fake_user=True
    obj.animation_data.action=act
    try:
        if hasattr(obj.animation_data,'action_slot') and obj.animation_data.action_slot is None and len(act.slots)==0:
            slot=act.slots.new(id_type='OBJECT',name=obj.name); obj.animation_data.action_slot=slot
    except Exception as e: print("slot",e)
    return act
_prevq={}
def key_pose(obj,frame,bones=None,exclude=(),loc_bones=('Root','Hips'),scale=False):
    for pb in obj.pose.bones:
        if bones and pb.name not in bones: continue
        if pb.name.startswith(exclude): continue
        q=pb.rotation_quaternion.copy()
        k=(obj.name,pb.name)
        if k in _prevq and _prevq[k].dot(q)<0: q.negate(); pb.rotation_quaternion=q
        _prevq[k]=q.copy()
        pb.keyframe_insert("rotation_quaternion",frame=frame,group=pb.name)
        if pb.name in loc_bones: pb.keyframe_insert("location",frame=frame,group=pb.name)
        if scale: pb.keyframe_insert("scale",frame=frame,group=pb.name)
def reset_prev(): _prevq.clear()
