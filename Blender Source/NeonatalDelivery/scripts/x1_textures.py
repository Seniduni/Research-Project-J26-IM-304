# Extract textures from the source GLBs, downscale to <=2048, write a material manifest for the Unity setup.
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import json, struct, io, os, re
from PIL import Image
OUT=ND_OUT+"/Art"
def glb(path):
    f=open(path,'rb'); f.read(12); l,_=struct.unpack('<II',f.read(8)); j=json.loads(f.read(l))
    l2,_=struct.unpack('<II',f.read(8)); b=f.read(l2); return j,b
def img_bytes(j,b,ii):
    bv=j['bufferViews'][j['images'][ii]['bufferView']]; o=bv.get('byteOffset',0); return b[o:o+bv['byteLength']]
manifest={}
def save(j,b,tex_index,folder,name,kind):
    ii=j['textures'][tex_index]['source']; im=Image.open(io.BytesIO(img_bytes(j,b,ii)))
    if max(im.size)>2048: im=im.resize((min(2048,im.size[0]*2048//max(im.size)),min(2048,im.size[1]*2048//max(im.size))),Image.LANCZOS)
    has_alpha=im.mode in('RGBA','LA') and im.getextrema()[-1][0]<255
    ext='png' if (has_alpha or kind!='base') else 'jpg'
    if ext=='jpg': im=im.convert('RGB')
    rel=f"Textures/{folder}/{name}_{kind}.{ext}"
    im.save(os.path.join(OUT,rel),quality=92) if ext=='jpg' else im.save(os.path.join(OUT,rel),optimize=True)
    return rel,has_alpha
for src,folder,rename in ((ND+"/input/LadyWITH_CLOTHING_3.glb","Mother",{"Green Duck Cloth":"ND_DrapeGreen"}),
                          (ND+"/input/Baby_model.glb","Baby",{"Material_0":"ND_BabySkin"})):
    j,b=glb(src)
    for m in j['materials']:
        name=m['name']; key=rename.get(name,name)
        fold="Drape" if key=="ND_DrapeGreen" else folder
        safe=re.sub(r'[^A-Za-z0-9_]','_',key)
        e={"alphaMode":m.get('alphaMode','OPAQUE'),"alphaCutoff":m.get('alphaCutoff',0.5),"doubleSided":m.get('doubleSided',False)}
        pbr=m.get('pbrMetallicRoughness',{})
        e["baseColorFactor"]=pbr.get('baseColorFactor',[1,1,1,1]); e["metallic"]=pbr.get('metallicFactor',1.0); e["roughness"]=pbr.get('roughnessFactor',1.0)
        if 'baseColorTexture' in pbr: e["baseColor"],e["baseHasAlpha"]=save(j,b,pbr['baseColorTexture']['index'],fold,safe,'base')
        if 'metallicRoughnessTexture' in pbr: e["metalRough"],_=save(j,b,pbr['metallicRoughnessTexture']['index'],fold,safe,'metalrough')
        if 'normalTexture' in m: e["normal"],_=save(j,b,m['normalTexture']['index'],fold,safe,'normal'); e["normalScale"]=m['normalTexture'].get('scale',1.0)
        manifest[key]=e
manifest["ND_Mucosa"]={"procedural":"mucosa"}
manifest["ND_Towel"]={"procedural":"towel"}
json.dump(manifest,open(os.path.join(OUT,"ND_MaterialManifest.json"),"w"),indent=1)
for k,v in manifest.items(): print(k,{kk:vv for kk,vv in v.items() if kk in('alphaMode','baseColor','normal','metalRough','baseHasAlpha')})
