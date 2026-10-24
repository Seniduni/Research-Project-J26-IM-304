# Stage 2a: detect anatomical joint landmarks on the mother mesh -> landmarks.json
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np, json
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m1_prep.blend")
body=bpy.data.objects["M_Body"]; me=body.data
V=np.array([v.co[:] for v in me.vertices])
mats=[m.name for m in me.materials]
vm=np.full(len(V),-1)
for p in me.polygons:
    for vi in p.vertices: vm[vi]=p.material_index
MAT={n:i for i,n in enumerate(mats)}
def sl(mask,z,h=0.01): return V[mask & (np.abs(V[:,2]-z)<h)]
L={}
eL=np.mean([v.co[:] for v in bpy.data.objects["M_Eye_L"].data.vertices],0)
eR=np.mean([v.co[:] for v in bpy.data.objects["M_Eye_R"].data.vertices],0)
L['eye_l']=eL.tolist(); L['eye_r']=eR.tolist()
eyez=(eL[2]+eR[2])/2; eyey=(eL[1]+eR[1])/2
allm=np.ones(len(V),bool)
# crotch: lowest z on midline that belongs to torso (between legs)
mid=V[(np.abs(V[:,0])<0.02)&(V[:,2]<1.0)&(V[:,2]>0.5)]
zc=mid[:,2].min(); L['crotch']=[0,float(mid[np.argmin(mid[:,2])][1]),float(zc)]
legm=vm==MAT['Std_Skin_Leg']
for side,sg in (('l',1),('r',-1)):
    m=legm&(V[:,0]*sg>0)
    s=sl(m,0.08,0.012); ank=[s[:,0].mean(), s[:,1].mean()+0.005, 0.078]
    foot=V[m&(V[:,2]<0.045)]
    ytip=foot[:,1].min(); yheel=foot[:,1].max()
    yb=yheel+0.72*(ytip-yheel)
    fb=V[m&(V[:,2]<0.07)&(np.abs(V[:,1]-yb)<0.012)]
    ball=[fb[:,0].mean(), yb, 0.028]
    tipv=foot[np.argmin(foot[:,1])]
    toe=[tipv[0], ytip+0.01, 0.02]
    hz=zc+0.075
    th=V[m&(np.abs(V[:,2]-(zc-0.02))<0.012)]
    hipx=sg*max(0.075,abs(th[:,0].mean())*0.92)
    torso_hip=V[(np.abs(V[:,2]-hz)<0.012)]
    hip=[hipx, th[:,1].mean(), hz]
    kz=ank[2]+0.475*(hz-ank[2])
    ks=sl(m,kz,0.012); knee=[ks[:,0].mean(), ks[:,1].mean()-0.005, kz]
    L['hip_'+side]=list(map(float,hip)); L['knee_'+side]=list(map(float,knee)); L['ankle_'+side]=list(map(float,ank))
    L['ball_'+side]=list(map(float,ball)); L['toe_'+side]=list(map(float,toe)); L['heel_'+side]=[float(ank[0]),float(yheel),0.0]
hz=L['hip_l'][2]; py=(L['hip_l'][1]+L['hip_r'][1])/2
def yback(z):
    s=V[(np.abs(V[:,0])<0.03)&(np.abs(V[:,2]-z)<0.01)]
    return s[:,1].max()
def yfront(z):
    s=V[(np.abs(V[:,0])<0.03)&(np.abs(V[:,2]-z)<0.01)]
    return s[:,1].min()
L['pelvis']=[0,float(py),float(hz+0.015)]
L['spine']=[0,float(yback(hz+0.11)-0.07),float(hz+0.11)]
L['chest']=[0,float(yback(hz+0.25)-0.08),float(hz+0.25)]
L['upperchest']=[0,float(yback(hz+0.38)-0.075),float(hz+0.38)]
nz=eyez-0.135
ns=V[(np.abs(V[:,0])<0.05)&(np.abs(V[:,2]-nz)<0.008)]
L['neck']=[0,float(ns[:,1].mean()+0.008),float(nz)]
hz2=eyez-0.05
hs=V[(np.abs(V[:,0])<0.05)&(np.abs(V[:,2]-hz2)<0.008)]
L['head']=[0,float(hs[:,1].mean()+0.01),float(hz2)]
L['head_top']=[0,float(hs[:,1].mean()),float(V[:,2].max())]
L['jaw']=[0,float(eyey+0.075),float(eyez-0.05)]
face=V[(np.abs(V[:,0])<0.01)&(V[:,2]<eyez-0.08)&(V[:,2]>eyez-0.16)&(V[:,1]<eyey+0.02)]
ch=face[np.argmin(face[:,1]+0.3*face[:,2])] if len(face) else [0,eyey-0.02,eyez-0.12]
L['chin']=[0,float(ch[1]+0.008),float(eyez-0.118)]
# belly: most anterior point of abdomen
ab=V[(np.abs(V[:,0])<0.04)&(V[:,2]>hz)&(V[:,2]<hz+0.35)]
bp=ab[np.argmin(ab[:,1])]; L['belly_front']=bp.tolist()
L['belly']=[0,float(bp[1]+0.11),float(bp[2])]
L['back_at_belly']=[0,float(yback(bp[2])),float(bp[2])]
# arms
armm=vm==MAT['Std_Skin_Arm']
for side,sg in (('l',1),('r',-1)):
    A=V[armm&(V[:,0]*sg>0)]
    c=A.mean(0); u,s_,vt=np.linalg.svd(A-c,full_matrices=False); ax=vt[0]
    if ax[0]*sg<0: ax=-ax
    s=(A-c)@ax; s0=s.min(); s1=s.max()
    def cen(t,h=0.012):
        q=A[np.abs(s-t)<h]; return q.mean(0)
    sh=cen(s0+0.035,0.02); sh[2]-=0.01
    Lr=s1-(s0+0.035)
    el=cen(s0+0.035+0.415*Lr); wr=cen(s0+0.035+0.745*Lr)
    L['shoulder_'+side]=sh.tolist(); L['elbow_'+side]=el.tolist(); L['wrist_'+side]=wr.tolist()
    L['clav_'+side]=[float(sg*0.02),float(L['neck'][1]-0.035),float(sh[2]+0.015)]
    # hand analysis
    sw=s0+0.035+0.745*Lr
    H=A[s>sw]; hs_=(H-wr)@ax
    hc=H.mean(0); _,_,hv=np.linalg.svd(H-hc,full_matrices=False)
    n=hv[2]; a=hv[0] if abs(hv[0]@ax)>abs(hv[1]@ax) else hv[1]
    if a@ax<0: a=-a
    b=np.cross(n,a)
    if b[1]>0: b=-b; n=-n   # b points anterior (-Y): thumb side
    Lh=hs_.max()
    L['hand_'+side]={'a':a.tolist(),'b':b.tolist(),'n':n.tolist(),'len':float(Lh)}
    # finger tips region
    F=H[hs_>0.55*Lh]; fs=(F-wr)@a; fb=(F-wr)@b
    order=np.argsort(fb); fbs=fb[order]
    gaps=np.diff(fbs); idx=np.argsort(gaps)[-4:]; idx.sort()
    groups=np.split(order, idx+1)
    info=[]
    for g in groups:
        P=F[g]; ps=(P-wr)@a
        info.append({'n':len(g),'b':float(((P-wr)@b).mean()),'smax':float(ps.max()),'smin':float(ps.min())})
    L['fingergroups_'+side]=info
    # thumb candidate: hand verts far on +b side relative to palm
    hb=(H-wr)@b
    L['hand_b_range_'+side]=[float(hb.min()),float(hb.max())]
json.dump(L,open(ND+"/blend/landmarks.json","w"),indent=1)
for k,v in L.items(): print(k,v if not isinstance(v,list) or len(v)!=3 else [round(x,3) for x in v])
