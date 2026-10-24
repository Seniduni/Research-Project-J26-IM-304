// MR Operating Theatre - shared ward runtime (H.M.H. Lakshan, IT23404564)
// Attach WardSetup to the imported ward model (MR_OperatingTheatre_Ward.fbx). At runtime it:
//   * tags every piece of equipment (EquipmentTag + collider) from the equipment catalogue
//   * creates the real-time lights at the positions modelled in Blender (LIGHT_* empties)
//   * makes walls/floor solid, sets up a reflection probe for stainless steel
//   * wires the hands-free sliding door and its foot sensors
//   * brings the monitors to life (live clock, sweeping vital-sign trace)
// Other team members can drop the "MR_OperatingTheatre_Ward" prefab into their own scenes.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MROT
{
    public class EquipmentTag : MonoBehaviour, IPointerTarget
    {
        public string id;
        public EquipmentInfo Info { get { return EquipmentCatalog.Get(id); } }
        public bool selectable = true;
        public static event Action<EquipmentTag> Selected;
        public static event Action<EquipmentTag> HoverChanged;
        public static bool SelectionEnabled = true;
        public bool IsInteractable { get { return selectable && SelectionEnabled && isActiveAndEnabled; } }
        Highlighter _hl;
        public Highlighter HL { get { if (_hl == null) _hl = gameObject.GetComponent<Highlighter>() ?? gameObject.AddComponent<Highlighter>(); return _hl; } }
        public Bounds WorldBounds
        {
            get
            {
                var rs = GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.2f);
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
            }
        }
        public void OnHoverEnter(PointerEvent e) { HL.Hover(true); if (HoverChanged != null) HoverChanged(this); }
        public void OnHoverExit(PointerEvent e) { HL.Hover(false); }
        public void OnSelect(PointerEvent e) { if (Selected != null) Selected(this); }
        public static EquipmentTag Find(string id)
        {
            foreach (var t in FindObjectsByType<EquipmentTag>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t.id == id) return t;
            return null;
        }
    }
}
