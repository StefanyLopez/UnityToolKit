using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Steff.QuickSceneAccess.Editor
{
    internal enum OpenResult
    {
        Opened,
        Cancelled,
        AlreadyActive,
        AlreadyLoaded,
        BlockedByPlayMode,
        Missing,
        Failed
    }

    /// <summary>
    /// Opens scenes safely: never in Play Mode, always asks before discarding unsaved
    /// changes, and resolves the current path from the GUID so a stale cache can't
    /// open the wrong file.
    /// </summary>
    internal static class SceneOpener
    {
        public static OpenResult TryOpen(SceneEntry entry, bool additive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return OpenResult.BlockedByPlayMode;

            string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
            if (string.IsNullOrEmpty(path))
            {
                SceneCatalog.Invalidate();
                return OpenResult.Missing;
            }

            if (additive)
            {
                if (IsLoaded(path))
                    return OpenResult.AlreadyLoaded;
            }
            else
            {
                if (SceneManager.GetActiveScene().path == path)
                {
                    Ping(entry);
                    return OpenResult.AlreadyActive;
                }

                if (!HandleUnsavedChanges())
                    return OpenResult.Cancelled;
            }

            try
            {
                EditorSceneManager.OpenScene(
                    path,
                    additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Quick Scene Access] Could not open '{path}': {ex.Message}");
                return OpenResult.Failed;
            }

            QuickSceneAccessState.PushRecent(entry.Guid);
            return OpenResult.Opened;
        }

        /// <summary>
        /// Loads every scene of a set: the first one in single mode (replacing the current
        /// scenes, with the usual unsaved-changes handling), the rest additively, then makes
        /// the set's active scene active. Scenes that no longer exist are skipped and counted.
        /// </summary>
        public static OpenResult TryOpenSet(QuickSceneAccessState.SceneSetData set, out int missingCount)
        {
            missingCount = 0;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return OpenResult.BlockedByPlayMode;

            var paths = new System.Collections.Generic.List<string>();
            string activePath = null;

            foreach (string guid in set.guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    missingCount++;
                    continue;
                }

                if (paths.Contains(path))
                    continue;

                paths.Add(path);
                if (guid == set.activeGuid)
                    activePath = path;
            }

            if (paths.Count == 0)
                return OpenResult.Missing;

            if (!HandleUnsavedChanges())
                return OpenResult.Cancelled;

            try
            {
                Scene activeScene = default;
                bool hasActive = false;

                for (int i = 0; i < paths.Count; i++)
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        paths[i],
                        i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);

                    if (paths[i] == activePath)
                    {
                        activeScene = scene;
                        hasActive = true;
                    }
                }

                if (hasActive)
                    SceneManager.SetActiveScene(activeScene);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Quick Scene Access] Could not load set '{set.name}': {ex.Message}");
                return OpenResult.Failed;
            }

            QuickSceneAccessState.PushRecent(AssetDatabase.AssetPathToGUID(activePath ?? paths[0]));
            return OpenResult.Opened;
        }

        public static void Ping(SceneEntry entry)
        {
            string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        public static void Reveal(SceneEntry entry)
        {
            string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
            if (!string.IsNullOrEmpty(path))
                EditorUtility.RevealInFinder(path);
        }

        // Returns false when the switch should be cancelled.
        private static bool HandleUnsavedChanges()
        {
            switch (QuickSceneAccessState.UnsavedMode)
            {
                case UnsavedChangesMode.AutoSave:
                    return SaveModifiedScenes();

                case UnsavedChangesMode.Discard:
                    return true;

                default:
                    // Unity's standard Save / Don't Save / Cancel prompt, only if needed.
                    return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
        }

        private static bool SaveModifiedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.isDirty)
                    continue;

                // An untitled scene opens Unity's Save As dialog; cancelling it cancels the switch.
                if (!EditorSceneManager.SaveScene(scene))
                {
                    string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
                    Debug.LogWarning($"[Quick Scene Access] Could not save '{sceneName}', so the scene was not switched.");
                    return false;
                }
            }

            return true;
        }

        private static bool IsLoaded(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path)
                    return true;
            }

            return false;
        }
    }
}