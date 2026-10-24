# Stage 1: import mother GLB, clean, scale to 1.62 m, merge seams, name objects, classify face morphs.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, bmesh, numpy as np
from mathutils import Vector, Matrix
bpy.ops.wm.read_factory_settings(use_empty=True)
_src=ND+"/input/LadyWITH_CLOTHING_3_new.glb"
if not _os.path.exists(_src): _src=ND+"/input/LadyWITH_CLOTHING_3.glb"
bpy.ops.import_scene.gltf(filepath=_src)
for _m in bpy.data.materials:   # the v2 export suffixes every material with .001
    if _m.name.endswith(".001") and not bpy.data.materials.get(_m.name[:-4]): _m.name=_m.name[:-4]
names={"1":"M_Body","0":"M_Tongue","5":"M_Nails","6":"M_Eyelash","7":"M_Eye_R","9":"M_Eye_L","11":"M_TeethUpper","12":"M_TeethLower","Object_30":"M_HairCap","Object_31":"M_Hair","Plane":"ND_GreenCloth_Src"}
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for o in list(bpy.data.objects):
    if o.type!='MESH': bpy.data.objects.remove(o); continue
    if o.animation_data: o.animation_data_clear()
    if o.data.shape_keys and o.data.shape_keys.animation_data: o.data.shape_keys.animation_data_clear()
    o.name=names.get(o.name,o.name); o.data.name=o.name
# bake world transforms into mesh data (incl. shape keys)
for o in bpy.data.objects:
    mw=o.matrix_world.copy(); o.parent=None
    o.data.transform(mw, shape_keys=True); o.matrix_world=Matrix.Identity(4)
body=bpy.data.objects["M_Body"]
co=np.array([v.co[:] for v in body.data.vertices])
zmin,zmax=co[:,2].min(),co[:,2].max()
S=1.62/(zmax-zmin)
# centre: pelvis midpoint x=0, y = mean of body at hip height
T=Matrix.Translation(Vector((0,0,-zmin)))
M=Matrix.Scale(S,4)@T
for o in bpy.data.objects:
    o.data.transform(M, shape_keys=True)
co=np.array([v.co[:] for v in body.data.vertices]); 
cx=0.5*(co[:,0].min()+co[:,0].max()); 
sl=co[(co[:,2]>0.85)&(co[:,2]<0.95)]; cy=sl[:,1].mean()
M2=Matrix.Translation(Vector((-cx,-cy,0)))
for o in bpy.data.objects: o.data.transform(M2, shape_keys=True); o.data.update()
print("scale",S,"height",1.62)
# merge coincident seam verts on body/eyelash/nails (material-split seams)
for n in ["M_Body"]:
    o=bpy.data.objects[n]; bm=bmesh.new(); bm.from_mesh(o.data)
    before=len(bm.verts); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00008); 
    print(n,"merged",before-len(bm.verts)); bm.to_mesh(o.data); bm.free(); o.data.update()
# ---- classify face morph targets on body by displacement region
kb=body.data.shape_keys.key_blocks; basis=np.array([v.co[:] for v in kb[0].data])
eyeL=np.array(bpy.data.objects["M_Eye_L"].data.vertices[0].co[:]); 
eL=np.mean([v.co[:] for v in bpy.data.objects["M_Eye_L"].data.vertices],axis=0)
eR=np.mean([v.co[:] for v in bpy.data.objects["M_Eye_R"].data.vertices],axis=0)
print("eyeL",eL,"eyeR",eR)
info=[]
for i,k in enumerate(kb):
    if i==0: continue
    d=np.array([v.co[:] for v in k.data])-basis
    m=np.linalg.norm(d,axis=1)
    if m.max()<1e-5: info.append((k.name,0,None,None)); continue
    w=m/m.sum(); c=(basis*w[:,None]).sum(0); md=(d*w[:,None]).sum(0)
    info.append((k.name,m.max(),c,md))
import json
out=[]
for n,mx,c,md in info:
    if c is None: out.append({"name":n,"max":0}); continue
    out.append({"name":n,"max":float(mx),"c":[float(x) for x in c],"d":[float(x) for x in md]})
json.dump({"eyeL":eL.tolist(),"eyeR":eR.tolist(),"keys":out},open(ND+"/blend/morph_info.json","w"),indent=0)
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m1_prep.blend")
print("saved")
