// Physics drape: turns the mother's green surgical drape (ND_Drape, skinned to the mother rig) into a Unity Cloth.
// * Per-vertex freedom comes from the drape's vertex colour (R: 0 = resting on the body, 1 = free hanging edge), baked in
//   Blender from the cloth simulation, so the drape stays on the abdomen but its edges swing with the mother's movement.
// * Sphere-capsule colliders on the belly, hips, thighs and forearms keep the cloth outside the body.
// * The trainee's palms are cloth colliders too: the drape can be pushed / lifted with tracked hands.
// Quest budget: ~1.1k particles, single cloth, solver 90 Hz (about 0.3-0.6 ms CPU).
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class DrapeCloth : MonoBehaviour
    {
        public Transform motherRoot;
        public HandTrackingSource hands;
        [Header("Cloth")]
        public float minMaxDistance = 0.004f;     // vertices lying on the body
        public float maxMaxDistance = 0.16f;      // free hanging edge
        public float stretchingStiffness = 0.9f;
        public float bendingStiffness = 0.12f;
        public float damping = 0.2f;
        public float friction = 0.55f;
        public float handRadius = 0.045f;

        Cloth _cloth;
        SphereCollider _handL, _handR;
        readonly List<ClothSphereColliderPair> _pairs = new List<ClothSphereColliderPair>();

        void Start() { Build(); }

        public void Build()
        {
            var smr = GetComponent<SkinnedMeshRenderer>();
            var mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) return;
            _cloth = NDUtil.GetOrAdd<Cloth>(gameObject);
            _cloth.useGravity = true;
            _cloth.stretchingStiffness = stretchingStiffness;
            _cloth.bendingStiffness = bendingStiffness;
            _cloth.damping = damping;
            _cloth.friction = friction;
            _cloth.worldVelocityScale = 0.25f;
            _cloth.worldAccelerationScale = 0.6f;
            _cloth.clothSolverFrequency = 90f;
            _cloth.sleepThreshold = 0.0005f;

            // ---- per-vertex freedom from the baked vertex colour
            if (!mesh.isReadable) Debug.LogWarning("[NeonatalDelivery] ND_Drape mesh is not readable (enable Read/Write on ND_Mother.fbx) - using uniform cloth freedom.");
            var cols = mesh.isReadable ? mesh.colors : null;
            var coeff = _cloth.coefficients;
            if (coeff != null && coeff.Length > 0)
            {
                int[] map = mesh.isReadable ? ClothToMeshIndex(mesh, coeff.Length) : new int[coeff.Length];
                for (int i = 0; i < coeff.Length; i++)
                {
                    int m = map[i];
                    float free = (cols != null && cols.Length == mesh.vertexCount && m >= 0) ? cols[m].r : 0.5f;
                    coeff[i].maxDistance = Mathf.Lerp(minMaxDistance, maxMaxDistance, free * free);
                    coeff[i].collisionSphereDistance = float.MaxValue;   // no back-stop sphere; the colliders keep it outside
                }
                _cloth.coefficients = coeff;
            }

            // ---- body colliders (sized so they stay inside the skin)
            _pairs.Clear();
            var rest = new Mesh(); smr.BakeMesh(rest, true);
            var restWorld = rest.vertices; var l2w = transform.localToWorldMatrix;
            for (int i = 0; i < restWorld.Length; i++) restWorld[i] = l2w.MultiplyPoint3x4(restWorld[i]);
            Destroy(rest);
            if (motherRoot != null)
            {
                Pair("Belly", 0.16f, "Hips", 0.15f, restWorld);
                Pair("Chest", 0.13f, "UpperChest", 0.12f, restWorld);       // keeps the drape over the breasts
                Pair("LeftUpperLeg", 0.085f, "LeftLowerLeg", 0.06f, restWorld);
                Pair("RightUpperLeg", 0.085f, "RightLowerLeg", 0.06f, restWorld);
                Pair("LeftLowerArm", 0.04f, "LeftHand", 0.035f, restWorld);
                Pair("RightLowerArm", 0.04f, "RightHand", 0.035f, restWorld);
            }
            // ---- trainee hands
            _handL = MakeSphere("ND_ClothHandL", null, handRadius);
            _handR = MakeSphere("ND_ClothHandR", null, handRadius);
            _pairs.Add(new ClothSphereColliderPair(_handL)); _pairs.Add(new ClothSphereColliderPair(_handR));
            _cloth.sphereColliders = _pairs.ToArray();
        }

        void LateUpdate()
        {
            if (hands == null || _handL == null) return;
            Place(_handL, hands.Left); Place(_handR, hands.Right);
        }

        static void Place(SphereCollider c, TrackedHand h)
        {
            bool on = h != null && h.tracked;
            c.enabled = on;
            if (on) c.transform.position = h.palmPos + h.palmNormal * 0.01f;
        }

        void Pair(string boneA, float maxA, string boneB, float maxB, Vector3[] drapeWorld)
        {
            var a = RigUtil.FindDeep(motherRoot, boneA); var b = RigUtil.FindDeep(motherRoot, boneB);
            if (a == null || b == null) return;
            var sa = MakeSphere("ND_ClothCol_" + boneA, a, Fit(a.position, maxA, drapeWorld));
            var sb = MakeSphere("ND_ClothCol_" + boneB, b, Fit(b.position, maxB, drapeWorld));
            _pairs.Add(new ClothSphereColliderPair(sa, sb));
        }

        // radius = anatomical maximum, but never reaching the drape's rest pose (it must start outside every collider)
        static float Fit(Vector3 c, float max, Vector3[] drape)
        {
            float d = float.MaxValue;
            foreach (var p in drape) { float q = (p - c).sqrMagnitude; if (q < d) d = q; }
            return Mathf.Clamp(Mathf.Sqrt(d) - 0.012f, 0.02f, max);
        }

        SphereCollider MakeSphere(string name, Transform parent, float r)
        {
            var go = new GameObject(name); go.layer = 2;   // Ignore Raycast: used only by the cloth solver
            go.transform.SetParent(parent != null ? parent : transform, false);
            var s = go.AddComponent<SphereCollider>(); s.radius = r / Mathf.Max(1e-4f, go.transform.lossyScale.x); s.isTrigger = false;
            return s;
        }

        // Unity's Cloth welds vertices that share a position; our drape grid has no seams, but map robustly anyway.
        static int[] ClothToMeshIndex(Mesh mesh, int clothCount)
        {
            var v = mesh.vertices; var map = new int[clothCount];
            if (clothCount == v.Length) { for (int i = 0; i < clothCount; i++) map[i] = i; return map; }
            var seen = new Dictionary<Vector3Int, int>(); int k = 0;
            for (int i = 0; i < v.Length && k < clothCount; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e5f), Mathf.RoundToInt(v[i].y * 1e5f), Mathf.RoundToInt(v[i].z * 1e5f));
                if (seen.ContainsKey(key)) continue;
                seen[key] = i; map[k++] = i;
            }
            for (; k < clothCount; k++) map[k] = -1;
            return map;
        }

        /// <summary>Reset the cloth to the skinned pose (e.g. after a scenario restart).</summary>
        public void ResetCloth() { if (_cloth != null) _cloth.ClearTransformMotion(); }
    }
}
