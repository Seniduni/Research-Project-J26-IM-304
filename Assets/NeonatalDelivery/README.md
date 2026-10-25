# Neonatal Delivery & Hand-Placement Training Module

**Project:** J26-IM-304, *VR Nursing Training Platform*
**Component owner:** Himal G.M.H (IT23152632)
**Target:** Unity 6000.3.19f1 · URP 17.3 · OpenXR 1.18 · Meta Quest 3 (hand tracking, passthrough mixed reality)

This module is a self-contained trainer for a normal vaginal (LOA) birth. It runs from the second stage of labour through to cutting the cord. The trainee uses hand tracking to:

- control the head at crowning while supporting the perineum;
- check for a nuchal cord;
- deliver the shoulders with axial traction;
- receive the baby and place it skin-to-skin;
- dry and stimulate the baby;
- wait for delayed cord clamping, then clamp and cut the cord.

Hand placement is resolved against anatomical landmark zones on the newborn and the mother. Faults are classified against an external, clinician-editable taxonomy. Each fault produces a visible consequence on the baby or the mother. A third-person replay of the faulty interval follows, and the step must be repeated until it is done correctly.

All files live in `Assets/NeonatalDelivery/` and `Assets/StreamingAssets/NeonatalDelivery/`. **No teammate file is modified.**
- The shared ward (`MR_OperatingTheatre_Ward.prefab`), its `M_*` materials, `MayoStand.prefab` and `M_Glove_Blue` are referenced, never edited.
- Furniture that would stand inside the delivery area is hidden as a scene-only override in the module's own scene.

**Update v5:**
- The baby is unrigged and comes out as one rigid piece.
- Cord scissors cut automatically when held on the cord, and clamps lock on by themselves.
- Hands brought close to the baby **auto-fit** into the correct supporting pose.
- The mirrored (inverted) hands are fixed.
- The gown uses your light-green cloth texture.

**Update v4:**
- The mother wears your green gown.
- The new baby has new textures and simple clips.
- The umbilical cord is a kinematic curve that leaves the belly cleanly, with no rope physics.
- The face is not animated.
- Sounds are removed.

---

## 1. Setup (about 10 minutes)

1. Extract the overlay zip at the **project root**, next to `Assets/`. It only adds `Assets/NeonatalDelivery`, `Assets/StreamingAssets/NeonatalDelivery`, `Blender Source/NeonatalDelivery` and `_incoming/commands_nd_build.txt`. Open the project in **Unity 6000.3.19f1**.
   - **Ready-made full project:** `J26-IM-304_Unity6000.3.19f1.zip` is the team's main branch with this module already merged in. Open it in Unity Hub with *Add → Add project from disk*.
     - Its `ProjectSettings/ProjectVersion.txt` is set to **6000.3.19f1**. This is the only team file that differs from main, because main was saved with 6000.3.22f1.
     - Do not commit `ProjectVersion.txt` unless the whole team agrees to use 6000.3.19f1.
   - **If you use the team's DevBridge:** drop the zip into `<project>/_incoming/` instead. DevBridge unpacks it, then runs *Build Everything* and opens the scene. Progress and errors appear in `_dev_log.txt`, tagged `[NeonatalDelivery]`.
   - The menu steps that change shared project files are never run automatically: *Install Dependencies* (`Packages/manifest.json`), *Configure OpenXR* (OpenXR settings) and *Add Scene to Build Settings*. Run them yourself after agreeing them with the team.
2. Choose **Neonatal Delivery → 1. Install Dependencies**. This adds `com.unity.xr.hands@1.9.0` (hand tracking) and `com.unity.xr.meta-openxr@2.6.1` (Quest passthrough and spatial anchors; it brings AR Foundation 6). Without them the module still compiles: it falls back to controllers and VR-only mode.
3. Choose **Neonatal Delivery → 2. Configure OpenXR**. This enables Hand Tracking Subsystem, Meta Hand Tracking Aim, Hand Interaction Profile, Meta Quest Support, the Touch controller profiles, and the Meta AR Session/Camera/Anchor features on Android.
   - Then open *Project Settings → XR Plug-in Management → Project Validation* and click **Fix All** for Android.
4. Choose **Neonatal Delivery → 3. Build Everything**. It:
   - imports the models (Generic rigs, clips renamed `ND_M_*` / `ND_B_*`) and textures (ASTC 6×6 on Android);
   - builds the materials from `Art/ND_MaterialManifest.json` and remaps the FBX slots;
   - creates `Scenes/ND_NeonatalDelivery.unity` and `Prefabs/ND_DeliveryStation.prefab`.

   The console prints the bed-space fit residual. It should be below 2 mm.
5. Choose **4. Open Delivery Scene** and press **Play**. With no headset connected, the desktop simulator runs (see §4).
6. To deploy: choose **5. Add Scene to Build Settings**, switch the platform to **Android**, then *Build And Run* with the Quest 3 connected.
   - Other members' scenes stay in Build Settings; tick only the scenes you want.

## 2. Assets

| Asset | Contents |
|---|---|
| `ND_Mother.fbx` | 79-bone rig made from the supplied sculpt. It includes a **birth-canal rig**: introitus ring ×8, two canal rings ×6, a perineum bone and a BirthAnchor. 6 clips: `ND_M_Base`, `Idle`, `Push` (scrubbed per contraction), `Pant`, `Reach`, `Cradle`. Anchors: placenta, voice, skin-to-skin, fundus. MR calibration markers: introitus, ASIS L/R, pubis. **No facial animation:** all shape keys were removed and `MotherController.faceAnimation` is off, so the face, eyes and jaw keep your sculpted neutral expression. All textures are embedded in the FBX. |
| Green gown | `M_Gown` is **your green gown** from `LadyWITH_CLOTHING_Green_cloth.glb`, with your colour and normal maps. It was fitted to the body in the delivery (lithotomy) pose with a surface-deform bind and skinned to the same bones as the body, so it follows every clip without stretching. The part below the hip crease and the back panels inside the backrest were removed so the perineum stays clear. It renders double-sided (`ND_Gown`, URP Lit). |
| `ND_Baby.fbx` | **Your new baby** (`Baby_new.glb`) is scaled to a term newborn: head 12.2 cm occipito-frontal, about 9.3 cm biparietal. **v5: no rig.** Your sculpt is exported as it is, as a static mesh, so nothing can deform or glitch. It sits under an animated `Root` empty. `ND_B_Birth` (10 s) moves the whole baby along the curve of Carus: descent, internal rotation, crowning, restitution, external rotation, birth. Joint points (`Hips`, `Spine`, `Chest`, `Neck`, `Head`) and 18 landmark markers (`ND_Z_*`) are plain empties, so the touch zones, canal dilation, carrying and cord attachment work unchanged. Limb animations (cry, limp, Erb's palsy) are not shown while the baby is unrigged. Textures come from your FFOutput set at 2048: colour × AO × veins, normal and roughness. |
| Props | Delivery bed in lithotomy with Goepel leg troughs and grips; cord clamp (hinged arm); cord scissors (two blades); towel; instrument tray. The tray sits on a slim stand at the trainee's right, behind the raised feet, so it never overlaps the legs. |
| Shaders | `NeonatalDelivery/Skin` adds wrapped subsurface plus layers for wetness, vernix, blood and cyanosis, and a gore attenuation layer used by censorship. Also `Ghost`, `Veil` and `Occluder` (for MR on the silicone doll). All variants were compiled with DXC against URP 17.3. |
| Audio | **None.** Sounds and SFX are switched off: no clips ship, the scene has no `NDAudio`, and `NDAudio.Enabled = false` makes any remaining call a silent no-op. *Build Everything* deletes the old `Resources/NeonatalDelivery/Audio` folder. |

## 3. Where to touch: hand-placement zones (FR1)

Zones are spheres on the baby's skeleton and the mother's perineum, built from the FBX markers. They are evaluated each frame against all 26 joints of both tracked hands. Force is estimated from penetration depth × stiffness: contact 420 N/m, traction 260 N/m.

| Step | Correct placement | Faults detected (code) |
|---|---|---|
| Crowning: head control | **Dominant hand:** flat fingers on the **occiput/vertex**, gentle pressure. **Other hand:** on the **maternal perineum**. Ask the mother to pant. | No head support (F01) · head pressure > 25 N (F02) · perineum unsupported (F03) · touching the face (F04) |
| Nuchal cord check | Two fingertips slide along the **neck** | Step skipped (F05) |
| Restitution | **Hands off.** Let the head rotate to face the mother's thigh. | Traction before rotation (F06) · face contact (F04) |
| Anterior shoulder | Both hands on the **parietal bones** (biparietal hold), gentle **axial** traction in line with the fetal spine | Wrong axis (F07) · > 40 N (F08) · gripping the neck (F09) · lateral flexion > 12 N (F10) · finger hooked in the axilla (F11) |
| Posterior shoulder | Biparietal hold, guide the head **upward/anteriorly** along the curve | F12, F08–F11 |
| Body | One hand supports **head and neck**, the other slides along the **back to the buttocks** | Premature release (F13), F08, F09, F11 |
| Skin-to-skin | Two-handed carry to the mother's chest | One-handed hold (F14) · baby dropped (F15) |
| Dry & stimulate | Towel over the head, back and limbs | |
| Delayed clamping | Wait **≥ 60 s** (configurable) | Early clamping (F16) |
| Clamp | 1st clamp **2–4 cm** from the umbilicus (target 3 cm); 2nd clamp **4–7.5 cm** (target 5 cm); spacing 1–4 cm | F17, F18, F19 |
| Cut | Cut **between** the clamps; shield the cut with the other hand | No clamps (F20) · cut outside the clamps (F21) |

The rules and thresholds are in `StreamingAssets/NeonatalDelivery/nd_fault_rules.json` (NFR9).
- Each rule has steps, zones, a detector, a threshold, a minimum duration, severity, consequence, complication, explanation, remediation and a guideline source.
- Edit the JSON, or drop an override at `<persistentDataPath>/NeonatalDelivery/nd_fault_rules.json`; no recompile is needed.
- Validate it with *Neonatal Delivery → Tools → Validate Fault Rules JSON*.
- The status field is "DRAFT – pending review by ≥ 3 midwifery/obstetric experts". **The thresholds must be reviewed by clinicians before the evaluation study.**

## 4. Controls

**Quest 3 (hand tracking):**
- *Pinch* picks up clamps and scissors; closing the scissors cuts.
- *Grip* picks up the towel and holds the baby.
- *Poke or pinch* presses world-space UI buttons.
- If hand tracking is lost, controllers take over automatically.

**Desktop simulator (Play mode without a headset):**

| Input | Action |
|---|---|
| Mouse | Move the active hand |
| Scroll | Hand depth |
| Tab | Switch hand |
| LMB | Pinch |
| RMB | Grip |
| Q/E | Roll the hand |
| R/F | Pitch the hand |
| Z/X | Yaw the hand |
| Space | Freeze both hands |
| WASD / arrow keys / middle-drag | Move and look |
| Ctrl+LMB | Press a UI button |

**Educator shortcuts:**

| Key | Action |
|---|---|
| P | Encourage push |
| O | Pant |
| Enter | Pause |
| Backspace | Restart the step |
| N | Skip to the next step (debug) |
| M | Start/stop expert motion capture of the current step |

### Assisted interaction (v5)
- **Cutting the cord:** pick up the scissors (pinch) and bring the blades onto the cord between the clamps. The blades close by themselves in 0.3 s and the cord is cut there. Pull away before then to cancel.
- **Clamping:** hold an open clamp on the cord for about 0.35 s and it closes there. You can also let go of it on the cord.
- **Hand auto-fit:** at crowning, the nuchal-cord check, the shoulders, body delivery and skin-to-skin, a hand brought within about 12 cm of its correct place snaps into the correct supporting pose and follows the baby. Move it more than about 16 cm away to free it.
  - Turn this off for assessments with `"handAutoFit": false` in `nd_session_config.json`.
- **Hands:** generated hands (desktop simulator, Quest controllers, guide hands) had their thumbs on the wrong side, so they looked mirrored. This is fixed. In the simulator the hands now start palms-down with the fingers forward, so you see the backs of the gloves.
  - The guide hands for body delivery and skin-to-skin now cradle the baby from below with the palms up. Before, the palms faced away from the baby.

## 5. Mixed reality with the silicone doll

1. On the educator panel, set **Mixed reality: On** and choose how the virtual mother is shown:
   - **Visible**: full overlay;
   - **Occluder**: invisible, but hides virtual objects behind the doll and keeps the real doll visible;
   - **Hidden**.
2. Press **Calibrate mannequin**. Pinch, in order, on the doll's:
   - **introitus**;
   - **left ASIS** (iliac crest point);
   - **right ASIS**.

   A yaw-only rigid fit aligns the virtual station to the doll. The result is saved in PlayerPrefs and, with AR Foundation, as a spatial anchor.
3. In MR the ward and the bed are hidden. Passthrough shows the real room, doll and bed.

   The virtual baby, cord, hands and feedback are still rendered. If the doll's own baby is used, the virtual baby can be hidden: set `showVirtualBabyInMR` in the config.

The baby's scale follows `babyLengthCm` (default 47 cm). Match it to the doll's baby.

## 6. Proposal requirements

| Requirement | How it is implemented |
|---|---|
| **FR1** Hand tracking vs. landmark zones | `HandTrackingSource` (XR Hands → controllers → simulator), `LandmarkZone`, `HandPlacementEvaluator` |
| **FR2** Traction direction and magnitude | `HandPlacementEvaluator.EvaluationFrame`: traction vector in the canal frame, angle from the fetal spine axis, lateral component |
| **FR3** Fault taxonomy and exact interval | `FaultClassifier` plus the JSON rules; each `FaultEvent` has start and end times |
| **FR4** Clinical consequence on the baby or mother | `ConsequenceEngine` covers 12 consequence types: perineal tear with bleeding, Erb's palsy (limp arm), clavicle fracture, cervical injury, facial bruising, hypoxia (cyanosis, limp, silent), fetal bradycardia on the CTG, shoulder dystocia (impacted-shoulder marker), haemorrhage, near-drop, umbilical injury and anaemia (lost placental transfusion marker) |
| **FR5** Third-person replay with annotation | `MotionRecorder` (40 s ring buffer, 60 Hz, 26 joints per hand plus the newborn pose) and `ReflectiveReplay`: third-person camera on a world-space panel, replayed ghost hands, fault label, complication and explanation |
| **FR6** Mandatory re-practice | The Director's reflective loop restores the step checkpoint; progress is blocked until the step is completed without the fault |
| **FR7** Educator configuration screen | `EducatorPanel`: difficulty, censorship, dominant hand, guided ghost, ablation, nuchal-cord scenario, MR, mother render, speed, posture, student code |
| **FR8** Censorship, at least 3 levels | `CensorshipFilter`: 4 levels (Full / Moderate / Clinical / Minimal). Graded blood and gore attenuation, privacy veil and desaturation (audio hooks remain but sound is off in v4) |
| **FR9** Telemetry | `SessionStore`: per-step attempts, times, time to correct placement, faults, plus a CSV of the trajectory |
| **FR10** End-of-session summary | `SummaryPanel`: faults, re-practice attempts, scores, competency (Competent / Developing / Novice) |
| **FR11** Export in the common result format | `SessionResult` (`schema: J26-IM-304/session-result/1`) via `IEvaluationService`. The default `FileEvaluationService` writes `<persistentDataPath>/NeonatalDelivery/Results/*.json`. Swap in the shared platform service. |
| **FR12** Guided ghost-hand mode | `GhostHandGuide`: an animated ghost demonstration before each step, from the procedural pose library. Captured expert motion (`MocapLibrary`) replaces it when recorded. |
| Ablation mode (evaluation) | `ablationMode` disables consequences and replay (faults are still logged) for the controlled comparison |
| **NFR1** 72 fps | Baby 4k vertices, 2k/1k textures, ASTC, one realtime spot light plus the ward lights, no runtime cloth (the gown is skinned), meshes not CPU-readable. Enable *GPU Skinning* in Player Settings. **Profile it on the device.** |
| **NFR2** Feedback within 500 ms | The consequence is triggered in the same frame the fault fires. `feedbackLatencyMs` is stored per fault in the result JSON, and a warning is logged above 500 ms. |
| **NFR6** Session limit | `sessionLengthLimitMinutes` gives a rest prompt |
| **NFR7** Privacy | Pseudonymous student code only; no patient data. Educator authentication is provided by the shared platform service. |
| **NFR9** External rules | JSON taxonomy (see §3) |
| **NFR10** Accessibility | Left- or right-hand dominance (the classifier's dominant-hand rules and the ghost demonstration follow the setting); seated or standing (in seated mode the eye height is raised by 38 cm); subtitles for all spoken instructions |

**Motion capture:**
- Every session logs both palms (pose, zone, estimated force, grip) and the traction vector at 60 Hz to `Sessions/*_trajectory.csv`.
- **Expert capture:** an educator performs a step while you press **M** on a keyboard attached to the PC (Quest Link). Press **M** again to save. The 26-joint motion of both hands is stored in `<persistentDataPath>/NeonatalDelivery/Mocap/<Step>.json`, and from then on the guided ghost replays it instead of the procedural demonstration.
- Ship the captures to every headset by copying them to `Resources/NeonatalDelivery/Mocap/`.

## 7. Clinical references used

- WHO Recommendations: *Intrapartum care for a positive childbirth experience* (2018) and *Delayed umbilical cord clamping* (2014): clamp no earlier than 1 minute (1–3 min).
- WHO/HEAT, *Labour and Delivery Care* module: first clamp about two finger-widths (~3 cm) from the umbilicus, second clamp ~2 cm further, cut between.
- RCOG Green-top Guideline No. 42, *Shoulder Dystocia*; NHS Scotland shoulder dystocia guideline (2026): routine **axial** traction only; downward or excessive traction is inappropriate.
- RCOG Green-top Guideline No. 29 and the RCOG/RCM **OASI Care Bundle**: manual perineal protection at crowning.
- Supplied video reference on umbilical cord clamping and cutting.

## 8. Known limitations

- **The scene has not yet been opened in Unity on this machine.**
  - The C# was type-checked against the 6000.3.19f1 engine assemblies, TMP and uGUI, and against signature stubs of XR Hands, AR Foundation and the Input System (with and without the optional packages).
  - The shaders were compiled with DXC against the URP 17.3 includes.
  - Expect small fixes on the first import, for example light intensities and panel positions.
- **There is no audio** (removed on request in v4).
- The fault thresholds are literature-based **drafts** that need expert review (NFR3 agreement study).
- **Soft-tissue deformation of the birth canal is bone-driven,** not FEM. Tears are shown with a shader and decals, not geometry.

## 9. Folder map

```
Assets/NeonatalDelivery/
  Art/Models        ND_Mother, ND_Baby, ND_DeliveryBed, ND_CordClamp, ND_CordScissors, ND_Towel, ND_DeliveryTray (.fbx)
  Art/Textures      Mother / Baby / Gown
  Art/Shaders       ND_Skin, ND_Ghost, ND_Veil, ND_Occluder
  Art/Materials     (generated by Build Everything)
  Runtime/          NeonatalDelivery.Runtime.asmdef. Core, Rig, Interaction, Cord, Clinical, Feedback, Data, UI, MR, Audio
  Editor/           NeonatalDelivery.Editor.asmdef (menus, importer rules, material and scene builder), XR/ (OpenXR configurator)
  Tests/Editor      EditMode tests (rules JSON, classifier timing, rigid alignment, rubric)
  Resources/NeonatalDelivery   fallback rules and config
  Scenes/, Prefabs/ (generated)
Assets/StreamingAssets/NeonatalDelivery/   nd_fault_rules.json, nd_session_config.json
Blender Source/NeonatalDelivery/           rigging, animation and export scripts plus .blend sources
```
