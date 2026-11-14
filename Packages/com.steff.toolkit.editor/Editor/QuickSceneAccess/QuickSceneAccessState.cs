using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SteffsToolkit.Editor
{
    /// <summary>
    /// Per-project, per-user state for Quick Scene Access (favorites, recents, colors,
    /// grouping, collapsed sections, unsaved-changes mode).
    /// Stored as JSON in UserSettings/SteffsToolkit/QuickSceneAccess.json, which Unity's
    /// default .gitignore already excludes. Scenes are referenced by GUID.
    /// </summary>
    internal static class QuickSceneAccessState
    {
        private const int MaxRecents = 8;

        [Serializable]
        internal sealed class ColorEntry
        {
            public string guid;
            public int color; // SceneColor
        }

        [Serializable]
        internal sealed class Data
        {
            public List<string> favorites = new List<string>();
            public List<string> recents = new List<string>();
            public List<ColorEntry> colors = new List<ColorEntry>();
            public List<string> collapsed = new List<string>();
            public bool groupByFolder; // legacy: migrated to groupMode on load
            public int groupMode;      // SceneGroupMode; 0 = None
            public int unsavedMode;    // UnsavedChangesMode; 0 = Ask
            public string playFromGuid = string.Empty; // "Play from here" scene; empty = off
        }

        private static Data data;
        private static Dictionary<string, SceneColor> colorMap;

        /// <summary>Raised after anything stored here changed.</summary>
        public static event Action Changed;

        private static Data Current => data ?? (data = Load());

        private static string FilePath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "UserSettings",
            "SteffsToolkit",
            "QuickSceneAccess.json");

        public static IReadOnlyList<string> Favorites => Current.favorites;
        public static IReadOnlyList<string> Recents => Current.recents;

        // ---------------------------------------------------------------- view settings

        public static SceneGroupMode GroupMode
        {
            get => Enum.IsDefined(typeof(SceneGroupMode), Current.groupMode)
                ? (SceneGroupMode)Current.groupMode
                : SceneGroupMode.None;
            set
            {
                int mode = (int)value;
                if (Current.groupMode == mode)
                    return;

                Current.groupMode = mode;
                Save();
            }
        }

        public static UnsavedChangesMode UnsavedMode
        {
            get => Enum.IsDefined(typeof(UnsavedChangesMode), Current.unsavedMode)
                ? (UnsavedChangesMode)Current.unsavedMode
                : UnsavedChangesMode.Ask;
            set
            {
                int mode = (int)value;
                if (Current.unsavedMode == mode)
                    return;

                Current.unsavedMode = mode;
                Save();
            }
        }

        public static string PlayFromGuid
        {
            get => Current.playFromGuid ?? string.Empty;
            set
            {
                value = value ?? string.Empty;
                if (Current.playFromGuid == value)
                    return;

                Current.playFromGuid = value;
                Save();
            }
        }

        // ---------------------------------------------------------------- favorites / recents

        public static bool IsFavorite(string guid) => Current.favorites.Contains(guid);

        public static void ToggleFavorite(string guid)
        {
            if (Current.favorites.Remove(guid))
            {
                Save();
                return;
            }

            Current.favorites.Add(guid);

            // A newly favorited scene without a color gets the least-used palette color,
            // so favorites are easy to tell apart. A color you chose yourself is never replaced.
            if (GetColor(guid) == SceneColor.None)
            {
                Current.colors.Add(new ColorEntry { guid = guid, color = (int)PickLeastUsedColor() });
                colorMap = null;
            }

            Save();
        }

        private static SceneColor PickLeastUsedColor()
        {
            var usage = new Dictionary<SceneColor, int>();
            foreach (SceneColor used in ColorMap.Values)
            {
                usage.TryGetValue(used, out int current);
                usage[used] = current + 1;
            }

            SceneColor best = SceneColor.Red;
            int bestCount = int.MaxValue;
            foreach (SceneColor candidate in SceneColorUtil.All)
            {
                if (candidate == SceneColor.None)
                    continue;

                usage.TryGetValue(candidate, out int count);
                if (count < bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            return best;
        }

        public static void PushRecent(string guid)
        {
            Current.recents.Remove(guid);
            Current.recents.Insert(0, guid);

            if (Current.recents.Count > MaxRecents)
                Current.recents.RemoveRange(MaxRecents, Current.recents.Count - MaxRecents);

            Save();
        }

        // ---------------------------------------------------------------- colors

        private static Dictionary<string, SceneColor> ColorMap
        {
            get
            {
                if (colorMap != null)
                    return colorMap;

                colorMap = new Dictionary<string, SceneColor>();
                foreach (ColorEntry entry in Current.colors)
                {
                    if (!string.IsNullOrEmpty(entry.guid) &&
                        entry.color != 0 &&
                        Enum.IsDefined(typeof(SceneColor), entry.color))
                        colorMap[entry.guid] = (SceneColor)entry.color;
                }

                return colorMap;
            }
        }

        public static SceneColor GetColor(string guid) =>
            ColorMap.TryGetValue(guid ?? string.Empty, out SceneColor color) ? color : SceneColor.None;

        public static void SetColor(string guid, SceneColor color)
        {
            Current.colors.RemoveAll(e => e.guid == guid);
            if (color != SceneColor.None)
                Current.colors.Add(new ColorEntry { guid = guid, color = (int)color });

            colorMap = null;
            Save();
        }

        // ---------------------------------------------------------------- collapsed sections

        public static bool IsCollapsed(string key) => Current.collapsed.Contains(key);

        public static void ToggleCollapsed(string key)
        {
            if (!Current.collapsed.Remove(key))
                Current.collapsed.Add(key);

            Save();
        }

        /// <summary>Collapses the given sections, or expands everything when collapsed is false.</summary>
        public static void SetAllCollapsed(IEnumerable<string> keys, bool collapsed)
        {
            if (collapsed)
            {
                foreach (string key in keys)
                {
                    if (!Current.collapsed.Contains(key))
                        Current.collapsed.Add(key);
                }
            }
            else
            {
                Current.collapsed.Clear();
            }

            Save();
        }

        // ---------------------------------------------------------------- persistence

        private static Data Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new Data();

            try
            {
                var loaded = JsonUtility.FromJson<Data>(File.ReadAllText(path));
                if (loaded != null)
                {
                    if (loaded.favorites == null) loaded.favorites = new List<string>();
                    if (loaded.recents == null) loaded.recents = new List<string>();
                    if (loaded.colors == null) loaded.colors = new List<ColorEntry>();
                    if (loaded.collapsed == null) loaded.collapsed = new List<string>();

                    if (loaded.groupMode == 0 && loaded.groupByFolder)
                        loaded.groupMode = (int)SceneGroupMode.Folder;
                    loaded.groupByFolder = false;

                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[Quick Scene Access] Could not read '{path}' ({ex.Message}). " +
                    "A copy was kept as '.corrupt' and the state was reset.");
            }

            BackupUnreadableFile(path);
            return new Data();
        }

        private static void BackupUnreadableFile(string path)
        {
            try
            {
                File.Copy(path, path + ".corrupt", true);
            }
            catch (Exception)
            {
                // Best effort only.
            }
        }

        private static void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.WriteAllText(path, JsonUtility.ToJson(Current, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Quick Scene Access] Could not save state: {ex.Message}");
            }

            Changed?.Invoke();
        }
    }
}