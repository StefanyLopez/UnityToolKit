using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    /// <summary>A tiny modal window that asks for a name (Unity has no built-in text prompt).</summary>
    internal sealed class NamePromptWindow : EditorWindow
    {
        private string value = string.Empty;
        private Action<string> onConfirm;

        public static void Show(string windowTitle, string initialValue, Action<string> onConfirm)
        {
            var window = CreateInstance<NamePromptWindow>();
            window.titleContent = new GUIContent(windowTitle);
            window.value = initialValue ?? string.Empty;
            window.onConfirm = onConfirm;

            var size = new Vector2(320f, 86f);
            window.minSize = size;
            window.maxSize = size;

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.x + (main.width - size.x) * 0.5f,
                main.y + main.height * 0.3f,
                size.x,
                size.y);

            window.ShowModalUtility();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            var field = new TextField { value = value };
            field.RegisterValueChangedCallback(evt => value = evt.newValue);
            root.Add(field);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8;

            var cancel = new Button(Close) { text = "Cancel" };
            var ok = new Button(Confirm) { text = "OK" };
            ok.style.marginLeft = 6;
            buttons.Add(cancel);
            buttons.Add(ok);
            root.Add(buttons);

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Confirm();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        private void Confirm()
        {
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
                return;

            Action<string> callback = onConfirm;
            Close();
            callback?.Invoke(trimmed);
        }
    }
}