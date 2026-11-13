using UnityEditor;

namespace SteffsToolkit.Editor
{
    /// <summary>What Quick Scene Access does with unsaved changes when switching scenes.</summary>
    internal enum UnsavedChangesMode
    {
        /// <summary>Show Unity's Save / Don't Save / Cancel prompt (default).</summary>
        Ask = 0,

        /// <summary>Save every modified scene automatically, then switch.</summary>
        AutoSave = 1,

        /// <summary>Switch without saving. Unsaved changes are lost.</summary>
        Discard = 2
    }

    internal static class UnsavedChangesModeUtil
    {
        public static string Label(this UnsavedChangesMode mode)
        {
            switch (mode)
            {
                case UnsavedChangesMode.AutoSave: return "Auto-save changes";
                case UnsavedChangesMode.Discard: return "Discard changes";
                default: return "Ask before switching";
            }
        }

        public static string ShortLabel(this UnsavedChangesMode mode)
        {
            switch (mode)
            {
                case UnsavedChangesMode.AutoSave: return "Auto-save";
                case UnsavedChangesMode.Discard: return "Discard";
                default: return "Ask";
            }
        }

        /// <summary>
        /// Changes the mode. The two non-default modes ask for confirmation first,
        /// because one overwrites scene files and the other throws changes away.
        /// </summary>
        public static void Request(UnsavedChangesMode mode)
        {
            if (mode == QuickSceneAccessState.UnsavedMode)
                return;

            if (mode == UnsavedChangesMode.AutoSave &&
                !EditorUtility.DisplayDialog(
                    "Enable auto-save?",
                    "When you switch scenes, Quick Scene Access will save your modified scenes " +
                    "automatically, overwriting their files without asking.",
                    "Enable auto-save",
                    "Cancel"))
                return;

            if (mode == UnsavedChangesMode.Discard &&
                !EditorUtility.DisplayDialog(
                    "Discard unsaved changes?",
                    "When you switch scenes, Quick Scene Access will NOT save your changes and " +
                    "will not ask. Anything you modified in the current scene is lost and cannot " +
                    "be recovered.\n\nUse this only for experiments you don't want to keep.",
                    "Discard changes",
                    "Cancel"))
                return;

            QuickSceneAccessState.UnsavedMode = mode;
        }
    }
}