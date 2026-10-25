# J26-IM-304 Baby Delivery — targeted fixes (24 October 2026)

These are *script-level fixes* to the uploaded Unity project, not modified FBX meshes or new baked animations.

## Changed files
- `Assets/NeonatalDelivery/Runtime/Interaction/CordScissorsTool.cs`: while the scissors are held and the session is in `CutCord`, blades automatically open near the cord, then close and cut only **between two attached clamps**. `autoCutAtCord`, `autoCutRange` (default 2.5 cm), `approachDwell` (0.18 s) and `closeDuration` (0.36 s) are adjustable in Inspector. The existing manual pinch-cut behavior is retained as a fallback.
- `Assets/NeonatalDelivery/Runtime/Core/DeliveryDirector.cs`: connects clamps to the scissors; improves two-hand support placement and prevents baby rotation inversion when the trainee's hands swap/cross. One-hand support no longer rotates the baby around the glove's finger axis.
- `Assets/NeonatalDelivery/Runtime/Rig/BabyController.cs`: smoother birth-clip scrubbing, gentle emerging/newborn limb motion, continuity when changing from delivery to held posture, restore initial model transform on restart.
- `Assets/NeonatalDelivery/Runtime/Interaction/GloveHandVisual.cs`: smooth, anatomically mirrored, **visual-only** glove auto-fit as hands approach newborn support zones. Limits correction to 2.5 cm by default and does not interfere with holding instruments, tracked positions, fault evaluation or recorded measurements.
- `Assets/NeonatalDelivery/Runtime/Rig/MotherController.cs`: slower natural facial-morph transitions, limited jaw/eye travel, gentler expression strengths and subtle post-delivery belly change.
- `Assets/NeonatalDelivery/Tests/Editor/NeonatalDeliveryTests.cs`: EditMode check for mirrored procedural hands and palm normals.

## Installation
1. Make a backup or git commit before replacing files. Extract the **fixes-only** zip directly into the existing Unity project root, merging its `Assets` folder.
2. Open the project in Unity **6000.3.19f1**, and open `Assets/NeonatalDelivery/Scenes/ND_NeonatalDelivery.unity`.
3. Allow script compilation and check the Console. These changes are runtime components and **do not require `Build Everything`**, which may overwrite scene adjustments.
4. Test with Play Mode first, then Quest 3 hand tracking and controllers. This environment could not run Unity Editor or Quest hardware tests.

## Smoke tests
1. Restart a scenario; baby should return to the birth path's original position and move smoothly with delivery progress.
2. Advance to body delivery, support baby with both hands; switch or cross hands near head/back and confirm the baby's body doesn't flip. Near the baby, the **visual glove** should gently cradle the relevant support marker rather than twist or jump.
3. During pushes/pulls, ensure mother's facial expression changes gradually without eye/jaw overextension.
4. At cord cutting, **attach both cord clamps first**. Pick up the scissors and bring the blade tip within ~2.5 cm of the cord between the clamps. Hold around 0.18 s: the blades should open/close and cut. Verify no automatic cut before both clamps, when outside the clamp gap, or while the tool is on its tray.
5. Run `Window > General > Test Runner > EditMode > NeonatalDelivery.Tests`.

## Important limits
- The scissor `tip` is currently auto-generated with a local offset of `(0.045, 0, 0)` meters. If the FBX tool geometry was modified, adjust `CutPoint` against the mesh to align actual blade contact.
- These fixes improve runtime continuity, not the source FBX animation's keyframe quality, inverse kinematics or an independently authored hand grab animation. Fine-tune in the Unity editor based on Quest 3 testing.
- This is a training simulation. Clinical assessment criteria must be reviewed by an appropriately qualified expert before any educational validation.
