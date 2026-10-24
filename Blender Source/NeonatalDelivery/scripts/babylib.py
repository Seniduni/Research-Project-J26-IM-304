# Baby posture + mechanism-of-labour path helpers (baby armature space: up=+Z, face=-Y, left=+X)
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, math, json
from mathutils import Vector, Matrix, Quaternion
import sys; sys.path.insert(0,ND+"/scripts")
from poselib import aim, n, reset
def smooth(a,b,t): t=max(0,min(1,t)); t=t*t*(3-2*t); return a+(b-a)*t
def interp(keys,t):
    if t<=keys[0][0]: return keys[0][1]
    for (t0,v0),(t1,v1) in zip(keys,keys[1:]):
        if t0<=t<=t1: return smooth(v0,v1,(t-t0)/(t1-t0) if t1>t0 else 1)
    return keys[-1][1]
def rot_about(arm,bone,pivot,q):
    pb=arm.pose.bones[bone]; M=pb.matrix.copy()
    T=Matrix.Translation(pivot)@q.to_matrix().to_4x4()@Matrix.Translation(-pivot)
    pb.matrix=T@M; bpy.context.view_layer.update()
def posture(arm,flex=1.0,cry=0.0,phase=0.0,arms_cross=1.0,erb=0.0):
    """flex=1 fetal (in utero), 0 = relaxed newborn (semi-flexed limbs)."""
    reset(arm)
    f=flex; s=math.sin(phase*2*math.pi)
    # spine curl (C-shape)
    for b,a in (('Spine',18*f+4),('Chest',14*f+3)):
        pb=arm.pose.bones[b]; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(a))
    bpy.context.view_layer.update()
    neckflex=25*f+5
    pb=arm.pose.bones['Neck']; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(neckflex*0.5))
    pb=arm.pose.bones['Head']; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(neckflex*0.5))
    bpy.context.view_layer.update()
    for side,sg in (('Left',1),('Right',-1)):
        kick=(0.12*cry*math.sin((phase+0.25*sg)*2*math.pi))
        th=Vector((sg*0.30,-0.75-0.15*f,0.55*f-0.05*(1-f)+kick)).normalized()
        aim(arm,side+'UpperLeg',th)
        sh=Vector((sg*0.10,0.35+0.55*f,-0.75+0.35*f-kick)).normalized()
        aim(arm,side+'LowerLeg',sh)
        aim(arm,side+'Foot',Vector((sg*0.2,-0.85,0.45*f+0.2)).normalized())
        if erb>0 and side=='Right':        # Erb's palsy "waiter's tip": adducted, internally rotated, elbow extended, wrist flexed
            aim(arm,side+'UpperArm',Vector((sg*0.15,0.10,-0.98)).normalized())
            aim(arm,side+'LowerArm',Vector((sg*0.05,0.35,-0.93)).normalized())
            aim(arm,side+'Hand',Vector((-sg*0.2,0.85,-0.45)).normalized())
            continue
        wave=0.25*cry*math.sin((phase+0.5*sg)*2*math.pi)
        ua=Vector((sg*0.45,-0.45-0.2*f,-0.75+0.25*(1-f)+wave)).normalized()
        aim(arm,side+'UpperArm',ua)
        fa=Vector((-sg*0.55*arms_cross*f+sg*0.35*(1-f),-0.55,0.6+0.1*f+wave*0.5)).normalized()
        aim(arm,side+'LowerArm',fa)
        aim(arm,side+'Hand',(fa+Vector((0,-0.2,0.3))).normalized())
        pb=arm.pose.bones[side+'Fingers']; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(-55-20*f+20*cry*max(0,s)))
        pb=arm.pose.bones[side+'Thumb']; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(-25))
        bpy.context.view_layer.update()
    if cry>0:
        pb=arm.pose.bones['Jaw']; pb.rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(cry*(10+12*max(0,s))))
        bpy.context.view_layer.update()
J=json.load(open(ND+"/blend/baby_joints.json"))
def head_center(arm):
    pb=arm.pose.bones['Head']; return pb.head+(pb.tail-pb.head)*0.48+ (pb.matrix.to_3x3()@Vector((0,0,1)))*0.0
def frame_from(up,back):
    z=n(up); y=(back-z*back.dot(z)).normalized(); x=y.cross(z)
    return Matrix((x,y,z)).transposed()      # columns: baby X,Y,Z expressed in anchor space
# ---- mechanism of labour (anchor frame: +Y out of the mother, +Z anterior/pubic side, +X mother's right)
KEYS=dict(
 sN=[(0,-0.165),(0.30,-0.10),(0.45,-0.066),(0.55,-0.030),(0.62,-0.020),(0.70,-0.010),(0.80,0.018),(0.88,0.050),(1.0,0.26)],
 zoff=[(0,0),(0.70,0),(0.80,-0.035),(0.88,0.018),(1.0,0.0)],
 pitch=[(0,22),(0.40,28),(0.46,22),(0.55,-48),(0.62,-12),(0.70,-2),(1.0,0)],
 phead=[(0,45),(0.30,0),(0.55,0),(0.62,45),(0.70,90),(1.0,90)],
 pbody=[(0,45),(0.62,45),(0.70,90),(1.0,90)],
 flex=[(0,1),(0.86,1),(1.0,0.6)],
 latflex=[(0,0),(0.88,0),(1.0,18)],
)
def canal_point(s):
    return Vector((0,s,(3.0*s*s if s<0 else 0.85*s*s)))
def canal_tangent(s):
    return Vector((0,1,(6.0*s if s<0 else 1.7*s))).normalized()
def birth_pose(arm,t,anchor=Matrix.Identity(4)):
    """Pose the baby for normalised delivery progress t (0..1). Returns head centre & chest pos in anchor space."""
    k={kk:interp(v,t) for kk,v in KEYS.items()}
    posture(arm,flex=k['flex'])
    # lateral flexion of the trunk while being born (bend toward anterior)
    if k['latflex']>0.01:
        for b in ('Spine','Chest'):
            pb=arm.pose.bones[b]; pb.rotation_quaternion=Quaternion(Vector((0,0,1)),math.radians(-k['latflex']*0.5))@pb.rotation_quaternion
        bpy.context.view_layer.update()
    # head pitch & twist relative to body (applied at the neck)
    twist=k['phead']-k['pbody']
    neck=arm.pose.bones['Neck']; piv=neck.head.copy()
    bX=Vector((1,0,0)); bZ=(arm.pose.bones['Head'].tail-arm.pose.bones['Neck'].head).normalized()
    rot_about(arm,'Neck',piv,Quaternion(bZ,math.radians(twist))@Quaternion(bX,math.radians((k['pitch']-25*k['flex'])*0.55)))
    hb=arm.pose.bones['Head']
    rot_about(arm,'Head',hb.head.copy(),Quaternion(bX,math.radians((k['pitch']-25*k['flex'])*0.45)))
    # body frame in anchor space
    s=k['sN']; Np=canal_point(s)+Vector((0,0,k['zoff'])); A=canal_tangent(s)
    ph=math.radians(k['pbody'])
    back=Vector((-math.sin(ph),0,math.cos(ph)))     # phi measured from +Z (anterior) toward -X (mother's left)
    R=frame_from(A,back)
    neck_local=arm.pose.bones['Neck'].head.copy()   # with Root at identity
    M=Matrix.Translation(Np-R@neck_local)@R.to_4x4()
    root=arm.pose.bones['Root']; root.matrix=M; bpy.context.view_layer.update()
    # centre the leading part on the canal axis: head before restitution, chest afterwards
    hc=head_center(arm); ch=arm.pose.bones['Chest'].head.copy()
    w=interp([(0,0),(0.5,0),(0.75,1),(1,1)],t)
    ref=hc.lerp(ch,w)
    if t<0.9:
        target=canal_point(ref.y)+Vector((0,0,k['zoff']*(1 if w>0.5 else 0)))
        off=Vector((target.x-ref.x,0,target.z-ref.z))*(1.0 if t<0.88 else smooth(1,0,(t-0.88)/0.02))
        M=Matrix.Translation(off)@M; root.matrix=M; bpy.context.view_layer.update()
    root.matrix=anchor@root.matrix; bpy.context.view_layer.update()
    return k
