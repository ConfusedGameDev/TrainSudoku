using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Keeps Addressables on <see cref="BuildScriptPackedMode"/>.
    ///
    /// Addressables 4.x defaults to <c>BuildScriptSchemaDriven</c>, whose <c>ContentDirectorySchemaBuilder.Init</c>
    /// writes <c>Library/BuildInstructions/ContentDirectoryRootAssets/AddressableRootAsset.asset</c>. Unity
    /// 6000.7.0a6 does not mount that folder in the AssetDatabase, so <c>CreateAsset</c> throws and every player
    /// build dies in <c>AddressablesPlayerBuildProcessor.PrepareForBuild</c>. Nothing in this project asks for
    /// content directories, so the schema-driven builder buys us nothing and costs us the build.
    ///
    /// This has to be a guard rather than a one-off edit to the settings asset: the package's own
    /// <c>AddressableAssetSettings.CheckForUpgrades</c> is an <c>[InitializeOnLoadMethod]</c> that, whenever
    /// <c>ENABLE_CONTENT_DIRECTORIES</c> is on, flips an active PackedMode builder to the schema-driven one — on
    /// every single domain reload. So do this after theirs (an assembly-reload callback) and again at the top of a
    /// player build.
    /// </summary>
    internal static class AddressablesBuildGuard
    {
        /// <summary>Puts the active builder back if something has changed it. Returns true when it had to act.</summary>
        internal static bool Enforce(bool logWhenChanged)
        {
            if (!AddressableAssetSettingsDefaultObject.SettingsExists) return false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.DataBuilders == null) return false;

            if (settings.ActivePlayerDataBuilder is BuildScriptPackedMode) return false;

            var packed = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedMode);
            if (packed < 0)
            {
                Debug.LogWarning("Addressables has no BuildScriptPackedMode data builder; player builds will fail.");
                return false;
            }

            var previous = settings.ActivePlayerDataBuilder;
            settings.ActivePlayerDataBuilderIndex = packed;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            if (logWhenChanged)
                Debug.Log($"Addressables build script was '{(previous == null ? "none" : previous.Name)}'; " +
                          "put back to BuildScriptPackedMode (see AddressablesBuildGuard).");

            return true;
        }
    }

    internal static class AddressablesBuildGuardLoader
    {
        [InitializeOnLoadMethod]
        private static void OnLoad() => AssemblyReloadEvents.afterAssemblyReload += AfterReload;

        // Not EditorApplication.delayCall: that waits for an editor tick, which an unfocused Editor can withhold
        // for minutes. afterAssemblyReload runs in-domain straight after every [InitializeOnLoadMethod], which is
        // exactly where the package does its flip. Silent, because this has to fire on every single recompile.
        private static void AfterReload()
        {
            AssemblyReloadEvents.afterAssemblyReload -= AfterReload;
            AddressablesBuildGuard.Enforce(false);
        }
    }

    /// <summary>
    /// Last chance before a player build, which is where the schema-driven builder actually breaks. This has to be
    /// a <see cref="BuildPlayerProcessor"/> rather than an <c>IPreprocessBuildWithReport</c>: Addressables builds
    /// its content from <c>AddressablesPlayerBuildProcessor.PrepareForBuild</c>, which runs in the earlier
    /// <c>BuildPipeline.PreparePlayerBuild</c> pass, before any preprocess-build callback.
    /// </summary>
    internal sealed class AddressablesBuildGuardPreprocessor : BuildPlayerProcessor
    {
        // Ahead of AddressablesPlayerBuildProcessor, which sits at 1.
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext) =>
            AddressablesBuildGuard.Enforce(true);
    }
}
