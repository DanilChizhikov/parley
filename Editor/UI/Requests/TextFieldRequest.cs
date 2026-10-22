using System;

namespace DTech.Parley.Editor.UI
{
    internal readonly struct TextFieldRequest
    {
        public string Label { get; }
        public string Value { get; }
        public Action<string> Apply { get; }
        public string Placeholder { get; }
        public string Tooltip { get; }
        public bool Multiline { get; }

        public TextFieldRequest(
            string label,
            string value,
            Action<string> apply,
            string placeholder = null,
            string tooltip = null,
            bool multiline = false)
        {
            Label = label;
            Value = value;
            Apply = apply;
            Placeholder = placeholder;
            Tooltip = tooltip;
            Multiline = multiline;
        }
    }
}
