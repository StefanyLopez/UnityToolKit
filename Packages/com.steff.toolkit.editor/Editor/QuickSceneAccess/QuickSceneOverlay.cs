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
    /// <summary>How the overlay is currently laid out, depending on where it is docked.</summary>
    internal enum QuickSceneLayout
    {
        Horizontal, // docked at the top or bottom of the Scene view
        Vertical,   // docked at the left or right edge
        Panel       // floating or docked in a column
    }

    /// <summary>
    /// Scene view overlay for Quick Scene Access: a scene dropdown, a star for the active
    /// scene, a save-mode button and one-click buttons for your favorite scenes.
    /// Unity asks the overlay for different content depending on where it is docked, so it
    /// reorganizes itself (row, column or panel) when you drag it to another edge.
    /// If it is not visible, open the Overlay menu (` key) and enable "Quick Scenes".
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Quick Scenes", defaultDisplay = true, defaultDockZone = DockZone.BottomToolbar)]
    public sealed class QuickSceneOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar
    {
        public const string OverlayId = "steffs-toolkit-quick-scenes";

        public override VisualElement CreatePanelContent()
        {
            var content = new VisualElement();
            content.style.minWidth = 180;
            content.style.paddingTop = 4;
            content.style.paddingBottom = 4;
            content.style.paddingLeft = 4;
            content.style.paddingRight = 4;
            content.Add(new QuickSceneDropdown(QuickSceneLayout.Panel));
            content.Add(new QuickSceneFavoritesStrip(QuickSceneLayout.Panel));
            return content;
        }

        public OverlayToolbar CreateHorizontalToolbarContent() => CreateToolbar(QuickSceneLayout.Horizontal);

        public OverlayToolbar CreateVerticalToolbarContent() => CreateToolbar(QuickSceneLayout.Vertical);

        private static OverlayToolbar CreateToolbar(QuickSceneLayout layout)
        {
            var toolbar = new OverlayToolbar();
            toolbar.style.flexDirection = layout == QuickSceneLayout.Horizontal
                ? FlexDirection.Row
                : FlexDirection.Column;

            toolbar.Add(new QuickSceneDropdown(layout));
            toolbar.Add(new QuickSceneFavoritesStrip(layout));
            return toolbar;
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

        public static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value.Substring(0, Math.Max(1, maxLength - 2)) + "..";

        public static void ShowUnsavedModeMenu()
        {
            var menu = new GenericMenu();
            foreach (UnsavedChangesMode mode in Enum.GetValues(typeof(UnsavedChangesMode)))
            {
                UnsavedChangesMode captured = mode;
                menu.AddItem(
                    new GUIContent(captured.Label()),
                    QuickSceneAccessState.UnsavedMode == captured,
                    () => UnsavedChangesModeUtil.Request(captured));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Gives a button in the panel layout a visible outline, rounded corners, margin and
        /// padding so neighboring buttons are easy to tell apart.
        /// </summary>
        public static void StylePanelButton(VisualElement button)
        {
            var outline = new Color(0.5f, 0.5f, 0.5f, 0.45f);

            button.style.height = 22;
            button.style.marginTop = 2;
            button.style.marginBottom = 2;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;

            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = outline;
            button.style.borderBottomColor = outline;
            button.style.borderLeftColor = outline;
            button.style.borderRightColor = outline;

            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
        }
    }

    /// <summary>Shows the active scene; click to pick another one.</summary>
    internal sealed class QuickSceneDropdown : EditorToolbarDropdown
    {
        private const int FlatListLimit = 12;

        private readonly QuickSceneLayout quickLayout;

        private int MaxLength => quickLayout == QuickSceneLayout.Vertical ? 9 : 24;

        public QuickSceneDropdown(QuickSceneLayout quickLayout)
        {
            this.quickLayout = quickLayout;

            icon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
            clicked += ShowMenu;

            if (quickLayout == QuickSceneLayout.Panel)
            {
                // Keep it compact instead of stretching across the whole panel.
                QuickSceneOverlayUtil.StylePanelButton(this);
                style.alignSelf = Align.FlexStart;
                style.marginBottom = 4;
            }

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
            string label = scene.isDirty ? sceneName + " *" : sceneName;

            text = QuickSceneOverlayUtil.Truncate(label, MaxLength);
            tooltip = $"{label}\nClick to switch scene";

            SceneColor color = string.IsNullOrEmpty(scene.path)
                ? SceneColor.None
                : QuickSceneAccessState.GetColor(AssetDatabase.AssetPathToGUID(scene.path));

            if (quickLayout == QuickSceneLayout.Panel && color == SceneColor.None)
                style.borderLeftWidth = 1; // keep the plain outline
            else
                SceneColorUtil.ApplyMarker(this, color);

            // The scene you are in is shown with its full color.
            if (color != SceneColor.None)
                SceneColorUtil.ApplyFill(this, color);
            else
                SceneColorUtil.ClearFill(this);
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

            // "Play from here" for the active scene.
            if (SceneCatalog.TryGetByGuid(AssetDatabase.AssetPathToGUID(activePath), out SceneEntry activeEntry))
            {
                bool isPlayFrom = PlayFromScene.IsPlayFrom(activeEntry.Guid);
                string playLabel = isPlayFrom
                    ? "Stop Playing From This Scene"
                    : $"Play From '{activeEntry.Name}'";

                if (canOpen)
                    menu.AddItem(new GUIContent(playLabel), isPlayFrom, () => PlayFromScene.Toggle(activeEntry));
                else
                    menu.AddDisabledItem(new GUIContent(playLabel), isPlayFrom);
            }

            // Scene sets
            foreach (QuickSceneAccessState.SceneSetData set in QuickSceneAccessState.Sets)
            {
                QuickSceneAccessState.SceneSetData captured = set;
                var setLabel = new GUIContent($"Scene Sets/{captured.name} ({captured.guids.Count})");

                if (canOpen)
                    menu.AddItem(setLabel, SceneSetActions.IsLoaded(captured), () => SceneSetActions.Load(captured));
                else
                    menu.AddDisabledItem(setLabel, SceneSetActions.IsLoaded(captured));
            }

            menu.AddItem(
                new GUIContent("Scene Sets/Save Loaded Scenes as New Set..."),
                false,
                SceneSetActions.SaveLoadedAsNew);

            menu.AddItem(new GUIContent("Search Scenes..."), false, QuickSceneSearchWindow.ShowPalette);
            menu.AddItem(
                new GUIContent("Open Quick Scene Access Window"),
                false,
                QuickSceneAccessWindow.ShowWindow);

            menu.ShowAsContext();
        }
    }

    /// <summary>
    /// A star for the active scene, the save-mode button and one button per favorite scene.
    /// Left-click opens, Shift+Click opens additively, right-click for more options.
    /// Adapts to the overlay layout: a row when docked top/bottom, a column otherwise.
    /// </summary>
    internal sealed class QuickSceneFavoritesStrip : VisualElement
    {
        private static readonly Color WarningColor = new Color(0.93f, 0.6f, 0.2f);

        private readonly QuickSceneLayout quickLayout;

        private int MaxButtons =>
            quickLayout == QuickSceneLayout.Horizontal ? 6 : quickLayout == QuickSceneLayout.Vertical ? 8 : 12;

        private int MaxLabelLength =>
            quickLayout == QuickSceneLayout.Horizontal ? 18 : quickLayout == QuickSceneLayout.Vertical ? 8 : 24;

        public QuickSceneFavoritesStrip(QuickSceneLayout quickLayout)
        {
            this.quickLayout = quickLayout;
            style.flexDirection = quickLayout == QuickSceneLayout.Horizontal
                ? FlexDirection.Row
                : FlexDirection.Column;

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

            // In the panel layout the star and the save-mode button sit side by side.
            VisualElement actions = this;
            if (quickLayout == QuickSceneLayout.Panel)
            {
                actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                Add(actions);
            }

            AddStar(actions, activePath);
            AddSaveModeButton(actions);
            AddPlayFromButton(canOpen);
            AddFavoriteButtons(canOpen, activePath);
            AddSetButtons(canOpen);
        }

        private void AddStar(VisualElement parent, string activePath)
        {
            if (string.IsNullOrEmpty(activePath))
                return;

            if (!SceneCatalog.TryGetByGuid(AssetDatabase.AssetPathToGUID(activePath), out SceneEntry current))
                return;

            bool isFavorite = QuickSceneAccessState.IsFavorite(current.Guid);
            var star = new EditorToolbarButton
            {
                text = isFavorite ? "★" : "☆",
                tooltip = (isFavorite
                              ? $"Remove '{current.Name}' from favorites"
                              : $"Add '{current.Name}' to favorites") +
                          "\nRight-click to set its color"
            };

            if (quickLayout == QuickSceneLayout.Panel)
                QuickSceneOverlayUtil.StylePanelButton(star);

            star.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button == 0)
                    QuickSceneAccessState.ToggleFavorite(current.Guid);
            });

            star.AddManipulator(new ContextualMenuManipulator(evt =>
                SceneColorUtil.AppendColorMenu(
                    evt.menu,
                    QuickSceneAccessState.GetColor(current.Guid),
                    color => QuickSceneAccessState.SetColor(current.Guid, color))));

            parent.Add(star);
        }

        private void AddSaveModeButton(VisualElement parent)
        {
            UnsavedChangesMode mode = QuickSceneAccessState.UnsavedMode;

            var button = new EditorToolbarButton
            {
                text = quickLayout == QuickSceneLayout.Horizontal
                    ? $"Save: {mode.ShortLabel()}"
                    : mode.ShortLabel(),
                tooltip = $"When switching scenes with unsaved changes: {mode.Label()}\nClick to change"
            };

            if (quickLayout == QuickSceneLayout.Panel)
            {
                QuickSceneOverlayUtil.StylePanelButton(button);
                button.style.flexGrow = 1;
            }

            if (mode != UnsavedChangesMode.Ask)
                button.style.color = new StyleColor(WarningColor);

            button.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button == 0)
                    QuickSceneOverlayUtil.ShowUnsavedModeMenu();
            });

            parent.Add(button);
        }

        // Only shown while "Play from here" is active, so it is hard to forget.
        private void AddPlayFromButton(bool canOpen)
        {
            string playFrom = PlayFromScene.CurrentName;
            if (playFrom == null)
                return;

            var button = new EditorToolbarButton
            {
                text = $"▶ {QuickSceneOverlayUtil.Truncate(playFrom, MaxLabelLength)}",
                tooltip = $"Play starts from '{playFrom}'.\nClick to stop playing from it."
            };

            if (quickLayout == QuickSceneLayout.Panel)
                QuickSceneOverlayUtil.StylePanelButton(button);

            button.style.color = new StyleColor(WarningColor);
            button.SetEnabled(canOpen);
            button.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button == 0)
                    PlayFromScene.Set(null);
            });

            Add(button);
        }

        private void AddFavoriteButtons(bool canOpen, string activePath)
        {
            int count = 0;
            foreach (string guid in QuickSceneAccessState.Favorites)
            {
                if (count >= MaxButtons)
                    break;

                if (!SceneCatalog.TryGetByGuid(guid, out SceneEntry entry))
                    continue;

                var button = new EditorToolbarButton
                {
                    text = QuickSceneOverlayUtil.Truncate(entry.Name, MaxLabelLength),
                    tooltip = $"{entry.AssetPath}\nClick: open  |  Shift+Click: open additively  |  Right-click: more"
                };

                if (quickLayout == QuickSceneLayout.Panel)
                    QuickSceneOverlayUtil.StylePanelButton(button);

                SceneColor color = QuickSceneAccessState.GetColor(entry.Guid);
                if (color != SceneColor.None)
                    SceneColorUtil.ApplyMarker(button, color);

                if (entry.AssetPath == activePath)
                {
                    // Current scene: bold text on the full color (a neutral fill if it has none).
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;
                    SceneColorUtil.ApplyFill(button, color);
                }

                button.SetEnabled(canOpen);
                button.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.button == 0)
                        QuickSceneOverlayUtil.Open(entry, evt.shiftKey);
                });

                button.AddManipulator(new ContextualMenuManipulator(evt => PopulateFavoriteMenu(evt, entry, canOpen)));

                Add(button);
                count++;
            }
        }

        // One "Set: name" button per saved set; click loads all of its scenes.
        private void AddSetButtons(bool canOpen)
        {
            int maxSets = quickLayout == QuickSceneLayout.Horizontal ? 3 : quickLayout == QuickSceneLayout.Vertical ? 4 : 8;
            int count = 0;

            foreach (QuickSceneAccessState.SceneSetData set in QuickSceneAccessState.Sets)
            {
                if (count >= maxSets)
                    break;

                QuickSceneAccessState.SceneSetData captured = set;
                var button = new EditorToolbarButton
                {
                    text = quickLayout == QuickSceneLayout.Vertical
                        ? QuickSceneOverlayUtil.Truncate(captured.name, MaxLabelLength)
                        : "Set: " + QuickSceneOverlayUtil.Truncate(captured.name, Mathf.Max(6, MaxLabelLength - 5)),
                    tooltip = $"Scene set '{captured.name}' ({captured.guids.Count} scenes)\n" +
                              "Click: load all its scenes  |  Right-click: more"
                };

                if (quickLayout == QuickSceneLayout.Panel)
                    QuickSceneOverlayUtil.StylePanelButton(button);

                if (SceneSetActions.IsLoaded(captured))
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                button.SetEnabled(canOpen);
                button.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.button == 0)
                        SceneSetActions.Load(captured);
                });

                button.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction(
                        "Load Set",
                        _ => SceneSetActions.Load(captured),
                        _ => canOpen ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Update From Loaded Scenes...", _ => SceneSetActions.UpdateFromLoaded(captured));
                    evt.menu.AppendAction("Rename...", _ => SceneSetActions.Rename(captured));
                    evt.menu.AppendAction("Delete...", _ => SceneSetActions.Delete(captured));
                }));

                Add(button);
                count++;
            }
        }

        private static void PopulateFavoriteMenu(ContextualMenuPopulateEvent evt, SceneEntry entry, bool canOpen)
        {
            evt.menu.AppendAction(
                "Open",
                _ => QuickSceneOverlayUtil.Open(entry, false),
                _ => canOpen ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                "Open Additively",
                _ => QuickSceneOverlayUtil.Open(entry, true),
                _ => canOpen ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendSeparator();

            SceneColorUtil.AppendColorMenu(
                evt.menu,
                QuickSceneAccessState.GetColor(entry.Guid),
                color => QuickSceneAccessState.SetColor(entry.Guid, color));

            evt.menu.AppendSeparator();

            evt.menu.AppendAction(
                PlayFromScene.IsPlayFrom(entry.Guid) ? "Stop Playing From This Scene" : "Play From This Scene",
                _ => PlayFromScene.Toggle(entry),
                _ => canOpen ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                "Remove from Favorites",
                _ => QuickSceneAccessState.ToggleFavorite(entry.Guid));
            evt.menu.AppendAction("Ping in Project", _ => SceneOpener.Ping(entry));
        }
    }
}