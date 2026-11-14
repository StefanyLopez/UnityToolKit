using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Steff.QuickSceneAccess.Editor
{
    /// <summary>
    /// Quick search palette: a small popup where you type part of a scene name, move with the
    /// arrow keys and press Enter to open it. Opened with a keyboard shortcut (rebindable in
    /// Edit > Shortcuts under "Steff's Toolkit"), from the Window menu, or from the overlay.
    /// It closes by itself when it loses focus.
    /// </summary>
    internal sealed class QuickSceneSearchWindow : EditorWindow
    {
        private const string StyleSheetFileName = "QuickSceneAccess.uss";
        private const float RowHeight = 36f;

        private static readonly Vector2 PopupSize = new Vector2(460f, 380f);

        private static QuickSceneSearchWindow instance;

        private ToolbarSearchField searchField;
        private ListView listView;
        private Label emptyLabel;

        private List<ListRow> rows = new List<ListRow>();
        private SceneStatus status;
        private string filter = string.Empty;

        // ---------------------------------------------------------------- entry points

        [Shortcut("Quick Scene Access/Quick Scene Search", KeyCode.O, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void ShortcutEntry() => ShowPalette();

        [MenuItem("Quick Scene Access/Quick Scene Search")]
        public static void ShowPalette()
        {
            // Pressing the shortcut again closes it.
            if (instance != null)
            {
                instance.Close();
                return;
            }

            var window = CreateInstance<QuickSceneSearchWindow>();
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            var anchor = new Rect(
                main.x + (main.width - PopupSize.x) * 0.5f,
                main.y + main.height * 0.18f,
                0f,
                0f);

            window.ShowAsDropDown(anchor, PopupSize);
            window.Focus();
        }

        // ---------------------------------------------------------------- lifecycle

        private void OnEnable() => instance = this;

        private void OnDisable()
        {
            if (instance == this)
                instance = null;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            root.AddToClassList("qsa");
            root.AddToClassList("qsa-palette");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "qsa--dark" : "qsa--light");

            StyleSheet sheet = LoadStyleSheet();
            if (sheet != null)
                root.styleSheets.Add(sheet);

            status = SceneStatus.Capture();

            searchField = new ToolbarSearchField();
            searchField.AddToClassList("qsa-palette__search");
            searchField.tooltip = "Type part of a scene name, folder or color";
            searchField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue ?? string.Empty;
                RefreshResults();
            });
            searchField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.Add(searchField);

            listView = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = MakeRow,
                bindItem = (element, index) =>
                    ((SceneRowView)element).Bind(rows[index], status, !EditorApplication.isPlayingOrWillChangePlaymode),
                itemsSource = rows
            };
            listView.AddToClassList("qsa-list");
            root.Add(listView);

            emptyLabel = new Label("No scenes match.");
            emptyLabel.AddToClassList("qsa-palette__empty");
            root.Add(emptyLabel);

            var hint = new Label("↑↓ navigate   Enter open   Shift+Enter open additively   Esc close");
            hint.AddToClassList("qsa-palette__hint");
            root.Add(hint);

            RefreshResults();

            // Focus the search field once the window is shown.
            root.schedule.Execute(() =>
            {
                TextField input = searchField.Q<TextField>();
                if (input != null)
                    input.Focus();
                else
                    searchField.Focus();
            });
        }

        private StyleSheet LoadStyleSheet()
        {
            MonoScript script = MonoScript.FromScriptableObject(this);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;
            string directory = string.IsNullOrEmpty(scriptPath)
                ? null
                : Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');

            return directory != null
                ? AssetDatabase.LoadAssetAtPath<StyleSheet>($"{directory}/{StyleSheetFileName}")
                : null;
        }

        // ---------------------------------------------------------------- rows

        private VisualElement MakeRow()
        {
            var row = new SceneRowView();
            row.OpenRequested += OpenEntry;
            row.FavoriteToggleRequested += entry =>
            {
                QuickSceneAccessState.ToggleFavorite(entry.Guid);
                RefreshResults(keepSelection: true);
            };
            row.ColorRequested += (entry, color) =>
            {
                QuickSceneAccessState.SetColor(entry.Guid, color);
                RefreshResults(keepSelection: true);
            };
            row.PlayFromRequested += entry =>
            {
                PlayFromScene.Toggle(entry);
                RefreshResults(keepSelection: true);
            };
            row.PingRequested += SceneOpener.Ping;
            row.RevealRequested += SceneOpener.Reveal;
            return row;
        }

        private void RefreshResults(bool keepSelection = false)
        {
            int previous = listView.selectedIndex;

            rows = SceneListBuilder.BuildFlat(filter).Select(ListRow.Scene).ToList();
            listView.itemsSource = rows;
            listView.Rebuild();

            bool any = rows.Count > 0;
            listView.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            emptyLabel.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;

            if (any)
            {
                int index = keepSelection ? Mathf.Clamp(previous, 0, rows.Count - 1) : 0;
                listView.SetSelectionWithoutNotify(new[] { index });
                listView.ScrollToItem(index);
            }
        }

        // ---------------------------------------------------------------- keyboard

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    MoveSelection(1);
                    evt.StopPropagation();
                    break;

                case KeyCode.UpArrow:
                    MoveSelection(-1);
                    evt.StopPropagation();
                    break;

                case KeyCode.PageDown:
                    MoveSelection(6);
                    evt.StopPropagation();
                    break;

                case KeyCode.PageUp:
                    MoveSelection(-6);
                    evt.StopPropagation();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (rows.Count > 0)
                    {
                        int index = Mathf.Clamp(listView.selectedIndex, 0, rows.Count - 1);
                        OpenEntry(rows[index].Entry, evt.shiftKey);
                    }
                    evt.StopPropagation();
                    break;

                case KeyCode.Escape:
                    Close();
                    evt.StopPropagation();
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            if (rows.Count == 0)
                return;

            int index = Mathf.Clamp(listView.selectedIndex + delta, 0, rows.Count - 1);
            listView.SetSelectionWithoutNotify(new[] { index });
            listView.ScrollToItem(index);
        }

        // ---------------------------------------------------------------- opening

        private void OpenEntry(SceneEntry entry, bool additive)
        {
            Close();

            // Open after the popup is gone, so any Save prompt appears on top of the editor.
            EditorApplication.delayCall += () => QuickSceneOverlayUtil.Open(entry, additive);
        }
    }
}