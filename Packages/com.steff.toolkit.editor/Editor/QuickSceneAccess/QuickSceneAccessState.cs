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

        /// <summary>A saved group of scenes that can be loaded together.</summary>
        [Serializable]
        internal sealed class SceneSetData
        {
            public string id;
            public string name;
            public List<string> guids = new List<string>(); // load order; the first opens in single mode
            public string activeGuid = string.Empty;        // scene made active after loading (optional)
        }

        [Serializable]
        internal sealed class Data
        {
            public List<SceneSetData> sets = new List<SceneSetData>();
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

        // ---------------------------------------------------------------- scene sets

        public static IReadOnlyList<SceneSetData> Sets => Current.sets;

        public static SceneSetData FindSet(string id) => Current.sets.Find(s => s.id == id);

        public static SceneSetData CreateSet(string name, IEnumerable<string> guids, string activeGuid)
        {
            var set = new SceneSetData
            {
                id = Guid.NewGuid().ToString("N"),
                name = MakeUniqueSetName(name),
                guids = new List<string>(guids),
                activeGuid = activeGuid ?? string.Empty
            };

            Current.sets.Add(set);
            Save();
            return set;
        }

        public static void RenameSet(string id, string name)
        {
            SceneSetData set = FindSet(id);
            if (set == null)
                return;

            set.name = MakeUniqueSetName(name, id);
            Save();
        }

        public static void DeleteSet(string id)
        {
            if (Current.sets.RemoveAll(s => s.id == id) > 0)
                Save();
        }

        public static void ReplaceSetScenes(string id, IEnumerable<string> guids, string activeGuid)
        {
            SceneSetData set = FindSet(id);
            if (set == null)
                return;

            set.guids = new List<string>(guids);
            set.activeGuid = activeGuid ?? string.Empty;
            Save();
        }

        public static void AddToSet(string id, string guid)
        {
            SceneSetData set = FindSet(id);
            if (set == null || set.guids.Contains(guid))
                return;

            set.guids.Add(guid);
            if (string.IsNullOrEmpty(set.activeGuid))
                set.activeGuid = guid;
            Save();
        }

        public static void RemoveFromSet(string id, string guid)
        {
            SceneSetData set = FindSet(id);
            if (set == null || !set.guids.Remove(guid))
                return;

            if (set.activeGuid == guid)
                set.activeGuid = set.guids.Count > 0 ? set.guids[0] : string.Empty;
            Save();
        }

        // Names are shown in menus where "/" would create a submenu, so it is replaced.
        private static string MakeUniqueSetName(string name, string ignoreId = null)
        {
            string baseName = string.IsNullOrWhiteSpace(name) ? "Set" : name.Trim().Replace('/', '-');
            string candidate = baseName;
            int n = 2;

            while (Current.sets.Exists(s => s.id != ignoreId &&
                                            string.Equals(s.name, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = $"{baseName} {n++}";

            return candidate;
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
                    if (loaded.sets == null) loaded.sets = new List<SceneSetData>();
                    loaded.sets.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
                    foreach (SceneSetData set in loaded.sets)
                    {
                        if (set.guids == null) set.guids = new List<string>();
                        if (string.IsNullOrEmpty(set.name)) set.name = "Set";
                    }

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