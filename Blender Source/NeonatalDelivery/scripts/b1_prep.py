import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np
from mathutils import Matrix, Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=ND+"/input/Baby_model.glb")
o=bpy.data.objects["Mesh_0"]; o.name="B_Body"; o.data.name="B_Body"
o.data.transform(o.matrix_world); o.matrix_world=Matrix.Identity(4)
V=np.array([v.co[:] for v in o.data.vertices]); zmin,zmax=V[:,2].min(),V[:,2].max()
S=0.50/(zmax-zmin)
o.data.transform(Matrix.Scale(S,4)@Matrix.Translation(Vector((0,0,-zmin))))
V=np.array([v.co[:] for v in o.data.vertices]); o.data.transform(Matrix.Translation(Vector((-(V[:,0].min()+V[:,0].max())/2,-V[V[:,2]>0.3][:,1].mean(),0))))
print("scale",S)
# decimate for Quest
m=o.modifiers.new("dec",'DECIMATE'); m.ratio=0.16; m.use_collapse_triangulate=True
bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier="dec")
bpy.ops.object.shade_smooth()
print("verts after decimate",len(o.data.vertices),"faces",len(o.data.polygons))
V=np.array([v.co[:] for v in o.data.vertices])
H=V[:,2].max(); print("height",H)
for z in np.arange(0.0,0.5,0.02):
    s=V[np.abs(V[:,2]-z)<0.006]
    mid=s[np.abs(s[:,0])<0.01]
    print("z=%.2f xr=[%.3f,%.3f] yr=[%.3f,%.3f] nmid=%d"%(z,s[:,0].min(),s[:,0].max(),s[:,1].min(),s[:,1].max(),len(mid)))
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/b1_prep.blend")
