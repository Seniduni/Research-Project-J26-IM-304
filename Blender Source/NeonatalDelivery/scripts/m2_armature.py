# Stage 2b: build the mother armature from landmarks (Unity-humanoid compatible names + birth bones)
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np, json, math
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m1_prep.blend")
L=json.load(open(ND+"/blend/landmarks.json"))
V=np.array([v.co[:] for v in bpy.data.objects["M_Body"].data.vertices])
P=lambda k: Vector(L[k])
J={}
J['Hips']=Vector((0,0.025,0.855)); J['Spine']=Vector((0,0.035,0.95)); J['Chest']=Vector((0,0.008,1.065))
J['UpperChest']=Vector((0,0.022,1.175)); J['Neck']=Vector((0,0.03,1.372)); J['Head']=Vector((0,0.0,1.447))
J['HeadTop']=Vector((0,0.0,1.62))
for s,sg in (('Left',1),('Right',-1)):
    k='l' if sg>0 else 'r'
    J[s+'Shoulder']=Vector((sg*0.022,0.012,1.335)); J[s+'UpperArm']=Vector((sg*0.160,0.032,1.305))
    J[s+'LowerArm']=Vector((sg*0.372,0.050,1.188)); J[s+'Hand']=Vector(L['wrist_'+k])
    J[s+'UpperLeg']=Vector((sg*0.085,0.005,0.835)); J[s+'LowerLeg']=Vector((sg*0.086,0.002,0.44)); J[s+'Foot']=Vector(L['ankle_'+k])
    J[s+'Toes']=Vector(L['ball_'+k]); J[s+'ToesEnd']=Vector(L['toe_'+k])
    # fingers from hand frame
    h=L['hand_'+k]; a=np.array(h['a']); b=np.array(h['b']); n=np.array(h['n']); wr=np.array(L['wrist_'+k])
    D=V-wr; S=D@a; B=D@b; N=D@n
    hm=(np.abs(N)<0.05)&(S>-0.01)&(S<0.2)&(np.abs(B)<0.12)&(V[:,0]*sg>0.4)
    def jp(sv,bv,w=0.006):
        q=hm&(np.abs(S-sv)<w)&(np.abs(B-bv)<w)
        if q.sum()<2: q=hm&(np.abs(S-sv)<2*w)&(np.abs(B-bv)<2*w)
        nn=N[q].mean() if q.sum() else 0.0
        return Vector((wr+a*sv+b*bv+n*nn).tolist())
    lanes={'Index':(0.040,0.085,0.155),'Middle':(0.022,0.090,0.165),'Ring':(0.004,0.085,0.156),'Little':(-0.013,0.075,0.136)}
    for f,(bc,s0,s1) in lanes.items():
        Lf=s1-s0
        J[s+f+'Proximal']=jp(s0,bc); J[s+f+'Intermediate']=jp(s0+0.45*Lf,bc); J[s+f+'Distal']=jp(s0+0.73*Lf,bc); J[s+f+'End']=jp(s1-0.004,bc)
    J[s+'ThumbProximal']=jp(0.022,0.032); J[s+'ThumbIntermediate']=jp(0.048,0.060); J[s+'ThumbDistal']=jp(0.070,0.076); J[s+'ThumbEnd']=jp(0.088,0.085)
    e=Vector(L['eye_'+k]); J[s+'Eye']=e
J['Jaw']=Vector((0,-0.008,1.462)); J['JawEnd']=Vector((0,-0.088,1.398))
# belly + birth bones (canal axis placeholder; refined in m3)
J['Belly']=Vector((0,-0.075,0.955)); J['BellyEnd']=Vector((0,-0.185,0.965))
arm_d=bpy.data.armatures.new("ND_MotherRig"); arm=bpy.data.objects.new("ND_MotherRig",arm_d)
bpy.context.scene.collection.objects.link(arm); bpy.context.view_layer.objects.active=arm
arm_d.display_type='STICK'; arm.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT')
eb=arm_d.edit_bones
def bone(name,head,tail,parent=None,deform=True,connect=False,roll_up=Vector((0,-1,0))):
    b=eb.new(name); b.head=head; b.tail=tail
    if (b.tail-b.head).length<1e-4: b.tail=b.head+Vector((0,0,0.02))
    b.align_roll(roll_up); b.use_deform=deform
    if parent: b.parent=eb[parent]; b.use_connect=connect
    return b
root=bone("Root",Vector((0,0,0)),Vector((0,0.15,0)),deform=False,roll_up=Vector((0,0,1)))
bone("Hips",J['Hips'],J['Spine'],"Root")
bone("Spine",J['Spine'],J['Chest'],"Hips",connect=True)
bone("Chest",J['Chest'],J['UpperChest'],"Spine",connect=True)
bone("UpperChest",J['UpperChest'],J['Neck'],"Chest",connect=True)
bone("Neck",J['Neck'],J['Head'],"UpperChest",connect=True)
bone("Head",J['Head'],J['HeadTop'],"Neck",connect=True)
bone("Jaw",J['Jaw'],J['JawEnd'],"Head",roll_up=Vector((0,0,1)))
bone("Belly",J['Belly'],J['BellyEnd'],"Spine",roll_up=Vector((0,0,1)))
for s,sg in (('Left',1),('Right',-1)):
    bone(s+"Eye",J[s+'Eye']+Vector((0,0.012,0)),J[s+'Eye']+Vector((0,-0.02,0)),"Head",roll_up=Vector((0,0,1)))
    bone(s+"Shoulder",J[s+'Shoulder'],J[s+'UpperArm'],"UpperChest",roll_up=Vector((0,0,1)))
    bone(s+"UpperArm",J[s+'UpperArm'],J[s+'LowerArm'],s+"Shoulder",connect=True,roll_up=Vector((0,0,1)))
    bone(s+"LowerArm",J[s+'LowerArm'],J[s+'Hand'],s+"UpperArm",connect=True,roll_up=Vector((0,0,1)))
    mid=J[s+'MiddleProximal']
    bone(s+"Hand",J[s+'Hand'],mid,s+"LowerArm",connect=True,roll_up=Vector((0,0,1)))
    for f in ['Thumb','Index','Middle','Ring','Little']:
        par=s+"Hand"
        for seg,nxt in (('Proximal','Intermediate'),('Intermediate','Distal'),('Distal','End')):
            bone(s+f+seg,J[s+f+seg],J[s+f+nxt],par,connect=(seg!='Proximal'),roll_up=Vector((0,0,1)))
            par=s+f+seg
    bone(s+"UpperLeg",J[s+'UpperLeg'],J[s+'LowerLeg'],"Hips",roll_up=Vector((0,-1,0)))
    bone(s+"LowerLeg",J[s+'LowerLeg'],J[s+'Foot'],s+"UpperLeg",connect=True,roll_up=Vector((0,-1,0)))
    bone(s+"Foot",J[s+'Foot'],J[s+'Toes'],s+"LowerLeg",connect=True,roll_up=Vector((0,0,1)))
    bone(s+"Toes",J[s+'Toes'],J[s+'ToesEnd'],s+"Foot",connect=True,roll_up=Vector((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')
json.dump({k:list(v) for k,v in J.items()},open(ND+"/blend/joints.json","w"),indent=1)
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m2_arm.blend")
# --- render overlay: spheres at joints
sc=bpy.context.scene
for k,v in J.items():
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.006 if any(f in k for f in['Thumb','Index','Middle','Ring','Little']) else 0.012,location=v)
    o=bpy.context.object; o.name="J_"+k
    m=bpy.data.materials.get("JM") or bpy.data.materials.new("JM"); m.diffuse_color=(1,0.1,0.1,1); o.data.materials.append(m)
for o in bpy.data.objects:
    if o.name in("ND_GreenCloth_Src","M_Hair","M_HairCap"): o.hide_render=True
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'
sc.display.shading.show_xray=True; sc.display.shading.xray_alpha=0.45
sc.render.resolution_x=700; sc.render.resolution_y=900
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'
for nm,loc,sz in [('front',Vector((0,-3,0.85)),1.8),('side',Vector((3,0,0.85)),1.8),('handL',Vector((0.6,-0.0,2.0)),0.3)]:
    cam.data.ortho_scale=sz
    tgt=Vector((0,0,0.85)) if nm!='handL' else Vector((0.6,0.0,1.06))
    if nm=='handL': cam.location=tgt+Vector((0,0,1.0))
    else: cam.location=loc
    cam.rotation_euler=(tgt-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=f"{ND}/renders/skel_{nm}.png"; bpy.ops.render.render(write_still=True)
