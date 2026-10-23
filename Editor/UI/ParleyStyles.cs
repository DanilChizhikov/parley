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
        private const string TextClass = "pl-text";
        private const long SkinPollMs = 1000;

        private const string MainSheet = "Parley/Parley";
        private const string DarkTokens = "Parley/ParleyTokensDark";
        private const string LightTokens = "Parley/ParleyTokensLight";

        public static bool IsDark => EditorGUIUtility.isProSkin;

        public static string CodeColor => IsDark ? "#E6C07B" : "#A0522D";

        public static string LinkColor => IsDark ? "#6CB6FF" : "#0969DA";

        public static string MarkColor => IsDark ? "#FFFFFF14" : "#0000000F";

        private static Font _monospace;

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

        public static void Apply(VisualElement root)
        {
            root.AddToClassList(Root);
            bool dark = IsDark;
            StyleSheet tokens = AddSheet(root, dark ? DarkTokens : LightTokens);
            AddSheet(root, MainSheet);
            root.schedule.Execute(() =>
            {
                if (dark == IsDark)
                {
                    return;
                }

                dark = IsDark;
                if (tokens != null)
                {
                    root.styleSheets.Remove(tokens);
                }

                tokens = Resources.Load<StyleSheet>(dark ? DarkTokens : LightTokens);
                if (tokens != null)
                {
                    root.styleSheets.Add(tokens);
                }
            }).Every(SkinPollMs);
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
            label.AddToClassList(TextClass);
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
            label.AddToClassList(TextClass);
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
            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("pl-spacer");
            return spacer;
        }

        public static Toggle AllToggle(int enabled, int total, Action<bool> onChange)
        {
            Toggle toggle = new Toggle { text = "All", value = total > 0 && enabled == total, showMixedValue = enabled > 0 && enabled < total };
            toggle.tooltip = "Turn every item in this section on or off for this chat";
            toggle.AddToClassList("pl-mcp__all");
            toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return toggle;
        }

        public static VisualElement RowActions(Action edit, Action delete, string noun)
        {
            VisualElement actions = new VisualElement();
            actions.AddToClassList("pl-mcp__actions");
            Button editButton = ParleyStyles.Button("Edit", edit, "pl-button--small");
            editButton.tooltip = "Edit this " + noun;
            actions.Add(editButton);
            Button deleteButton = ParleyStyles.Button("Delete", delete, "pl-button--small");
            deleteButton.AddToClassList("pl-button--danger");
            deleteButton.tooltip = "Delete this " + noun;
            actions.Add(deleteButton);
            return actions;
        }

        public static TextField SearchField(Action<string> onChange)
        {
            TextField field = new TextField { tooltip = "Filter by name or description" };
            field.textEdition.placeholder = "Search…";
            field.AddToClassList("pl-search");
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue ?? string.Empty));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape && field.value.Length > 0)
                {
                    field.value = string.Empty;
                    evt.StopPropagation();
                }
            });
            return field;
        }

        public static bool Matches(string query, string first, string second = null)
        {
            string trimmed = query?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return true;
            }

            return (first != null && first.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                || (second != null && second.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static void ScrollIntoView(VisualElement element)
        {
            element.schedule.Execute(() => element.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(element));
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static StyleSheet AddSheet(VisualElement root, string path)
        {
            StyleSheet sheet = Resources.Load<StyleSheet>(path);
            if (sheet == null)
            {
                Debug.LogWarning("[Parley] Style sheet not found: " + path);
                return null;
            }

            root.styleSheets.Add(sheet);
            return sheet;
        }
    }
}