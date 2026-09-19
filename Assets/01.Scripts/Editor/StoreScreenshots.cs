using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// Window &gt; TrainSudoku &gt; Screenshots. Shoots the App Store and Google Play screenshots out of the Game view at
    /// the exact pixel sizes the stores demand, so a submission is a matter of pressing F12 on a running game rather
    /// than resizing a window by hand and hoping.
    /// </summary>
    /// <remarks>
    /// <b>The captures must have no alpha.</b> App Store Connect rejects a PNG with an alpha channel outright, and the
    /// rejection names the file rather than the cause, so it reads as a mysterious upload failure. Unity's
    /// <see cref="ScreenCapture.CaptureScreenshot(string)"/> writes an opaque image as long as the camera clears to an
    /// opaque colour — which is the default and what this game does. If a capture ever comes back transparent, the
    /// fault is the camera's <c>clearFlags</c> (a Don't Clear or a Solid Color with a zero alpha), not this script.
    ///
    /// <b>The output folder is deliberately outside <c>Assets/</c>.</b> A 2064x2752 PNG imported as a texture costs
    /// import time and Library space on every capture, and would end up in a build. <c>StoreScreenshots/</c> sits
    /// beside <c>Assets/</c> where the AssetDatabase never looks, so nothing here calls <c>AssetDatabase.Refresh</c>.
    ///
    /// The Game view size API this registers into is all internal (<c>UnityEditor.GameViewSizes</c> and friends), so
    /// the registration half is reflection. It is written to report a missing symbol by name instead of throwing a
    /// raw reflection exception, because the only thing that would break it is a future Unity renaming one of them,
    /// and "GameViewSize has no (type, width, height, name) constructor" is a fixable message where a
    /// <c>NullReferenceException</c> inside <c>AddStoreSizes</c> is not.
    /// </remarks>
    public static class StoreScreenshots
    {
        private const string Menu = "Window/TrainSudoku/Screenshots/";

        /// <summary>The folder name, resolved against the project root rather than <c>Assets/</c>. See the remarks.</summary>
        private const string FolderName = "StoreScreenshots";

        /// <summary>
        /// The sizes the two stores ask for: a 6.9" iPhone, a 13" iPad and the Play Store's phone screenshot. The
        /// names are the ones that appear in the Game view dropdown.
        /// </summary>
        /// <remarks>
        /// <b>The names deliberately do not state their own resolution.</b> Unity composes a custom size's display
        /// text as <c>baseText (WxH)</c>, so a name that already carries the numbers is shown with them twice — the
        /// dropdown reads <c>Store iPad 13 (2064x2752) (2064x2752)</c>. Naming them plainly lets Unity add the
        /// numbers once, which is the form a human actually scans a thirty-entry dropdown for.
        ///
        /// The name is also the identity <see cref="GroupContains"/> matches on to stay re-runnable, so renaming an
        /// entry here does not rename the registered size: any size already added under the old name stays in the
        /// dropdown and a fresh one appears beside it. Delete the stale one from the Game view dropdown by hand.
        /// Unity clamps a base text to 40 characters — keep new entries under it, or the clamp truncates the name and
        /// the duplicate check stops recognising its own size.
        /// </remarks>
        private static readonly (string Name, int Width, int Height)[] StoreSizes =
        {
            ("Store iPhone 6.9", 1320, 2868),
            ("Store iPad 13", 2064, 2752),
            ("Store Play Phone", 1080, 1920),
        };

        /// <summary>
        /// Paths handed to <see cref="ScreenCapture"/> this session. <see cref="ScreenCapture.CaptureScreenshot(string)"/>
        /// writes at the end of the frame, so a file requested a moment ago does not exist yet and
        /// <see cref="File.Exists"/> would hand the same name out twice — two F12s in quick succession would silently
        /// overwrite the first shot. Remembering what has been asked for is the only way to see the collision coming.
        /// </summary>
        private static readonly HashSet<string> Requested = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// <c>StoreScreenshots/</c> at the project root, the sibling of <c>Assets/</c>. Derived from
        /// <see cref="Application.dataPath"/> rather than the working directory, which a batchmode run can set anywhere.
        /// </summary>
        public static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));

        /// <summary>
        /// Registers the store sizes in the Game view's Standalone group, skipping any that is already there so the
        /// item can be run whenever and never stacks up duplicates.
        /// </summary>
        [MenuItem(Menu + "Add Store Game View Sizes", priority = 100)]
        public static void AddStoreGameViewSizes()
        {
            if (!TryResolveGroup(out var group, out var gameViewSize, out var fixedResolution)) return;

            var constructor = FindSizeConstructor(gameViewSize);
            if (constructor == null)
            {
                Debug.LogError("Screenshots: UnityEditor.GameViewSize has no (GameViewSizeType, int, int, string) " +
                               "constructor. Unity changed the signature; find it in UnityEditor.dll and update " +
                               nameof(FindSizeConstructor) + ".");
                return;
            }

            var addCustomSize = group.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
            if (addCustomSize == null)
            {
                Debug.LogError("Screenshots: UnityEditor.GameViewSizeGroup has no AddCustomSize method.");
                return;
            }

            var added = 0;
            var skipped = 0;
            foreach (var size in StoreSizes)
            {
                if (GroupContains(group, size.Name)) { skipped++; continue; }

                var instance = constructor.Invoke(new[] { fixedResolution, (object)size.Width, size.Height, size.Name });
                addCustomSize.Invoke(group, new[] { instance });
                added++;
            }

            if (added > 0) PersistSizes();
            Debug.Log($"Screenshots: {added} Game view size(s) added, {skipped} already present. " +
                      "Pick one from the Game view's size dropdown, then press F12 in Play mode.");
        }

        /// <summary>
        /// Asks for a screenshot of the Game view at whatever size it is currently rendering, named for that size.
        /// Bound to F12 because the game has to be running to be worth photographing and the menu bar is a long way
        /// from a board mid-play.
        /// </summary>
        /// <remarks>
        /// The file is <b>not</b> written by the time this returns — <see cref="ScreenCapture.CaptureScreenshot(string)"/>
        /// only queues the request and the encode happens at the end of the frame. So nothing here reads the file back
        /// or claims it is done; the log says where it is going to land. In Edit mode the Game view may not render
        /// another frame at all, in which case the request sits unfulfilled forever, which is why that case warns.
        /// </remarks>
        [MenuItem(Menu + "Capture (F12) _F12", priority = 101)]
        public static void Capture()
        {
            var folder = OutputFolder;
            Directory.CreateDirectory(folder);

            // Public but undocumented, and the only honest source: Screen.width in an editor script reports the
            // focused EditorWindow, not the Game view, so it would name a 1320x2868 shot after the Inspector.
            var size = Handles.GetMainGameViewSize();
            var width = Mathf.RoundToInt(size.x);
            var height = Mathf.RoundToInt(size.y);

            var path = NextPath(folder, width, height);
            Requested.Add(path);
            ScreenCapture.CaptureScreenshot(path);

            if (!EditorApplication.isPlaying)
                Debug.LogWarning("Screenshots: not in Play mode. The capture is queued, but it is only written once " +
                                 "the Game view renders another frame — enter Play mode to be sure it lands.");

            Debug.Log($"Screenshots: requested {width}x{height} ->\n{path}\n" +
                      "Written at the end of the frame, so give it a moment before opening it.");
        }

        /// <summary>Opens the output folder, creating it first so the item never fails on a project that has yet to capture.</summary>
        [MenuItem(Menu + "Open Folder", priority = 102)]
        public static void OpenFolder()
        {
            var folder = OutputFolder;
            Directory.CreateDirectory(folder);

            // The trailing separator is what makes RevealInFinder open the folder rather than select it in its parent.
            EditorUtility.RevealInFinder(folder + Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// The first free <c>&lt;width&gt;x&lt;height&gt;_&lt;NN&gt;.png</c> in <paramref name="folder"/>, counting
        /// past both the files on disk and the ones queued this session.
        /// </summary>
        private static string NextPath(string folder, int width, int height)
        {
            var prefix = $"{width}x{height}_";
            for (var index = 1; index <= 999; index++)
            {
                var candidate = Path.Combine(folder, prefix + index.ToString("D2", CultureInfo.InvariantCulture) + ".png");
                if (!File.Exists(candidate) && !Requested.Contains(candidate)) return candidate;
            }

            // 999 shots of one size in one folder is somebody's mistake, not a case worth a counter; timestamp it and
            // let them sort it out rather than overwriting a file.
            return Path.Combine(folder, prefix + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".png");
        }

        /// <summary>
        /// The Standalone group's <c>GameViewSizeGroup</c>, plus the two internal types the caller needs to build a
        /// size. Returns false having logged which symbol was missing.
        /// </summary>
        /// <remarks>
        /// Standalone is the group the Editor shows while the build target is macOS: a size added to the iOS or
        /// Android group is real but invisible until the project switches platform, which reads as the menu item
        /// having done nothing.
        /// </remarks>
        private static bool TryResolveGroup(out object group, out Type gameViewSize, out object fixedResolution)
        {
            group = null;
            gameViewSize = null;
            fixedResolution = null;

            var sizesType = FindEditorType("UnityEditor.GameViewSizes");
            var groupTypeEnum = FindEditorType("UnityEditor.GameViewSizeGroupType");
            var sizeTypeEnum = FindEditorType("UnityEditor.GameViewSizeType");
            gameViewSize = FindEditorType("UnityEditor.GameViewSize");
            if (sizesType == null || groupTypeEnum == null || sizeTypeEnum == null || gameViewSize == null) return false;

            // instance is declared on ScriptableSingleton<GameViewSizes>, the base — GameViewSizes itself has no such
            // member, so a lookup on the derived type alone comes back null.
            var instanceProperty = sizesType.BaseType?.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty == null)
            {
                Debug.LogError("Screenshots: ScriptableSingleton<GameViewSizes>.instance not found.");
                return false;
            }

            var sizes = instanceProperty.GetValue(null);
            if (sizes == null)
            {
                Debug.LogError("Screenshots: GameViewSizes.instance was null.");
                return false;
            }

            var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance);
            if (getGroup == null)
            {
                Debug.LogError("Screenshots: UnityEditor.GameViewSizes has no GetGroup method.");
                return false;
            }

            group = getGroup.Invoke(sizes, new[] { Enum.Parse(groupTypeEnum, "Standalone") });
            if (group == null)
            {
                Debug.LogError("Screenshots: GetGroup(Standalone) returned no group.");
                return false;
            }

            fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
            return true;
        }

        /// <summary>
        /// The <c>(GameViewSizeType, int, int, string)</c> constructor, matched on shape rather than on an exact
        /// parameter list: the enum is internal, so it cannot be named at compile time, and the signature has changed
        /// between Unity versions before.
        /// </summary>
        private static ConstructorInfo FindSizeConstructor(Type gameViewSize)
        {
            foreach (var candidate in gameViewSize.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var parameters = candidate.GetParameters();
                if (parameters.Length != 4) continue;
                if (!parameters[0].ParameterType.IsEnum) continue;
                if (parameters[1].ParameterType != typeof(int) || parameters[2].ParameterType != typeof(int)) continue;
                if (parameters[3].ParameterType != typeof(string)) continue;
                return candidate;
            }

            return null;
        }

        /// <summary>
        /// Whether the group already holds a size with this name. Matches on <c>baseText</c>, the name as typed, not
        /// on the display text, which Unity has already decorated with the resolution.
        /// </summary>
        private static bool GroupContains(object group, string name)
        {
            var groupType = group.GetType();
            var getTotalCount = groupType.GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
            var getGameViewSize = groupType.GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
            if (getTotalCount == null || getGameViewSize == null)
            {
                // Better to add a duplicate than to refuse to add anything, so this is a warning and a "no".
                Debug.LogWarning("Screenshots: GameViewSizeGroup has no GetTotalCount/GetGameViewSize, so existing " +
                                 "sizes could not be checked. A duplicate entry may appear in the dropdown.");
                return false;
            }

            var count = (int)getTotalCount.Invoke(group, null);
            for (var i = 0; i < count; i++)
            {
                var size = getGameViewSize.Invoke(group, new object[] { i });
                var baseText = size?.GetType()
                    .GetProperty("baseText", BindingFlags.Public | BindingFlags.Instance)?
                    .GetValue(size) as string;
                if (string.Equals(baseText, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Flushes the size list to disk. Unity would save it on quit anyway, so a failure here is not worth an error
        /// — it only means a crash before quitting loses the entries and the menu item has to be run again.
        /// </summary>
        private static void PersistSizes()
        {
            var sizesType = FindEditorType("UnityEditor.GameViewSizes");
            var instance = sizesType?.BaseType?.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var saveToHdd = instance != null
                ? sizesType.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.Instance)
                : null;
            saveToHdd?.Invoke(instance, null);
        }

        /// <summary>
        /// One of the Editor's internal types by full name. Tried against the <c>UnityEditor</c> assembly first and
        /// then against the two editor assemblies by name, because Unity has split the editor into modules over time
        /// and a type can move between <c>UnityEditor.dll</c> and a <c>UnityEditor.*Module.dll</c> without warning.
        /// </summary>
        private static Type FindEditorType(string fullName)
        {
            var type = Type.GetType(fullName + ",UnityEditor", false);
            if (type != null) return type;

            // Named through two types that cannot move out of their assemblies, rather than a sweep of the whole
            // AppDomain: AppDomain.GetAssemblies can hand back assemblies Unity has already unloaded (UAC0005), and
            // UnityEditor.dll and UnityEditor.CoreModule.dll are the only two homes these types have ever had.
            foreach (var assembly in new[] { typeof(Handles).Assembly, typeof(EditorWindow).Assembly })
            {
                type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }

            Debug.LogError($"Screenshots: the internal type {fullName} was not found in the editor assemblies. " +
                           "Unity has renamed or moved it; the Game view size registration needs updating.");
            return null;
        }
    }
}
