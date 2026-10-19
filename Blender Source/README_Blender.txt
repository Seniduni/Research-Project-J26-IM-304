MR OPERATING THEATRE - BLENDER SOURCE FILES
Author: H.M.H. Lakshan (IT23404564) - SLIIT J26-IM-304
=====================================================

Every 3D model in the operating theatre (room, doors, ceiling systems, surgical lights,
anaesthesia machine, monitors, trolleys, instruments...) is built in Blender from real-world
dimensions using the Python modelling scripts in the "Scripts" folder.

FILES
  MR_OperatingTheatre_Ward.blend   - the complete ward (all equipment placed, collections per zone,
                                     real lights at the modelled light positions)
  Equipment\<Asset>.blend           - each piece of equipment on its own, at the origin
  Instruments\<Instrument>.blend    - each surgical instrument on its own
  Scripts\                          - the modelling source code
        otlib.py                  helpers: materials, primitives, bevelled boxes, lathes, tubes, castors...
        assets_room.py            room shell, hermetic sliding door + foot sensors, swing doors,
                                  laminar flow canopy, twin LED surgical lights, ceiling pendants
        assets_anaesthesia.py     operating table, anaesthetic screen, anaesthesia machine, patient monitor,
                                  anaesthetic trolley, sharps bin, IV drip stand, infusion pumps
        assets_periphery.py       Mayo stand, instrument trolley, diathermy, suction, bins, crash cart,
                                  warmers, sterile cabinet, workstation, wall monitors, control panel,
                                  swab board, scrub sink, PPE trolley, fire safety ... patient mannequin
        assets_instruments.py     scalpel, scissors, forceps, needle holder, retractor, dishes, swabs
        build_ward.py             assembles the ward, exports FBX for Unity and saves the .blend files

REBUILD (Blender 4.2 or newer)
  1. Open Blender > Scripting workspace > Text > Open > Scripts\build_ward.py
  2. Press "Run Script" (Alt+P). The .blend files in this folder are regenerated.
     (Command line alternative: blender -b --factory-startup -P build_ward.py -- <output folder> --fbx)

The FBX files used by Unity are in Assets\MR_OperatingTheatre\Models of the Unity project.
