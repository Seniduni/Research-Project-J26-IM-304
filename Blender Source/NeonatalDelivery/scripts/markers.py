# Compute anatomical marker positions (rest pose) for baby zones and mother calibration landmarks.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, numpy as np, json
from mathutils import Vector
def baby_markers():
    o=bpy.data.objects["B_Body"]; V=np.array([v.co[:] for v in o.data.vertices])
    J=json.load(open(ND+"/blend/baby_joints.json"))
    hz0=J['Head'][2]; top=V[:,2].max()
    H=V[(V[:,2]>hz0)&(np.abs(V[:,0])<0.06)]
    # skull (exclude ears): use |x|<0.045 for y extents
    S=V[(V[:,2]>hz0+0.02)&(np.abs(V[:,0])<0.03)]
    yf,yb=S[:,1].min(),S[:,1].max(); zc=(hz0+top)/2+0.005
    cx=0.0; cy=(yf+yb)/2
    sl=V[(np.abs(V[:,2]-(zc+0.01))<0.006)&(np.abs(V[:,1]-cy)<0.02)]
    halfw=np.abs(sl[:,0]).max()
    M={}
    M['HeadCenter']=('Head',(cx,cy,zc))
    M['Vertex']=('Head',(cx,cy+0.012,top-0.012))
    M['Occiput']=('Head',(cx,yb-0.014,zc-0.01))
    M['ParietalL']=('Head',(halfw-0.014,cy+0.006,zc+0.012))
    M['ParietalR']=('Head',(-(halfw-0.014),cy+0.006,zc+0.012))
    eye=V[(np.abs(V[:,0])<0.01)&(np.abs(V[:,2]-(hz0+0.03))<0.006)]
    M['Face']=('Head',(0,eye[:,1].min()+0.016,hz0+0.025))
    M['Chin']=('Jaw',(0,J['JawEnd'][1]+0.005,J['JawEnd'][2]+0.004))
    nk=V[(np.abs(V[:,2]-(J['Neck'][2]+0.012))<0.005)]
    M['Neck']=('Neck',(0,(nk[:,1].min()+nk[:,1].max())/2,J['Neck'][2]+0.012))
    for s,sg in (('L',1),('R',-1)):
        side='Left' if sg>0 else 'Right'
        ua=Vector(J[side+'UpperArm'])
        M['Shoulder'+s]=(side+'Shoulder',(ua.x-sg*0.006,ua.y,ua.z+0.016))
        M['Axilla'+s]=(side+'UpperArm',(ua.x-sg*0.004,ua.y,ua.z-0.022))
        th=Vector(J[side+'UpperLeg']).lerp(Vector(J[side+'LowerLeg']),0.5); M['Thigh'+s]=(side+'UpperLeg',tuple(th))
    ch=V[(np.abs(V[:,2]-J['Chest'][2])<0.006)&(np.abs(V[:,0])<0.01)]
    M['ChestFront']=('Chest',(0,ch[:,1].min()+0.012,J['Chest'][2]))
    M['Back']=('Chest',(0,ch[:,1].max()-0.012,J['Chest'][2]))
    bt=V[(np.abs(V[:,2]-(J['Hips'][2]-0.01))<0.006)&(np.abs(V[:,0])<0.02)]
    M['Buttocks']=('Hips',(0,bt[:,1].max()-0.012,J['Hips'][2]-0.01))
    M['NavelSkin']=('Hips',tuple(J['Navel']))
    return M,{'headHalfWidth':float(halfw),'headLength':float(yb-yf),'headHeight':float(top-hz0)}
def mother_markers():
    o=bpy.data.objects["M_Body"]; V=np.array([v.co[:] for v in o.data.vertices])
    M={}
    M['Introitus']=('Perineum',(0,-0.014,0.787))
    for s,sg in (('L',1),('R',-1)):
        sl=V[(np.abs(V[:,2]-0.905)<0.008)&(np.abs(V[:,0]-sg*0.115)<0.01)]
        M['ASIS'+s]=('Hips',(sg*0.115,sl[:,1].min()+0.012,0.905))
    pb=V[(np.abs(V[:,0])<0.01)&(np.abs(V[:,2]-0.835)<0.008)]
    M['Pubis']=('Hips',(0,pb[:,1].min()+0.010,0.835))
    return M
