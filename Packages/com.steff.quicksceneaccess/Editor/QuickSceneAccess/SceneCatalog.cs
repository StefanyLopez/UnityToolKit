using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Steff.QuickSceneAccess.Editor
{
    /// <summary>
    /// A scene asset in the project. Identity is the asset GUID, so favorites and
    /// recents survive renames and moves.
    /// </summary>
    internal sealed class SceneEntry
    {
        public SceneEntry(string guid, string assetPath)
        {
            Guid = guid;
            AssetPath = assetPath;
            Name = Path.GetFileNameWithoutExtension(assetPath);
            Folder = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
            DisplayFolder = Folder.StartsWith("Assets/", StringComparison.Ordinal)
                ? Folder.Substring("Assets/".Length)
                : Folder;
        }

        public string Guid { get; }
        public string AssetPath { get; }
        public string Name { get; }

        /// <summary>Full project-relative folder, e.g. "Assets/Scenes/Levels".</summary>
        public string Folder { get; }

        /// <summary>Folder without the leading "Assets/", e.g. "Scenes/Levels".</summary>
        public string DisplayFolder { get; }
    }

    /// <summary>
    /// Cached list of every scene under Assets/. The AssetDatabase is queried once and
    /// only again after <see cref="Invalidate"/> (called by the postprocessor below or
    /// the Refresh button).
    /// </summary>
    internal static class SceneCatalog
    {
        private static readonly string[] SearchFolders = { "Assets" };

        private static List<SceneEntry> cache;
        private static Dictionary<string, SceneEntry> byGuid;

        /// <summary>Raised after the cache was invalidated. Listeners should re-read <see cref="Scenes"/>.</summary>
        public static event Action Changed;

        public static IReadOnlyList<SceneEntry> Scenes
        {
            get
            {
                EnsureBuilt();
                return cache;
            }
        }

        public static bool TryGetByGuid(string guid, out SceneEntry entry)
        {
            EnsureBuilt();
            return byGuid.TryGetValue(guid ?? string.Empty, out entry);
        }

        public static void Invalidate()
        {
            cache = null;
            byGuid = null;
            Changed?.Invoke();
        }

        private static void EnsureBuilt()
        {
            if (cache != null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:Scene", SearchFolders);
            var list = new List<SceneEntry>(guids.Length);
            var map = new Dictionary<string, SceneEntry>(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || map.ContainsKey(guid))
                    continue;

                var entry = new SceneEntry(guid, path);
                list.Add(entry);
                map.Add(guid, entry);
            }

            list.Sort((a, b) =>
            {
                int byFolder = string.Compare(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);
                return byFolder != 0
                    ? byFolder
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            cache = list;
            byGuid = map;
        }
    }

    /// <summary>
    /// Invalidates the catalog when scene assets are added, deleted, renamed or moved.
    /// Folder operations are covered too: paths without an extension are treated as
    /// folders that may contain scenes.
    /// </summary>
    internal sealed class SceneCatalogPostprocessor : AssetPostprocessor
    {
        private static bool invalidateQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!Affects(importedAssets) && !Affects(deletedAssets) &&
                !Affects(movedAssets) && !Affects(movedFromAssetPaths))
                return;

            if (invalidateQueued)
                return;

            invalidateQueued = true;
            EditorApplication.delayCall += () =>
            {
                invalidateQueued = false;
                SceneCatalog.Invalidate();
            };
        }

        private static bool Affects(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.IsNullOrEmpty(Path.GetExtension(path)))
                    return true; // folder
            }

            return false;
        }
    }
}
