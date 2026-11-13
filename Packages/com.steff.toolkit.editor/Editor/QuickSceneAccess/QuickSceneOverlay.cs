using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    /// <summary>
    /// Scene view toolbar for Quick Scene Access: a dropdown with the current scene
    /// (click for favorites / recents / all scenes) and one button per favorite scene.
    /// It docks at the bottom of the Scene view by default but can be dragged anywhere.
    /// If it is not visible, open the Overlay menu (` key) and enable "Quick Scenes".
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Quick Scenes", defaultDisplay = true, defaultDockZone = DockZone.BottomToolbar)]
    public sealed class QuickSceneOverlay : ToolbarOverlay
    {
        public const string OverlayId = "steffs-toolkit-quick-scenes";

        public QuickSceneOverlay()
            : base(QuickSceneDropdown.ElementId, QuickSceneFavoritesStrip.ElementId)
        {
        }
    }

    /// <summary>Raises one event whenever anything the overlay displays may have changed.</summary>
    [InitializeOnLoad]
    internal static class QuickSceneEvents
    {
        public static event Action Changed;

        static QuickSceneEvents()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Raise();
            EditorSceneManager.sceneClosed += scene => Raise();
            EditorSceneManager.newSceneCreated += (scene, setup, mode) => Raise();
            EditorSceneManager.activeSceneChangedInEditMode += (previous, current) => Raise();
            EditorSceneManager.sceneDirtied += scene => Raise();
            EditorSceneManager.sceneSaved += scene => Raise();
            EditorApplication.playModeStateChanged += change => Raise();
            SceneCatalog.Changed += Raise;
            QuickSceneAccessState.Changed += Raise;
        }

        private static void Raise() => Changed?.Invoke();
    }

    internal static class QuickSceneOverlayUtil
    {
        public static void Open(SceneEntry entry, bool additive)
        {
            string message;
            switch (SceneOpener.TryOpen(entry, additive))
            {
                case OpenResult.BlockedByPlayMode: message = "Exit Play Mode to switch scenes."; break;
                case OpenResult.Missing: message = "That scene no longer exists."; break;
                case OpenResult.AlreadyLoaded: message = "That scene is already loaded."; break;
                case OpenResult.Failed: message = "Could not open the scene. See the Console."; break;
                default: message = null; break;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (message != null && view != null)
                view.ShowNotification(new GUIContent(message));
        }
    }

    /// <summary>Shows the active scene; click to pick another one.</summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public sealed class QuickSceneDropdown : EditorToolbarDropdown
    {
        public const string ElementId = "SteffsToolkit/QuickScenes/Dropdown";

        private const int FlatListLimit = 12;

        public QuickSceneDropdown()
        {
            icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            tooltip = "Switch scene";
            clicked += ShowMenu;

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                QuickSceneEvents.Changed += OnChanged;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => QuickSceneEvents.Changed -= OnChanged);
        }

        private void OnChanged() => schedule.Execute(Refresh);

        private void Refresh()
        {
            Scene scene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            text = scene.isDirty ? sceneName + " *" : sceneName;
        }

        private void ShowMenu()
        {
            var menu = new GenericMenu();
            bool canOpen = !EditorApplication.isPlayingOrWillChangePlaymode;
            string activePath = SceneManager.GetActiveScene().path;
            var shownGuids = new HashSet<string>();
            var usedLabels = new HashSet<string>();

            void AddItem(string label, SceneEntry entry)
            {
                if (!usedLabels.Add(label))
                    label = $"{label} ({entry.DisplayFolder})";

                bool isActive = entry.AssetPath == activePath;
                if (canOpen)
                    menu.AddItem(new GUIContent(label), isActive, () => QuickSceneOverlayUtil.Open(entry, false));
                else
                    menu.AddDisabledItem(new GUIContent(label), isActive);
            }

            // Favorites
            foreach (string guid in QuickSceneAccessState.Favorites)
            {
                if (SceneCatalog.TryGetByGuid(guid, out SceneEntry entry) && shownGuids.Add(guid))
                    AddItem(entry.Name, entry);
            }

            // Recents
            bool separatorPending = menu.GetItemCount() > 0;
            foreach (string guid in QuickSceneAccessState.Recents)
            {
                if (!SceneCatalog.TryGetByGuid(guid, out SceneEntry entry) || !shownGuids.Add(guid))
                    continue;

                if (separatorPending)
                {
                    menu.AddSeparator(string.Empty);
                    separatorPending = false;
                }

                AddItem(entry.Name, entry);
            }

            // Everything else
            IReadOnlyList<SceneEntry> all = SceneCatalog.Scenes;
            bool flat = all.Count <= FlatListLimit;

            if (flat)
            {
                separatorPending = menu.GetItemCount() > 0;
                foreach (SceneEntry entry in all)
                {
                    if (shownGuids.Contains(entry.Guid))
                        continue;

                    if (separatorPending)
                    {
                        menu.AddSeparator(string.Empty);
                        separatorPending = false;
                    }

                    AddItem(entry.Name, entry);
                }
            }
            else
            {
                if (menu.GetItemCount() > 0)
                    menu.AddSeparator(string.Empty);

                foreach (SceneEntry entry in all)
                {
                    string folder = entry.DisplayFolder == "Assets" ? string.Empty : entry.DisplayFolder + "/";
                    AddItem($"All Scenes/{folder}{entry.Name}", entry);
                }
            }

            if (all.Count == 0)
                menu.AddDisabledItem(new GUIContent("No scenes in this project"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Open Quick Scene Access Window"),
                false,
                QuickSceneAccessWindow.ShowWindow);

            menu.ShowAsContext();
        }
    }

    /// <summary>
    /// A star for the active scene plus one button per favorite scene.
    /// Click opens, Shift+Click opens additively.
    /// </summary>
    [EditorToolbarElement(ElementId, typeof(SceneView))]
    public sealed class QuickSceneFavoritesStrip : VisualElement
    {
        public const string ElementId = "SteffsToolkit/QuickScenes/Favorites";

        private const int MaxButtons = 6;
        private const int MaxLabelLength = 18;

        public QuickSceneFavoritesStrip()
        {
            style.flexDirection = FlexDirection.Row;

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                QuickSceneEvents.Changed += OnChanged;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => QuickSceneEvents.Changed -= OnChanged);
        }

        private void OnChanged() => schedule.Execute(Refresh);

        private void Refresh()
        {
            if (panel == null)
                return;

            Clear();

            bool canOpen = !EditorApplication.isPlayingOrWillChangePlaymode;
            string activePath = SceneManager.GetActiveScene().path;

            // Star: add/remove the active scene from favorites.
            if (!string.IsNullOrEmpty(activePath) &&
                SceneCatalog.TryGetByGuid(AssetDatabase.AssetPathToGUID(activePath), out SceneEntry current))
            {
                bool isFavorite = QuickSceneAccessState.IsFavorite(current.Guid);
                var star = new EditorToolbarButton
                {
                    text = isFavorite ? "★" : "☆",
                    tooltip = isFavorite
                        ? $"Remove '{current.Name}' from favorites"
                        : $"Add '{current.Name}' to favorites"
                };
                star.RegisterCallback<ClickEvent>(_ => QuickSceneAccessState.ToggleFavorite(current.Guid));
                Add(star);
            }

            int count = 0;
            foreach (string guid in QuickSceneAccessState.Favorites)
            {
                if (count >= MaxButtons)
                    break;

                if (!SceneCatalog.TryGetByGuid(guid, out SceneEntry entry))
                    continue;

                var button = new EditorToolbarButton
                {
                    text = Truncate(entry.Name),
                    tooltip = $"{entry.AssetPath}\nClick: open  |  Shift+Click: open additively"
                };

                if (entry.AssetPath == activePath)
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                button.SetEnabled(canOpen);
                button.RegisterCallback<ClickEvent>(evt => QuickSceneOverlayUtil.Open(entry, evt.shiftKey));
                Add(button);
                count++;
            }
        }

        private static string Truncate(string value) =>
            value.Length <= MaxLabelLength ? value : value.Substring(0, MaxLabelLength - 2) + "..";
    }
}