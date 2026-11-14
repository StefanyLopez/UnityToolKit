using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Steff.QuickSceneAccess.Editor
{
    /// <summary>
    /// Snapshot of which scenes are active, loaded, or in the Build Settings scene list.
    /// Cheap to capture; rebuilt whenever the editor reports a relevant change.
    /// </summary>
    internal sealed class SceneStatus
    {
        public readonly struct BuildSlot
        {
            public BuildSlot(int index, bool enabled)
            {
                Index = index;
                Enabled = enabled;
            }

            /// <summary>Build index among enabled scenes, or -1 when disabled.</summary>
            public int Index { get; }
            public bool Enabled { get; }
        }

        private readonly HashSet<string> loaded = new HashSet<string>();
        private readonly Dictionary<string, BuildSlot> build = new Dictionary<string, BuildSlot>();

        public string ActivePath { get; private set; } = string.Empty;
        public int EnabledBuildCount { get; private set; }

        public bool IsActive(string path) => !string.IsNullOrEmpty(path) && path == ActivePath;
        public bool IsLoaded(string path) => loaded.Contains(path);
        public bool TryGetBuildSlot(string path, out BuildSlot slot) => build.TryGetValue(path, out slot);

        public static SceneStatus Capture()
        {
            var status = new SceneStatus
            {
                ActivePath = SceneManager.GetActiveScene().path
            };

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string path = SceneManager.GetSceneAt(i).path;
                if (!string.IsNullOrEmpty(path))
                    status.loaded.Add(path);
            }

            int buildIndex = 0;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(scene.path))
                    continue;

                int index = scene.enabled ? buildIndex++ : -1;
                if (scene.enabled)
                    status.EnabledBuildCount++;

                status.build.TryAdd(scene.path, new BuildSlot(index, scene.enabled));
            }

            return status;
        }
    }
}
