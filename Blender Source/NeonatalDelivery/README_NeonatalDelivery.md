# Neonatal Delivery – Blender sources (IT23152632)

These are the final, rigged and animated sources for the files in `Assets/NeonatalDelivery/Art/Models`.

| File | Contents |
|---|---|
| `ND_Mother_Rigged.blend` | Mother. The `ND_MotherRig` armature has 79 bones, including the birth-canal ring bones (`Introitus_00..07`, `CanalA/B_00..05`, `Perineum`, `BirthAnchor`). It includes the 23 renamed `ND_*` facial morphs, the actions `ND_M_*` and the simulated green drape. |
| `ND_Baby_Rigged.blend` | Newborn. The `ND_BabyRig` armature has 28 bones and the actions `ND_B_Birth`, `Calm`, `Cry`, `Limp` and `ErbsPalsy`. Landmark empties are named `ND_Z_*`. |
| `ND_DeliveryProps.blend` | Delivery bed (lithotomy, Goepel troughs), cord clamp, cord scissors, towel and tray. The shared `M_*` materials are used through `scripts/nd_otlib.py`, which is a credited copy of the ward's `otlib.py`. |

## Re-running the pipeline (Blender 4.2+ / 5.x, or the `bpy` module)

1. Put the original models in `input/`: `LadyWITH_CLOTHING_3.glb` and `Baby_model.glb`.
2. Run each script with `blender -b -P scripts/<name>.py`, or with `python scripts/<name>.py` when using the `bpy` module.
   - By default the paths are relative to this folder. Override them with the environment variables `ND_WORK` and `ND_OUT` (the export target).

The scripts run in this order:

| # | Script(s) | What it does |
|---|---|---|
| 1 | `m1_prep` | Scale the mother to 1.62 m, merge seams, identify the morphs |
| 2 | `m2_landmarks`, `m2_armature`, `m2_skin` | Build the skeleton from anatomical landmarks, then skin it with bone heat |
| 3 | `m3_canal` | Cut the introitus and extrude the canal sleeve. Adds the ring bones with geodesic skin falloff. |
| 4 | `b1_prep`, `b2_rig` | Decimate and rig the newborn |
| 5 | `b3_birthtest` | Settle the lithotomy pose on the bed (writes `blend/settle.json`) and run a birth-path test |
| 6 | `legs_dump` | Write the leg-trough fitting data |
| 7 | `m4_bake`, `b4_bake` | Bake the mother and baby clips. The mechanism of labour follows the curve of Carus. |
| 8 | `p1_props`, `p2b_drape` | Build the props and the drape cloth simulation (v2: lightweight physics drape with per-vertex freedom in the vertex colour; `p2_drape` was the original) |
| 9 | `x1_textures`, `x6_body_texture`, `x2_export` (baby, props), `x2b_export_mother` (mother) | Export the textures, material manifest and FBX files to `ND_OUT`. The mother FBX embeds its textures. |
| 10 | `x3_verify`, `x4_markcheck`, `v1_compose` | Run the checks and the preview renders |

Run `gen_audio.py` and `gen_rules.py` with plain Python 3 (numpy) to regenerate the placeholder audio and the fault-rule JSON.

## Cycles stills
`scripts/c1_cycles.py` renders path-traced figures for the report or a presentation: crowning, head born, shoulders and birth. They are set in the team's OT ward (`Blender Source/MR_OperatingTheatre_Ward.blend`). Run it with `blender -b -P scripts/c1_cycles.py`; the output goes to `renders_cycles/`. You can set these environment variables:

| Variable | Meaning |
|---|---|
| `ND_RES` | Resolution scale (1.0 = 1600×900) |
| `ND_SAMPLES` | Sample count |
| `ND_EXP` | Exposure |
| `ND_ONLY` | Render only the shot whose name contains this text |

## v2 model update
`LadyWITH_CLOTHING_3_new.glb` (the model with the old cloth removed) has exactly the same geometry and UVs as the original, so the rig, canal, morphs and animations carry over unchanged. Two things changed:

- The new drape comes from `p2b_drape`.
- The torso texture is now your new one. It is tone-matched to the head, arm and leg maps so the seams don't show; your untouched originals are in `textures_user/`.
