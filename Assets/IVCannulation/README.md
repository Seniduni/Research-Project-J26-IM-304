# IV Cannulation Module – VR Nursing Trainer (Unity 6.3 LTS, URP)

A complete, playable IV cannulation training module: procedural 3D arm with anatomically placed superficial
veins, a 20G cannula with flashback chamber and catheter-over-needle mechanics, alcohol swab cleaning, tourniquet,
syringes, dressing, sharps bin, three difficulty levels, live angle feedback, an OSCE-style scored report and CSV
research logs. Works in **VR (Quest via OpenXR)** and with **mouse & keyboard** (for testing without a headset).

---

## 1. Install (5 minutes)

1. Open your **3D (URP)** project in Unity **6000.3.x**.
2. **Assets > Import Package > Custom Package…** and pick `IVCannulationModule.unitypackage` (import everything).
3. Wait for scripts to compile, then use the new top menu: **IV Cannulation > Build Training Scene**.
   This generates all models, materials, the scene `Assets/IVCannulation/Scenes/IVCannulationTraining.unity`,
   adds it to Build Settings, and creates two layers (`IVSkin`, `IVInset`).
4. Press **Play**. Without a headset you get the desktop rig automatically.

> If text looks pink/missing: make sure the project is URP (Edit > Project Settings > Graphics shows a URP asset).

## 2. Desktop controls (testing without VR)

| Action | Control |
|---|---|
| Start Easy / Medium / Hard | click the green/yellow/red buttons on the left of the table, or keys **1 / 2 / 3** |
| Pick up a tool / use an object | **Left-click** |
| Put tool back | **Right-click** (without dragging) or **G** |
| Orbit / zoom / pan camera | **Right-drag** / **scroll** / **middle-drag**; **C** centres on your chosen site |
| Palpate the skin | empty hand, **hold left mouse** on the arm |
| Scrub with the swab | holding the wet swab, **hold left mouse** and move over the skin |
| Cannula angle / aim / bevel | **W/S** angle, **A/D** aim left-right, **Q/E** rotate bevel |
| Push in / pull back | hold **Space** / hold **X** (hold **Shift** for fine control) |
| Advance catheter off needle | hold **F** |
| Hints on/off | **H** |

## 3. VR controls

| Action | Control |
|---|---|
| Grab / release tools | **Grip** |
| Use object you touch (tourniquet, bottle, syringe, gauze, buttons) | **Trigger** |
| Palpate | empty hand on the skin + **hold Trigger** (vibrates over a vein, pulses over an artery) |
| Insert | hold the cannula, push the needle into the skin **along its axis**; once in, pull/push and tilt your wrist to change the angle |
| Advance the catheter | while holding the inserted cannula, **squeeze Trigger** |
| Hints on/off | **A / X** |
| Recentre at the bedside | **B / Y** |
| Menu buttons | just touch them with your fingertip |

## 4. Setting up VR (do this once, before testing at university)

1. **Edit > Project Settings > XR Plug-in Management** → click *Install XR Plugin Management* if asked.
2. Tick **OpenXR** on the tab you will use:
   - **Windows tab** – fastest way to test: Quest connected by **Link cable / Air Link** and you just press Play in the Editor.
     In the Meta Quest Link PC app set *OpenXR runtime = Meta Quest Link*.
   - **Android tab** – standalone Quest build. Also tick **Meta Quest feature group**, and **File > Build Profiles**,
     switch to Android.
3. **XR Plug-in Management > OpenXR > Interaction Profiles** → add **Oculus Touch Controller Profile**
   (add *Meta Quest Touch Pro/Plus* profiles too if your headset uses them).
4. Open **XR Plug-in Management > Project Validation** and press **Fix All**.
5. Make sure *Initialize XR on Startup* is ticked. Press Play (Link) or Build & Run (Android).

The rig uses Unity's built-in XR input (no XR Interaction Toolkit needed), so it works with any OpenXR headset.
If XR is running, `RigBootstrap` switches to the VR rig automatically (you can force a mode on the `IVTrainer` object).

## 5. What the learner does (the procedure flow)

1. Hand hygiene & gloves → 2. Apply tourniquet (veins visibly fill) → 3. Palpate & select a vein (site is rated
Ideal / Acceptable / Avoid) → 4. Clean the site with the alcohol swab (coverage + scrub time measured) → 5. Let it dry
(touching the clean site contaminates it) → 6. Insert bevel-up at 15–30° until **flashback** → 7. Lower the angle and
advance 1–2 mm → 8. Advance the catheter (it bends along the vein) → 9. Release tourniquet → 10. Withdraw needle →
11. Sharps bin → 12. Draw blood → 13. Saline flush (patency/infiltration) → 14. Dressing → 15. Remove cannula →
16. Apply pressure → **scored report**.

**What can go wrong (and is detected):** too steep → back-wall puncture + haematoma; too shallow → slides above the vein;
deep at the elbow → brachial artery puncture (bright pulsing flashback); threading before the catheter tip is in the
vein; inserting through uncleaned/wet skin; re-using a cannula; leaving a needle out of the sharps bin; removing the
needle with the tourniquet on (blood spill); tourniquet left on > 2 min; flushing a misplaced catheter (swelling).

## 6. Difficulty levels

| | Easy – Guided | Medium – Assisted | Hard – Assessment |
|---|---|---|---|
| Skin | see-through (35%) | faint veins (88%) | opaque – palpate to find veins |
| Vein labels, site markers | yes | no | no |
| Live angle gauge + feedback | yes | yes | no (report only) |
| Side "ultrasound" view | yes | no | no |
| Step order | enforced with hints | enforced | free, errors scored |
| Attempts | unlimited | 3 | 2 (then "escalate to a colleague") |

Edit these on the **IVTrainer → IVSession** component (*Profiles*). Angle limits, scrub/dry times and the tourniquet
limit are under *Clinical*. **Step text is editable too – have your nursing lecturers review it against your
hospital's protocol** (e.g. scrub 30 s and dry 30 s are shortened to 15 s / 10 s by default for training flow).

## 7. Research data (for your evaluation chapter)

Set **Participant Id** on `IVSession` before each participant. Every finished session appends to:
- `sessions.csv` – one row per session (score, grade, angle, attempts, asepsis, errors, duration…)
- `attempts.csv` – one row per insertion attempt

Location: `Application.persistentDataPath/IVCannulationLogs` – on Windows
`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\IVCannulationLogs`, on Quest
`/sdcard/Android/data/<package>/files/IVCannulationLogs` (copy via USB). The path is also printed in the Console.

## 8. Project structure

```
Assets/IVCannulation/
  Scripts/Core        MeshBuilder (procedural meshes), IVInput (keyboard/mouse wrapper)
  Scripts/Anatomy     ArmModel, VeinPath, VascularSystem, SiteZone
  Scripts/Tools       Cannula (insertion physics), Swab, ProcedureAction (tourniquet, syringes, ...), AlcoholBottle, SharpsBin
  Scripts/Procedure   IVSession (steps, feedback, scoring), SiteCleaning, ReportBuilder, SessionLogger, BloodFX
  Scripts/Interaction Interactable, Grabbable, IVHand, DesktopHand, VRHand, PushButton
  Scripts/Rig         RigBootstrap, DesktopRig, XRRigController
  Scripts/UI          TrainerUI, AngleGauge, SideViewCamera, UIFactory
  Editor              IVSceneBuilder (menu), ObjExporter
  (generated) Materials, Generated (meshes), Prefabs, Scenes
```

Reusable prefabs for your teammates: `IV_PatientArm`, `IV_Cannula_20G`, `IV_CottonSwab`.

## 9. Using a more realistic arm model later

The procedural arm is deliberately simple so everything works out of the box. The **vein layout follows standard
anatomy** (cephalic, basilic, median cubital, median antebrachial veins + radial and brachial arteries for safety
teaching). To upgrade the visuals:

- **IV Cannulation > Export Arm as OBJ (for Blender)** gives you the arm + veins to sculpt/texture in Blender.
- Free anatomical sources: **Z-Anatomy** (Blender atlas, CC BY-SA 4.0, derived partly from BodyParts3D) for vein
  reference, or a scanned arm (check the licence and credit it in your report).
- Put your new mesh on the `Skin` object (MeshFilter + MeshCollider). Each vein is defined by child points
  `P0, P1, …` under `PatientArm/Vessels/<vein>` – drag them in the Scene view to trace the veins on the new arm, then
  right-click the `VeinPath` component → *Rebuild Mesh*. The insertion logic only uses the skin collider and these
  vein paths, so it keeps working.

## 10. Notes & next steps

- Clinical content (angles 10–30°, bevel up, lower and advance after flashback, two attempts then escalate) follows
  common published guidance, but **must be validated by your nursing faculty** before use with students.
- Ideas: real hand-tracking (XR Hands), sound (patient reactions), skin anchoring with the other hand, multiple
  patient profiles (difficult veins, different skin tones via `IVSession.skinTone`), XR Interaction Toolkit adapter if
  your teammates' modules use it.
