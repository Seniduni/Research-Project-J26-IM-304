// Installs the XR packages this project needs (XR Plug-in Management + OpenXR).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MROT.EditorTools
{
    public static class MROTPackages
    {
        static AddAndRemoveRequest _req;
        [MenuItem("MR Operating Theatre/2. Install XR Packages (OpenXR)", priority = 2)]
        public static void Install()
        {
            _req = Client.AddAndRemove(new[] { "com.unity.xr.management", "com.unity.xr.openxr" }, null);
            EditorApplication.update += Poll;
            Debug.Log("[MROT] Installing XR Plug-in Management + OpenXR ...");
        }
        static void Poll()
        {
            if (_req == null || !_req.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (_req.Status == StatusCode.Success) Debug.Log("[MROT] XR packages installed. Now run 'MR Operating Theatre > 3. Configure OpenXR'.");
            else Debug.LogError("[MROT] Package install failed: " + (_req.Error != null ? _req.Error.message : "unknown"));
            _req = null;
        }
    }
}
