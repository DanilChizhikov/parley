using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace DTech.Parley.Editor.UI
{
    internal static class ParleyStyles
    {
        public const string Muted = "pl-muted";
        
        private const string Root = "pl-root";
        private const string Mono = "pl-mono";

        private const string MainSheet = "Parley/Parley";
        private const string DarkTokens = "Parley/ParleyTokensDark";
        private const string LightTokens = "Parley/ParleyTokensLight";

        public static bool IsDark => EditorGUIUtility.isProSkin;

        public static string CodeColor => IsDark ? "#E6C07B" : "#A0522D";

        public static string LinkColor => IsDark ? "#6CB6FF" : "#0969DA";

        public static string MarkColor => IsDark ? "#FFFFFF14" : "#0000000F";
        
        private static Font Monospace
        {
            get
            {
                if (_monospace == null)
                {
                    _monospace = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
                    if (_monospace == null)
                    {
                        _monospace = Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Consolas", "Courier New" }, 12);
                    }
                }

                return _monospace;
            }
        }

        private static Font _monospace;

        public static void Apply(VisualElement root)
        {
            root.AddToClassList(Root);
            AddSheet(root, IsDark ? DarkTokens : LightTokens);
            AddSheet(root, MainSheet);
        }

        public static void UseMonospace(VisualElement element)
        {
            element.AddToClassList(Mono);
            element.style.unityFontDefinition = new StyleFontDefinition(Monospace);
        }

        public static Label RichLabel(string text, string className = null)
        {
            Label label = new Label(text) { enableRichText = true };
            label.selection.isSelectable = true;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (className != null)
            {
                label.AddToClassList(className);
            }

            label.RegisterCallback<PointerUpLinkTagEvent>(evt => LinkHandler.Open(evt.linkID));
            return label;
        }

        public static Label Text(string text, string className = null)
        {
            Label label = new Label(text) { enableRichText = false };
            label.style.whiteSpace = WhiteSpace.Normal;
            if (className != null)
            {
                label.AddToClassList(className);
            }

            return label;
        }

        public static Button Button(string text, Action onClick, string className = null)
        {
            Button button = new Button(onClick) { text = text };
            button.AddToClassList("pl-button");
            if (className != null)
            {
                button.AddToClassList(className);
            }

            return button;
        }

        public static VisualElement Spacer()
        {
            return new VisualElement { style = { flexGrow = 1.0f } };
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void AddSheet(VisualElement root, string path)
        {
            StyleSheet sheet = Resources.Load<StyleSheet>(path);
            if (sheet == null)
            {
                Debug.LogWarning("[Parley] Style sheet not found: " + path);
                return;
            }

            root.styleSheets.Add(sheet);
        }
    }
}