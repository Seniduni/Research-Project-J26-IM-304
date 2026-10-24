# Delivery bed (fitted to the mother's settled lithotomy pose) + umbilical instruments + linen.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, bmesh, sys, math, json
sys.path.insert(0,ND+"/scripts")
from nd_otlib import *
from mathutils import Vector, Matrix, Euler
from poselib import SEAT
import nd_otlib
nd_otlib.MATS["ND_Towel"]=((0.95,0.95,0.93,1),0.0,0.95,None)
bpy.ops.wm.read_factory_settings(use_empty=True)
ref=json.load(open(ND+"/blend/mother_ref.json")); H=ref['settle']['hinge']
legs=json.load(open(ND+"/blend/legs.json"))
C45=math.cos(math.radians(45)); S45=math.sin(math.radians(45))
def backrest_center(along,depth):   # along: distance up the backrest from the hinge, depth: below top surface
    return Vector((0,H+along*C45+depth*S45,SEAT+along*S45-depth*C45))
# ---------------- DELIVERY BED
root=begin_asset("ND_DeliveryBed")
box("Chassis",(0.66,1.28,0.10),(0,0.47,0.17),"M_Plastic_OffWhite",0.02)
box("ChassisSkirt",(0.62,1.22,0.04),(0,0.47,0.105),"M_Plastic_Grey",0.01)
for x in (-0.29,0.29):
    for y in (-0.10,1.04):
        cyl(f"CastorWheel_{x}_{y}",0.05,0.035,(x,y,0.05),"M_Rubber_Black",24,0.004,rot=(0,math.radians(90),0))
        box(f"CastorFork_{x}_{y}",(0.05,0.05,0.07),(x,y,0.10),"M_Steel_Dark",0.006)
        cyl(f"CastorCap_{x}_{y}",0.018,0.042,(x,y,0.05),"M_Plastic_Blue",16,rot=(0,math.radians(90),0))
box("Column",(0.30,0.36,0.42),(0,0.52,0.42),"M_Plastic_White",0.03)
for i in range(5): box(f"ColumnRib_{i}",(0.31,0.37,0.012),(0,0.52,0.25+i*0.08),"M_Plastic_Grey",0.004)
box("ColumnHead",(0.46,0.62,0.07),(0,0.42,0.67),"M_Steel_Dark",0.01)
# seat section (foot section removed for delivery)
box("SeatFrame",(0.76,H+0.02,0.06),(0,H/2,SEAT-0.135),"M_Steel_Brushed",0.008)
box("SeatMattress",(0.80,H+0.03,0.11),(0,H/2-0.005,SEAT-0.055),"M_Upholstery_Blue",0.035,3)
box("SeatLinen",(0.81,H+0.035,0.004),(0,H/2-0.005,SEAT+0.001),"M_Paper_White",0.002)
# stowed foot section under the seat
box("FootSectionStowed",(0.74,0.46,0.07),(0,0.20,SEAT-0.215),"M_Upholstery_Blue",0.02)
box("FootSectionTray",(0.70,0.44,0.03),(0,0.20,SEAT-0.265),"M_Steel_Brushed",0.006)
# back section 45 degrees
rot=(math.radians(45),0,0)
box("BackMattress",(0.80,0.95,0.11),backrest_center(0.475,0.055),"M_Upholstery_Blue",0.035,3,rot=rot)
box("BackLinen",(0.81,0.96,0.004),backrest_center(0.475,-0.001),"M_Paper_White",0.002,rot=rot)
box("BackFrame",(0.74,0.90,0.05),backrest_center(0.46,0.135),"M_Steel_Brushed",0.008,rot=rot)
box("BackShell",(0.70,0.80,0.06),backrest_center(0.45,0.19),"M_Plastic_OffWhite",0.02,rot=rot)
tube("BackStrut",[(0,0.42,0.70),(0,H+0.30*C45,SEAT+0.30*S45-0.19)],0.022,"M_Steel_Polished")
box("Pillow",(0.52,0.30,0.09),backrest_center(0.80,-0.05),"M_Paper_White",0.04,3,rot=rot)
# lowered side rails along the back section + hand grips beside the hips
for sg in (-1,1):
    p0=backrest_center(0.10,0.06)+Vector((sg*0.44,0,0)); p1=backrest_center(0.85,0.06)+Vector((sg*0.44,0,0))
    tube(f"SideRail_{sg}",[tuple(p0),tuple(p1)],0.012,"M_Plastic_White")
    for a in (0.15,0.75):
        q=backrest_center(a,0.06)+Vector((sg*0.44,0,0)); tube(f"SideRailPost_{sg}_{a}",[tuple(q),tuple(q+Vector((-sg*0.05,0,-0.04)))],0.009,"M_Steel_Polished")
    gx=sg*0.41; gy=H+0.10; gz=SEAT+0.13
    tube(f"GripPostA_{sg}",[(sg*0.40,gy-0.10,SEAT-0.11),(gx,gy-0.09,gz)],0.012,"M_Steel_Polished")
    tube(f"GripPostB_{sg}",[(sg*0.40,gy+0.10,SEAT-0.11),(gx,gy+0.09,gz)],0.012,"M_Steel_Polished")
    tube(f"Grip_{sg}",[(gx,gy-0.09,gz),(gx,gy+0.09,gz)],0.016,"M_Rubber_Grey")
# leg supports (Goepel-type calf holders) fitted to the posed calves
for side,sg in (("Left",1),("Right",-1)):
    K=Vector(legs[side+"Knee"]); A=Vector(legs[side+"Ankle"]); d=(A-K).normalized()
    mid=K.lerp(A,0.45)
    down=Vector((0,0,-1)); down=(down-d*down.dot(d)).normalized()
    cradle=mid+down*0.004
    # trough: half cylinder along the shin
    bm=bmesh.new(); seg=14; L=0.30; R=0.064
    rows=[]
    for i in range(seg+1):
        a=math.pi*(0.15+0.7*i/seg)
        rows.append([Vector((math.cos(a)*R,math.sin(a)*R*-1,z)) for z in (-L/2,L/2)])
    vs=[[bm.verts.new(p) for p in r] for r in rows]
    for i in range(seg): bm.faces.new((vs[i][0],vs[i][1],vs[i+1][1],vs[i+1][0]))
    bmesh.ops.solidify(bm,geom=bm.faces[:],thickness=0.012)
    ob=mesh_obj(f"LegSupport_Trough_{side}",bm,"M_Upholstery_Black")
    # orient trough: local Z along shin, local -Y toward 'down'
    z=d; y=(-down); x=y.cross(z).normalized(); y=z.cross(x)
    M=Matrix((x,y,z)).transposed().to_4x4(); M.translation=cradle+down*0.0
    ob.matrix_world=M
    pad=cyl(f"LegSupport_Pad_{side}",R-0.008,L*0.95,(0,0,0),"M_Gel_Pad",20,cap=False); pad.matrix_world=M@Matrix.Scale(1.0,4)
    pad.scale=(1,1,1); pad.hide_render=True; bpy.data.objects.remove(pad)
    strap=torus(f"LegSupport_Strap_{side}",R+0.012,0.006,(0,0,0),"M_Plastic_Black",24,6,arc=200); strap.matrix_world=M@Matrix.Rotation(math.radians(100),4,'Z')
    clamp=Vector((sg*0.43,0.03,SEAT-0.10))
    elbow=Vector((sg*0.47,clamp.y-0.02,cradle.z-0.20))
    under=cradle+down*0.03
    tube(f"LegSupport_Post_{side}",[tuple(clamp),tuple(elbow),tuple(under)],0.014,"M_Steel_Polished")
    box(f"LegSupport_Clamp_{side}",(0.05,0.08,0.07),tuple(clamp),"M_Steel_Dark",0.008)
    cyl(f"LegSupport_Knob_{side}",0.02,0.03,tuple(clamp+Vector((sg*0.035,0,0))),"M_Plastic_Black",16,rot=(0,math.radians(90),0))
# under-buttocks drape with fluid-collection pouch (disposable)
bm=bmesh.new(); W=0.86; pts=[]
prof=[(0.0+H,SEAT+0.003),(0.0,SEAT+0.004),(-0.02,SEAT-0.005),(-0.035,SEAT-0.03),(-0.04,SEAT-0.12),(-0.035,SEAT-0.30),(-0.02,SEAT-0.42)]
nx=10; grid=[]
for j,(y,zz) in enumerate(prof):
    row=[]
    for i in range(nx+1):
        x=-W/2+W*i/nx
        sag=0.0 if j<2 else 0.03*math.sin(math.pi*i/nx)*(j/len(prof))
        row.append(bm.verts.new((x,y-sag,zz)))
    grid.append(row)
for j in range(len(grid)-1):
    for i in range(nx): bm.faces.new((grid[j][i],grid[j][i+1],grid[j+1][i+1],grid[j+1][i]))
mesh_obj("UnderPad_Drape",bm,"M_Drape_Blue")
lathe("UnderPad_Pouch",[(0.005,SEAT-0.60),(0.10,SEAT-0.56),(0.20,SEAT-0.46),(0.24,SEAT-0.34),(0.25,SEAT-0.30)],(0,-0.08,0),"M_Plastic_Clear",32)
pouch=bpy.data.objects["UnderPad_Pouch"]; pouch.scale=(1.0,0.35,1.0); pouch.location=(0,-0.09,0)
finalize_asset(root)
end_asset()
# ---------------- CORD CLAMP (Hollister-type), moving parts kept separate
def cord_clamp(name):
    r=begin_asset(name)
    # arm A (fixed): long flat arm with serrated jaw, hinge loop at origin, extends along +X
    box(name+"_ArmA_Body",(0.050,0.010,0.004),(0.025,0,-0.003),"M_Plastic_White",0.0015)
    for i in range(9): box(name+f"_ArmA_Tooth{i}",(0.0018,0.009,0.002),(0.008+i*0.0035,0,-0.0002),"M_Plastic_White",0.0004)
    box(name+"_ArmA_Latch",(0.006,0.010,0.010),(0.051,0,0.003),"M_Plastic_White",0.001)
    torus(name+"_Hinge",0.0045,0.0016,(0,0,0),"M_Plastic_White",20,8,rot=(math.radians(90),0,0))
    finalize_asset(r,keep_separate=("_ArmB",))
    # arm B (moving) pivots at hinge (origin of its own object), open 35 degrees
    b=begin_asset(name+"_ArmB")
    box(name+"_ArmB_Body",(0.048,0.010,0.004),(0.025,0,0.003),"M_Plastic_White",0.0015)
    for i in range(9): box(name+f"_ArmB_Tooth{i}",(0.0018,0.009,0.002),(0.0098+i*0.0035,0,0.0002),"M_Plastic_White",0.0004)
    box(name+"_ArmB_Grip",(0.010,0.012,0.003),(0.044,0,0.0055),"M_Plastic_White",0.001)
    finalize_asset(b); end_asset()
    b.parent=r; b.rotation_euler=(0,-math.radians(35),0)
    return r
c=cord_clamp("ND_CordClamp")
c.location=(1.2,0,0)
# ---------------- UMBILICAL (cord) SCISSORS: curved blunt blades, two moving halves
def blade(name,side):
    r=begin_asset(name)
    # blade (curved) from pivot (origin) toward +X, finger ring toward -X
    pts=[(0.0,0,0),(0.03,0.002*side,0),(0.06,0.006*side,0),(0.085,0.012*side,0)]
    tube(name+"_Blade",pts,0.0035,"M_Instrument_Steel",8)
    bpy.data.objects[name+"_Blade"].scale=(1,1,0.45)
    tube(name+"_Shank",[(0,0,0),(-0.05,-0.010*side,0),(-0.085,-0.016*side,0)],0.003,"M_Instrument_Steel",8)
    torus(name+"_Ring",0.011,0.0028,(-0.098,-0.02*side,0),"M_Instrument_Steel",24,8)
    cyl(name+"_Screw",0.004,0.004,(0,0,0.002*side),"M_Steel_Polished",16)
    finalize_asset(r); end_asset(); return r
s=begin_asset("ND_CordScissors"); end_asset()
b1=blade("ND_CordScissors_BladeA",1); b2=blade("ND_CordScissors_BladeB",-1)
b1.parent=s; b2.parent=s; b1.rotation_euler=(0,0,math.radians(9)); b2.rotation_euler=(0,0,-math.radians(9))
s.location=(1.2,0.2,0)
# ---------------- TOWEL (folded, absorbent) + receiving blanket
t=begin_asset("ND_Towel")
for i in range(3): box(f"ND_Towel_Fold{i}",(0.34,0.24,0.012),(0,0,0.006+i*0.0125),"ND_Towel",0.006,3)
finalize_asset(t); end_asset(); t.location=(1.2,0.5,0)
tr=begin_asset("ND_DeliveryTray")
box("Tray_Base",(0.42,0.30,0.004),(0,0,0.002),"M_Steel_Brushed",0.002)
for sx,sy,L,w in ((0,0.148,0.42,0.004),(0,-0.148,0.42,0.004)): box(f"Tray_RimY{sy}",(L,w,0.03),(sx,sy,0.017),"M_Steel_Brushed",0.0015)
for sx in (-0.208,0.208): box(f"Tray_RimX{sx}",(0.004,0.30,0.03),(sx,0,0.017),"M_Steel_Brushed",0.0015)
box("Tray_Wrap",(0.40,0.28,0.002),(0,0,0.005),"M_Drape_Green",0.001)
finalize_asset(tr); end_asset(); tr.location=(1.2,0.9,0)
# custom material for towel
m=bpy.data.materials.get("ND_Towel") or bpy.data.materials.new("ND_Towel"); m.diffuse_color=(0.95,0.95,0.93,1)
# bake joined meshes so each mesh origin == its asset root (clean pivots for Unity)
for ob in [o for o in bpy.data.objects if o.type=='MESH']:
    par=ob.parent; mw=ob.matrix_world.copy()
    R=par.matrix_world.copy() if par else Matrix.Identity(4)
    ob.data.transform(R.inverted()@mw); ob.matrix_world=R
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/p1_props.blend")
print("props ok")
