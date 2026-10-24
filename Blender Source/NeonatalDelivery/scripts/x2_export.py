import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json
sys.path.insert(0,ND+"/scripts")
from mathutils import Vector, Matrix
from poselib import lithotomy
from markers import baby_markers, mother_markers
OUT=ND_OUT+"/Art/Models/"
KEEP={8:"ND_BrowRaiseInner_L",9:"ND_BrowRaiseInner_R",12:"ND_BrowLower_L",13:"ND_BrowLower_R",16:"ND_EyeClose_L",17:"ND_EyeClose_R",
      28:"ND_EyeSquint_L",29:"ND_EyeSquint_R",46:"ND_NoseSneer_L",47:"ND_NoseSneer_R",60:"ND_CheekRaise_L",61:"ND_CheekRaise_R",
      66:"ND_MouthSmile_L",67:"ND_MouthSmile_R",70:"ND_MouthFrown_L",71:"ND_MouthFrown_R",72:"ND_MouthStretch_L",73:"ND_MouthStretch_R",
      107:"ND_MouthPress",117:"ND_MouthOpenTeeth",127:"ND_JawDropSoft",136:"ND_NeckStrain_L",137:"ND_NeckStrain_R"}
def trim_shapes(o,keep):
    if not o.data.shape_keys: return
    kb=o.data.shape_keys.key_blocks
    names={i:k.name for i,k in enumerate(kb)}
    for i,k in list(enumerate(kb))[::-1]:
        if i==0: continue
        idx=int(k.name.split('_')[1]) if k.name.startswith('target_') else -1
        if idx in keep: k.name=keep[idx]
        else: o.shape_key_remove(k)
def export(path,objs,actions=True):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},use_mesh_modifiers=False,
        add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,
        bake_anim=actions,bake_anim_use_all_actions=actions,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,bake_anim_simplify_factor=0.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',axis_up='Y',path_mode='STRIP',mesh_smooth_type='FACE',use_tspace=True,embed_textures=False,
        use_custom_props=False,bake_space_transform=False)
    print("exported",path)
# ---------- MOTHER
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m5_drape.blend")
sc=bpy.context.scene
arm=bpy.data.objects["ND_MotherRig"]
if bpy.data.objects.get("ND_GreenCloth_Src"): bpy.data.objects.remove(bpy.data.objects["ND_GreenCloth_Src"])
for o in [o for o in bpy.data.objects if o.type=='MESH']:
    if o.name in("M_Body","M_Eyelash"): trim_shapes(o,KEEP)
    elif o.data.shape_keys:
        if o.name=="M_TeethLower" or o.name=="M_Tongue":
            for k in list(o.data.shape_keys.key_blocks)[::-1]: o.shape_key_remove(k)
        else:
            for k in list(o.data.shape_keys.key_blocks)[::-1]: o.shape_key_remove(k)
for o in bpy.data.objects:
    if o.type=='MESH':
        for m in o.data.materials:
            if m and m.name=="Green Duck Cloth": m.name="ND_DrapeGreen"
# anchors (positions only are used in Unity; orientations are computed at runtime)
base=json.load(open(ND+"/blend/settle.json"))
arm.animation_data.action=bpy.data.actions["ND_M_Base"]; sc.frame_set(0)
def anchor(name,bone,world_pos):
    e=bpy.data.objects.new(name,None); sc.collection.objects.link(e); e.empty_display_size=0.03
    pb=arm.pose.bones[bone]
    e.parent=arm; e.parent_type='BONE'; e.parent_bone=bone
    # bone-parent space: relative to bone tail in rest; use rest pose so offsets are bind-pose correct
    Mb=arm.matrix_world@pb.bone.matrix_local; Mb.translation=arm.matrix_world@pb.bone.tail_local
    rest_pos=world_pos
    e.matrix_world=Matrix.Translation(rest_pos)
    return e
# compute rest-space anchor positions
P0=Vector((0,-0.014,0.787)); D=Vector((0,0.5,0.866)).normalized()
arm.data.pose_position='REST'; bpy.context.view_layer.update()
anchor("ND_Anchor_Placenta","Hips",P0+D*0.11)
anchor("ND_Anchor_Voice","Head",Vector((0,-0.095,1.445)))
anchor("ND_Anchor_SkinToSkin","Chest",Vector((0,-0.215,1.12)))
anchor("ND_Anchor_FundusTop","Spine",Vector((0,-0.17,1.06)))
for k,(bn,pos) in mother_markers().items(): anchor("ND_Z_"+k,bn,Vector(pos))
bpy.context.view_layer.update(); arm.data.pose_position='POSE'
for o in bpy.data.objects:
    if o.type=='MESH':
        if o.animation_data: o.animation_data_clear()
        if o.data.shape_keys and o.data.shape_keys.animation_data: o.data.shape_keys.animation_data_clear()
objs=[arm]+[o for o in bpy.data.objects if o.parent==arm]
export(OUT+"ND_Mother.fbx",objs)
# ---------- BABY
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/b4_baked.blend")
b=bpy.data.objects["B_Body"]; b.data.materials[0].name="ND_BabySkin"
barm=bpy.data.objects["ND_BabyRig"]
barm.data.pose_position='REST'; bpy.context.view_layer.update()
BM,binfo=baby_markers(); markers=[]
for k,(bn,pos) in BM.items():
    e=bpy.data.objects.new("ND_Z_"+k,None); bpy.context.scene.collection.objects.link(e); e.empty_display_size=0.01
    e.parent=barm; e.parent_type='BONE'; e.parent_bone=bn; e.matrix_world=Matrix.Translation(Vector(pos)); markers.append(e)
bpy.context.view_layer.update(); barm.data.pose_position='POSE'
json.dump(binfo,open(ND+"/blend/baby_info.json","w")); print("baby info",binfo)
export(OUT+"ND_Baby.fbx",[barm,b]+markers)
# ---------- PROPS
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/p1_props.blend")
for root in ("ND_DeliveryBed","ND_CordClamp","ND_CordScissors","ND_Towel","ND_DeliveryTray"):
    r=bpy.data.objects[root]; r.location=(0,0,0)
    bpy.context.view_layer.update()
    export(OUT+root+".fbx",[r]+list(r.children_recursive),actions=False)
