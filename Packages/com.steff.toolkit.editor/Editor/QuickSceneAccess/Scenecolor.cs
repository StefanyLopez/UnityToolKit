using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteffsToolkit.Editor
{
    /// <summary>A color label you can give a scene to spot it faster.</summary>
    internal enum SceneColor
    {
        None = 0,
        Red,
        Orange,
        Yellow,
        Green,
        Teal,
        Blue,
        Purple,
        Pink,
        Gray
    }

    internal static class SceneColorUtil
    {
        public static readonly SceneColor[] All = (SceneColor[])Enum.GetValues(typeof(SceneColor));

        public static Color ToColor(this SceneColor color)
        {
            switch (color)
            {
                case SceneColor.Red: return new Color(0.91f, 0.30f, 0.28f);
                case SceneColor.Orange: return new Color(0.95f, 0.58f, 0.20f);
                case SceneColor.Yellow: return new Color(0.93f, 0.78f, 0.22f);
                case SceneColor.Green: return new Color(0.35f, 0.72f, 0.38f);
                case SceneColor.Teal: return new Color(0.22f, 0.70f, 0.70f);
                case SceneColor.Blue: return new Color(0.30f, 0.55f, 0.92f);
                case SceneColor.Purple: return new Color(0.62f, 0.42f, 0.86f);
                case SceneColor.Pink: return new Color(0.90f, 0.42f, 0.68f);
                case SceneColor.Gray: return new Color(0.60f, 0.62f, 0.66f);
                default: return Color.clear;
            }
        }

        public static string Label(this SceneColor color) =>
            color == SceneColor.None ? "None" : color.ToString();

        /// <summary>Adds a "Color" submenu with a check mark on the current color.</summary>
        public static void AppendColorMenu(DropdownMenu menu, SceneColor current, Action<SceneColor> onPick)
        {
            foreach (SceneColor color in All)
            {
                SceneColor captured = color;
                menu.AppendAction(
                    $"Color/{captured.Label()}",
                    _ => onPick(captured),
                    _ => captured == current
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        /// <summary>Draws a colored strip on the left edge of an element (no strip for None).</summary>
        public static void ApplyMarker(VisualElement element, SceneColor color)
        {
            if (color == SceneColor.None)
            {
                element.style.borderLeftWidth = 0;
                return;
            }

            element.style.borderLeftWidth = 3;
            element.style.borderLeftColor = color.ToColor();
        }
    }
}