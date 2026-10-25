J26-IM-304 Neonatal Delivery - NEW BABY + CLOTH + AUDIO UPDATE (24 October 2026)
=============================================================================
This builds on the prior fixed-project ZIP; prior hand, facial and scissor script changes remain.

WHAT IS INCLUDED
----------------
- Original GLB supplied by user: Assets/NeonatalDelivery/Art/Models/ND_NewBaby_Original.glb.
- OBJ UV-preserving render mesh extracted from GLB for Unity's native model importer:
  Assets/NeonatalDelivery/Art/Models/ND_NewBaby_Source.obj.
- All SIX supplied image maps (color, normal, AO, 2 veins, displacement) at:
  Assets/NeonatalDelivery/Art/Textures/NewBaby/. Color, normal and AO are configured
  automatically; veins/displacement are retained as reference/high-resolution authoring maps,
  rather than using an unsuitable realtime displacement effect on standalone Meta Quest.
- Existing ND_Baby.fbx (rig, delivery animations and clinical landmarks) is KEPT. Do not delete it!
- Added Editor -> NDNewBabyInstaller.cs: auto-transfer bone weights from old skinned mesh
  to new model (best-effort approximation). Original birth/calm/cry clips drive the new skin.
- Added Unity Cloth continuous collision and new torso/hip/thigh proxies. Still requires
  checking real cloth collision in Play Mode, because low-poly drape + moving patient can clip.
- More appropriate crisp tool UI/fetal-monitor foley, subtle room ambience, softer spatial
  voices. Foleys are procedural, NOT real recordings.
- Added Editor -> NDRealAudioInstaller.cs: one-click CC0 human recording download.

IMPORTANT: UPLOADED 'Baby new.glb' WAS NOT RIGGED
-------------------------------------------------
Inspection showed 1 mesh, 1 node, 0 skins, 0 armatures/joints and 0 animations.
The new mesh therefore uses an approximate automatic skin-weight transfer from the
ORIGINAL ND_Baby.fbx skeleton. It has not undergone manual artist-quality weight painting.
Potential distortion may occur particularly at elbows, fingers, shoulders, hips, knees
and in the body when changing birth poses. Inspect it carefully before a medical demo.

HOW TO APPLY WITH UNITY 6000.3.19f1
----------------------------------
1) Unzip/open this whole folder as the Unity project. Keep a copy of the old project.
2) Wait for Unity to import all assets and finish compiling scripts.
3) Open Assets/NeonatalDelivery/Scenes/ND_NeonatalDelivery.unity.
4) Menu: Neonatal Delivery > 4. Apply New Baby To Open Scene.
   This takes the provided OBJ, fits it to the original body renderer, transfers weights,
   creates ND_NewBaby_Skinned.asset, and keeps the old bone hierarchy. It also saves the scene.
5) Menu: Neonatal Delivery > 5. Download Real CC0 Voice Audio.
   Requires internet in Unity Editor. Replaces synthetic mother/baby voices with real
   recorded human breathing and newborn crying. Existing procedural foley remains offline.
   The mother breathing clip is a yoga breathing recording, NOT a labour-specific take.
6) If you instead need a full scene rebuild: Neonatal Delivery > 3. Build Everything.
   It automatically calls the new baby binding installer in the generated scene.
7) Test from Play Mode: emergence stages, limb deformation, chest handhold,
   cord clipping/scissors, cloth vs patient while moving, recorded newborn cry.
8) Quest 3 performance: reduce texture import resolutions if GPU memory is constrained.

SOURCE RECORDING LICENCES (downloaded only when menu 5 is clicked)
-----------------------------------------------------------------
* the_yura, 'Crying newborn baby.ogg', CC0 1.0
  https://commons.wikimedia.org/wiki/File:Crying_newborn_baby.ogg
* Lcporschen, 'Ujjayi.ogg' (actual breathing recording), CC0 1.0
  https://commons.wikimedia.org/wiki/File:Ujjayi.ogg
No recordings are downloaded silently; internet and permission are required.

VERIFICATION STATUS
-------------------
Assets, code, model data and archive structure inspected; current machine has no Unity
Editor nor Blender executable so generated skinned mesh/cloth cannot be visually
validated or run on Quest. Full in-Unity import/play test must still be performed.
This is a build-ready source project with an installer, NOT a verified production scene.
