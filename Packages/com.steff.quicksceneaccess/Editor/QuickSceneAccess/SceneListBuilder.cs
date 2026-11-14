using System;
using System.Collections.Generic;
using System.Linq;

namespace Steff.QuickSceneAccess.Editor
{
    internal enum SceneGroupMode
    {
        None = 0,
        Folder = 1,
        Color = 2
    }

    /// <summary>One line in the list: either a section header or a scene.</summary>
    internal sealed class ListRow
    {
        public SceneEntry Entry { get; private set; }

        public string HeaderText { get; private set; }
        public int HeaderCount { get; private set; }

        /// <summary>Stable id used to remember whether the section is collapsed.</summary>
        public string Key { get; private set; }
        public bool IsCollapsed { get; private set; }
        public SceneColor HeaderColor { get; private set; }

        public bool IsHeader => Entry == null;
        public bool IsCollapsible => IsHeader && Key != null;

        public static ListRow Header(string key, string text, int count, bool collapsed, SceneColor color) =>
            new ListRow
            {
                Key = key,
                HeaderText = text,
                HeaderCount = count,
                IsCollapsed = collapsed,
                HeaderColor = color
            };

        public static ListRow Scene(SceneEntry entry) =>
            new ListRow { Entry = entry };
    }

    /// <summary>
    /// Pure data transformation: catalog + favorites/recents/colors + filter -> flat list of rows.
    /// No UI and no AssetDatabase calls, so it is cheap to run on every keystroke.
    /// </summary>
    internal static class SceneListBuilder
    {
        private const int MaxRecentsShown = 5;

        public static List<ListRow> Build(string filter, SceneGroupMode groupMode, out int matchCount)
        {
            string[] tokens = Tokenize(filter);
            bool filtering = tokens.Length > 0;

            // While searching, every section is shown expanded so matches are never hidden.
            bool honorCollapse = !filtering;

            IEnumerable<SceneEntry> query = SceneCatalog.Scenes.Where(e => Matches(e, tokens));
            if (filtering)
            {
                string phrase = filter.Trim();
                query = query.OrderBy(e => Score(e, phrase, tokens)); // stable sort
            }

            List<SceneEntry> matches = query.ToList();
            matchCount = matches.Count;

            var favorites = new List<SceneEntry>();
            foreach (string guid in QuickSceneAccessState.Favorites)
            {
                if (SceneCatalog.TryGetByGuid(guid, out SceneEntry entry) && Matches(entry, tokens))
                    favorites.Add(entry);
            }

            var recents = new List<SceneEntry>();
            if (!filtering)
            {
                foreach (string guid in QuickSceneAccessState.Recents)
                {
                    if (recents.Count >= MaxRecentsShown)
                        break;

                    if (!QuickSceneAccessState.IsFavorite(guid) &&
                        SceneCatalog.TryGetByGuid(guid, out SceneEntry entry))
                        recents.Add(entry);
                }
            }

            var rows = new List<ListRow>(matches.Count + 8);
            AddSection(rows, "favorites", "Favorites", favorites, honorCollapse, SceneColor.None);
            AddSection(rows, "recent", "Recent", recents, honorCollapse, SceneColor.None);

            switch (groupMode)
            {
                case SceneGroupMode.Folder:
                    foreach (IGrouping<string, SceneEntry> group in matches.GroupBy(e => e.DisplayFolder))
                        AddSection(rows, "folder:" + group.Key, group.Key, group.ToList(), honorCollapse, SceneColor.None);
                    break;

                case SceneGroupMode.Color:
                    AddColorSections(rows, matches, honorCollapse);
                    break;

                default:
                    bool hasPinnedSections = favorites.Count > 0 || recents.Count > 0;
                    if (hasPinnedSections)
                    {
                        AddSection(rows, "all", "All scenes", matches, honorCollapse, SceneColor.None);
                    }
                    else
                    {
                        foreach (SceneEntry entry in matches)
                            rows.Add(ListRow.Scene(entry));
                    }
                    break;
            }

            return rows;
        }

        /// <summary>
        /// Flat, de-duplicated list for the quick search palette: every scene appears once and
        /// collapsed sections are ignored. Without a filter the order is favorites, recents,
        /// then everything else; with a filter, best matches first (favorites win ties).
        /// </summary>
        public static List<SceneEntry> BuildFlat(string filter)
        {
            string[] tokens = Tokenize(filter);
            var result = new List<SceneEntry>();

            if (tokens.Length > 0)
            {
                string phrase = filter.Trim();
                result.AddRange(SceneCatalog.Scenes
                    .Where(e => Matches(e, tokens))
                    .OrderBy(e => Score(e, phrase, tokens))
                    .ThenBy(e => QuickSceneAccessState.IsFavorite(e.Guid) ? 0 : 1));
                return result;
            }

            var seen = new HashSet<string>();

            foreach (string guid in QuickSceneAccessState.Favorites)
            {
                if (SceneCatalog.TryGetByGuid(guid, out SceneEntry entry) && seen.Add(guid))
                    result.Add(entry);
            }

            foreach (string guid in QuickSceneAccessState.Recents)
            {
                if (SceneCatalog.TryGetByGuid(guid, out SceneEntry entry) && seen.Add(guid))
                    result.Add(entry);
            }

            foreach (SceneEntry entry in SceneCatalog.Scenes)
            {
                if (seen.Add(entry.Guid))
                    result.Add(entry);
            }

            return result;
        }

        private static void AddColorSections(List<ListRow> rows, List<SceneEntry> matches, bool honorCollapse)
        {
            Dictionary<SceneColor, List<SceneEntry>> byColor = matches
                .GroupBy(e => QuickSceneAccessState.GetColor(e.Guid))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (SceneColor color in SceneColorUtil.All)
            {
                if (color == SceneColor.None || !byColor.TryGetValue(color, out List<SceneEntry> list))
                    continue;

                AddSection(rows, "color:" + color, color.Label(), list, honorCollapse, color);
            }

            if (byColor.TryGetValue(SceneColor.None, out List<SceneEntry> uncolored))
                AddSection(rows, "color:None", "No color", uncolored, honorCollapse, SceneColor.None);
        }

        private static void AddSection(
            List<ListRow> rows,
            string key,
            string title,
            List<SceneEntry> entries,
            bool honorCollapse,
            SceneColor color)
        {
            if (entries.Count == 0)
                return;

            bool collapsed = honorCollapse && QuickSceneAccessState.IsCollapsed(key);
            rows.Add(ListRow.Header(key, title, entries.Count, collapsed, color));

            if (collapsed)
                return;

            foreach (SceneEntry entry in entries)
                rows.Add(ListRow.Scene(entry));
        }

        private static string[] Tokenize(string filter) =>
            string.IsNullOrWhiteSpace(filter)
                ? Array.Empty<string>()
                : filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Every token must appear in the scene name, its folder or its color name.
        private static bool Matches(SceneEntry entry, string[] tokens)
        {
            if (tokens.Length == 0)
                return true;

            SceneColor color = QuickSceneAccessState.GetColor(entry.Guid);
            foreach (string token in tokens)
            {
                bool found =
                    Contains(entry.Name, token) ||
                    Contains(entry.DisplayFolder, token) ||
                    (color != SceneColor.None && Contains(color.Label(), token));

                if (!found)
                    return false;
            }

            return true;
        }

        // 0: name starts with the typed text, 1: name contains every token, 2: matched via folder/color.
        private static int Score(SceneEntry entry, string phrase, string[] tokens)
        {
            if (entry.Name.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
                return 0;

            return tokens.All(t => Contains(entry.Name, t)) ? 1 : 2;
        }

        private static bool Contains(string text, string value) =>
            text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}