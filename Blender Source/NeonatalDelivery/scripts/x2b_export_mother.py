import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, sys, math, json, os
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
EMBED=True
def export(path,objs,actions=True):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},use_mesh_modifiers=False,
        add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,
        bake_anim=actions,bake_anim_use_all_actions=actions,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,bake_anim_simplify_factor=0.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',axis_up='Y',path_mode=('COPY' if EMBED else 'STRIP'),mesh_smooth_type='FACE',use_tspace=True,embed_textures=EMBED,
        use_custom_props=False,bake_space_transform=False)
    print("exported",path)
# ---------- MOTHER
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m5b_drape.blend")
sc=bpy.context.scene
arm=bpy.data.objects["ND_MotherRig"]
if bpy.data.objects.get("ND_GreenCloth_Src"): bpy.data.objects.remove(bpy.data.objects["ND_GreenCloth_Src"])
# v2: point every image node at the Unity texture files and EMBED them in the FBX, so the imported FBX materials are
# textured even before the module's own materials are remapped onto them
MAN=json.load(open(ND_OUT+"/Art/ND_MaterialManifest.json")); ART=ND_OUT+"/Art/"
def kind_of(node,nt):
    for l in nt.links:
        if l.from_node==node:
            if l.to_node.type=='NORMAL_MAP': return "normal"
            if l.to_node.type=='BSDF_PRINCIPLED' and l.to_socket.name=="Base Color": return "baseColor"
            if l.to_node.type=='BSDF_PRINCIPLED' and l.to_socket.name=="Alpha": return "baseColor"
            if l.to_node.type in('SEPARATE_COLOR','SEPRGB','SEPARATE_RGB'): return "metalRough"
            if l.to_node.type=='BSDF_PRINCIPLED': return "metalRough"
    return None
for m in bpy.data.materials:
    e=MAN.get(m.name)
    if not e or not m.node_tree: continue
    for n in m.node_tree.nodes:
        if n.type!='TEX_IMAGE': continue
        k=kind_of(n,m.node_tree); rel=e.get(k) if k else None
        if rel and os.path.exists(ART+rel):
            img=bpy.data.images.load(ART+rel,check_existing=True)
            if k!="baseColor": img.colorspace_settings.name="Non-Color"
            n.image=img
    # base colour must feed the Principled BSDF directly (glTF import routes it through a vertex-colour multiply,
    # which the FBX exporter cannot write) -> add a direct image node
    bs=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bs is not None and e.get("baseColor") and os.path.exists(ART+e["baseColor"]):
        n=m.node_tree.nodes.new("ShaderNodeTexImage"); n.image=bpy.data.images.load(ART+e["baseColor"],check_existing=True)
        m.node_tree.links.new(n.outputs["Color"],bs.inputs["Base Color"])
        if e.get("baseHasAlpha"): m.node_tree.links.new(n.outputs["Alpha"],bs.inputs["Alpha"])
    print("textures linked for",m.name)
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
