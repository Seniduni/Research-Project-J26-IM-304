# Procedural audio placeholders for the delivery module (replace with recorded/licensed audio for production).
import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import numpy as np, wave, os
SR=44100; OUT=ND_OUT+"/Resources/NeonatalDelivery/Audio"
rng=np.random.default_rng(7)
def save(name,x):
    x=np.clip(x/ (np.max(np.abs(x))+1e-9)*0.85,-1,1); x=(x*32767).astype(np.int16)
    with wave.open(os.path.join(OUT,name+".wav"),"wb") as w: w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(x.tobytes())
def env(n,a,r): e=np.ones(n); a=int(a*SR); r=int(r*SR); e[:a]=np.linspace(0,1,a); e[-r:]=np.linspace(1,0,r); return e
def biquad_bp(x,f,q):
    w=2*np.pi*f/SR; al=np.sin(w)/(2*q); b0,b1,b2=al,0,-al; a0,a1,a2=1+al,-2*np.cos(w),1-al
    y=np.zeros_like(x); x1=x2=y1=y2=0.0
    for i,xi in enumerate(x):
        yi=(b0*xi+b1*x1+b2*x2-a1*y1-a2*y2)/a0; x2,x1=x1,xi; y2,y1=y1,yi; y[i]=yi
    return y
def lowpass(x,f):
    a=np.exp(-2*np.pi*f/SR); y=np.zeros_like(x); s=0.0
    for i,v in enumerate(x): s=a*s+(1-a)*v; y[i]=s
    return y
def glottal(f0,dur,jitter=0.01):
    n=int(dur*SR); t=np.arange(n)/SR
    f=f0(t) if callable(f0) else np.full(n,f0)
    f=f*(1+jitter*rng.standard_normal(n).cumsum()/np.sqrt(np.arange(1,n+1)))
    ph=np.cumsum(2*np.pi*f/SR)
    x=np.zeros(n)
    for k in range(1,18): x+=np.sin(k*ph)/k**1.2
    return x
def formants(x,fs,qs,gs):
    return sum(g*biquad_bp(x,f,q) for f,q,g in zip(fs,qs,gs))
# --- newborn cry: 3 bursts with rising-falling f0 ~ 450-600 Hz, formants of open vowel, inhalation gaps
cry=[]
for b in range(3):
    d=0.85+0.15*rng.random()
    f0=lambda t,d=d: 470+120*np.sin(np.pi*np.clip(t/d,0,1))+18*np.sin(2*np.pi*6*t)
    v=glottal(f0,d,0.02); v=formants(v,[1100,2500,4200],[6,8,10],[1.0,0.6,0.25]); v*=env(len(v),0.05,0.18)
    v+=0.04*rng.standard_normal(len(v))*env(len(v),0.05,0.18)
    gap=int((0.32+0.1*rng.random())*SR); inh=0.05*biquad_bp(rng.standard_normal(gap),2500,1.5)*np.hanning(gap)
    cry+= [v,inh]
save("baby_cry",np.concatenate(cry))
# --- mother breathing in/out, push strain (voiced 'mmmh' grunt), panting, relief sigh, cry out (pain)
def breath(d,f=900,q=0.9): n=int(d*SR); return biquad_bp(rng.standard_normal(n),f,q)*np.hanning(n)
save("mother_breath",np.concatenate([breath(1.4,700),np.zeros(int(0.2*SR)),breath(1.8,500)]))
strain=glottal(lambda t: 175+10*np.sin(2*np.pi*3*t),3.2,0.015); strain=formants(strain,[450,1000,2600],[5,6,8],[1,0.5,0.2])
strain=lowpass(strain,2500)*env(len(strain),0.25,0.5)+0.15*breath(3.2,600)
save("mother_push_strain",np.concatenate([breath(0.9,900),strain]))
pants=[]
for i in range(8): pants+=[breath(0.16,1300,1.2),np.zeros(int(0.09*SR))]
save("mother_pant",np.concatenate(pants))
sigh=glottal(lambda t: 220-60*t,1.4,0.01); sigh=formants(sigh,[700,1200,2600],[5,6,8],[1,0.5,0.2])*env(int(1.4*SR),0.1,0.8)*0.6+breath(1.4,600)
save("mother_relief",sigh)
cryout=glottal(lambda t: 330+80*np.sin(np.pi*np.clip(t/1.1,0,1)),1.1,0.02); cryout=formants(cryout,[800,1300,2800],[5,6,8],[1,0.6,0.3])*env(int(1.1*SR),0.05,0.4)
save("mother_cry_out",cryout)
# --- fetal heart doppler beat (double "whoosh")
n=int(0.32*SR); t=np.arange(n)/SR
b=rng.standard_normal(n); b=biquad_bp(b,350,1.2)
e=np.exp(-((t-0.05)/0.025)**2)+0.7*np.exp(-((t-0.17)/0.03)**2)
save("fhr_beat",b*e)
# --- instruments & UI
n=int(0.12*SR); t=np.arange(n)/SR
click=(np.sin(2*np.pi*2400*t)*np.exp(-t*90)+0.6*rng.standard_normal(n)*np.exp(-t*160))
save("clamp_click",np.concatenate([click,np.zeros(int(0.03*SR)),0.7*click]))
n=int(0.18*SR); t=np.arange(n)/SR
snip=biquad_bp(rng.standard_normal(n),4200,2)*np.exp(-t*30)+0.5*np.sin(2*np.pi*1800*t)*np.exp(-t*60)
save("scissor_snip",snip)
def tone(fs,d,decay=8): n=int(d*SR); t=np.arange(n)/SR; return sum(np.sin(2*np.pi*f*t) for f in fs)*np.exp(-t*decay)
save("ui_click",tone([1500],0.06,60))
save("step_ok",np.concatenate([tone([660,990],0.14,14),tone([880,1320],0.22,9)]))
save("fault_alert",np.concatenate([tone([440,466],0.18,10),tone([392,415],0.26,8)]))
save("replay_start",tone([523,659,784],0.5,5))
print(sorted(os.listdir(OUT)))
