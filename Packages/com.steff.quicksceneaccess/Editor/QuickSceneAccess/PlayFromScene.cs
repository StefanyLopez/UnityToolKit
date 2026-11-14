using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Steff.QuickSceneAccess.Editor
{
    /// <summary>
    /// "Play from here": makes the Play button start the game from a chosen scene
    /// (for example your Menu) without opening it, using Unity's own
    /// EditorSceneManager.playModeStartScene. It is opt-in and always visible while active.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFromScene
    {
        static PlayFromScene()
        {
            EditorApplication.delayCall += Sync;
            SceneCatalog.Changed += Sync;
        }

        public static string CurrentGuid => QuickSceneAccessState.PlayFromGuid;

        public static bool IsActive => !string.IsNullOrEmpty(CurrentGuid);

        public static bool IsPlayFrom(string guid) => IsActive && CurrentGuid == guid;

        /// <summary>Name of the scene Play starts from, or null when the feature is off.</summary>
        public static string CurrentName =>
            IsActive && SceneCatalog.TryGetByGuid(CurrentGuid, out SceneEntry entry) ? entry.Name : null;

        /// <summary>Starts playing from <paramref name="entry"/>, or turns the feature off when null.</summary>
        public static void Set(SceneEntry entry)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Quick Scene Access] Exit Play Mode before changing the start scene.");
                return;
            }

            if (entry == null)
            {
                QuickSceneAccessState.PlayFromGuid = string.Empty;
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            // Don't silently replace a start scene that was set outside this tool.
            SceneAsset existing = EditorSceneManager.playModeStartScene;
            if (existing != null && !IsActive)
            {
                string existingPath = AssetDatabase.GetAssetPath(existing);
                if (existingPath != entry.AssetPath &&
                    !EditorUtility.DisplayDialog(
                        "Replace the Play Mode start scene?",
                        $"This project already starts Play Mode from '{existing.name}'.\n\n" +
                        $"Quick Scene Access will change it to '{entry.Name}'. " +
                        "Turning the feature off clears it (it will not restore the previous one).",
                        "Replace",
                        "Cancel"))
                    return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.AssetPath);
            if (asset == null)
                return;

            EditorSceneManager.playModeStartScene = asset;
            QuickSceneAccessState.PlayFromGuid = entry.Guid;
        }

        public static void Toggle(SceneEntry entry) => Set(IsPlayFrom(entry.Guid) ? null : entry);

        // Keeps Unity's setting in line with ours (after a restart, or if the scene was moved/deleted).
        private static void Sync()
        {
            if (!IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!SceneCatalog.TryGetByGuid(CurrentGuid, out SceneEntry entry))
            {
                QuickSceneAccessState.PlayFromGuid = string.Empty;
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            SceneAsset current = EditorSceneManager.playModeStartScene;
            if (current != null && AssetDatabase.GetAssetPath(current) == entry.AssetPath)
                return;

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.AssetPath);
            if (asset != null)
                EditorSceneManager.playModeStartScene = asset;
        }
    }
}