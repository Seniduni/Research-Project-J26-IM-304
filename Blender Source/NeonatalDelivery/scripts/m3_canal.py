# Stage 3: create the introitus opening + birth canal sleeve + ring bones on the mother (rest pose).
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import bpy, bmesh, numpy as np, math
from mathutils import Vector, Matrix, kdtree
bpy.ops.wm.open_mainfile(filepath=ND+"/blend/m2_skinned.blend")
arm=bpy.data.objects["ND_MotherRig"]; body=bpy.data.objects["M_Body"]; me=body.data
P0=Vector((0,-0.014,0.787))                 # introitus centre on the skin (rest pose)
D=Vector((0,0.5,0.866)).normalized()        # canal axis, pointing INTO the body (posterior-superior)
U=(Vector((0,1,0))-D*D.dot(Vector((0,1,0)))).normalized()   # slit direction (posterior) in canal cross-section
X=Vector((1,0,0))
A_SLIT,B_SLIT=0.014,0.0080                  # resting ellipse semi-axes (m)
NR=16                                       # rim resolution
bm=bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
skin_mi=[m.name for m in me.materials].index("Std_Skin_Body")
def region_faces(rad):
    out=[]
    for f in bm.faces:
        c=f.calc_center_median()
        if (c-P0).length<rad and abs(f.normal.x)<0.8 and f.normal.z<0.3: out.append(f)
    return out
for it in range(1):
    fs=region_faces(0.045 if it==0 else 0.03)
    es=set(e for f in fs for e in f.edges)
    bmesh.ops.subdivide_edges(bm,edges=list(es),cuts=1,use_grid_fill=True,smooth=0.0)
    bm.faces.ensure_lookup_table()
bmesh.ops.triangulate(bm,faces=[f for f in region_faces(0.05)])
# delete faces inside the ellipse (measured in the horizontal skin plane)
def inside(p,s=1.0):
    q=p-P0; return (q.y/(A_SLIT*s))**2+(q.x/(B_SLIT*s))**2<1 and abs(q.z)<0.012
kill=[f for f in bm.faces if inside(f.calc_center_median())]
print("faces removed:",len(kill))
bmesh.ops.delete(bm,geom=kill,context='FACES')
bm.verts.ensure_lookup_table()
# boundary loop nearest P0
bverts=[v for v in bm.verts if v.is_boundary and (v.co-P0).length<0.03]
print("rim verts:",len(bverts))
# verify single loop
seen=set(); loops=0
for v0 in bverts:
    if v0 in seen: continue
    loops+=1; st=[v0]
    while st:
        x=st.pop()
        if x in seen: continue
        seen.add(x); st+=[e.other_vert(x) for e in x.link_edges if e.is_boundary and e.other_vert(x) in bverts]
print("rim loops:",loops)
ang=lambda v: math.atan2((v.co-P0).dot(X),(v.co-P0).dot(Vector((0,1,0))))
bverts.sort(key=ang)
shape_layers=[bm.verts.layers.shape[k] for k in bm.verts.layers.shape.keys()]
def setco(v,co):
    for L in shape_layers: v[L]=co.copy()     # canal geometry has no facial-morph delta
    v.co=co
# snap rim onto clean ellipse, keep skin z
for v in bverts:
    a=ang(v); co=Vector((P0.x+B_SLIT*math.sin(a),P0.y+A_SLIT*math.cos(a),v.co.z)); setco(v,co)
# extrude rings into the body
rim_edges=[e for e in bm.edges if e.is_boundary and all(vv in bverts for vv in e.verts)]
rings=[bverts]; cur_edges=rim_edges
depths=[0.004,0.012,0.024,0.040,0.058,0.076,0.092]
radii =[0.011,0.0125,0.0135,0.0140,0.0145,0.0140,0.0110]
canal_faces=[]
uv_info={}
for k,(dep,rad) in enumerate(zip(depths,radii)):
    res=bmesh.ops.extrude_edge_only(bm,edges=cur_edges)
    nv=[g for g in res['geom'] if isinstance(g,bmesh.types.BMVert)]
    ne=[g for g in res['geom'] if isinstance(g,bmesh.types.BMEdge)]
    # gentle curve of the canal (bends anteriorly as it deepens - curve of Carus, reversed)
    C=P0+D*dep+U*(-0.10*dep*dep/0.09)
    blend=min(1,(k+1)/3)
    for v in nv:
        a=ang(v)
        ell=Vector((P0.x+B_SLIT*math.sin(a),P0.y+A_SLIT*math.cos(a),P0.z))   # rim shape
        circ=C+X*rad*math.sin(a)+U*rad*1.15*math.cos(a)
        rimproj=ell+D*dep
        setco(v,rimproj.lerp(circ,blend))
    for e in ne:
        if e.is_boundary: pass
    cur_edges=[e for e in ne if all(vv in nv for vv in e.verts) and e.is_boundary]
    rings.append(sorted(nv,key=ang))
# cap the end (cervix dimple)
endc=bmesh.ops.contextual_create(bm,geom=cur_edges)
bm.faces.ensure_lookup_table()
# mark canal faces: any face with all verts in rings[1:] or rim
ringset=set(v for r in rings for v in r)
mucosa_mi=len(me.materials)
for f in bm.faces:
    if all(v in ringset for v in f.verts): f.material_index=mucosa_mi; canal_faces.append(f)
for f in endc.get('faces',[]): f.material_index=mucosa_mi; canal_faces.append(f)
# UVs for canal (simple cylindrical)
for uvl in bm.loops.layers.uv.values():
    for f in canal_faces:
        for l in f.loops:
            v=l.vert; depth=(v.co-P0).dot(D)
            l[uvl].uv=((ang(v)/(2*math.pi))+0.5, depth*8)
canal_faces=list(set(canal_faces))
bmesh.ops.recalc_face_normals(bm,faces=canal_faces)
# normals should face inward into canal lumen (toward axis): check one face
f0=canal_faces[len(canal_faces)//2]; c=f0.calc_center_median(); axisp=P0+D*(c-P0).dot(D)
if f0.normal.dot(axisp-c)<0:
    bmesh.ops.reverse_faces(bm,faces=canal_faces)
bm.to_mesh(me); bm.free(); me.update()
mat=bpy.data.materials.new("ND_Mucosa"); mat.diffuse_color=(0.55,0.18,0.2,1); me.materials.append(mat)
# ---- bones
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT')
eb=arm.data.edit_bones
per=eb.new("Perineum"); per.head=P0; per.tail=P0+D*0.03; per.parent=eb["Hips"]; per.align_roll(-U); per.use_deform=False
anc=eb.new("BirthAnchor"); anc.head=P0; anc.tail=P0-D*0.05; anc.parent=eb["Hips"]; anc.use_deform=False
anc.align_roll(-U)    # local Z ~ anterior (toward pubis)
NB=8
def ringpos(a,dep,rad=None):
    if rad is None: return Vector((P0.x+B_SLIT*math.sin(a),P0.y+A_SLIT*math.cos(a),P0.z))+D*dep
    C=P0+D*dep+U*(-0.10*dep*dep/0.09)
    return C+X*rad*math.sin(a)+U*rad*1.15*math.cos(a)
for i in range(NB):
    a=2*math.pi*i/NB
    h=ringpos(a,0.0); out=(h-P0); out=(out-D*out.dot(D)).normalized()
    b=eb.new("Introitus_%02d"%i); b.head=h; b.tail=h+out*0.012; b.parent=per; b.align_roll(D)
for ringname,dep,rad in (("CanalA",0.030,0.014),("CanalB",0.062,0.0145)):
    for i in range(6):
        a=2*math.pi*i/6; h=ringpos(a,dep,rad); C=P0+D*dep+U*(-0.10*dep*dep/0.09)
        out=(h-C).normalized()
        b=eb.new("%s_%02d"%(ringname,i)); b.head=h; b.tail=h+out*0.012; b.parent=per; b.align_roll(D)
bpy.ops.object.mode_set(mode='OBJECT')
# ---- weights
V=[v.co.copy() for v in me.vertices]
def angp(p): q=p-P0; return math.atan2(q.dot(X),q.dot(Vector((0,1,0))))
def ring_w(a,n):
    t=(a%(2*math.pi))/(2*math.pi)*n; i0=int(math.floor(t))%n; f=t-math.floor(t)
    return {i0:1-f,(i0+1)%n:f}
gw={g.name:g for g in body.vertex_groups}
def grp(n): 
    if n not in gw: gw[n]=body.vertex_groups.new(name=n)
    return gw[n]
canal_idx=set(); 
for p in me.polygons:
    if p.material_index==len(me.materials)-1: canal_idx.update(p.vertices)
def set_weights(vi,d):
    for g in body.vertex_groups:
        try: g.remove([vi])
        except: pass
    s=sum(d.values())
    for n,w in sorted(d.items(),key=lambda x:-x[1])[:4]:
        if w/s>0.003: grp(n).add([vi],w/s,'REPLACE')
for vi in range(len(V)):
    p=V[vi]; a=angp(p)
    if vi in canal_idx:
        dep=(p-P0).dot(D)
        layers=[(0.0,"Introitus",NB),(0.030,"CanalA",6),(0.062,"CanalB",6),(0.095,"Hips",0)]
        d={}
        for j in range(len(layers)-1):
            d0,n0,c0=layers[j]; d1,n1,c1=layers[j+1]
            if d0<=dep<=d1 or (j==0 and dep<d0) or (j==len(layers)-2 and dep>d1):
                f=min(1,max(0,(dep-d0)/(d1-d0)))
                for i,w in ring_w(a,c0).items(): d["%s_%02d"%(n0,i)]=d.get("%s_%02d"%(n0,i),0)+w*(1-f)
                if n1=="Hips": d["Hips"]=d.get("Hips",0)+f
                else:
                    for i,w in ring_w(a,c1).items(): d["%s_%02d"%(n1,i)]=d.get("%s_%02d"%(n1,i),0)+w*f
                break
        set_weights(vi,d); continue
    pass
# ---- skin falloff around the opening: geodesic (edge-path) distance from the rim, not crossing thigh walls
import heapq
rimset=set()
for p in me.polygons:
    pass
adj=[[] for _ in range(len(V))]
for e in me.edges:
    a,b=e.vertices; L=(V[a]-V[b]).length; adj[a].append((b,L)); adj[b].append((a,L))
NRM=[v.normal.copy() for v in me.vertices]
rim_idx=[vi for vi in range(len(V)) if vi not in canal_idx and any(nb in canal_idx for nb,_ in adj[vi])]
dist={vi:0.0 for vi in rim_idx}; hq=[(0.0,vi) for vi in rim_idx]; heapq.heapify(hq)
while hq:
    d,u=heapq.heappop(hq)
    if d>dist.get(u,9): continue
    for w,L in adj[u]:
        if w in canal_idx: continue
        if abs(NRM[w].x)>0.72 or V[w].z<P0.z-0.012: continue    # stop at the inner-thigh walls
        nd=d+L
        if nd<dist.get(w,9) and nd<0.07:
            dist[w]=nd; heapq.heappush(hq,(nd,w))
nset=0
for vi,dd in dist.items():
    p=V[vi]; a=angp(p)
    post=max(0,math.cos(a))             # posterior (perineum) stretches most; anterior (pubic arch) least
    Rf=0.022+0.030*post
    if dd>Rf: continue
    w=1-dd/Rf; w=w*w*(3-2*w)
    if w<0.01: continue
    old={body.vertex_groups[g.group].name:g.weight for g in me.vertices[vi].groups}
    d={k:v*(1-w) for k,v in old.items()}
    for i,ww in ring_w(a,NB).items(): d["Introitus_%02d"%i]=d.get("Introitus_%02d"%i,0)+w*ww
    set_weights(vi,d); nset+=1
print("skin falloff verts:",nset)
for b in arm.data.bones:
    if b.name.startswith(("Introitus_","CanalA_","CanalB_")): b.use_deform=True
print("canal verts:",len(canal_idx))
bpy.ops.wm.save_as_mainfile(filepath=ND+"/blend/m3_canal.blend")
