using System;
using System.Collections.Generic;
using System.Linq;

namespace SteffsToolkit.Editor
{
    /// <summary>One line in the list: either a section header or a scene.</summary>
    internal sealed class ListRow
    {
        public SceneEntry Entry { get; private set; }
        public string HeaderText { get; private set; }
        public int HeaderCount { get; private set; }

        public bool IsHeader => Entry == null;

        public static ListRow Header(string text, int count) =>
            new ListRow { HeaderText = text, HeaderCount = count };

        public static ListRow Scene(SceneEntry entry) =>
            new ListRow { Entry = entry };
    }

    /// <summary>
    /// Pure data transformation: catalog + favorites/recents + filter -> flat list of rows.
    /// No UI and no AssetDatabase calls, so it is cheap to run on every keystroke.
    /// </summary>
    internal static class SceneListBuilder
    {
        private const int MaxRecentsShown = 5;

        public static List<ListRow> Build(string filter, bool groupByFolder, out int matchCount)
        {
            string[] tokens = Tokenize(filter);
            bool filtering = tokens.Length > 0;

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
            AddSection(rows, "Favorites", favorites);
            AddSection(rows, "Recent", recents);

            if (groupByFolder)
            {
                foreach (IGrouping<string, SceneEntry> group in matches.GroupBy(e => e.DisplayFolder))
                    AddSection(rows, group.Key, group.ToList());
            }
            else
            {
                bool hasPinnedSections = favorites.Count > 0 || recents.Count > 0;
                if (hasPinnedSections && matches.Count > 0)
                    rows.Add(ListRow.Header("All scenes", matches.Count));

                foreach (SceneEntry entry in matches)
                    rows.Add(ListRow.Scene(entry));
            }

            return rows;
        }

        private static void AddSection(List<ListRow> rows, string title, List<SceneEntry> entries)
        {
            if (entries.Count == 0)
                return;

            rows.Add(ListRow.Header(title, entries.Count));
            foreach (SceneEntry entry in entries)
                rows.Add(ListRow.Scene(entry));
        }

        private static string[] Tokenize(string filter) =>
            string.IsNullOrWhiteSpace(filter)
                ? Array.Empty<string>()
                : filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Every token must appear in the scene name or its folder.
        private static bool Matches(SceneEntry entry, string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (!Contains(entry.Name, token) && !Contains(entry.DisplayFolder, token))
                    return false;
            }

            return true;
        }

        // 0: name starts with the typed text, 1: name contains every token, 2: matched via folder.
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
