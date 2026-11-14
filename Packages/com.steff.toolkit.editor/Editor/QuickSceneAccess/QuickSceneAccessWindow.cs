using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    /// <summary>
    /// Quick Scene Access: search, pin, color and open project scenes.
    /// This class only wires UI to the services next to it:
    /// SceneCatalog (discovery), SceneListBuilder (filter/sections),
    /// SceneStatus (active/loaded/build), SceneOpener (safe opening),
    /// QuickSceneAccessState (favorites/recents/colors/settings).
    /// </summary>
    public class QuickSceneAccessWindow : EditorWindow
    {
        private const string StyleSheetFileName = "QuickSceneAccess.uss";
        private const float RowHeight = 36f;

        private ToolbarSearchField searchField;
        private ToolbarMenu groupMenu;
        private ToolbarMenu unsavedMenu;
        private ListView listView;
        private VisualElement emptyState;
        private Label emptyTitle;
        private Label emptyMessage;
        private HelpBox playModeBanner;
        private Label statusLabel;
        private Button playFromButton;

        private List<ListRow> rows = new List<ListRow>();
        private SceneStatus status = new SceneStatus();
        private string filter = string.Empty;
        private int matchCount;

        private bool rebuildQueued;
        private bool statusRefreshQueued;

        private static bool CanOpenNow => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Window/Steff's Toolkit/Quick Scene Access")]
        public static void ShowWindow()
        {
            GetWindow<QuickSceneAccessWindow>();
        }

        // ---------------------------------------------------------------- lifecycle

        private void OnEnable()
        {
            titleContent = new GUIContent(
                "Quick Scene Access",
                EditorGUIUtility.IconContent("SceneAsset Icon").image);
            minSize = new Vector2(330f, 260f);

            SceneCatalog.Changed += QueueRebuild;
            QuickSceneAccessState.Changed += QueueRebuild; // favorites/colors changed (e.g. from the overlay)
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorBuildSettings.sceneListChanged += QueueStatusRefresh;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            SceneCatalog.Changed -= QueueRebuild;
            QuickSceneAccessState.Changed -= QueueRebuild;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorBuildSettings.sceneListChanged -= QueueStatusRefresh;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            root.AddToClassList("qsa");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "qsa--dark" : "qsa--light");

            StyleSheet sheet = LoadStyleSheet();
            if (sheet != null)
                root.styleSheets.Add(sheet);

            root.Add(BuildToolbar());

            playModeBanner = new HelpBox("Scene switching is disabled in Play Mode.", HelpBoxMessageType.Info);
            playModeBanner.AddToClassList("qsa-banner");
            root.Add(playModeBanner);

            root.Add(BuildBody());
            root.Add(BuildStatusBar());

            UpdatePlayModeState();
            RebuildList();
        }

        // ---------------------------------------------------------------- UI construction

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("qsa-toolbar");

            searchField = new ToolbarSearchField();
            searchField.AddToClassList("qsa-search");
            searchField.tooltip = "Filter by scene name, folder or color. Enter opens the first result.";
            searchField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue ?? string.Empty;
                RebuildList();
                if (rows.Count > 0)
                    listView.ScrollToItem(0);
            });
            searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);
            toolbar.Add(searchField);

            groupMenu = new ToolbarMenu();
            AddGroupAction("No Grouping", SceneGroupMode.None);
            AddGroupAction("By Folder", SceneGroupMode.Folder);
            AddGroupAction("By Color", SceneGroupMode.Color);
            groupMenu.menu.AppendSeparator();
            groupMenu.menu.AppendAction("Collapse All Groups", _ => SetAllGroupsCollapsed(true));
            groupMenu.menu.AppendAction("Expand All Groups", _ => SetAllGroupsCollapsed(false));
            toolbar.Add(groupMenu);
            UpdateGroupMenu();

            var setsButton = new ToolbarButton
            {
                text = "Sets",
                tooltip = "Load, save and manage groups of scenes"
            };
            setsButton.clicked += () => SceneSetMenu.Build().DropDown(setsButton.worldBound);
            toolbar.Add(setsButton);

            unsavedMenu = new ToolbarMenu
            {
                tooltip = "What to do with unsaved changes when you switch scenes"
            };
            foreach (UnsavedChangesMode mode in System.Enum.GetValues(typeof(UnsavedChangesMode)))
            {
                UnsavedChangesMode captured = mode;
                unsavedMenu.menu.AppendAction(
                    captured.Label(),
                    _ =>
                    {
                        UnsavedChangesModeUtil.Request(captured);
                        UpdateUnsavedMenu();
                    },
                    _ => QuickSceneAccessState.UnsavedMode == captured
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
            toolbar.Add(unsavedMenu);
            UpdateUnsavedMenu();

            var refreshButton = new ToolbarButton(SceneCatalog.Invalidate)
            {
                tooltip = "Rescan the project for scenes"
            };
            Texture refreshIcon = EditorGUIUtility.IconContent("Refresh").image;
            if (refreshIcon != null)
                refreshButton.Add(new Image { image = refreshIcon });
            else
                refreshButton.text = "Refresh";
            toolbar.Add(refreshButton);

            return toolbar;
        }

        private void AddGroupAction(string label, SceneGroupMode mode)
        {
            groupMenu.menu.AppendAction(
                label,
                _ => QuickSceneAccessState.GroupMode = mode,
                _ => QuickSceneAccessState.GroupMode == mode
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }

        private VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("qsa-body");

            listView = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = rows
            };
            listView.AddToClassList("qsa-list");
            body.Add(listView);

            emptyState = new VisualElement();
            emptyState.AddToClassList("qsa-empty");
            emptyTitle = new Label();
            emptyTitle.AddToClassList("qsa-empty__title");
            emptyMessage = new Label();
            emptyMessage.AddToClassList("qsa-empty__message");
            emptyState.Add(emptyTitle);
            emptyState.Add(emptyMessage);
            body.Add(emptyState);

            return body;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("qsa-status");

            statusLabel = new Label();
            statusLabel.AddToClassList("qsa-status__label");
            bar.Add(statusLabel);

            playFromButton = new Button(() => PlayFromScene.Set(null));
            playFromButton.AddToClassList("qsa-status__button");
            playFromButton.AddToClassList("qsa-status__play");
            bar.Add(playFromButton);

            var buildButton = new Button(OpenBuildProfiles)
            {
                text = "Build Profiles",
                tooltip = "Open Build Profiles to manage which scenes are included in a build"
            };
            buildButton.AddToClassList("qsa-status__button");
            bar.Add(buildButton);

            return bar;
        }

        private StyleSheet LoadStyleSheet()
        {
            // Resolve next to this script so the package keeps working if it is moved.
            MonoScript script = MonoScript.FromScriptableObject(this);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;
            string directory = string.IsNullOrEmpty(scriptPath)
                ? null
                : Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');

            var sheet = directory != null
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>($"{directory}/{StyleSheetFileName}")
                : null;

            if (sheet == null)
                Debug.LogWarning($"[Quick Scene Access] Could not find {StyleSheetFileName} next to the window script; the UI will be unstyled.");

            return sheet;
        }

        // ---------------------------------------------------------------- list rows

        private VisualElement MakeRow()
        {
            var row = new SceneRowView();
            row.OpenRequested += OpenEntry;
            row.FavoriteToggleRequested += ToggleFavorite;
            row.ColorRequested += SetColor;
            row.PlayFromRequested += PlayFromScene.Toggle;
            row.HeaderToggleRequested += QuickSceneAccessState.ToggleCollapsed;
            row.PingRequested += SceneOpener.Ping;
            row.RevealRequested += SceneOpener.Reveal;
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            ((SceneRowView)element).Bind(rows[index], status, CanOpenNow);
        }

        // ---------------------------------------------------------------- refresh

        private void RebuildList()
        {
            if (listView == null)
                return;

            status = SceneStatus.Capture();
            rows = SceneListBuilder.Build(filter, QuickSceneAccessState.GroupMode, out matchCount);

            listView.itemsSource = rows;
            listView.Rebuild();

            UpdateEmptyState();
            UpdateStatusBar();
            UpdateGroupMenu();
            UpdateUnsavedMenu();
        }

        private void RefreshStatus()
        {
            if (listView == null)
                return;

            status = SceneStatus.Capture();
            listView.RefreshItems();
            UpdateStatusBar();
        }

        // Editor events often arrive in bursts (opening a scene fires several), and some
        // arrive in the middle of a click handler. Defer and coalesce.
        private void QueueRebuild()
        {
            if (rebuildQueued)
                return;

            rebuildQueued = true;
            EditorApplication.delayCall += () =>
            {
                rebuildQueued = false;
                if (this != null)
                    RebuildList();
            };
        }

        private void QueueStatusRefresh()
        {
            if (statusRefreshQueued)
                return;

            statusRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                statusRefreshQueued = false;
                if (this != null)
                    RefreshStatus();
            };
        }

        private void UpdateEmptyState()
        {
            bool hasRows = rows.Count > 0;
            listView.style.display = hasRows ? DisplayStyle.Flex : DisplayStyle.None;
            emptyState.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;

            if (hasRows)
                return;

            if (SceneCatalog.Scenes.Count == 0)
            {
                emptyTitle.text = "No scenes in this project";
                emptyMessage.text = "Create one with File > New Scene and save it under Assets.";
            }
            else
            {
                emptyTitle.text = $"No scenes match '{filter.Trim()}'";
                emptyMessage.text = "Try part of the scene name, its folder or its color.";
            }
        }

        private void UpdateStatusBar()
        {
            int total = SceneCatalog.Scenes.Count;
            string text;

            if (total == 0)
                text = "No scenes";
            else if (matchCount == total)
                text = total == 1 ? "1 scene" : $"{total} scenes";
            else
                text = $"{matchCount} of {total} scenes";

            if (status != null && status.EnabledBuildCount > 0)
                text += $"  ·  {status.EnabledBuildCount} in build";

            statusLabel.text = text;

            string playFrom = PlayFromScene.CurrentName;
            playFromButton.style.display = playFrom == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (playFrom != null)
            {
                playFromButton.text = $"▶ {playFrom}  ✕";
                playFromButton.tooltip = $"Play starts from '{playFrom}'. Click to stop playing from it.";
            }
        }

        private void UpdatePlayModeState()
        {
            if (playModeBanner == null || listView == null)
                return;

            playModeBanner.style.display = CanOpenNow ? DisplayStyle.None : DisplayStyle.Flex;
            listView.RefreshItems();
        }

        private void UpdateGroupMenu()
        {
            if (groupMenu == null)
                return;

            SceneGroupMode mode = QuickSceneAccessState.GroupMode;
            groupMenu.text = mode == SceneGroupMode.Folder ? "Folder"
                : mode == SceneGroupMode.Color ? "Color"
                : "Group";
            groupMenu.tooltip = "Group scenes (current: " +
                (mode == SceneGroupMode.None ? "no grouping" : mode.ToString().ToLowerInvariant()) + ")";
        }

        private void UpdateUnsavedMenu()
        {
            if (unsavedMenu == null)
                return;

            UnsavedChangesMode mode = QuickSceneAccessState.UnsavedMode;
            unsavedMenu.text = $"Save: {mode.ShortLabel()}";

            // Highlight non-default modes so they are not forgotten.
            unsavedMenu.style.color = mode == UnsavedChangesMode.Ask
                ? new StyleColor(StyleKeyword.Null)
                : new StyleColor(new Color(0.93f, 0.6f, 0.2f));
        }

        // ---------------------------------------------------------------- actions

        private void OpenEntry(SceneEntry entry, bool additive)
        {
            switch (SceneOpener.TryOpen(entry, additive))
            {
                case OpenResult.Opened:
                    QueueRebuild(); // recents changed
                    break;
                case OpenResult.BlockedByPlayMode:
                    ShowNotification(new GUIContent("Exit Play Mode to switch scenes."));
                    break;
                case OpenResult.Missing:
                    ShowNotification(new GUIContent("That scene no longer exists."));
                    break;
                case OpenResult.AlreadyLoaded:
                    ShowNotification(new GUIContent("That scene is already loaded."));
                    break;
                case OpenResult.Failed:
                    ShowNotification(new GUIContent("Could not open the scene. See the Console."));
                    break;
                // Cancelled / AlreadyActive: nothing to report.
            }
        }

        private void ToggleFavorite(SceneEntry entry)
        {
            QuickSceneAccessState.ToggleFavorite(entry.Guid);
            QueueRebuild();
        }

        private void SetColor(SceneEntry entry, SceneColor color)
        {
            QuickSceneAccessState.SetColor(entry.Guid, color);
            QueueRebuild();
        }

        private void SetAllGroupsCollapsed(bool collapsed)
        {
            List<string> keys = rows.Where(r => r.IsCollapsible).Select(r => r.Key).ToList();
            QuickSceneAccessState.SetAllCollapsed(keys, collapsed);
        }

        private static void OpenBuildProfiles()
        {
            // Unity 6: File > Build Profiles. Falls back to the older entry point.
            if (!EditorApplication.ExecuteMenuItem("File/Build Profiles"))
                BuildPlayerWindow.ShowBuildPlayerWindow();
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ListRow first = rows.FirstOrDefault(r => !r.IsHeader);
                if (first != null)
                {
                    OpenEntry(first.Entry, evt.shiftKey);
                    evt.StopPropagation();
                }
            }
            else if (evt.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(filter))
            {
                searchField.value = string.Empty;
                evt.StopPropagation();
            }
        }

        // ---------------------------------------------------------------- editor callbacks

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => QueueStatusRefresh();
        private void OnSceneClosed(Scene scene) => QueueStatusRefresh();
        private void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode) => QueueStatusRefresh();
        private void OnActiveSceneChanged(Scene previous, Scene current) => QueueStatusRefresh();
        private void OnPlayModeStateChanged(PlayModeStateChange change) => UpdatePlayModeState();
    }
}