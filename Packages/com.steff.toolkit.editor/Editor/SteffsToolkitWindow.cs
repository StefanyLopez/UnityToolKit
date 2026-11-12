using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    public class SteffsToolkitWindow : EditorWindow
    {
        private const string WindowTitle = "Steff's Toolkit";

        [MenuItem("Window/Steff's Toolkit/Overview")]
        public static void ShowWindow()
        {
            var window = GetWindow<SteffsToolkitWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(360, 260);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;

            var title = new Label("STEFF'S UNITY TOOLKIT");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            root.Add(title);

            var subtitle = new Label("Tools for a faster, cleaner Unity workflow.");
            subtitle.style.marginTop = 4;
            subtitle.style.marginBottom = 16;
            subtitle.style.opacity = 0.7f;
            root.Add(subtitle);

            var section = new Label("EDITOR TOOLS");
            section.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.style.marginBottom = 6;
            root.Add(section);

            var sceneButton = new Button(() =>
                Debug.Log("Quick Scene Access will be implemented in the next iteration."))
            {
                text = "Quick Scene Access — coming next"
            };
            sceneButton.SetEnabled(false);
            root.Add(sceneButton);

            var setupButton = new Button(() =>
                Debug.Log("Project Setup Wizard will be implemented in a future module."))
            {
                text = "Project Setup Wizard — planned"
            };
            setupButton.SetEnabled(false);
            root.Add(setupButton);

            var footer = new Label("Version 0.1.0 · Editor Foundation");
            footer.style.marginTop = 18;
            footer.style.opacity = 0.6f;
            footer.style.fontSize = 10;
            root.Add(footer);
        }
    }
}
