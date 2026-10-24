import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np, json, math
from mathutils import Vector, Matrix
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/b1_prep.blend")
o=bpy.data.objects["B_Body"]; me=o.data
LEN=0.47
o.data.transform(Matrix.Scale(LEN/0.5,4)); me.update()
V=np.array([v.co[:] for v in me.vertices]); k=LEN/0.5
def cen(mask): q=V[mask]; return Vector(q.mean(0))
def zs(z,h=0.006): return np.abs(V[:,2]-z*k)<h*k
J={}
crotch=0.158*k
for s,sg in (('Left',1),('Right',-1)):
    side=V[:,0]*sg>0.005*k
    th=cen(zs(0.13)&side); J[s+'UpperLeg']=Vector((th.x,th.y-0.002,0.175*k))
    kn=cen(zs(0.095)&side); J[s+'LowerLeg']=Vector((kn.x,kn.y-0.004,0.095*k))
    an=cen(zs(0.035)&side); J[s+'Foot']=Vector((an.x,an.y+0.004,0.032*k))
    ft=V[side&(V[:,2]<0.03*k)]; tip=ft[np.argmin(ft[:,1])]
    J[s+'Toes']=Vector((an.x*0.9+tip[0]*0.1,tip[1]*0.55+an.y*0.45,0.014*k)); J[s+'ToesEnd']=Vector((tip[0],tip[1],0.01*k))
    # arms along x in T-pose
    def xs(x,h=0.006): return (np.abs(V[:,0]-sg*x*k)<h*k)&(V[:,2]>0.29*k)&(V[:,2]<0.37*k)
    sh=cen(xs(0.072)); J[s+'UpperArm']=Vector((sg*0.068*k,sh.y+0.004,sh.z-0.002))
    el=cen(xs(0.135)); J[s+'LowerArm']=el
    wr=cen(xs(0.183)); J[s+'Hand']=wr
    hd=V[(V[:,0]*sg>0.19*k)]; tipx=hd[np.argmax(np.abs(hd[:,0]))]
    J[s+'HandEnd']=Vector(tipx)
    J[s+'Shoulder']=Vector((sg*0.012*k,sh.y+0.006,0.338*k))
    # fingers (single curl bone) + thumb
    J[s+'Fingers']=wr.lerp(Vector(tipx),0.55); J[s+'FingersEnd']=Vector(tipx)
    th_=V[(V[:,0]*sg>0.185*k)&(V[:,0]*sg<0.215*k)]; thumb=th_[np.argmin(th_[:,1])]
    J[s+'Thumb']=wr.lerp(Vector(thumb),0.35); J[s+'ThumbEnd']=Vector(thumb)
torso=lambda z: cen(zs(z)&(np.abs(V[:,0])<0.04*k))
J['Hips']=Vector((0,torso(0.19).y+0.008,0.185*k))
J['Spine']=Vector((0,torso(0.235).y+0.012,0.235*k))
J['Chest']=Vector((0,torso(0.285).y+0.012,0.285*k))
nk=cen(zs(0.372)); J['Neck']=Vector((0,nk.y+0.006,0.352*k))
hp=cen(zs(0.40)); J['Head']=Vector((0,hp.y+0.004,0.385*k))
J['HeadTop']=Vector((0,hp.y,V[:,2].max()))
# face: jaw pivot near ear level, chin = most anterior-lower face point
face=V[(np.abs(V[:,0])<0.01*k)&(V[:,2]>0.37*k)&(V[:,2]<0.42*k)]
chin=face[np.argmin(face[:,1]+0.5*(face[:,2]-0.37*k))]
J['Jaw']=Vector((0,hp.y-0.005,0.407*k)); J['JawEnd']=Vector((0,chin[1]+0.006,chin[2]+0.004))
# navel: dip on the anterior abdominal midline
ab=V[(np.abs(V[:,0])<0.014*k)&(V[:,2]>0.19*k)&(V[:,2]<0.28*k)&(V[:,1]<0.0)]
prof=[]
for z in np.arange(0.19,0.28,0.004):
    q=ab[(np.abs(ab[:,2]-z*k)<0.003*k)&(np.abs(ab[:,0])<0.006*k+0.0)]
    if len(q)<2: q=ab[np.abs(ab[:,2]-z*k)<0.003*k]
    if len(q): prof.append((z*k,q[:,1].min()))
prof=np.array(prof); 
# local maximum of y (dent) relative to smoothed neighbours
best=None;bs=-1
for i in range(2,len(prof)-2):
    d=prof[i,1]-0.5*(prof[i-2,1]+prof[i+2,1])
    if d>bs: bs=d; best=prof[i]
J['Navel']=Vector((0,best[1]+0.002,best[0])); J['NavelEnd']=J['Navel']+Vector((0,-0.02,0))
print("navel",J['Navel'],"dent",bs)
arm_d=bpy.data.armatures.new("ND_BabyRig"); arm=bpy.data.objects.new("ND_BabyRig",arm_d)
bpy.context.scene.collection.objects.link(arm); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT'); eb=arm_d.edit_bones
def bone(n,h,t,p=None,c=False,up=Vector((0,-1,0)),deform=True):
    b=eb.new(n); b.head=h; b.tail=t
    if (b.tail-b.head).length<1e-4: b.tail=b.head+Vector((0,0,0.01))
    b.align_roll(up); b.use_deform=deform
    if p: b.parent=eb[p]; b.use_connect=c
    return b
bone("Root",Vector((0,0,0)),Vector((0,0.06,0)),deform=False,up=Vector((0,0,1)))
bone("Hips",J['Hips'],J['Spine'],"Root"); bone("Spine",J['Spine'],J['Chest'],"Hips",True)
bone("Chest",J['Chest'],J['Neck'],"Spine",True); bone("Neck",J['Neck'],J['Head'],"Chest",True)
bone("Head",J['Head'],J['HeadTop'],"Neck",True); bone("Jaw",J['Jaw'],J['JawEnd'],"Head",up=Vector((0,0,1)))
bone("Navel",J['Navel'],J['NavelEnd'],"Hips",deform=False,up=Vector((0,0,1)))
for s,sg in (('Left',1),('Right',-1)):
    bone(s+"Shoulder",J[s+'Shoulder'],J[s+'UpperArm'],"Chest",up=Vector((0,0,1)))
    bone(s+"UpperArm",J[s+'UpperArm'],J[s+'LowerArm'],s+"Shoulder",True,up=Vector((0,0,1)))
    bone(s+"LowerArm",J[s+'LowerArm'],J[s+'Hand'],s+"UpperArm",True,up=Vector((0,0,1)))
    bone(s+"Hand",J[s+'Hand'],J[s+'Fingers'],s+"LowerArm",True,up=Vector((0,0,1)))
    bone(s+"Fingers",J[s+'Fingers'],J[s+'FingersEnd'],s+"Hand",True,up=Vector((0,0,1)))
    bone(s+"Thumb",J[s+'Thumb'],J[s+'ThumbEnd'],s+"Hand",up=Vector((0,0,1)))
    bone(s+"UpperLeg",J[s+'UpperLeg'],J[s+'LowerLeg'],"Hips")
    bone(s+"LowerLeg",J[s+'LowerLeg'],J[s+'Foot'],s+"UpperLeg",True)
    bone(s+"Foot",J[s+'Foot'],J[s+'Toes'],s+"LowerLeg",True,up=Vector((0,0,1)))
    bone(s+"Toes",J[s+'Toes'],J[s+'ToesEnd'],s+"Foot",True,up=Vector((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')
# weights: bone heat at 10x
arm.data.bones["Jaw"].use_deform=False
SC=20.0
for x in (arm,o): x.scale=(SC,SC,SC)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
o.parent=None
for x in (arm,o): x.scale=(1,1,1)
o.matrix_world=Matrix.Identity(4); o.parent=arm; bpy.context.view_layer.update()
arm.data.bones["Jaw"].use_deform=True
bad=[v.index for v in me.vertices if sum(g.weight for g in v.groups)<1e-4]
print("baby unweighted:",len(bad))
# fill unweighted from nearest weighted
from mathutils import kdtree
V=np.array([v.co[:] for v in me.vertices])
good=[i for i in range(len(V)) if i not in set(bad)]
kd=kdtree.KDTree(len(good))
for j,i in enumerate(good): kd.insert(V[i],j)
kd.balance()
for i in bad:
    co,j,d=kd.find(V[i]); src=me.vertices[good[j]]
    for g in src.groups: o.vertex_groups[g.group].add([i],g.weight,'REPLACE')
# jaw weights (lower face)
jg=o.vertex_groups.new(name="Jaw")
mouthz=J['Jaw'].z-0.012
for i,p in enumerate(V):
    if p[1]>J['Jaw'].y+0.005 or abs(p[0])>0.035 or p[2]>mouthz+0.004 or p[2]<J['Neck'].z+0.006: continue
    w=min(1,(mouthz+0.004-p[2])/0.008)*min(1,(0.035-abs(p[0]))/0.012)*min(1,(J['Jaw'].y+0.005-p[1])/0.015)
    if w>0.02:
        v=me.vertices[i]
        for g in v.groups: g.weight*=(1-w*0.9)
        jg.add([i],w*0.9,'REPLACE')
json.dump({kk:list(vv) for kk,vv in J.items()},open(ND+"/blend/baby_joints.json","w"),indent=1)
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/b2_rig.blend")
