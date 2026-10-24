import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np, math, json
from mathutils import Vector, kdtree, Matrix
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m2_arm.blend")
arm=bpy.data.objects["ND_MotherRig"]; body=bpy.data.objects["M_Body"]
J={k:Vector(v) for k,v in json.load(open(ND+"/blend/joints.json")).items()}
custom=["Jaw","Belly","LeftEye","RightEye"]
for b in arm.data.bones:
    if b.name in custom: b.use_deform=False
SC=10.0
for o in (arm,body): o.scale=(SC,SC,SC)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
body.parent=None
for o in (arm,body): o.scale=(1,1,1)
body.matrix_world=Matrix.Identity(4)
body.parent=arm
bpy.context.view_layer.update()
for b in arm.data.bones:
    if b.name in custom: b.use_deform=True
# check unweighted
me=body.data
def wsum(v): return sum(g.weight for g in v.groups)
bad=[v.index for v in me.vertices if wsum(v)<1e-4]
print("unweighted after heat:",len(bad))
V=np.array([v.co[:] for v in me.vertices])
vg={g.name:g for g in body.vertex_groups}
def getw():
    W={}
    for v in me.vertices:
        W[v.index]={body.vertex_groups[g.group].name:g.weight for g in v.groups if g.weight>1e-5}
    return W
W=getw()
# fill unweighted via nearest weighted vertex
kd=kdtree.KDTree(len(V)); good=[i for i in range(len(V)) if W[i]]
kd2=kdtree.KDTree(len(good))
for j,i in enumerate(good): kd2.insert(V[i],j)
kd2.balance()
for i in bad:
    co,j,d=kd2.find(V[i]); W[i]=dict(W[good[j]])
def blend_in(i,bone,w):
    d=W[i]; 
    for k in d: d[k]*= (1-w)
    d[bone]=d.get(bone,0)+w
# Jaw: lower face region below mouth line, in front of jaw pivot
jaw=J['Jaw']; chin=J['JawEnd']
for i,p in enumerate(V):
    if p[2]>1.47 or p[2]<1.36 or p[1]>0.03: continue
    if abs(p[0])>0.075: continue
    # mouth line ~1.442 at front; jaw region below lips
    zl=1.440 - 0.25*max(0,p[1]+0.09)    # slopes up toward back
    t=(zl-p[2])/0.012
    if t<=0: continue
    w=min(1,t)
    w*=min(1,max(0,(0.075-abs(p[0]))/0.02))        # fade toward ear width
    w*=min(1,max(0,(p[2]-1.36)/0.025))              # fade into neck
    w*=min(1,max(0,(0.03-p[1])/0.04))               # fade toward back
    if w>0.01: blend_in(i,"Jaw",w*0.95)
# Belly: anterior abdomen bump, smooth radial falloff
bc=Vector((0,-0.12,0.955))
for i,p in enumerate(V):
    if p[1]>-0.02 or p[2]<0.78 or p[2]>1.12: continue
    d=(Vector(p)-bc); d.y*=0.6
    r=d.length
    w=max(0,1-r/0.17); w=w*w*(3-2*w)
    if p[2]<0.8: w*=max(0,(p[2]-0.78)/0.02)
    if w>0.01: blend_in(i,"Belly",w*0.85)
# write back (limit 4 influences, normalise)
for g in list(body.vertex_groups): body.vertex_groups.remove(g)
groups={}
for b in arm.data.bones:
    if b.use_deform: groups[b.name]=body.vertex_groups.new(name=b.name)
for i,d in W.items():
    items=sorted(d.items(),key=lambda x:-x[1])[:4]; s=sum(w for _,w in items)
    for n,w in items:
        if n in groups and w/s>0.002: groups[n].add([i],w/s,'REPLACE')
# rigid parts
def rigid(obj,bone):
    o=bpy.data.objects[obj]; o.parent=arm
    for g in list(o.vertex_groups): o.vertex_groups.remove(g)
    g=o.vertex_groups.new(name=bone); g.add([v.index for v in o.data.vertices],1.0,'REPLACE')
    m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
for o,b in [("M_Hair","Head"),("M_HairCap","Head"),("M_TeethUpper","Head"),("M_TeethLower","Jaw"),("M_Tongue","Jaw"),("M_Eye_L","LeftEye"),("M_Eye_R","RightEye"),("M_Eyelash","Head")]:
    rigid(o,b)
# nails: copy from nearest body vertex
kdb=kdtree.KDTree(len(V))
for i,p in enumerate(V): kdb.insert(p,i)
kdb.balance()
o=bpy.data.objects["M_Nails"]; o.parent=arm
for g in list(o.vertex_groups): o.vertex_groups.remove(g)
ng={}
for v in o.data.vertices:
    co,j,d=kdb.find(v.co)
    for g in me.vertices[j].groups:
        n=body.vertex_groups[g.group].name
        if n not in ng: ng[n]=o.vertex_groups.new(name=n)
        ng[n].add([v.index],g.weight,'REPLACE')
m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
print("unweighted final:",sum(1 for v in me.vertices if wsum(v)<1e-4))
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m2_skinned.blend")
