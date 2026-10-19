// MR Operating Theatre - free exploration of the ward (used by the "MR Operating Theatre" PP1 project
// and reusable by any module). Select equipment to see an information card.
using System;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace MROT
{
    /// <summary>Information card that appears beside a piece of equipment.</summary>
    public class InfoCard : MonoBehaviour
    {
        public Canvas canvas;
        Text _title, _cat, _body;
        Transform _buttons;
        public Action onClose;
        public static readonly Vector2 Size = new Vector2(620, 560);

        public static InfoCard Create(string name = "InfoCard")
        {
            var c = UIKit.WorldCanvas(name, Size);
            var card = c.gameObject.AddComponent<InfoCard>();
            card.canvas = c;
            var bg = UIKit.Panel(c.transform, "Bg", Theme.Panel);
            var head = UIKit.Panel(c.transform, "Header", Theme.AccentDark);
            UIKit.Place(head.rectTransform, 0, 0, Size.x, 120);
            card._cat = UIKit.Label(head.transform, "", 22, new Color(0.85f, 1f, 0.97f), TextAnchor.UpperLeft, UIKit.SemiBold);
            ((RectTransform)card._cat.transform).offsetMin = new Vector2(30, 10); ((RectTransform)card._cat.transform).offsetMax = new Vector2(-30, -16);
            card._title = UIKit.Label(head.transform, "", 36, Color.white, TextAnchor.LowerLeft, UIKit.Bold);
            ((RectTransform)card._title.transform).offsetMin = new Vector2(30, 12); ((RectTransform)card._title.transform).offsetMax = new Vector2(-30, -10);
            card._body = UIKit.Label(c.transform, "", 25, Theme.Text, TextAnchor.UpperLeft);
            UIKit.Place((RectTransform)card._body.transform, 30, 138, Size.x - 60, Size.y - 240);
            var btnRow = new GameObject("Buttons", typeof(RectTransform));
            UIKit.Place(UIKit.Rect(btnRow, c.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), 0, Size.y - 96, Size.x, 96);
            card._buttons = btnRow.transform;
            return card;
        }

        public void Show(EquipmentInfo info, string extraFooter = null)
        {
            _title.text = info != null ? info.name : "Unknown";
            _cat.text = info != null ? info.category.ToUpperInvariant() : "";
            var sb = new System.Text.StringBuilder();
            if (info != null)
            {
                sb.Append(info.summary).Append("\n");
                if (info.points != null)
                    foreach (var p in info.points) sb.Append("\n<color=#20BEAE>•</color>  ").Append(p);
                if (!string.IsNullOrEmpty(info.nurseRole)) sb.Append("\n\n<color=#FFC83C><b>Nurse's role:</b></color> ").Append(info.nurseRole);
            }
            if (!string.IsNullOrEmpty(extraFooter)) sb.Append("\n\n").Append(extraFooter);
            _body.text = sb.ToString();
            // grow the card to fit the text
            float h = Mathf.Clamp(_body.preferredHeight + 250, 360, 1000);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(Size.x, h);
            UIKit.Place((RectTransform)_body.transform, 30, 138, Size.x - 60, h - 240);
            UIKit.Place((RectTransform)_buttons, 0, h - 96, Size.x, 96);
            gameObject.SetActive(true);
        }

        public WorldButton AddButton(string text, Action a, Color? col = null, float x = -1, float w = 260)
        {
            var b = UIKit.Button(_buttons, text, a, col);
            int count = _buttons.childCount - 1;
            UIKit.Place((RectTransform)b.transform, x >= 0 ? x : 30 + count * (w + 20), 10, w, 70);
            return b;
        }
        public void ClearButtons() { foreach (Transform t in _buttons) Destroy(t.gameObject); }

        /// <summary>Place the card beside the equipment, facing the user.</summary>
        public void PlaceNear(EquipmentTag tag)
        {
            var rig = PlayerRig.Instance;
            var b = tag != null ? tag.WorldBounds : new Bounds(rig.InFront(1.5f), Vector3.one * 0.3f);
            var head = rig.Head.position;
            var toObj = b.center - head; toObj.y = 0;
            float dist = Mathf.Clamp(toObj.magnitude * 0.6f, 0.9f, 1.6f) * (rig.IsVR ? 1f : 0.7f);
            var dir = toObj.sqrMagnitude > 0.01f ? toObj.normalized : rig.Head.forward;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            var p = head + dir * dist + side * 0.45f * (rig.IsVR ? 1f : 0.7f);
            p.y = Mathf.Clamp(head.y - 0.1f, 1.1f, 1.8f);
            transform.position = p;
            var look = p - head; look.y = 0;
            transform.rotation = Quaternion.LookRotation(look);
        }
        public void PlaceInFront(float d = 1.2f, float h = -0.05f)
        {
            var rig = PlayerRig.Instance;
            transform.position = rig.InFront(d, h);
            var look = transform.position - rig.Head.position; look.y = 0;
            transform.rotation = Quaternion.LookRotation(look);
        }
    }

    /// <summary>Free-roam explorer: point at / click any equipment to read about it. Door works hands-free.</summary>
    public class WardExplorer : MonoBehaviour
    {
        public bool showHelp = true;
        InfoCard _card;
        EquipmentTag _current;
        GameObject _help;

        void Start()
        {
            _card = InfoCard.Create();
            _card.gameObject.SetActive(false);
            EquipmentTag.Selected += OnSelected;
            if (showHelp) BuildHelp();
            var ward = WardSetup.Instance;
            if (ward != null && ward.MainDoor != null) ward.MainDoor.OnWrongAttempt += () => Toast.Show("Theatre doors are hands-free - use the foot sensor near the floor (F / A button).", Theme.Warning);
        }
        void OnDestroy() { EquipmentTag.Selected -= OnSelected; }

        void OnSelected(EquipmentTag t)
        {
            if (t.id == "OT_RoomShell") return;
            if (_current != null) _current.HL.Focus(false);
            _current = t; t.HL.Focus(true);
            _card.ClearButtons();
            _card.Show(t.Info);
            _card.AddButton("Close", Close, Theme.Button);
            _card.PlaceNear(t);
            UIAudio.Play("popup", 0.6f);
        }
        void Close() { _card.gameObject.SetActive(false); if (_current != null) _current.HL.Focus(false); _current = null; }

        void BuildHelp()
        {
            var c = UIKit.WorldCanvas("HelpPanel", new Vector2(600, 330));
            _help = c.gameObject;
            UIKit.Panel(c.transform, "Bg", new Color(0.05f, 0.1f, 0.16f, 0.85f));
            var t = UIKit.Label(c.transform, "<b>MR OPERATING THEATRE</b>\n" +
                (InputBridge.XRActive ? "Right stick: teleport / turn\nTrigger: select equipment\nA / X: foot tap (door sensor)" :
                "W A S D: walk     Hold right mouse: look\nLeft click: select equipment\nF: foot tap (door sensor)     H: hide help"), 27, Theme.Text);
            ((RectTransform)t.transform).offsetMin = new Vector2(24, 20); ((RectTransform)t.transform).offsetMax = new Vector2(-24, -20);
            var lf = c.gameObject.AddComponent<LazyFollow>(); lf.distance = 1.3f; lf.height = -0.45f; lf.angleThreshold = 40; lf.desktopLateral = -0.5f;   // lower-left on desktop, keeps the view clear
        }
        void Update()
        {
            if (InputBridge.KeyDown(KeyCode.H) && _help != null) _help.SetActive(!_help.activeSelf);
        }
    }

    /// <summary>Short notification shown in front of the user.</summary>
    public class Toast : MonoBehaviour
    {
        static Toast _inst;
        Text _text; Image _bg; float _until;
        public static void Show(string msg, Color accent, float seconds = 4f)
        {
            if (_inst == null)
            {
                var c = UIKit.WorldCanvas("Toast", new Vector2(760, 120));
                _inst = c.gameObject.AddComponent<Toast>();
                _inst._bg = UIKit.Panel(c.transform, "Bg", Theme.Panel);
                _inst._text = UIKit.Label(c.transform, "", 26, Theme.Text, TextAnchor.MiddleCenter, UIKit.SemiBold);
                ((RectTransform)_inst._text.transform).offsetMin = new Vector2(24, 8); ((RectTransform)_inst._text.transform).offsetMax = new Vector2(-24, -8);
                var lf = c.gameObject.AddComponent<LazyFollow>(); lf.distance = 1.1f; lf.height = 0.28f; lf.angleThreshold = 15;
            }
            _inst.gameObject.SetActive(true);
            _inst._text.text = msg;
            _inst._bg.color = Color.Lerp(Theme.Panel, accent, 0.35f);
            _inst._until = Time.unscaledTime + seconds;
            _inst.GetComponent<LazyFollow>().Snap();
        }
        void Update() { if (Time.unscaledTime > _until) gameObject.SetActive(false); }
    }
}
