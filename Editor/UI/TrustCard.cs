using System.Collections.Generic;
using System.Text;
using DTech.Parley.Editor.Settings;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class TrustCard : VisualElement
    {
        private const int MaxPromptPreviewChars = 300;

        private readonly Label _message;

        private string _dismissedFingerprint;
        private string _renderedFingerprint;

        public TrustCard()
        {
            AddToClassList("pl-auth");
            _message = ParleyStyles.Text(string.Empty, "pl-auth__text");
            _message.selection.isSelectable = true;
            Add(_message);
            Add(ParleyStyles.Text("Trusting applies to chats started after this point (press New).", ParleyStyles.Muted));
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-request__buttons");
            buttons.Add(ParleyStyles.Button("Trust", TrustClickedHandler, "pl-button--primary"));
            buttons.Add(ParleyStyles.Button("Preferences", ParleyWindow.OpenPreferences));
            buttons.Add(ParleyStyles.Button("Ignore", IgnoreClickedHandler, "pl-button--ghost"));
            Add(buttons);
            ParleyStyles.SetVisible(this, false);
        }

        public void Refresh()
        {
            ParleyProjectSettings settings = ParleyProjectSettings.instance;
            bool visible = !settings.IsTrusted && settings.Fingerprint != _dismissedFingerprint;
            ParleyStyles.SetVisible(this, visible);
            if (visible && settings.Fingerprint != _renderedFingerprint)
            {
                _renderedFingerprint = settings.Fingerprint;
                _message.text = Describe(settings);
            }
        }

        private static string Describe(ParleyProjectSettings settings)
        {
            StringBuilder builder = new StringBuilder("ProjectSettings/ParleySettings.asset changed. Parley ignores these values until you trust them:");
            AppendList(builder, "Extra directories the agent may access", settings.ConfiguredAdditionalDirectories);
            AppendList(builder, "Extra instruction files", settings.ConfiguredInstructionFiles);
            if (!string.IsNullOrWhiteSpace(settings.ConfiguredAppendSystemPrompt))
            {
                builder.Append("\n• Extra instructions: ").Append(ToolPresenter.Truncate(settings.ConfiguredAppendSystemPrompt.Trim(), MaxPromptPreviewChars));
            }

            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, string title, IReadOnlyList<string> items)
        {
            if (items.Count > 0)
            {
                builder.Append("\n• ").Append(title).Append(": ").Append(string.Join(", ", items));
            }
        }

        private void TrustClickedHandler()
        {
            ParleyProjectSettings.instance.Trust();
            Refresh();
        }

        private void IgnoreClickedHandler()
        {
            _dismissedFingerprint = ParleyProjectSettings.instance.Fingerprint;
            Refresh();
        }
    }
}
