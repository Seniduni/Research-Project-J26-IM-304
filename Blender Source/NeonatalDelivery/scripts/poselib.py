# Shared posing helpers: aim bones at world (armature-space) directions, chain-safe.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, math
from mathutils import Vector, Matrix, Quaternion
SEAT=0.92          # mattress top (bed space)
def n(v): v=Vector(v); return v.normalized()
def reset(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode='QUATERNION'; pb.rotation_quaternion=(1,0,0,0); pb.location=(0,0,0); pb.scale=(1,1,1)
    bpy.context.view_layer.update()
def aim(arm,name,d,head=None,twist=0.0,up=None):
    """Point bone 'name' along armature-space direction d. Optional head position (for unconnected roots)."""
    pb=arm.pose.bones[name]; b=pb.bone
    R0=b.matrix_local.to_3x3()
    if pb.parent is None:
        par=Matrix.Identity(4)
    else:
        par=pb.parent.matrix @ pb.parent.bone.matrix_local.inverted() @ b.matrix_local
    cur_head=par.to_translation() if head is None else Vector(head)
    # rotation that the parent already applies to this bone's rest frame
    Rp=par.to_3x3().normalized()
    y=Rp.col[1].normalized()
    q=y.rotation_difference(n(d))
    R=q.to_matrix()@Rp
    if up is not None:
        # roll so that bone local Z aligns as close as possible to 'up'
        dd=n(d); z=R.col[2]; upv=n(up); upv=(upv-dd*upv.dot(dd)).normalized()
        zz=(z-dd*z.dot(dd)).normalized()
        ang=zz.angle(upv); s=1 if zz.cross(upv).dot(dd)>0 else -1
        R=Quaternion(dd,s*ang).to_matrix()@R
    if twist: R=Quaternion(n(d),math.radians(twist)).to_matrix()@R
    M=R.to_4x4(); M.translation=cur_head
    pb.matrix=M
    bpy.context.view_layer.update()
def curl(arm,name,deg,axis='X'):
    pb=arm.pose.bones[name]; pb.rotation_mode='QUATERNION'
    ax={'X':Vector((1,0,0)),'Y':Vector((0,1,0)),'Z':Vector((0,0,1))}[axis]
    pb.rotation_quaternion=Quaternion(ax,math.radians(deg))@pb.rotation_quaternion
    bpy.context.view_layer.update()
FINGERS=['Index','Middle','Ring','Little']
def grip(arm,side,amount=1.0,spread=0.0):
    for f in FINGERS:
        for seg,a in (('Proximal',55),('Intermediate',75),('Distal',45)):
            curl(arm,side+f+seg,a*amount)
    curl(arm,side+'ThumbIntermediate',25*amount); curl(arm,side+'ThumbDistal',35*amount)

HINGE=0.14         # backrest hinge distance from the seat's foot edge (bed space)
def ik2(arm,upper,lower,target,pole,end=None,end_dir=None,end_up=None):
    """Analytic two-bone IK: place 'lower' bone tail at target, bending toward pole direction."""
    pu=arm.pose.bones[upper]; pl=arm.pose.bones[lower]
    a=(pu.bone.tail_local-pu.bone.head_local).length; b=(pl.bone.tail_local-pl.bone.head_local).length
    # current posed head of upper (after parents)
    par=pu.parent.matrix @ pu.parent.bone.matrix_local.inverted() @ pu.bone.matrix_local
    S=par.to_translation(); T=Vector(target)
    d=(T-S); dist=min(d.length,(a+b)*0.999); dist=max(dist,abs(a-b)*1.01); dn=d.normalized()
    ca=(a*a+dist*dist-b*b)/(2*a*dist); ca=max(-1,min(1,ca)); sa=math.sqrt(1-ca*ca)
    p=n(pole); p=(p-dn*p.dot(dn)).normalized()
    E=S+a*(ca*dn+sa*p)
    aim(arm,upper,(E-S).normalized(),up=p)
    aim(arm,lower,(S+dn*dist-E).normalized(),up=p)
    if end and end_dir is not None: aim(arm,end,end_dir,up=end_up)
def bone_head(arm,name):
    return arm.pose.bones[name].head.copy()
def lithotomy(arm,cfg=None):
    """Semi-recumbent lithotomy delivery pose in bed space (seat foot edge at y=0, head toward +Y, trainee stands at -Y)."""
    c=dict(trunk=45,hipz=SEAT+0.095,hipy=0.115,abd=34,thighflex=72,shin=(0.38,-0.86,-0.22),
           head=(0,0.40,0.92),armpose='handles',curlspine=0.0,chin=0.0,grip=0.85,breath=0.0,tilt=24.0,hinge=None)
    if cfg: c.update(cfg)
    reset(arm)
    t=math.radians(c['trunk'])
    tr=Vector((0,math.cos(t),math.sin(t)))
    up_dir=Vector((0,-math.sin(t),math.cos(t)))      # body-front direction
    cs=math.radians(c['curlspine'])
    aim(arm,'Root',(0,1,0))
    pt=math.radians(c['tilt'])
    hdir=(tr*math.cos(pt)-up_dir*math.sin(pt)).normalized()          # posterior pelvic tilt
    aim(arm,'Hips',hdir,head=(0,c['hipy'],c['hipz']),up=(up_dir*math.cos(pt)+tr*math.sin(pt)))
    for i,bn in enumerate(['Spine','Chest','UpperChest']):
        k=(i+1)/3.0
        d=(tr*math.cos(cs*k)+up_dir*math.sin(cs*k)).normalized()
        aim(arm,bn,d,up=up_dir)
    hd=Vector(c['head']).normalized(); ch=math.radians(c['chin'])
    hd=(hd*math.cos(ch)+up_dir*math.sin(ch)).normalized()
    aim(arm,'Neck',(tr*0.6+hd*0.4).normalized(),up=up_dir); aim(arm,'Head',hd,up=up_dir)
    ab=math.radians(c['abd']); fl=math.radians(c['thighflex'])
    out={}
    for s,sg in (('Left',1),('Right',-1)):
        th=(tr*math.cos(fl)+up_dir*math.sin(fl)).normalized()
        th=(th*math.cos(ab)+Vector((sg,0,0))*math.sin(ab)).normalized()
        aim(arm,s+'UpperLeg',th,up=Vector((0,-1,0)))
        sh=Vector((sg*c['shin'][0],c['shin'][1],c['shin'][2])).normalized()
        aim(arm,s+'LowerLeg',sh,up=Vector((0,0,1)))
        ft=(Vector((sg*0.20,-0.25,0.95))).normalized()
        aim(arm,s+'Foot',ft,up=-sh)
        aim(arm,s+'Toes',(ft*0.6+Vector((0,-0.8,0.0))).normalized(),up=-sh)
        knee=bone_head(arm,s+'LowerLeg'); out[s+'Knee']=knee; out[s+'Ankle']=bone_head(arm,s+'Foot')
    arms=c.get('arms') or {c['armpose']:1.0}
    tot=sum(arms.values())
    for s,sg in (('Left',1),('Right',-1)):
        aim(arm,s+'Shoulder',(Vector((sg,0,0))+tr*0.12+up_dir*0.05*arms.get('knees',0)).normalized(),up=up_dir)
        T=Vector(); Pl=Vector(); E=Vector(); Eu=Vector()
        hg=c['hinge'] if c['hinge'] is not None else HINGE
        for mode,w in arms.items():
            w/=tot
            if mode=='handles':
                tgt=Vector((sg*0.40,hg+0.10,SEAT+0.13)); pole=Vector((sg*1.0,0.6,-0.2)); ed=Vector((sg*0.05,-0.95,-0.25)); eu=Vector((sg,0,0.5))
            elif mode=='knees':
                k=out[s+'Knee']; thd=(k-bone_head(arm,s+'UpperLeg')).normalized()
                tgt=k-thd*0.10+Vector((0,0.0,-0.035))+Vector((-sg*0.02,0,0)); pole=Vector((sg*1.0,0.2,0.6))
                ed=(thd*0.3+Vector((0,0,-1))+Vector((-sg*0.4,0,0))).normalized(); eu=Vector((-sg,0,0))
            elif mode=='cradle':
                chest=bone_head(arm,'UpperChest')+up_dir*0.16-tr*0.12
                tgt=chest+Vector((sg*0.07,-0.02*sg,0))+tr*(0.06 if sg>0 else -0.08); pole=Vector((sg*1.0,-0.3,-0.3))
                ed=(Vector((-sg,0.2*sg,0))+up_dir*0.2).normalized(); eu=-up_dir
            elif mode=='reach':
                tgt=Vector((sg*0.10,-0.05,SEAT+0.42)); pole=Vector((sg*1.0,0.3,0.0)); ed=Vector((-sg*0.2,-0.6,-0.75)).normalized(); eu=Vector((-sg,0,0))
            T+=tgt*w; Pl+=pole*w; E+=ed*w; Eu+=eu*w
        ik2(arm,s+'UpperArm',s+'LowerArm',T,Pl,s+'Hand',E.normalized(),Eu.normalized())
        grip(arm,s,c['grip'])
    if c['breath']:
        for bn,sc_ in (('Chest',1.0),('UpperChest',0.6)):
            pb=arm.pose.bones[bn]; pb.scale=(1+0.02*c['breath']*sc_,1,1+0.03*c['breath']*sc_)
    return out

PER_Y=-0.03       # perineum overhangs the seat edge by 3 cm ("buttocks to the edge of the bed")
def settle(arm,body,cfg_apply,iters=4,back_angle=45.0,base=None):
    """Place the pelvis so the buttocks rest on the seat with the perineum just beyond the edge,
    then fit the backrest hinge so the back rests on it. Returns cfg incl. 'hinge'."""
    import numpy as np
    cfg=dict(base or {})
    cfg.setdefault('hipz',SEAT+0.11); cfg.setdefault('hipy',0.10)
    t=math.radians(back_angle); s_,c_=math.sin(t),math.cos(t)
    for it in range(iters):
        cfg_apply(cfg)
        per=arm.pose.bones["Perineum"].head
        dg=bpy.context.evaluated_depsgraph_get(); ev=body.evaluated_get(dg); me=ev.to_mesh()
        P=np.array([v.co[:] for v in me.vertices]); ev.to_mesh_clear()
        seatpts=P[(P[:,1]>0.005)&(P[:,1]<0.22)&(np.abs(P[:,0])<0.2)]
        cfg['hipz']+= (SEAT-0.006)-seatpts[:,2].min()
        cfg['hipy']+= PER_Y-per.y
    bk=P[(P[:,2]>SEAT+0.12)&(np.abs(P[:,0])<0.16)&(P[:,1]>0.1)]
    h=np.max(bk[:,1]-(c_/s_)*(bk[:,2]-SEAT))+0.004/s_
    cfg['hinge']=float(h)
    cfg_apply(cfg)
    return cfg
def dilate(arm,radius,bulge=0.0,canal=None):
    """Open the introitus to 'radius' (m). bulge pushes the posterior perineum outward (crowning)."""
    per=arm.pose.bones["Perineum"]
    Mp=per.matrix; P0=Mp.translation; R=Mp.to_3x3().normalized(); D=R.col[1]
    Rrest=per.bone.matrix_local.to_3x3().normalized()
    for pb in arm.pose.bones:
        if not pb.name.startswith(("Introitus_","CanalA_","CanalB_")): continue
        pb.location=(0,0,0); 
    bpy.context.view_layer.update()
    for pb in arm.pose.bones:
        nm=pb.name
        if not nm.startswith(("Introitus_","CanalA_","CanalB_")): continue
        rad=radius if nm.startswith("Introitus") else (canal if canal is not None else radius*0.9)
        h=pb.head.copy(); q=h-P0; ax=D*q.dot(D); r=q-ax; cur=r.length; rd=r.normalized()
        # posterior factor: rest offset direction relative to Perineum local -Z (anterior axis)
        post=max(0,-rd.dot(R.col[2]))
        tgt=P0+ax+rd*max(cur,rad*(1.0+0.12*post)) - D*bulge*(0.4+0.6*post)*(1 if nm.startswith("Introitus") else 0.3)
        M=pb.matrix.copy(); M.translation=tgt; pb.matrix=M
    bpy.context.view_layer.update()
