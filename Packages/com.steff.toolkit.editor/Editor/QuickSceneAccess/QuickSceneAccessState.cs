using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SteffsToolkit.Editor
{
    /// <summary>
    /// Per-project, per-user state for Quick Scene Access (favorites, recents, view mode).
    /// Stored as JSON in UserSettings/SteffsToolkit/QuickSceneAccess.json, which Unity's
    /// default .gitignore already excludes. Scenes are referenced by GUID.
    /// </summary>
    internal static class QuickSceneAccessState
    {
        private const int MaxRecents = 8;

        [Serializable]
        internal sealed class Data
        {
            public List<string> favorites = new List<string>();
            public List<string> recents = new List<string>();
            public bool groupByFolder;
            public int unsavedMode; // UnsavedChangesMode; 0 = Ask
        }

        private static Data data;

        /// <summary>Raised after favorites, recents or view mode changed.</summary>
        public static event Action Changed;

        private static Data Current => data ?? (data = Load());

        private static string FilePath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "UserSettings",
            "SteffsToolkit",
            "QuickSceneAccess.json");

        public static IReadOnlyList<string> Favorites => Current.favorites;
        public static IReadOnlyList<string> Recents => Current.recents;

        public static bool GroupByFolder
        {
            get => Current.groupByFolder;
            set
            {
                if (Current.groupByFolder == value)
                    return;

                Current.groupByFolder = value;
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

        public static bool IsFavorite(string guid) => Current.favorites.Contains(guid);

        public static void ToggleFavorite(string guid)
        {
            if (!Current.favorites.Remove(guid))
                Current.favorites.Add(guid);

            Save();
        }

        public static void PushRecent(string guid)
        {
            Current.recents.Remove(guid);
            Current.recents.Insert(0, guid);

            if (Current.recents.Count > MaxRecents)
                Current.recents.RemoveRange(MaxRecents, Current.recents.Count - MaxRecents);

            Save();
        }

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