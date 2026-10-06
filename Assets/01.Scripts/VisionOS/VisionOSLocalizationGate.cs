using TrainSudoku.XR;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TrainSudoku.VisionOS
{
    /// <summary>
    /// Holds the game and the board placement back until Localization has finished starting, because on Vision Pro a
    /// synchronous wait on it during startup never returns and visionOS kills the app.
    /// </summary>
    /// <remarks>
    /// <b>The crash</b> (2026-10-05, two crash reports, both <c>SIGKILL</c> with the main thread spinning in
    /// <c>AssetBundleLoadFromAsyncOperation::GetAssetBundleBlocking</c>): after the merge of XR9-XR10,
    /// <c>XRBoardPlacement.ShowSign</c> asks <c>XRPalette.CopyFont</c> for the sign's font, which asks
    /// <c>XRText.IsJapanese</c>, which reads <c>LocalizationSettings.SelectedLocale</c> — a synchronous
    /// <c>WaitForCompletion</c> on Localization's initialisation, whose string tables load from Addressables bundles.
    /// With no saved anchor the placement sign is built on the first frames, while that initialisation is still
    /// running, and on this platform the blocking bundle load never completes: the watchdog kills the app a few
    /// seconds in, just after the music starts. Whether it happens is a race — a session that restores its board from
    /// an anchor shows no sign and survives, which is why the build before it ran. <c>XRGame.Start</c> carries the
    /// same assumption ("Localization has long finished starting by now").
    ///
    /// <b>The fix here</b> disables both behaviours before their first <c>Start</c> (this installs after the scene
    /// loads, before any <c>Start</c> runs) and enables them once <c>LocalizationSettings.InitializationOperation</c>
    /// is done, so every later read of the language is answered at once. Nothing else waits on them: Awake has run,
    /// so <c>XRGame.Flow</c> exists. The real fix belongs in the shared code — <c>ShowSign</c> and <c>Start</c>
    /// waiting on the initialisation themselves — as a seam commit on <c>feat/MetaXR</c>; then this goes.
    /// </remarks>
    public sealed class VisionOSLocalizationGate : MonoBehaviour
    {
        const string k_Tag = "[AVP localization]";

        /// <summary>Past this, let the game go anyway and say so: better a possible hang than certainly none of the game.</summary>
        const float k_GiveUpSeconds = 20f;

        Behaviour[] m_Held;
        float m_Started;

#if UNITY_VISIONOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var held = new System.Collections.Generic.List<Behaviour>();
            foreach (var game in FindObjectsByType<XRGame>(FindObjectsInactive.Exclude)) if (game.enabled) held.Add(game);
            foreach (var placement in FindObjectsByType<XRBoardPlacement>(FindObjectsInactive.Exclude)) if (placement.enabled) held.Add(placement);
            if (held.Count == 0) return;
            if (LocalizationSettings.InitializationOperation.IsDone) return;

            foreach (var behaviour in held) behaviour.enabled = false;
            var host = new GameObject(nameof(VisionOSLocalizationGate));
            DontDestroyOnLoad(host);
            var gate = host.AddComponent<VisionOSLocalizationGate>();
            gate.m_Held = held.ToArray();
            gate.m_Started = Time.realtimeSinceStartup;
            Debug.Log($"{k_Tag} Holding {held.Count} behaviour(s) until Localization has started.");
        }
#endif

        void Update()
        {
            var operation = LocalizationSettings.InitializationOperation;
            var waited = Time.realtimeSinceStartup - m_Started;
            if (!operation.IsDone && waited < k_GiveUpSeconds) return;

            if (operation.IsDone)
                Debug.Log($"{k_Tag} Started after {waited:F1}s ({operation.Status}); releasing the game.");
            else
                Debug.LogWarning($"{k_Tag} Still not started after {waited:F0}s; releasing the game anyway.");

            foreach (var behaviour in m_Held)
                if (behaviour != null) behaviour.enabled = true;
            Destroy(gameObject);
        }
    }
}
