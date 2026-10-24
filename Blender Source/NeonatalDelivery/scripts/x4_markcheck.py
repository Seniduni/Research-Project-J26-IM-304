import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy
for f in ["ND_Mother","ND_Baby"]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f"{ND_OUT}/Art/Models/{f}.fbx")
    arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
    arm.data.pose_position='REST'; bpy.context.view_layer.update()
    for o in bpy.data.objects:
        if o.name.startswith("ND_Z_"): print(f, o.name, o.parent_bone, tuple(round(x,3) for x in o.matrix_world.translation))
