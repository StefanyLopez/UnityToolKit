using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SteffsToolkit.Editor
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

                // Shows Unity's standard Save / Don't Save / Cancel dialog if needed.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
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
