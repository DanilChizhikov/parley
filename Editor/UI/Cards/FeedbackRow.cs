using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class FeedbackRow : VisualElement
    {
        private readonly TextField _field;
        private readonly Action<string> _submit;
        private readonly bool _allowEmpty;

        public FeedbackRow(
            string placeholder,
            string buttonText,
            Action<string> submit,
            bool allowEmpty = false)
        {
            _submit = submit;
            _allowEmpty = allowEmpty;
            AddToClassList("pl-request__feedback");
            _field = new TextField { multiline = false };
            _field.textEdition.placeholder = placeholder;
            _field.AddToClassList("pl-request__feedback-field");
            _field.RegisterCallback<KeyDownEvent>(KeyDownHandler);
            Add(_field);
            Add(ParleyStyles.Button(buttonText, ButtonClickedHandler));
        }

        private void Submit(string text)
        {
            _field.value = string.Empty;
            _submit(text);
        }

        private void ButtonClickedHandler()
        {
            string text = _field.value?.Trim() ?? string.Empty;
            if (text.Length > 0 || _allowEmpty)
            {
                Submit(text);
            }
        }

        private void KeyDownHandler(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_field.value))
            {
                return;
            }

            Submit(_field.value.Trim());
            evt.StopPropagation();
        }
    }
}