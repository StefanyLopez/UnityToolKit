using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    /// <summary>
    /// One reusable ListView row. It renders either a section header or a scene,
    /// depending on what is bound. Styling lives in QuickSceneAccess.uss.
    /// </summary>
    internal sealed class SceneRowView : VisualElement
    {
        private static Texture sceneIcon;

        // Header
        private readonly VisualElement headerContainer;
        private readonly Label headerArrow;
        private readonly VisualElement headerSwatch;
        private readonly Label headerLabel;
        private readonly Label headerCount;

        // Scene
        private readonly VisualElement sceneContainer;
        private readonly VisualElement colorTint;
        private readonly VisualElement colorDot;
        private readonly Label nameLabel;
        private readonly Label pathLabel;
        private readonly Label stateChip;
        private readonly Label buildChip;
        private readonly Label star;

        private SceneEntry entry;
        private string headerKey;
        private bool canOpen;
        private bool isActive;
        private bool isLoaded;
        private bool isFavorite;

        public event Action<SceneEntry, bool> OpenRequested;
        public event Action<SceneEntry> FavoriteToggleRequested;
        public event Action<SceneEntry, SceneColor> ColorRequested;
        public event Action<SceneEntry> PingRequested;
        public event Action<SceneEntry> RevealRequested;
        public event Action<string> HeaderToggleRequested;

        public SceneRowView()
        {
            AddToClassList("qsa-row");

            // Header layout
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("qsa-row__header");

            headerArrow = new Label(">");
            headerArrow.AddToClassList("qsa-row__arrow");
            headerSwatch = new VisualElement();
            headerSwatch.AddToClassList("qsa-row__swatch");
            headerLabel = new Label();
            headerLabel.AddToClassList("qsa-row__header-label");
            headerCount = new Label();
            headerCount.AddToClassList("qsa-row__header-count");

            headerContainer.Add(headerArrow);
            headerContainer.Add(headerSwatch);
            headerContainer.Add(headerLabel);
            headerContainer.Add(headerCount);
            Add(headerContainer);

            // Scene layout
            sceneContainer = new VisualElement();
            sceneContainer.AddToClassList("qsa-row__scene");

            // Added first so it is drawn behind the row content.
            colorTint = new VisualElement { pickingMode = PickingMode.Ignore };
            colorTint.AddToClassList("qsa-row__tint");
            sceneContainer.Add(colorTint);

            colorDot = new VisualElement();
            colorDot.AddToClassList("qsa-row__dot");
            sceneContainer.Add(colorDot);

            var icon = new Image { image = SceneIcon };
            icon.AddToClassList("qsa-row__icon");
            sceneContainer.Add(icon);

            var text = new VisualElement();
            text.AddToClassList("qsa-row__text");
            nameLabel = new Label();
            nameLabel.AddToClassList("qsa-row__name");
            pathLabel = new Label();
            pathLabel.AddToClassList("qsa-row__path");
            text.Add(nameLabel);
            text.Add(pathLabel);
            sceneContainer.Add(text);

            stateChip = new Label();
            stateChip.AddToClassList("qsa-chip");
            stateChip.AddToClassList("qsa-chip--accent");
            sceneContainer.Add(stateChip);

            buildChip = new Label();
            buildChip.AddToClassList("qsa-chip");
            sceneContainer.Add(buildChip);

            star = new Label();
            star.AddToClassList("qsa-row__star");
            star.RegisterCallback<ClickEvent>(OnStarClicked);
            sceneContainer.Add(star);

            Add(sceneContainer);

            RegisterCallback<ClickEvent>(OnRowClicked);
            this.AddManipulator(new ContextualMenuManipulator(PopulateMenu));
        }

        private static Texture SceneIcon =>
            sceneIcon != null ? sceneIcon : (sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image);

        public void Bind(ListRow row, SceneStatus status, bool canOpenScenes)
        {
            entry = row.Entry;
            headerKey = row.IsCollapsible ? row.Key : null;
            canOpen = canOpenScenes;

            bool isHeader = row.IsHeader;
            headerContainer.style.display = isHeader ? DisplayStyle.Flex : DisplayStyle.None;
            sceneContainer.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            tooltip = string.Empty;

            if (isHeader)
            {
                BindHeader(row);
                return;
            }

            string path = entry.AssetPath;
            isActive = status.IsActive(path);
            isLoaded = status.IsLoaded(path);
            isFavorite = QuickSceneAccessState.IsFavorite(entry.Guid);

            SceneColor color = QuickSceneAccessState.GetColor(entry.Guid);
            if (color == SceneColor.None)
            {
                colorDot.style.backgroundColor = Color.clear;
                colorTint.style.backgroundColor = Color.clear;
            }
            else
            {
                Color rgb = color.ToColor();
                colorDot.style.backgroundColor = rgb;
                colorTint.style.backgroundColor = new Color(rgb.r, rgb.g, rgb.b, 0.16f);
            }

            nameLabel.text = entry.Name;
            pathLabel.text = entry.DisplayFolder;
            tooltip = path + "\nClick to open  |  Shift+Click to open additively  |  Right-click for more";

            sceneContainer.EnableInClassList("qsa-row--active", isActive);
            sceneContainer.EnableInClassList("qsa-row--locked", !canOpen);

            if (isActive)
                SetChip(stateChip, "Active", "This is the active scene.");
            else if (isLoaded)
                SetChip(stateChip, "Loaded", "Loaded additively alongside the active scene.");
            else
                SetChip(stateChip, null, null);

            if (status.TryGetBuildSlot(path, out SceneStatus.BuildSlot slot))
            {
                SetChip(
                    buildChip,
                    slot.Enabled ? $"#{slot.Index}" : "off",
                    slot.Enabled
                        ? $"In the Build Settings scene list (build index {slot.Index})."
                        : "In the Build Settings scene list, but disabled.");
            }
            else
            {
                SetChip(buildChip, null, null);
            }

            star.text = isFavorite ? "★" : "☆";
            star.tooltip = isFavorite ? "Remove from favorites" : "Add to favorites";
            star.EnableInClassList("qsa-row__star--on", isFavorite);
        }

        private void BindHeader(ListRow row)
        {
            bool collapsible = row.IsCollapsible;

            headerLabel.text = row.HeaderText;
            headerCount.text = row.HeaderCount.ToString();

            headerArrow.style.display = collapsible ? DisplayStyle.Flex : DisplayStyle.None;
            headerArrow.EnableInClassList("qsa-row__arrow--open", !row.IsCollapsed);
            headerContainer.EnableInClassList("qsa-row__header--collapsible", collapsible);

            bool hasColor = row.HeaderColor != SceneColor.None;
            headerSwatch.style.display = hasColor ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasColor)
                headerSwatch.style.backgroundColor = row.HeaderColor.ToColor();

            tooltip = collapsible ? "Click to collapse or expand" : string.Empty;
        }

        private static void SetChip(Label chip, string text, string chipTooltip)
        {
            bool visible = !string.IsNullOrEmpty(text);
            chip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            chip.text = text ?? string.Empty;
            chip.tooltip = chipTooltip ?? string.Empty;
        }

        private void OnRowClicked(ClickEvent evt)
        {
            if (evt.button != 0)
                return;

            if (entry == null)
            {
                if (headerKey != null)
                    HeaderToggleRequested?.Invoke(headerKey);
                return;
            }

            OpenRequested?.Invoke(entry, evt.shiftKey);
        }

        private void OnStarClicked(ClickEvent evt)
        {
            evt.StopPropagation(); // don't also open the scene
            if (entry != null)
                FavoriteToggleRequested?.Invoke(entry);
        }

        private void PopulateMenu(ContextualMenuPopulateEvent evt)
        {
            if (entry == null)
                return;

            SceneEntry target = entry;
            bool canOpenSingle = canOpen && !isActive;
            bool canOpenAdditive = canOpen && !isLoaded;

            evt.menu.AppendAction(
                "Open",
                _ => OpenRequested?.Invoke(target, false),
                _ => canOpenSingle ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction(
                "Open Additively",
                _ => OpenRequested?.Invoke(target, true),
                _ => canOpenAdditive ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendSeparator();

            evt.menu.AppendAction(
                isFavorite ? "Remove from Favorites" : "Add to Favorites",
                _ => FavoriteToggleRequested?.Invoke(target));

            SceneColorUtil.AppendColorMenu(
                evt.menu,
                QuickSceneAccessState.GetColor(target.Guid),
                color => ColorRequested?.Invoke(target, color));

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Ping in Project", _ => PingRequested?.Invoke(target));
            evt.menu.AppendAction(
                Application.platform == RuntimePlatform.OSXEditor ? "Reveal in Finder" : "Show in Explorer",
                _ => RevealRequested?.Invoke(target));
        }
    }
}