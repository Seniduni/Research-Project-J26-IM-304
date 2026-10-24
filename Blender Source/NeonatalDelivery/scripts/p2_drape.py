import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, bmesh, sys, math, json, numpy as np
sys.path.insert(0,ND+"/scripts")
from mathutils import Vector, Matrix, kdtree
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m4_baked.blend")
sc=bpy.context.scene
arm=bpy.data.objects["ND_MotherRig"]; body=bpy.data.objects["M_Body"]
arm.animation_data.action=bpy.data.actions["ND_M_Base"]; sc.frame_set(0)
with bpy.data.libraries.load(ND+"/blend/p1_props.blend") as (src,dst): dst.objects=[n for n in src.objects if n.startswith("ND_DeliveryBed")]
for o in dst.objects: sc.collection.objects.link(o)
bed=bpy.data.objects["ND_DeliveryBed_Mesh"]
# collision proxy of the posed mother
dg=bpy.context.evaluated_depsgraph_get()
me=bpy.data.meshes.new_from_object(body.evaluated_get(dg))
proxy=bpy.data.objects.new("CollProxy",me); sc.collection.objects.link(proxy)
d=proxy.modifiers.new("dec",'DECIMATE'); d.ratio=0.35
bpy.context.view_layer.objects.active=proxy; bpy.ops.object.modifier_apply(modifier="dec")
for o in (proxy,bed):
    m=o.modifiers.new("Collision",'COLLISION'); o.collision.thickness_outer=0.006; o.collision.cloth_friction=8
legs=json.load(open(ND+"/blend/legs.json"))
kz=legs["LeftKnee"][2]
# cloth sheet
bpy.ops.mesh.primitive_grid_add(x_subdivisions=58,y_subdivisions=42,size=1,location=(0,0.24,kz+0.10))
cl=bpy.context.object; cl.name="ND_Drape"; cl.scale=(1.20,0.78,1); bpy.ops.object.transform_apply(scale=True)
pin=cl.vertex_groups.new(name="pin")
for v in cl.data.vertices:
    p=cl.matrix_world@v.co
    if p.y<0.24-0.39+0.012 and abs(p.x)<0.33: pin.add([v.index],1.0,'REPLACE')
cm=cl.modifiers.new("Cloth",'CLOTH'); s=cm.settings
s.quality=7; s.mass=0.12; s.tension_stiffness=12; s.compression_stiffness=12; s.shear_stiffness=6; s.bending_stiffness=0.3
s.air_damping=1.5; s.vertex_group_mass="pin"; s.pin_stiffness=1.0
cm.collision_settings.distance_min=0.006; cm.collision_settings.use_self_collision=False; cm.collision_settings.collision_quality=3
cm.point_cache.frame_start=1; cm.point_cache.frame_end=70
# move pinned edge to knee tent line
for v in cl.data.vertices:
    p=cl.matrix_world@v.co
    if p.y<0.24-0.39+0.012 and abs(p.x)<0.33: v.co.z=kz-0.005-cl.location.z+0.0
for f in range(1,71): sc.frame_set(f)
bpy.context.view_layer.objects.active=cl; bpy.ops.object.modifier_apply(modifier="Cloth")
cl.vertex_groups.remove(cl.vertex_groups["pin"])
sol=cl.modifiers.new("sol",'SOLIDIFY'); sol.thickness=0.002; bpy.ops.object.modifier_apply(modifier="sol")
bpy.ops.object.shade_smooth()
for uv in cl.data.uv_layers:
    for l in uv.data: l.uv=(l.uv[0]*3.0,l.uv[1]*2.0)
cl.data.transform(cl.matrix_world); cl.matrix_world=Matrix.Identity(4)
# material: reuse the user's green duck cloth
src=bpy.data.objects.get("ND_GreenCloth_Src")
cl.data.materials.clear(); cl.data.materials.append(src.data.materials[0] if src else bpy.data.materials.new("Green Duck Cloth"))
# skin: copy weights from nearest posed body vertex, then inverse-skin to bind pose
P=np.array([v.co[:] for v in bpy.data.objects["CollProxy"].data.vertices])  # not used for weights (decimated)
evb=body.evaluated_get(dg).to_mesh(); PB=[ (body.matrix_world@v.co) for v in evb.vertices]; body.evaluated_get(dg).to_mesh_clear()
kd=kdtree.KDTree(len(PB))
for i,p in enumerate(PB): kd.insert(p,i)
kd.balance()
names={g.index:g.name for g in body.vertex_groups}
groups={}
mats={pb.name:(pb.matrix@pb.bone.matrix_local.inverted()) for pb in arm.pose.bones}
for v in cl.data.vertices:
    nb=kd.find_n(v.co,4); W={}
    for co,i,dist in nb:
        w0=1.0/(dist+0.01)
        for g in body.data.vertices[i].groups:
            nm=names[g.group]
            if nm.startswith(("Introitus","Canal")): nm="Hips"
            W[nm]=W.get(nm,0)+g.weight*w0
    s_=sum(W.values()); W={k:w/s_ for k,w in sorted(W.items(),key=lambda x:-x[1])[:4]}; s_=sum(W.values())
    B=Matrix(((0,0,0,0),)*4)
    for nm,w in W.items():
        if nm not in groups: groups[nm]=cl.vertex_groups.new(name=nm)
        groups[nm].add([v.index],w/s_,'REPLACE')
        B=B+mats[nm]*(w/s_)
    v.co=B.inverted()@v.co
cl.parent=arm; mm=cl.modifiers.new("Armature",'ARMATURE'); mm.object=arm
for o in ("CollProxy",): bpy.data.objects.remove(bpy.data.objects[o])
for o in [o for o in bpy.data.objects if o.name.startswith("ND_DeliveryBed")]: bpy.data.objects.remove(o)
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m5_drape.blend")
print("drape verts",len(cl.data.vertices))
