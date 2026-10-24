import os as _os
ND=_os.environ.get("ND_WORK", _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__))))  # Blender Source/NeonatalDelivery
ND_OUT=_os.environ.get("ND_OUT", ND+"/unity_out")  # where FBX/textures/audio/rules are exported
import json
SRC_DEL="WHO Managing Complications in Pregnancy and Childbirth (2nd ed., 2017) - normal vaginal delivery; WHO/HEAT labour & delivery care module"
SRC_OASI="RCOG Green-top Guideline No.29 (3rd/4th degree perineal tears); RCOG/RCM OASI Care Bundle - manual perineal protection"
SRC_SD="RCOG Green-top Guideline No.42 (shoulder dystocia); NHS Scotland shoulder dystocia guideline (2026): routine AXIAL traction only; downward/excessive traction inappropriate"
SRC_CORD="WHO guideline: delayed umbilical cord clamping (2014) - not earlier than 1 min; WHO/HEAT: first tie/clamp ~2 finger-widths (~3 cm), second ~5 cm, cut between"
A=["AnteriorShoulder","PosteriorShoulder"]
R=[]
def rule(**k):
    d=dict(hands="Any",minDuration=0.5,threshold=0,maxAngleDeg=40,expectedAxis="",progressMin=0,progressMax=1,minCm=0,maxCm=0,requireGrip=False,zones=[],requiresScenario="")
    d.update(k); R.append(d)
rule(code="F01_UNSUPPORTED_CRANIUM",name="Unsupported cranium",steps=["CrowningHeadControl"],detector="NoContactOnZones",zones=["Occiput","Vertex","ParietalLeft","ParietalRight"],
     minDuration=1.2,threshold=0.010,progressMin=0.36,progressMax=0.55,severity=0.8,consequence="PerinealTear",
     complication="Rapid, uncontrolled birth of the head: 2nd-3rd degree perineal tear (obstetric anal sphincter injury) and risk of neonatal intracranial haemorrhage from sudden decompression.",
     explanation="Without a hand on the occiput the head extends early and is expelled quickly, so a larger diameter distends the perineum abruptly instead of gradually.",
     remediation="Rest the flat fingers of your dominant hand on the occiput and apply gentle pressure to keep the head flexed; ask the mother to pant and let the head be born slowly between contractions.",
     source=SRC_DEL+"; "+SRC_OASI)
rule(code="F02_EXCESSIVE_HEAD_PRESSURE",name="Excessive pressure on the fetal head",steps=["CrowningHeadControl"],detector="ForceAbove",zones=["Occiput","Vertex","ParietalLeft","ParietalRight","Face"],
     minDuration=1.0,threshold=25,severity=0.6,consequence="FetalBradycardia",
     complication="Prolonged compression of the fetal skull: fetal heart rate decelerations and a delayed, obstructed birth of the head.",
     explanation="Controlling the speed of delivery needs only light pressure; pushing the head back works against the contraction and compresses the skull.",
     remediation="Use gentle, flat-finger pressure - enough to slow the head, never to hold it back.",source=SRC_DEL)
rule(code="F03_PERINEUM_UNSUPPORTED",name="Perineum not supported",steps=["CrowningHeadControl"],detector="NoContactOnZones",zones=["MaternalPerineum"],
     minDuration=2.0,threshold=0.008,progressMin=0.40,progressMax=0.55,severity=0.4,consequence="PerinealTear",
     complication="Unsupported perineum during crowning: increased risk of perineal tear.",
     explanation="Manual perineal protection with the other hand (thumb and index on either side of the perineum) reduces the stretch on the posterior fourchette as the head extends.",
     remediation="With your non-dominant hand hold a pad on the perineum below the introitus while the head is born.",source=SRC_OASI)
rule(code="F04_FACE_CONTACT",name="Pressure on the face / eyes",steps=["CrowningHeadControl","NuchalCordCheck","Restitution"],detector="ContactOnZones",zones=["Face"],
     minDuration=0.6,threshold=4,severity=0.4,consequence="FacialBruising",
     complication="Facial bruising and risk of ocular injury.",explanation="The face is born last by extension; pressure should be applied only to the bony occiput and parietal bones.",
     remediation="Keep your fingers on the occiput / parietal bones, away from the eyes and face.",source=SRC_DEL)
rule(code="F05_NUCHAL_NOT_CHECKED",name="Neck not checked for a nuchal cord",steps=["NuchalCordCheck"],detector="StepSkipped",minDuration=0,severity=0.5,consequence="Hypoxia",requiresScenario="NuchalCord",
     complication="An undetected tight nuchal cord can tighten as the body is born: cord compression, hypoxia and a floppy, cyanosed newborn.",
     explanation="After the head is born you must feel around the neck; a loose loop is slipped over the head, a tight one is managed before delivery continues.",
     remediation="Slide two fingers along the baby's neck to feel for a loop of cord before the next contraction.",source=SRC_DEL)
rule(code="F06_PREMATURE_TRACTION",name="Traction before external rotation",steps=["Restitution"],detector="TractionBeforeProgress",threshold=10,minDuration=0.6,severity=0.6,consequence="ShoulderDystocia",
     complication="Pulling before the shoulders have rotated into the antero-posterior diameter: anterior shoulder impaction (shoulder dystocia).",
     explanation="Restitution and external rotation are spontaneous; traction before they complete drags the bisacromial diameter against the pubic arch.",
     remediation="Keep your hands still and only support the head until it has rotated to face the mother's thigh.",source=SRC_SD)
rule(code="F07_WRONG_TRACTION_AXIS",name="Wrong axis of traction",steps=["AnteriorShoulder"],detector="TractionAxis",expectedAxis="Axial",maxAngleDeg=35,threshold=6,minDuration=0.4,severity=0.8,consequence="ErbsPalsy",
     complication="Brachial plexus stretch injury (Erb's palsy, C5-C6): arm held in the 'waiter's tip' position.",
     explanation="Traction that is not in line with the fetal spine laterally flexes the neck and stretches the upper roots of the brachial plexus.",
     remediation="Hold the head on both parietal bones and apply gentle AXIAL traction in line with the fetal spine (no downward or sideways pull).",source=SRC_SD)
rule(code="F08_EXCESSIVE_TRACTION",name="Excessive traction force",steps=A+["BodyDelivery"],detector="ForceAbove",threshold=40,minDuration=0.3,severity=0.9,consequence="ErbsPalsy",
     complication="Excessive traction: brachial plexus injury and clavicle fracture risk.",
     explanation="The shoulders are delivered by the contraction and maternal effort; traction should be gentle and only guide the direction.",
     remediation="Reduce your pull to gentle guidance and wait for the next contraction.",source=SRC_SD)
rule(code="F09_NECK_GRIP",name="Pulling on the neck",steps=A+["BodyDelivery"],detector="ContactOnZones",zones=["Neck"],requireGrip=True,threshold=5,minDuration=0.5,severity=0.7,consequence="CervicalInjury",
     complication="Traction transmitted through the neck: cervical spine and carotid compression injury.",
     explanation="The neck must never be gripped; the head is held over the parietal bones (biparietal hold).",
     remediation="Move your hands up onto the parietal bones on either side of the head.",source=SRC_DEL)
rule(code="F10_LATERAL_FLEXION",name="Excessive lateral flexion of the neck",steps=A,detector="LateralForceAbove",threshold=12,minDuration=0.4,severity=0.7,consequence="ErbsPalsy",
     complication="Lateral flexion of the neck stretches the brachial plexus on the opposite side (Erb's palsy).",
     explanation="Sideways force bends the cervical spine away from the shoulder and over-stretches C5-C6.",
     remediation="Keep the head in line with the spine - pull along the axis, never sideways.",source=SRC_SD)
rule(code="F11_AXILLA_HOOK",name="Finger hooked in the axilla",steps=A+["BodyDelivery"],detector="ContactOnZones",zones=["Axilla"],requireGrip=True,threshold=6,minDuration=0.4,severity=0.6,consequence="ErbsPalsy",
     complication="Axillary traction: brachial plexus and humerus injury.",explanation="Pulling on the arm pit concentrates force on the plexus and humerus.",
     remediation="Support the body along the back and buttocks instead of pulling under the arm.",source=SRC_SD)
rule(code="F12_POSTERIOR_SHOULDER_AXIS",name="Posterior shoulder delivered in the wrong direction",steps=["PosteriorShoulder"],detector="TractionAxis",expectedAxis="Anterior",maxAngleDeg=55,threshold=6,minDuration=0.5,severity=0.5,consequence="PerinealTear",
     complication="Uncontrolled posterior shoulder: perineal tear extension.",
     explanation="Once the anterior shoulder is free the posterior shoulder follows the curve of the birth canal upward; pulling downward drags it across the perineum.",
     remediation="Guide the head gently upward (anteriorly), following the curve, while watching the perineum.",source=SRC_DEL+"; "+SRC_OASI)
rule(code="F13_PREMATURE_RELEASE",name="Premature release during birth of the body",steps=["BodyDelivery"],detector="ReleaseDuring",minDuration=0.8,progressMin=0.88,severity=0.9,consequence="NearDrop",
     complication="The slippery newborn is unsupported as the body is born and can slip or fall.",
     explanation="The body delivers quickly by lateral flexion; one hand must support the head/neck and the other slide along the back to the buttocks.",
     remediation="Keep one hand under the head and neck and slide the other along the back to grasp the buttocks/legs as the body is born.",source=SRC_DEL)
rule(code="F14_ONE_HANDED_HOLD",name="Newborn held with one hand",steps=["SkinToSkin","DryAndStimulate","DelayedCordClamping","ClampCord","CutCord"],detector="ReleaseDuring",minDuration=0,severity=0.5,consequence="NearDrop",
     complication="Unsupported head and neck: risk of a fall and of neck hyperextension.",explanation="A newborn has no head control; always use two hands until it is on the mother's chest.",
     remediation="Hold the baby with both hands - one under the head/neck, one under the buttocks.",source=SRC_DEL)
rule(code="F15_BABY_DROPPED",name="Newborn released away from the mother",steps=["SkinToSkin","DryAndStimulate","DelayedCordClamping","ClampCord","CutCord"],detector="BabyDropped",minDuration=0,severity=1.0,consequence="NearDrop",
     complication="Newborn fall: head injury, fractures.",explanation="Release the baby only once it is resting securely on the mother's chest.",
     remediation="Carry the baby with both hands and place it prone on the mother's chest before letting go.",source=SRC_DEL)
rule(code="F16_EARLY_CLAMPING",name="Early cord clamping",steps=["SkinToSkin","DryAndStimulate","DelayedCordClamping"],detector="EarlyClamping",minDuration=0,severity=0.4,consequence="Anaemia",
     complication="Placental transfusion lost: lower blood volume and iron stores in infancy.",
     explanation="WHO recommends clamping no earlier than 1 minute after birth (unless resuscitation is needed); the cord transfuses blood while it pulsates.",
     remediation="Wait at least one minute (until the cord stops pulsating) before applying the clamps.",source=SRC_CORD)
rule(code="F17_CLAMP_FIRST_POSITION",name="First clamp misplaced",steps=["ClampCord","CutCord","DelayedCordClamping","DryAndStimulate"],detector="ClampPosition",minCm=2.0,maxCm=4.0,minDuration=0,severity=0.5,consequence="UmbilicalInjury",
     complication="Clamp too close to the skin risks catching the umbilical skin or a small umbilical hernia; too far leaves a long stump that bleeds and becomes infected.",
     explanation="The first clamp goes about two finger-widths (~3 cm) from the abdomen.",remediation="Place the first clamp about 3 cm from the umbilicus.",source=SRC_CORD)
rule(code="F18_CLAMP_SECOND_POSITION",name="Second clamp misplaced",steps=["ClampCord","CutCord","DelayedCordClamping","DryAndStimulate"],detector="ClampPosition",minCm=4.0,maxCm=7.5,minDuration=0,severity=0.3,consequence="Haemorrhage",
     complication="Second clamp misplaced: uncontrolled bleeding from the cut placental end.",explanation="The second clamp sits ~5 cm from the abdomen so the cord can be cut safely between the clamps.",
     remediation="Place the second clamp about 2 cm beyond the first (~5 cm from the abdomen).",source=SRC_CORD)
rule(code="F19_CLAMP_SPACING",name="Clamps too close / too far apart",steps=["ClampCord","CutCord","DelayedCordClamping","DryAndStimulate"],detector="ClampPosition",minCm=1.0,maxCm=4.0,minDuration=0,severity=0.3,consequence="UmbilicalInjury",
     complication="Insufficient space to cut between the clamps safely.",explanation="Leave about 2 cm between the clamps.",remediation="Re-site the clamps about 2 cm apart.",source=SRC_CORD)
rule(code="F20_CUT_WITHOUT_CLAMPS",name="Cord cut without two clamps",steps=["SkinToSkin","DryAndStimulate","DelayedCordClamping","ClampCord","CutCord"],detector="CutWithoutClamps",minDuration=0,severity=1.0,consequence="Haemorrhage",
     complication="Neonatal haemorrhage from the unclamped umbilical vessels.",explanation="Both sides of the cut must be clamped before cutting.",
     remediation="Apply two clamps first, then cut between them.",source=SRC_CORD)
rule(code="F21_CUT_OUTSIDE_CLAMPS",name="Cut outside the clamps",steps=["CutCord","ClampCord"],detector="CutOutsideClamps",minDuration=0,severity=0.9,consequence="Haemorrhage",
     complication="Cutting on the baby's side of the first clamp causes neonatal bleeding; beyond the second clamp, bleeding from the placental end.",
     explanation="The only safe place to cut is between the two clamps.",remediation="Position the scissor blades between the clamps, shield with your other hand, then cut.",source=SRC_CORD)
doc={"version":"1.0.0","status":"DRAFT - for content validation by >= 3 practising midwifery/obstetric experts (Section 3.5)",
     "reviewers":[],"lastReviewed":"","notes":"Thresholds (N, s, cm, progress-rate) are initial estimates to be calibrated against the expert-annotated reference recordings (Section 3.2). Edit freely; place an edited copy in persistentDataPath/NeonatalDelivery/ to override without rebuilding.",
     "rules":R}
txt=json.dumps(doc,indent=2,ensure_ascii=False)
for p in [ND_OUT+"/StreamingAssets/NeonatalDelivery/nd_fault_rules.json",ND_OUT+"/Resources/NeonatalDelivery/nd_fault_rules.json"]:
    open(p,"w").write(txt)
cfg={"studentCode":"STUDENT-001","educatorCode":"","difficulty":0,"censorship":1,"dominantHand":1,"guidedGhostHands":True,"ablationMode":False,"scenarioNuchalCord":False,
     "mixedReality":False,"mrMotherRender":1,"showVirtualBabyInMR":True,"timeScale":0.5,"delayedClampingSeconds":60,"sessionLengthLimitMinutes":30,"babyLengthCm":47,
     "seated":False,"subtitles":True,"masterVolume":1.0}
for p in [ND_OUT+"/StreamingAssets/NeonatalDelivery/nd_session_config.json",ND_OUT+"/Resources/NeonatalDelivery/nd_session_config.json"]:
    json.dump(cfg,open(p,"w"),indent=2)
print(len(R),"rules")
