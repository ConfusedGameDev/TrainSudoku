using System;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace TrainSudoku.XR
{
    /// <summary>
    /// The headset's spatial-data permission, which plane detection needs (XR-PRD 5.2) and depth occlusion will (5.6).
    /// Off the headset it reads as refused, so the Editor takes the floating fallback.
    /// </summary>
    public static class XRScenePermission
    {
        public const string Name = "com.oculus.permission.USE_SCENE";

        public static bool IsGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Name);
#else
                return false;
#endif
            }
        }

        /// <summary>Asks for the permission; <paramref name="answered"/> gets the answer, at once when it is already granted.</summary>
        public static void Request(Action<bool> answered)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Name))
            {
                answered?.Invoke(true);
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered?.Invoke(true);
            callbacks.PermissionDenied += _ => answered?.Invoke(false);
            callbacks.PermissionRequestDismissed += _ => answered?.Invoke(false);
            Permission.RequestUserPermission(Name, callbacks);
#else
            answered?.Invoke(false);
#endif
        }
    }
}
