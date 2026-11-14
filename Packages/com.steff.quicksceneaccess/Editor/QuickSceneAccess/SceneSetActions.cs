using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steff.QuickSceneAccess.Editor
{
    /// <summary>
    /// Everything you can do with scene sets (create, load, update, rename, delete).
    /// Shared by the window, the overlay and the quick search so they behave the same way.
    /// </summary>
    internal static class SceneSetActions
    {
        public static void Notify(string message)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                view.ShowNotification(new GUIContent(message));
            else
                Debug.Log($"[Quick Scene Access] {message}");
        }

        // ---------------------------------------------------------------- loading

        public static void Load(QuickSceneAccessState.SceneSetData set)
        {
            switch (SceneOpener.TryOpenSet(set, out int missing))
            {
                case OpenResult.Opened:
                    if (missing > 0)
                    {
                        Notify($"Loaded '{set.name}' ({missing} missing scene{(missing == 1 ? "" : "s")} skipped).");
                        Debug.LogWarning(
                            $"[Quick Scene Access] Set '{set.name}' refers to {missing} scene(s) that no longer exist. " +
                            "Use 'Update From Loaded Scenes' to refresh it.");
                    }
                    break;
                case OpenResult.BlockedByPlayMode:
                    Notify("Exit Play Mode to switch scenes.");
                    break;
                case OpenResult.Missing:
                    Notify($"None of the scenes in '{set.name}' exist anymore.");
                    break;
                case OpenResult.Failed:
                    Notify("Could not load the set. See the Console.");
                    break;
                // Cancelled: nothing to report.
            }
        }

        /// <summary>True when exactly the scenes of the set are loaded right now.</summary>
        public static bool IsLoaded(QuickSceneAccessState.SceneSetData set)
        {
            List<string> loaded = LoadedPaths();
            List<string> wanted = set.guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();

            return wanted.Count > 0 && wanted.Count == loaded.Count && wanted.All(loaded.Contains);
        }

        // ---------------------------------------------------------------- creating / editing

        public static void SaveLoadedAsNew()
        {
            if (!TryCaptureLoaded(out List<string> guids, out string activeGuid))
                return;

            NamePromptWindow.Show("New scene set", NextDefaultName(), name =>
            {
                QuickSceneAccessState.CreateSet(name, guids, activeGuid);
            });
        }

        public static void NewSetWith(SceneEntry entry)
        {
            NamePromptWindow.Show("New scene set", NextDefaultName(), name =>
            {
                QuickSceneAccessState.CreateSet(name, new[] { entry.Guid }, entry.Guid);
            });
        }

        public static void UpdateFromLoaded(QuickSceneAccessState.SceneSetData set)
        {
            if (!TryCaptureLoaded(out List<string> guids, out string activeGuid))
                return;

            if (!EditorUtility.DisplayDialog(
                    "Update scene set?",
                    $"Replace the scenes in '{set.name}' with the {guids.Count} scene(s) currently loaded?",
                    "Update",
                    "Cancel"))
                return;

            QuickSceneAccessState.ReplaceSetScenes(set.id, guids, activeGuid);
        }

        public static void Rename(QuickSceneAccessState.SceneSetData set)
        {
            string id = set.id;
            NamePromptWindow.Show("Rename scene set", set.name, name => QuickSceneAccessState.RenameSet(id, name));
        }

        public static void Delete(QuickSceneAccessState.SceneSetData set)
        {
            if (EditorUtility.DisplayDialog(
                    "Delete scene set?",
                    $"Delete the set '{set.name}'? Your scenes are not affected.",
                    "Delete",
                    "Cancel"))
                QuickSceneAccessState.DeleteSet(set.id);
        }

        public static void ToggleInSet(SceneEntry entry, string setId)
        {
            QuickSceneAccessState.SceneSetData set = QuickSceneAccessState.FindSet(setId);
            if (set == null)
                return;

            if (set.guids.Contains(entry.Guid))
                QuickSceneAccessState.RemoveFromSet(setId, entry.Guid);
            else
                QuickSceneAccessState.AddToSet(setId, entry.Guid);
        }

        // ---------------------------------------------------------------- helpers

        private static string NextDefaultName() => $"Set {QuickSceneAccessState.Sets.Count + 1}";

        private static List<string> LoadedPaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }

            return paths;
        }

        // Captures the saved scenes that are loaded now, in order, plus the active one.
        private static bool TryCaptureLoaded(out List<string> guids, out string activeGuid)
        {
            guids = new List<string>();
            activeGuid = string.Empty;
            string activePath = SceneManager.GetActiveScene().path;
            int skipped = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                string guid = string.IsNullOrEmpty(scene.path) ? null : AssetDatabase.AssetPathToGUID(scene.path);
                if (string.IsNullOrEmpty(guid) || !SceneCatalog.TryGetByGuid(guid, out _))
                {
                    skipped++;
                    continue;
                }

                guids.Add(guid);
                if (scene.path == activePath)
                    activeGuid = guid;
            }

            if (guids.Count == 0)
            {
                Notify("Open at least one saved scene first.");
                return false;
            }

            if (skipped > 0)
                Notify($"{skipped} unsaved or non-project scene(s) were left out of the set.");

            return true;
        }
    }

    /// <summary>Builds the "Sets" dropdown menu used by the window toolbar.</summary>
    internal static class SceneSetMenu
    {
        public static GenericMenu Build()
        {
            var menu = new GenericMenu();
            bool canOpen = !EditorApplication.isPlayingOrWillChangePlaymode;
            IReadOnlyList<QuickSceneAccessState.SceneSetData> sets = QuickSceneAccessState.Sets;

            if (sets.Count == 0)
                menu.AddDisabledItem(new GUIContent("No sets yet"));

            foreach (QuickSceneAccessState.SceneSetData set in sets)
            {
                QuickSceneAccessState.SceneSetData captured = set;
                var label = new GUIContent($"{captured.name} ({captured.guids.Count})");

                if (canOpen)
                    menu.AddItem(label, SceneSetActions.IsLoaded(captured), () => SceneSetActions.Load(captured));
                else
                    menu.AddDisabledItem(label, SceneSetActions.IsLoaded(captured));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Save Loaded Scenes as New Set..."),
                false,
                SceneSetActions.SaveLoadedAsNew);

            if (sets.Count > 0)
            {
                menu.AddSeparator(string.Empty);

                foreach (QuickSceneAccessState.SceneSetData set in sets)
                {
                    QuickSceneAccessState.SceneSetData captured = set;
                    menu.AddItem(
                        new GUIContent($"Update From Loaded Scenes/{captured.name}"),
                        false,
                        () => SceneSetActions.UpdateFromLoaded(captured));
                }

                foreach (QuickSceneAccessState.SceneSetData set in sets)
                {
                    QuickSceneAccessState.SceneSetData captured = set;
                    menu.AddItem(
                        new GUIContent($"Rename/{captured.name}"),
                        false,
                        () => SceneSetActions.Rename(captured));
                }

                foreach (QuickSceneAccessState.SceneSetData set in sets)
                {
                    QuickSceneAccessState.SceneSetData captured = set;
                    menu.AddItem(
                        new GUIContent($"Delete/{captured.name}"),
                        false,
                        () => SceneSetActions.Delete(captured));
                }
            }

            return menu;
        }
    }
}