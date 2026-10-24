import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys
for f in ["ND_Mother","ND_Baby","ND_DeliveryBed","ND_CordClamp","ND_CordScissors"]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f"{ND_OUT}/Art/Models/{f}.fbx")
    print("=====",f)
    for o in bpy.data.objects:
        info=""
        if o.type=='ARMATURE': info=f"bones={len(o.data.bones)}"
        if o.type=='MESH': info=f"v={len(o.data.vertices)} sk={len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0} vg={len(o.vertex_groups)} mats={[m.name for m in o.data.materials]}"
        print(" ",o.type,o.name,"parent=",o.parent.name if o.parent else None, o.parent_bone if o.parent_type=='BONE' else "",info)
    print("  actions:",[ (a.name,tuple(a.frame_range)) for a in bpy.data.actions])
    b=bpy.data.objects.get("M_Body")
    if b and b.data.shape_keys: print("  shapekeys:",[k.name for k in b.data.shape_keys.key_blocks])
