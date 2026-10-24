// Neonatal Delivery & Hand-Placement Training Module - session orchestrator.
// Owns the clinical step state machine, labour/progress dynamics, evaluation -> classification -> consequence -> reflective
// replay -> enforced re-practice loop, censorship, guided ghost hands, telemetry and the session result.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    [DefaultExecutionOrder(100)]
    public class DeliveryDirector : MonoBehaviour
    {
        public static DeliveryDirector Instance { get; private set; }

        [Header("Station & rigs")]
        public Transform station;
        public Transform babyPathFrame;
        public MotherController mother;
        public BirthCanalDeformer canal;
        public BabyController baby;
        public UmbilicalCord cord;
        public Transform perineumZone;
        public Transform skinToSkinAnchor;
        public Transform traineeSpawn;
        public GameObject environmentRoot;
        public GameObject deliveryBed;
        [Header("Interaction")]
        public HandTrackingSource hands;
        public HandPlacementEvaluator evaluator;
        public GloveHandVisual leftGlove, rightGlove;
        public CordClampTool clampA, clampB;
        public CordScissorsTool scissors;
        public TowelTool towel;
        [Header("Feedback")]
        public ConsequenceEngine consequences;
        public CueSystem cues;
        public ReflectiveReplay replay;
        public CensorshipFilter censorship;
        public GhostHandGuide ghost;
        public NDAudio audioSys;
        [Header("XR / MR")]
        public NDXRRig rig;
        public PassthroughController passthrough;
        public MannequinCalibration calibration;
        [Header("UI")]
        public EducatorPanel educatorPanel;
        public InstructionPanel instructionPanel;
        public SummaryPanel summaryPanel;
        [Header("Options")]
        public bool skipEducatorPanel;
        public float floorY;

        public SessionConfig Config { get; private set; }
        public DeliveryStep Step { get; private set; } = DeliveryStep.Briefing;
        public float Progress => _progress;
        public float SessionTime => _time;
        public bool Paused => _paused;
        public ContractionModel Labour { get; } = new ContractionModel();

        FaultRuleSet _rules; FaultClassifier _classifier; readonly ScoringRubric _rubric = new ScoringRubric(); readonly SessionStore _store = new SessionStore();
        MotionRecorder _recorder = new MotionRecorder(40f, 60f);
        float _progress, _maxProgress, _rate, _time, _stepStart, _bornTime = -1f, _holdOneHand, _lastFaultTime = -10f, _placementSince = -1f;
        bool _ready, _paused, _sessionRunning, _correctLogged, _nuchalPalpated, _nuchalPrompted, _gushDone, _awaitingRetry;
        int _contractionsInStep;
        LandmarkZone _perineumZone;
        struct Checkpoint { public DeliveryStep step; public float progress, maxProgress; public BabyMode mode; public Vector3 babyPos; public Quaternion babyRot; public float wet, cyan; }
        Checkpoint _cp;

        void Awake() { Instance = this; if (NDTheme.Font == null) NDTheme.Font = TMPro.TMP_Settings.defaultFontAsset; }

        IEnumerator Start()
        {
            Config = SessionConfig.LoadOrDefault();
            yield return FaultRuleSet.Load(r => _rules = r);
            _classifier = new FaultClassifier(_rules) { OnFault = OnFault, dominant = Config.dominantHand };
            InitRigs();
            CalibrateLights();
            BuildUI();
            _ready = true;
            if (skipEducatorPanel) BeginSession(Config);
            else ShowEducator();
        }

        // ------------------------------------------------------------------ initialisation
        void InitRigs()
        {
            if (rig != null) rig.EnsureBuilt();
            if (mother != null) { mother.Init(station); mother.lookTarget = rig != null ? rig.cam.transform : null; }
            if (canal != null && mother != null) canal.Init(mother.transform);
            if (baby != null)
            {
                baby.Init(Config.babyLengthCm / 47f);
                if (canal != null) baby.RegisterProbes(canal);
            }
            if (evaluator != null)
            {
                evaluator.ClearZones(); evaluator.canal = canal; evaluator.baby = baby;
                if (baby != null) { foreach (var z in baby.zones) evaluator.Register(z); evaluator.babyReference = baby.HeadCenter; }
                if (perineumZone == null) { perineumZone = new GameObject("ND_Zone_MaternalPerineum").transform; perineumZone.SetParent(station, false); }
                _perineumZone = NDUtil.GetOrAdd<LandmarkZone>(perineumZone.gameObject);
                _perineumZone.id = LandmarkZoneId.MaternalPerineum; _perineumZone.radius = 0.035f; _perineumZone.isBaby = false;
                evaluator.Register(_perineumZone);
            }
            if (cord != null)
            {
                if (baby != null) cord.babyEnd = baby.Navel;
                cord.canal = canal;
                cord.Build();
            }
            if (cues != null) { cues.evaluator = evaluator; cues.head = rig != null ? rig.cam : Camera.main; }
            if (consequences != null) { consequences.baby = baby; consequences.mother = mother; consequences.canal = canal; consequences.cord = cord; consequences.cues = cues; consequences.censor = censorship; consequences.contractions = Labour; }
            if (replay != null) { replay.baby = baby; if (replay.viewTarget == null) replay.viewTarget = perineumZone; replay.Build(NDTheme.Font); }
            if (ghost != null) { ghost.baby = baby; ghost.canal = canal; }
            if (leftGlove != null) leftGlove.Bind(hands.Left);
            if (rightGlove != null) rightGlove.Bind(hands.Right);
            if (clampA != null) { clampA.cord = cord; clampA.Clamped += OnClamped; }
            if (clampB != null) { clampB.cord = cord; clampB.Clamped += OnClamped; }
            if (scissors != null) { scissors.cord = cord; scissors.Cut += OnCut; }
            if (towel != null) towel.baby = baby;
            if (censorship != null) { censorship.baby = baby; censorship.canal = canal; censorship.audioSys = audioSys; }
            if (audioSys != null && mother != null)
            {
                var voice = RigUtil.FindDeep(mother.transform, "ND_Anchor_Voice");
                if (audioSys.motherVoice == null) { audioSys.motherVoice = NDAudio.MakeSource(voice != null ? voice : mother.transform, "MotherVoice", true); audioSys.motherLowPass = audioSys.motherVoice.gameObject.AddComponent<AudioLowPassFilter>(); }
                if (audioSys.babyVoice == null && baby != null) audioSys.babyVoice = NDAudio.MakeSource(baby.HeadCenter != null ? baby.HeadCenter : baby.transform, "BabyVoice", true);
                if (audioSys.monitor == null) audioSys.monitor = NDAudio.MakeSource(station, "FetalMonitor", true);
                if (audioSys.ui == null) audioSys.ui = NDAudio.MakeSource(transform, "UI", false);
            }
            if (calibration != null)
            {
                calibration.station = station; calibration.hands = hands; calibration.trackingSpace = rig != null ? rig.trackingSpace : null;
                if (mother != null)
                {
                    calibration.virtualIntroitus = RigUtil.FindDeep(mother.transform, "ND_Z_Introitus");
                    calibration.virtualAsisL = RigUtil.FindDeep(mother.transform, "ND_Z_ASISL");
                    calibration.virtualAsisR = RigUtil.FindDeep(mother.transform, "ND_Z_ASISR");
                }
                calibration.OnInstruction = s => Say(s);
            }
            Labour.OnContractionStart = n => { _contractionsInStep++; if (Step == DeliveryStep.SecondStagePushing || Step == DeliveryStep.CrowningHeadControl) { if (audioSys != null) audioSys.Mother("mother_push_strain", 1f); } };
            ResetDeliveryState();
            PlaceTrainee();
        }

        [Header("Lighting at the perineum (URP intensity = target x distance^2)")]
        public float surgicalLightTarget = 0.65f;   // per ward surgical light (the ward spots are tuned for an empty table)
        public float deliveryLampTarget = 1.1f;     // own lamp (the only key light in mixed reality)

        // The ward's surgical spots (created by WardSetup at intensity 6/4.5) point at the table centre, where the mother now
        // lies at ~1 m distance - unscaled they overexpose skin and drape to white. Scale them for this scene only.
        void CalibrateLights()
        {
            Vector3 focus = perineumZone != null ? perineumZone.position : (station != null ? station.position + Vector3.up : Vector3.up);
            var intro = mother != null ? RigUtil.FindDeep(mother.transform, "ND_Z_Introitus") : null; if (intro != null) focus = intro.position;
            if (environmentRoot != null)
                foreach (var l in environmentRoot.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Spot && l.name.StartsWith("LIGHT_Surgical"))
                    {
                        float d2 = (l.transform.position - focus).sqrMagnitude;
                        l.intensity = Mathf.Min(l.intensity, surgicalLightTarget * d2 * (l.name.Contains("Main") ? 1f : 0.7f));
                        l.transform.rotation = Quaternion.LookRotation(focus - l.transform.position);
                    }
            var lamp = station != null ? station.Find("ND_DeliveryLamp") : null;
            var ll = lamp != null ? lamp.GetComponent<Light>() : null;
            if (ll != null) { ll.intensity = deliveryLampTarget * (ll.transform.position - focus).sqrMagnitude; ll.transform.rotation = Quaternion.LookRotation(focus - ll.transform.position); }
        }

        void BuildUI()
        {
            if (educatorPanel != null) { educatorPanel.OnStart = BeginSession; educatorPanel.OnCalibrate = () => { { if (calibration != null) calibration.Begin(); } }; }
            if (instructionPanel != null) { instructionPanel.Build(); instructionPanel.OnCommand = Command; }
            if (summaryPanel != null) { summaryPanel.OnRestart = () => { summaryPanel.panel.gameObject.SetActive(false); ResetDeliveryState(); ShowEducator(); }; summaryPanel.panel.gameObject.SetActive(false); }
            NDEvents.Instruction += t => { if (instructionPanel != null) instructionPanel.SetSubtitle(Config != null && Config.subtitles ? t : ""); };
        }

        void ShowEducator()
        {
            Step = DeliveryStep.Briefing; _sessionRunning = false;
            if (educatorPanel != null) { educatorPanel.panel.gameObject.SetActive(true); educatorPanel.Build(Config); }
            if (instructionPanel != null) instructionPanel.panel.gameObject.SetActive(false);
        }

        void PlaceTrainee()
        {
            if (rig == null || canal == null || !canal.Ready) return;
            Vector3 stand = traineeSpawn != null ? traineeSpawn.position : canal.IntroitusCenter + Flat(canal.OutwardAxis) * 0.6f;
            stand.y = floorY;
            rig.PlaceUser(stand, canal.IntroitusCenter);
        }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        // ------------------------------------------------------------------ session control
        public void BeginSession(SessionConfig cfg)
        {
            Config = cfg.Clone();
            _classifier.dominant = Config.dominantHand;
            if (ghost != null) ghost.dominant = Config.dominantHand;
            Labour.SetTimeScale(Config.timeScale);
            if (censorship != null) censorship.Apply(Config.censorship);
            if (rig != null) rig.seated = Config.seated;
            if (audioSys != null) audioSys.masterVolume = Config.masterVolume;
            if (passthrough != null) passthrough.SetMixedReality(Config.mixedReality, Config.mrMotherRender);
            if (baby != null) baby.SetVisible(!Config.mixedReality || Config.showVirtualBabyInMR);
            if (Config.mixedReality && calibration != null) calibration.Load();
            if (educatorPanel != null) educatorPanel.panel.gameObject.SetActive(false);
            if (instructionPanel != null) instructionPanel.panel.gameObject.SetActive(true);
            ResetDeliveryState();
            _store.Begin(Config);
            _time = 0f; _sessionRunning = true;
            GoTo(DeliveryStep.Preparation);
            ND.Log($"Session started: {Config.studentCode} {Config.difficulty} censorship={Config.censorship} ablation={Config.ablationMode} MR={Config.mixedReality}");
        }

        void ResetDeliveryState()
        {
            _progress = _maxProgress = 0f; _bornTime = -1f; _nuchalPalpated = _nuchalPrompted = _gushDone = false; _paused = false; _awaitingRetry = false;
            if (baby != null) { baby.SetMode(BabyMode.Delivering); baby.SetProgress(0); baby.wetness = 1f; baby.vernix = 0.7f; baby.blood = 0.35f; baby.cyanosis = 0.9f; baby.crying = 0; baby.limp = 0; baby.erbsPalsy = 0; baby.ConfigureClip(canal, true); }
            if (mother != null) { mother.SetPose(MotherPose.Rest); mother.SetDelivered(false); mother.pain = mother.strain = mother.relief = 0; }
            if (cord != null && cord.babyEnd != null) cord.ResetRope();
            { if (clampA != null) clampA.ResetTool(); } { if (clampB != null) clampB.ResetTool(); } { if (scissors != null) scissors.ResetTool(); } { if (towel != null) towel.ResetTool(); }
            { if (consequences != null) consequences.Clear(); }
            Labour.Bradycardia = false; Labour.Panting = false; Labour.Running = true;
            _recorder.Clear();
            { if (evaluator != null) evaluator.ResetEngagement(); }
        }

        void GoTo(DeliveryStep s)
        {
            var prev = Step; Step = s; _stepStart = _time; _contractionsInStep = 0; _correctLogged = false; _placementSince = -1f;
            _classifier.ResetForStep(); _classifier.attempt = _rubric.Attempts(s) + 1;
            _rubric.Attempt(s); _store.StepStarted(s, _time);
            SaveCheckpoint();
            if (ghost != null) ghost.Show(s, Config.guidedGhostHands && Config.difficulty == Difficulty.Guided);
            { if (cues != null) cues.HideArrows(); }
            { if (instructionPanel != null) instructionPanel.SetStep(s, InstructionFor(s)); }
            Say(ShortInstruction(s));
            NDEvents.RaiseStepChanged(prev, s);
            switch (s)
            {
                case DeliveryStep.SecondStagePushing: Labour.StartSoon(3f); break;
                case DeliveryStep.CrowningHeadControl: Labour.StartSoon(4f); break;
                case DeliveryStep.Restitution: Labour.StartSoon(5f); break;
                case DeliveryStep.SkinToSkin: { if (mother != null) mother.SetPose(MotherPose.Reach); } break;
                case DeliveryStep.Complete: Finish(); break;
            }
        }

        void CompleteStep(DeliveryStep next, float timeQuality = 1f)
        {
            float score = _rubric.StepScore(Step, timeQuality);
            _store.StepCompleted(Step, _time, score);
            NDEvents.RaiseStepCompleted(Step, score);
            { if (audioSys != null) audioSys.UI("step_ok", 0.5f); }
            GoTo(next);
        }

        void SaveCheckpoint()
        {
            _cp = new Checkpoint { step = Step, progress = _progress, maxProgress = _maxProgress, mode = baby != null ? baby.Mode : BabyMode.Delivering,
                babyPos = baby != null && baby.model != null ? baby.model.transform.position : Vector3.zero, babyRot = baby != null && baby.model != null ? baby.model.transform.rotation : Quaternion.identity,
                wet = baby != null ? baby.wetness : 1, cyan = baby != null ? baby.cyanosis : 1 };
        }

        void RestoreCheckpoint()
        {
            _progress = _cp.progress; _maxProgress = _cp.maxProgress;
            if (baby != null)
            {
                baby.SetMode(_cp.mode); baby.SetProgress(_cp.progress);
                if (_cp.mode != BabyMode.Delivering) baby.model.transform.SetPositionAndRotation(_cp.babyPos, _cp.babyRot);
                baby.wetness = _cp.wet; baby.cyanosis = _cp.cyan;
            }
            if (Step == DeliveryStep.ClampCord || Step == DeliveryStep.CutCord) { { if (clampA != null) clampA.ResetTool(); } { if (clampB != null) clampB.ResetTool(); } { if (scissors != null) scissors.ResetTool(); } if (cord != null && cord.IsCut) cord.ResetRope(); }
        }

        // ------------------------------------------------------------------ commands
        // Educator / desktop testing shortcuts (keyboard attached to the PC or Quest Link): P push, O pant, Enter pause,
        // Backspace restart, N skip to the next step (debug, logged as skipped), M start/stop expert motion capture.
        void DesktopShortcuts()
        {
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null) return;
            if (kb[UnityEngine.InputSystem.Key.P].wasPressedThisFrame) Command("push");
            if (kb[UnityEngine.InputSystem.Key.O].wasPressedThisFrame) Command("pant");
            if (kb[UnityEngine.InputSystem.Key.Backspace].wasPressedThisFrame) Command("restart");
            if (kb[UnityEngine.InputSystem.Key.N].wasPressedThisFrame) Command("next");
            if (kb[UnityEngine.InputSystem.Key.Enter].wasPressedThisFrame) Command("pause");
            if (kb[UnityEngine.InputSystem.Key.M].wasPressedThisFrame) Command("mocap");
#endif
        }

        public void Command(string c)
        {
            switch (c)
            {
                case "push": if (Step <= DeliveryStep.CrowningHeadControl) { Labour.Panting = false; { if (instructionPanel != null) instructionPanel.SetPanting(false); } Say("\"Push with the next contraction - chin to chest.\""); } break;
                case "pant": Labour.Panting = !Labour.Panting; { if (instructionPanel != null) instructionPanel.SetPanting(Labour.Panting); } Say(Labour.Panting ? "\"Stop pushing - pant, short breaths...\"" : "\"You can push again.\""); break;
                case "pause": _paused = !_paused; { if (instructionPanel != null) instructionPanel.SetPaused(_paused); } break;
                case "restart": ResetDeliveryState(); if (_sessionRunning) BeginSession(Config); break;
                case "next": DebugAdvance(); break;
                case "mocap":   // expert motion capture of the current step -> ghost reference (FR12)
                    if (ghost == null) break;
                    if (!ghost.Recording) { ghost.StartRecording(Step, string.IsNullOrEmpty(Config.educatorCode) ? "expert" : Config.educatorCode); Say("Recording expert hand motion for: " + ND.StepTitle(Step) + ". Press M again to save."); }
                    else { ghost.StopRecording(true); Say("Expert motion saved - it is now the ghost demonstration for this step."); }
                    break;
            }
        }

        void Say(string s) { NDEvents.RaiseInstruction(s); }

        // ------------------------------------------------------------------ frame loop (deterministic order)
        void LateUpdate()
        {
            if (!_ready) return;
            DesktopShortcuts();
            float dt = Time.deltaTime;
            bool sim = _sessionRunning && !_paused && !_awaitingRetry && (replay == null || !replay.Active);
            float sdt = sim ? dt : 0f;
            if (sim) _time += dt;

            Labour.Running = sim && Step >= DeliveryStep.SecondStagePushing && Step <= DeliveryStep.BodyDelivery;
            Labour.Tick(sdt > 0 ? sdt : (Labour.Running ? dt : 0f));
            DriveMother(dt);
            if (mother != null) mother.Tick(dt);
            if (babyPathFrame != null && mother != null)
            {
                var m = mother.HipsDeltaLocal();
                babyPathFrame.localPosition = m.GetColumn(3);
                babyPathFrame.localRotation = m.rotation;
            }
            if (sim) StepLogic(sdt);
            if (baby != null && (replay == null || !replay.Active)) baby.SetProgress(_progress);
            if (baby != null) baby.Tick(dt, canal);
            if (canal != null) canal.Tick(dt);
            UpdatePerineumZone();
            var eval = evaluator != null ? evaluator.Evaluate(hands.Left, hands.Right, _time) : null;
            if (sim && eval != null)
                _classifier.Evaluate(new FaultClassifier.Context { step = Step, time = _time, dt = sdt, progress = _progress, progressRate = _rate, eval = eval,
                    babyHeld = baby != null && baby.Mode == BabyMode.Held, babyBorn = _bornTime >= 0, contraction = Labour.Intensity });
            if (cord != null) { CordColliders(eval); cord.Tick(dt); }
            if (sim) { _recorder.Record(_time, _progress, baby, hands.Left, hands.Right, eval); Telemetry(eval); }
            if (ghost != null && ghost.Recording) ghost.RecordFrame(hands.Left, hands.Right);
            Cues(eval);
            { if (audioSys != null) audioSys.SetFetalHeartRate(Labour.FetalHeartRate); }
            if (audioSys != null) audioSys.fetalMonitorOn = _sessionRunning && _bornTime < 0;
            { if (instructionPanel != null) instructionPanel.Tick(_progress, Labour, Status(eval)); }
            if (_sessionRunning && _time > Config.sessionLengthLimitMinutes * 60f && Mathf.Repeat(_time, 60f) < dt) Say("You have trained for a while - take a short rest when this scenario ends.");
        }

        void DriveMother(float dt)
        {
            if (mother == null) return;
            if (_bornTime >= 0)
            {
                bool onChest = baby != null && baby.Mode == BabyMode.OnChest;
                mother.SetPose(onChest ? MotherPose.Cradle : (Step == DeliveryStep.SkinToSkin ? MotherPose.Reach : MotherPose.Rest));
                mother.pain = Mathf.MoveTowards(mother.pain, 0f, dt * 0.5f); mother.strain = Mathf.MoveTowards(mother.strain, 0f, dt);
                mother.relief = Mathf.MoveTowards(mother.relief, onChest ? 1f : 0.6f, dt * 0.3f);
                return;
            }
            if (Labour.Active && Step >= DeliveryStep.SecondStagePushing)
            {
                if (Labour.Panting) { mother.SetPose(MotherPose.Pant); { if (audioSys != null) audioSys.Mother("mother_pant", 0.8f, false); } }
                else { mother.SetPose(MotherPose.Push); mother.SetPushPhase(Labour.Phase01); }
            }
            else { mother.SetPose(MotherPose.Rest); if (_sessionRunning && Mathf.Repeat(_time, 5f) < dt) { if (audioSys != null) audioSys.Mother("mother_breath", 0.45f, false); } }
            float stretch = canal != null ? canal.Stretch01 : 0f;
            mother.strain = Mathf.MoveTowards(mother.strain, Labour.Panting ? 0.2f : Labour.PushEffort, dt * 2f);
            mother.pain = Mathf.MoveTowards(mother.pain, Mathf.Max(Labour.Intensity * 0.55f, stretch * 0.9f), dt * 1.5f);
        }

        void UpdatePerineumZone()
        {
            if (perineumZone == null || canal == null || !canal.Ready) return;
            perineumZone.position = canal.IntroitusCenter - canal.AnteriorAxis * (canal.IntroitusRadius + 0.028f) + canal.OutwardAxis * 0.012f;
        }

        // ------------------------------------------------------------------ clinical step logic
        void StepLogic(float dt)
        {
            var e = evaluator != null ? evaluator.Current : null;
            float before = _progress;
            switch (Step)
            {
                case DeliveryStep.Preparation:
                {
                    // ready when the trainee is at the foot of the bed with both hands visible near the perineum
                    bool near = rig != null && canal != null && Vector3.Distance(rig.cam.transform.position, canal.IntroitusCenter) < 1.3f;
                    bool handsUp = hands.Left.tracked || hands.Right.tracked;
                    if (near && handsUp && _time - _stepStart > 4f) CompleteStep(DeliveryStep.SecondStagePushing);
                    break;
                }
                case DeliveryStep.SecondStagePushing:
                {
                    float adv = Labour.Active ? 0.0165f * Labour.PushEffort + 0.003f * Labour.Intensity : -0.0025f;
                    Advance(adv * dt, ND.Progress.HeadVisible, 0.04f);
                    if (_progress >= ND.Progress.HeadVisible - 0.001f) CompleteStep(DeliveryStep.CrowningHeadControl);
                    break;
                }
                case DeliveryStep.CrowningHeadControl:
                {
                    if (!_gushDone && _progress > 0.33f) { _gushDone = true; { if (consequences != null) consequences.AmnioticGush(canal.IntroitusCenter + canal.OutwardAxis * 0.02f); } }
                    float push = Labour.Panting ? 0.15f : Labour.PushEffort;
                    float adv = Labour.Active ? 0.030f * push + 0.006f * Labour.Intensity : -0.0012f;
                    bool control = e != null && e.headControl;
                    if (control) adv *= adv > 0 ? 0.42f : 1f;                       // flexion control slows the birth of the head
                    if (e != null && e.left.force + e.right.force > 30f) adv *= 0.2f;  // excessive pressure holds the head back
                    Advance(adv * dt, ND.Progress.HeadBorn, 0.02f);
                    Placement(control && e.perinealSupport);
                    if (Config.difficulty != Difficulty.Assessment && !control && _progress > 0.34f && Mathf.Repeat(_time, 3f) < dt)
                        { if (cues != null) cues.CueZones(new[] { LandmarkZoneId.Occiput, LandmarkZoneId.MaternalPerineum }, new Color(0.2f, 1f, 0.5f, 0.35f), 1.5f); }
                    if (_progress >= ND.Progress.HeadBorn - 0.001f) { Labour.Panting = false; { if (instructionPanel != null) instructionPanel.SetPanting(false); } CompleteStep(DeliveryStep.NuchalCordCheck); }
                    break;
                }
                case DeliveryStep.NuchalCordCheck:
                {
                    float palp = e != null ? Mathf.Max(e.left.fingerOnNeckTime, e.right.fingerOnNeckTime) : 0f;
                    if (palp >= 0.8f && !_nuchalPalpated)
                    {
                        _nuchalPalpated = true; Placement(true);
                        Say(Config.scenarioNuchalCord ? "Loose nuchal cord felt - slip it over the baby's head." : "No cord around the neck. Wait for restitution with the next contraction.");
                    }
                    if (_nuchalPalpated && _time - _stepStart > 2.5f) CompleteStep(DeliveryStep.Restitution);
                    else if (!_nuchalPalpated && _time - _stepStart > 14f * Mathf.Max(0.5f, Config.timeScale))
                    {
                        var f = _classifier.RaiseEvent(Detector.StepSkipped, Step, _time, "neck not palpated before restitution");
                        if (f == null) CompleteStep(DeliveryStep.Restitution);
                    }
                    break;
                }
                case DeliveryStep.Restitution:
                {
                    float adv = 0.010f + (Labour.Active ? 0.012f * Labour.Intensity : 0f);
                    Advance(adv * dt, ND.Progress.ExternallyRotated, 0f);
                    if (_progress >= ND.Progress.ExternallyRotated - 0.001f) CompleteStep(DeliveryStep.AnteriorShoulder);
                    break;
                }
                case DeliveryStep.AnteriorShoulder:
                case DeliveryStep.PosteriorShoulder:
                {
                    bool anterior = Step == DeliveryStep.AnteriorShoulder;
                    Vector3 expected = anterior ? (baby != null ? baby.SpineAxisOut : canal.OutwardAxis) : (canal.OutwardAxis * 0.55f + canal.AnteriorAxis * 0.83f).normalized;
                    float along = e != null ? Vector3.Dot(e.tractionWorld, expected) : 0f;
                    bool hold = e != null && (e.biparietalHold || e.bodySupported);
                    float adv = hold ? Mathf.Clamp(along, 0f, 30f) / 15f * 0.020f : 0f;
                    adv += 0.0015f * Labour.Intensity + (Labour.Active ? 0.003f * Labour.PushEffort : 0f);
                    Advance(adv * dt, anterior ? ND.Progress.AnteriorShoulderBorn : ND.Progress.PosteriorShoulderBorn, 0f);
                    Placement(e != null && e.biparietalHold);
                    if (cues != null && baby != null && baby.HeadCenter != null && Config.difficulty != Difficulty.Assessment)
                    {
                        cues.ShowExpectedAxis(baby.HeadCenter.position, expected);
                        if (e != null) cues.ShowTraction(baby.HeadCenter.position, e.tractionWorld, e.tractionMagnitude < 2f || Vector3.Angle(e.tractionWorld, expected) < 35f);
                    }
                    if (anterior && _progress >= ND.Progress.AnteriorShoulderBorn - 0.001f) CompleteStep(DeliveryStep.PosteriorShoulder);
                    if (!anterior && _progress >= ND.Progress.PosteriorShoulderBorn - 0.001f) { { if (cues != null) cues.HideArrows(); } CompleteStep(DeliveryStep.BodyDelivery); }
                    break;
                }
                case DeliveryStep.BodyDelivery:
                {
                    float adv = 0.045f + (e != null ? Mathf.Clamp(e.tractionMagnitude, 0, 20f) / 20f * 0.02f : 0f);
                    Advance(adv * dt, ND.Progress.Born, 0f);
                    Placement(e != null && e.fullySupported);
                    if (_progress >= ND.Progress.Born - 0.0005f) Birth(e);
                    break;
                }
                case DeliveryStep.SkinToSkin:
                    UpdateHeldBaby(e, dt);
                    if (baby != null && baby.Mode == BabyMode.OnChest && _time - _stepStart > 1f) CompleteStep(DeliveryStep.DryAndStimulate);
                    break;
                case DeliveryStep.DryAndStimulate:
                    UpdateHeldBaby(e, dt);
                    NewbornTransition(dt, towel != null ? towel.RubbedMetres : 0f);
                    if (baby != null && (baby.wetness < 0.4f || (towel != null && towel.RubbedMetres > 0.8f)) && baby.crying > 0.5f) CompleteStep(DeliveryStep.DelayedCordClamping);
                    break;
                case DeliveryStep.DelayedCordClamping:
                {
                    UpdateHeldBaby(e, dt); NewbornTransition(dt, 1f);
                    float wait = Config.delayedClampingSeconds * Config.timeScale;
                    float since = _time - _bornTime;
                    if (cord != null) cord.pulsation = Mathf.Clamp01(1f - since / Mathf.Max(5f, wait));
                    if (since >= wait) { Say("The cord has stopped pulsating - place the first clamp ~3 cm from the umbilicus and the second ~5 cm."); CompleteStep(DeliveryStep.ClampCord); }
                    break;
                }
                case DeliveryStep.ClampCord:
                    UpdateHeldBaby(e, dt); NewbornTransition(dt, 1f);
                    if (clampA != null && clampB != null && clampA.Attached && clampB.Attached) CompleteStep(DeliveryStep.CutCord);
                    break;
                case DeliveryStep.CutCord:
                    UpdateHeldBaby(e, dt); NewbornTransition(dt, 1f);
                    break;
            }
            _rate = dt > 0 ? (_progress - before) / dt : 0f;
            if (Step >= DeliveryStep.SkinToSkin && _bornTime >= 0 && cord != null && Step < DeliveryStep.DelayedCordClamping)
                cord.pulsation = Mathf.Clamp01(1f - (_time - _bornTime) / Mathf.Max(5f, Config.delayedClampingSeconds * Config.timeScale));
        }

        void Advance(float delta, float cap, float recedeFloorBelowMax)
        {
            _progress = Mathf.Clamp(_progress + delta, Mathf.Max(0f, _maxProgress - recedeFloorBelowMax), cap);
            _maxProgress = Mathf.Max(_maxProgress, _progress);
        }

        void Placement(bool correct)
        {
            if (_correctLogged) return;
            if (correct) { if (_placementSince < 0) _placementSince = _time; if (_time - _placementSince > 0.5f) { _correctLogged = true; _store.CorrectPlacement(Step, _placementSince - _stepStart); } }
            else _placementSince = -1f;
        }

        void Birth(EvaluationFrame e)
        {
            _progress = 1f; _bornTime = _time;
            NDEvents.RaiseBabyBorn();
            { if (mother != null) mother.SetDelivered(true); }
            { if (audioSys != null) audioSys.Mother("mother_relief", 1f); }
            baby.ConfigureClip(canal, false);
            if (e != null && e.bodySupported) baby.SetMode(BabyMode.Held);
            else { baby.SetMode(BabyMode.Resting); }
            CompleteStep(DeliveryStep.SkinToSkin);
        }

        /// <summary>Newborn carried by the trainee's hands; released near the mother's chest -> skin-to-skin.</summary>
        void UpdateHeldBaby(EvaluationFrame e, float dt)
        {
            if (baby == null || e == null) return;
            if (baby.Mode == BabyMode.OnChest) { PlaceOnChest(); return; }
            var l = hands.Left; var r = hands.Right;
            bool ls = e.left.tracked && e.left.minDistance < 0.04f, rs = e.right.tracked && e.right.minDistance < 0.04f;
            if (ls || rs)
            {
                baby.SetMode(BabyMode.Held);
                TrackedHand headHand, bodyHand;
                if (ls && rs)
                {
                    bool leftOnHead = e.left.HasAny(LandmarkZoneId.Neck, LandmarkZoneId.Occiput, LandmarkZoneId.ParietalLeft, LandmarkZoneId.ParietalRight, LandmarkZoneId.ShoulderAnterior, LandmarkZoneId.ShoulderPosterior);
                    headHand = leftOnHead ? l : r; bodyHand = leftOnHead ? r : l;
                    Vector3 up = (headHand.palmPos - bodyHand.palmPos); if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
                    Vector3 n = (headHand.palmNormal + bodyHand.palmNormal).normalized;
                    Vector3 pos = (headHand.palmPos + bodyHand.palmPos) * 0.5f + n * 0.045f * baby.babyScale;
                    baby.SetTarget(pos, Quaternion.LookRotation(-Vector3.ProjectOnPlane(n, up.normalized).normalized, up.normalized));
                    _holdOneHand = 0f;
                }
                else
                {
                    var h = ls ? l : r;
                    _holdOneHand += dt;
                    Vector3 pos = h.palmPos + h.palmNormal * 0.05f + Vector3.down * Mathf.Min(0.08f, _holdOneHand * 0.04f);
                    baby.SetTarget(pos, Quaternion.LookRotation(-h.palmNormal, h.palmForward));
                    if (_holdOneHand > 2.5f) _classifier.RaiseEvent(Detector.ReleaseDuring, Step, _time, "newborn held with one hand for more than 2.5 s");
                }
                // release near the chest -> skin-to-skin
                if (skinToSkinAnchor != null && Vector3.Distance(baby.ChestCenter, skinToSkinAnchor.position) < 0.16f && !(ls && rs) && Step >= DeliveryStep.SkinToSkin) baby.SetMode(BabyMode.OnChest);
            }
            else if (baby.Mode == BabyMode.Held)
            {
                bool nearChest = skinToSkinAnchor != null && Vector3.Distance(baby.ChestCenter, skinToSkinAnchor.position) < 0.2f;
                if (nearChest) baby.SetMode(BabyMode.OnChest);
                else
                {
                    _classifier.RaiseEvent(Detector.BabyDropped, Step, _time, "newborn released away from the mother's chest");
                    baby.SetMode(BabyMode.Resting);
                }
            }
            if (baby.Mode == BabyMode.Resting && canal != null)
            {
                // newborn lies on the under-buttocks pad just below the perineum
                Vector3 rest = canal.IntroitusCenter + Flat(canal.OutwardAxis) * 0.16f; rest.y = canal.IntroitusCenter.y - 0.06f;
                baby.SetTarget(rest, Quaternion.LookRotation(Vector3.up, -Flat(canal.OutwardAxis)));
            }
        }

        void PlaceOnChest()
        {
            if (skinToSkinAnchor == null || mother == null || mother.Head == null || mother.Hips == null) return;
            Vector3 up = Vector3.ProjectOnPlane(mother.Head.position - mother.Hips.position, Vector3.up * 0.0f).normalized;
            Vector3 front = (skinToSkinAnchor.position - (mother.Chest != null ? mother.Chest.position : mother.Hips.position)); front = Vector3.ProjectOnPlane(front, up).normalized;
            baby.SetTarget(skinToSkinAnchor.position + front * 0.055f * baby.babyScale, Quaternion.LookRotation(front, up));
        }

        void NewbornTransition(float dt, float stimulation)
        {
            if (baby == null || _bornTime < 0) return;
            float since = _time - _bornTime;
            if (baby.limp < 0.5f)
            {
                if (since > 4f || stimulation > 0.3f) baby.crying = Mathf.MoveTowards(baby.crying, 1f, dt * 0.8f);
                if (baby.crying > 0.5f) { baby.cyanosis = Mathf.MoveTowards(baby.cyanosis, 0.08f, dt * 0.025f / Mathf.Max(0.3f, Config.timeScale)); { if (audioSys != null) audioSys.Baby("baby_cry", true, 0.9f); } }
            }
            else { if (audioSys != null) audioSys.StopBaby(); }
        }

        // ------------------------------------------------------------------ faults -> consequence -> reflective loop
        void OnFault(FaultEvent f)
        {
            if (f == null) return;
            var rule = _rules.Get(f.code);
            f.hand = DominantFaultHand(f);
            _store.AddFault(f); _rubric.Fault(f);
            NDEvents.RaiseFault(f);
            ND.Log($"FAULT {f.code} ({f.step}) {f.evidence}");
            if (Config.ablationMode) return;                                 // ablated build: only recorded for the end-of-scenario score
            float detected = Time.realtimeSinceStartup;
            bool scenarioOk = rule == null || string.IsNullOrEmpty(rule.requiresScenario) || (rule.requiresScenario == "NuchalCord" && Config.scenarioNuchalCord);
            if (scenarioOk) { if (consequences != null) f.feedbackLatencyMs = consequences.Render(f, rule, detected); }
            else f.complication = "(no complication in this scenario) " + f.complication;
            Say($"{f.name}: {f.complication}");
            if (_awaitingRetry || _time - _lastFaultTime < 1f) return;
            _lastFaultTime = _time;
            StartCoroutine(ReflectiveLoop(f));
        }

        HandSide DominantFaultHand(FaultEvent f)
        {
            var e = evaluator != null ? evaluator.Current : null; if (e == null) return Config.dominantHand;
            return e.left.traction.magnitude + e.left.force > e.right.traction.magnitude + e.right.force ? HandSide.Left : HandSide.Right;
        }

        IEnumerator ReflectiveLoop(FaultEvent f)
        {
            _awaitingRetry = true;
            yield return new WaitForSeconds(1.6f);                         // let the trainee see the consequence first
            var frames = _recorder.Range(f.startTime - 2.5f, f.endTime + 1.0f);
            bool done = false;
            if (replay != null && frames.Count > 5) { replay.Begin(f, frames, () => done = true); while (!done) yield return null; }
            else yield return new WaitForSeconds(2f);
            { if (consequences != null) consequences.Clear(); }
            NDEvents.RaiseRetry(Step, _rubric.Attempts(Step) + 1);
            RestoreCheckpoint();
            { if (evaluator != null) evaluator.ResetEngagement(); }
            Say($"Repeat this step. {f.remediation}");
            var s = Step; Step = DeliveryStep.Briefing; GoTo(s);      // re-enter the same step (new attempt)
            if (ghost != null) ghost.Show(s, Config.guidedGhostHands || Config.difficulty != Difficulty.Assessment);
            _awaitingRetry = false;
        }

        void OnClamped(CordClampTool c)
        {
            float cm = c.DistanceFromNavelCm;
            if (Step < DeliveryStep.ClampCord) _classifier.RaiseEvent(Detector.EarlyClamping, Step, _time, $"cord clamped {(_time - _bornTime):F0}s after birth (before pulsation stopped)");
            bool first = !(clampA.Attached && clampB.Attached);
            if (!first)
            {
                float a = Mathf.Min(clampA.DistanceFromNavelCm, clampB.DistanceFromNavelCm), b = Mathf.Max(clampA.DistanceFromNavelCm, clampB.DistanceFromNavelCm);
                EvaluateClampPositions(a, b);
            }
            Say($"Clamp placed {cm:F1} cm from the umbilicus.");
        }

        void EvaluateClampPositions(float nearCm, float farCm)
        {
            foreach (var r in _rules.rules)
            {
                if (r.detector != Detector.ClampPosition) continue;
                float v = r.code.Contains("SECOND") ? farCm : nearCm;
                if (r.code.Contains("SPACING")) v = farCm - nearCm;
                if (v < r.minCm || v > r.maxCm)
                {
                    var f = _classifier.Make(r, Step, _time - 1f, _time, $"{(r.code.Contains("SPACING") ? "spacing" : "clamp")} at {v:F1} cm (expected {r.minCm:F0}-{r.maxCm:F0} cm)");
                    OnFault(f);
                }
            }
        }

        void OnCut(float sMetres, Vector3 at)
        {
            float cm = sMetres * 100f;
            int clamps = (clampA != null && clampA.Attached ? 1 : 0) + (clampB != null && clampB.Attached ? 1 : 0);
            if (clamps < 2) _classifier.RaiseEvent(Detector.CutWithoutClamps, Step, _time, $"cut at {cm:F1} cm with {clamps} clamp(s) applied");
            else
            {
                float a = Mathf.Min(clampA.DistanceFromNavelCm, clampB.DistanceFromNavelCm), b = Mathf.Max(clampA.DistanceFromNavelCm, clampB.DistanceFromNavelCm);
                if (cm < a || cm > b) _classifier.RaiseEvent(Detector.CutOutsideClamps, Step, _time, $"cut at {cm:F1} cm - outside the clamps ({a:F1}-{b:F1} cm)");
            }
            NDEvents.RaiseCordCut();
            if (Step == DeliveryStep.CutCord && !_awaitingRetry) StartCoroutine(FinishAfter(2.5f));
        }

        IEnumerator FinishAfter(float s) { yield return new WaitForSeconds(s); if (Step == DeliveryStep.CutCord && !_awaitingRetry) CompleteStep(DeliveryStep.Complete); }

        void Finish()
        {
            _sessionRunning = false;
            var res = _store.Finish(_time, _rubric);
            NDEvents.RaiseSessionFinished(res);
            if (instructionPanel != null) instructionPanel.panel.gameObject.SetActive(false);
            { if (summaryPanel != null) summaryPanel.Show(res); }
            { if (ghost != null) ghost.Show(Step, false); }
        }

        // ------------------------------------------------------------------ helpers
        void CordColliders(EvaluationFrame e)
        {
            cord.colliders.Clear();
            if (baby != null) foreach (var z in baby.zones) if (z != null && z.id != LandmarkZoneId.Chest) cord.colliders.Add(new UmbilicalCord.Sphere { c = z.Center, r = z.radius * 0.9f });
            if (mother != null && mother.Belly != null) cord.colliders.Add(new UmbilicalCord.Sphere { c = mother.Belly.position, r = 0.15f });
            if (e != null)
            {
                if (e.left.tracked) cord.colliders.Add(new UmbilicalCord.Sphere { c = hands.Left.palmPos, r = 0.03f });
                if (e.right.tracked) cord.colliders.Add(new UmbilicalCord.Sphere { c = hands.Right.palmPos, r = 0.03f });
            }
            cord.floorY = floorY;
        }

        void Telemetry(EvaluationFrame e)
        {
            if (e == null || station == null) return;
            _store.Sample(new TrajectorySample
            {
                t = _time, progress = _progress, step = Step.ToString(),
                leftPalm = station.InverseTransformPoint(hands.Left.palmPos), rightPalm = station.InverseTransformPoint(hands.Right.palmPos),
                leftRot = Quaternion.Inverse(station.rotation) * hands.Left.palmRot, rightRot = Quaternion.Inverse(station.rotation) * hands.Right.palmRot,
                leftZone = e.left.zone.ToString(), rightZone = e.right.zone.ToString(), leftForce = e.left.force, rightForce = e.right.force,
                traction = e.tractionCanal, leftGrip = e.left.grip, rightGrip = e.right.grip
            });
        }

        void Cues(EvaluationFrame e)
        {
            if (leftGlove != null) leftGlove.Visible = !(Config != null && Config.mixedReality);
            if (rightGlove != null) rightGlove.Visible = !(Config != null && Config.mixedReality);
        }

        string Status(EvaluationFrame e)
        {
            if (e == null) return "";
            string hz(HandEval h) => h.tracked ? $"{h.zone} {(h.force > 0.5f ? h.force.ToString("F0") + "N" : "")}" : "-";
            string src = hands != null ? hands.ActiveSource.ToString() : "";
            string born = _bornTime >= 0 ? $"  |  born {(_time - _bornTime):F0}s" : "";
            return $"Progress {_progress * 100f:F0}%  |  FHR {Labour.FetalHeartRate:F0} bpm  |  {(Labour.Active ? "CONTRACTION" : "rest")}{born}\nL: {hz(e.left)}   R: {hz(e.right)}   traction {e.tractionMagnitude:F1} N ({e.angleFromAxial:F0} deg)   [{src}]";
        }

        string ShortInstruction(DeliveryStep s)
        {
            switch (s)
            {
                case DeliveryStep.Preparation: return "Gloves on. Stand at the foot of the bed facing the perineum, towel and cord clamps within reach.";
                case DeliveryStep.SecondStagePushing: return "Second stage: the mother pushes with each contraction. Watch the head descend.";
                case DeliveryStep.CrowningHeadControl: return "Crowning: flat fingers of your dominant hand on the occiput to keep the head flexed; other hand supports the perineum. Ask her to pant.";
                case DeliveryStep.NuchalCordCheck: return "Head born. Slide two fingers along the neck to feel for a cord.";
                case DeliveryStep.Restitution: return "Hands off - let the head restitute and rotate externally with the next contraction.";
                case DeliveryStep.AnteriorShoulder: return "Hold the head on both parietal bones. Gentle AXIAL traction in line with the fetal spine to free the anterior shoulder.";
                case DeliveryStep.PosteriorShoulder: return "Now guide the head upward, following the curve, to deliver the posterior shoulder.";
                case DeliveryStep.BodyDelivery: return "Support the head and neck in one hand; slide the other along the back to the buttocks as the body is born.";
                case DeliveryStep.SkinToSkin: return "Lift the baby onto the mother's chest for skin-to-skin contact. Keep the cord slack.";
                case DeliveryStep.DryAndStimulate: return "Dry the baby with the towel (head, back, limbs) and stimulate. Check breathing, tone and colour.";
                case DeliveryStep.DelayedCordClamping: return "Delayed cord clamping: wait at least 1 minute while the cord pulsates. Do not clamp yet.";
                case DeliveryStep.ClampCord: return "Apply the first clamp ~3 cm from the umbilicus, the second ~5 cm (about 2 cm further).";
                case DeliveryStep.CutCord: return "Cut BETWEEN the clamps with the cord scissors, shielding the cut with your other hand.";
                case DeliveryStep.Complete: return "Delivery complete.";
            }
            return "";
        }

        string InstructionFor(DeliveryStep s) { return ShortInstruction(s); }

        /// <summary>Educator/desktop shortcut to jump ahead one stage (testing).</summary>
        public void DebugAdvance()
        {
            switch (Step)
            {
                case DeliveryStep.SecondStagePushing: _progress = _maxProgress = ND.Progress.HeadVisible; break;
                case DeliveryStep.CrowningHeadControl: _progress = _maxProgress = ND.Progress.HeadBorn; break;
                case DeliveryStep.NuchalCordCheck: _nuchalPalpated = true; _stepStart -= 5f; break;
                case DeliveryStep.Restitution: _progress = ND.Progress.ExternallyRotated; break;
                case DeliveryStep.AnteriorShoulder: _progress = ND.Progress.AnteriorShoulderBorn; break;
                case DeliveryStep.PosteriorShoulder: _progress = ND.Progress.PosteriorShoulderBorn; break;
                case DeliveryStep.BodyDelivery: _progress = ND.Progress.Born; break;
                case DeliveryStep.SkinToSkin: { if (baby != null) baby.SetMode(BabyMode.OnChest); } break;
                case DeliveryStep.DryAndStimulate: if (baby != null) { baby.wetness = 0.2f; baby.crying = 1f; } break;
                case DeliveryStep.DelayedCordClamping: _bornTime = _time - 999f; break;
            }
        }
    }
}
