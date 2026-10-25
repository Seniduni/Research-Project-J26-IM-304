Neonatal Delivery patch notes (2026-10-24)

1. Mother face animation disabled by default.
   - Body animation remains.
   - Facial morphs, eye aim and jaw motion are kept neutral.

2. Green ICU drape stabilized.
   - DrapeCloth now defaults to simulateCloth = false.
   - The green drape stays skinned to the mother instead of using unstable runtime cloth physics.

3. New baby mesh integration improved.
   - The installer now transfers bone weights using blended nearby weights instead of copying just one nearest vertex.
   - This reduces harsh deformations and baby mesh glitches.
   - Re-run: Neonatal Delivery > 4. Apply New Baby To Open Scene

4. OpenXR and Unity AI warnings.
   - These warnings are external/editor warnings and not delivery-scene logic bugs.
