using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Sessions;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class AuthCard : VisualElement
    {
        private readonly Label _title;
        private readonly Label _message;
        private readonly Label _status;
        private readonly Button _signIn;
        private readonly Button _check;
        private readonly Button _preferences;

        private ChatSession _session;

        public AuthCard()
        {
            AddToClassList("pl-auth");
            _title = ParleyStyles.Text(string.Empty, "pl-auth__title");
            Add(_title);
            _message = ParleyStyles.Text(string.Empty, "pl-auth__text");
            _message.selection.isSelectable = true;
            Add(_message);
            _status = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
            Add(_status);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-auth__buttons");
            _signIn = ParleyStyles.Button("Sign in (Terminal)", SignInInTerminal, "pl-button--primary");
            _signIn.tooltip = "Open a terminal with the sign-in command for this profile";
            buttons.Add(_signIn);
            _check = ParleyStyles.Button("Check status", CheckStatus);
            _check.tooltip = "Check whether this profile is signed in now";
            buttons.Add(_check);
            _preferences = ParleyStyles.Button("Open preferences", ParleyWindow.OpenPreferences);
            _preferences.tooltip = "Edit this profile's sign-in settings";
            buttons.Add(_preferences);
            Button dismiss = ParleyStyles.Button("Dismiss", () => _session?.ClearAuthMessage(), "pl-button--ghost");
            dismiss.tooltip = "Hide this message";
            buttons.Add(dismiss);
            Add(buttons);
            ParleyStyles.SetVisible(this, false);
        }

        public void Bind(ChatSession session)
        {
            _session = session;
            _status.text = string.Empty;
            Refresh();
        }

        public void Refresh()
        {
            string message = _session?.AuthMessage;
            ParleyStyles.SetVisible(this, !string.IsNullOrEmpty(message));
            _message.text = message ?? string.Empty;
            if (_session == null)
            {
                return;
            }

            ParleyProfile profile = _session.Profile;
            bool terminal = UsesTerminalLogin(profile);
            _title.text = (terminal ? "Sign in to " : "Check the settings of ") + profile.Name;
            ParleyStyles.SetVisible(_signIn, terminal);
            ParleyStyles.SetVisible(_check, profile.Kind != ProfileKind.Local);
            _preferences.EnableInClassList("pl-button--primary", !terminal);
        }

        private static bool UsesTerminalLogin(ParleyProfile profile)
        {
            return profile.Kind == ProfileKind.ClaudeCode
                || (profile.Kind == ProfileKind.Codex && profile.CodexAuthMethod == CodexAuthMethod.CliDefault);
        }

        private static Task<string> BuildLoginCommandAsync(ParleyProfile profile)
        {
            if (profile.Kind == ProfileKind.Codex)
            {
                return CodexAuthCommands.BuildTerminalCommandAsync(profile, CodexAuthCommands.LoginArguments(false));
            }

            return ClaudeAuthCommands.BuildTerminalCommandAsync(profile, ClaudeAuthCommands.LoginArguments(profile));
        }

        private async void SignInInTerminal()
        {
            ChatSession session = _session;
            if (session == null)
            {
                return;
            }

            ParleyProfile profile = session.Profile;
            if (!UsesTerminalLogin(profile))
            {
                ParleyWindow.OpenPreferences();
                return;
            }

            string command = await BuildLoginCommandAsync(profile);
            if (session != _session)
            {
                return;
            }

            if (command == null)
            {
                _status.text = ProfileLabels.Describe(profile) + " CLI not found.";
                return;
            }

            if (!TerminalLauncher.Open(command, out string error))
            {
                _status.text = "Could not open a terminal: " + error + ". Run: " + command;
                return;
            }

            _status.text = "Finish signing in in the terminal, then press Check status.";
        }

        private async void CheckStatus()
        {
            ChatSession session = _session;
            if (session == null || session.Profile.Kind == ProfileKind.Local)
            {
                return;
            }

            _status.text = "Checking…";
            bool loggedIn;
            string summary;
            if (session.Profile.Kind == ProfileKind.Codex)
            {
                CodexAuthStatus status = await CodexAuthCommands.GetStatusAsync(session.Profile);
                loggedIn = status.LoggedIn;
                summary = status.Summary;
            }
            else
            {
                AuthStatus status = await ClaudeAuthCommands.GetStatusAsync(session.Profile);
                loggedIn = status.LoggedIn;
                summary = status.Summary;
            }

            if (session != _session)
            {
                return;
            }

            _status.text = summary;
            if (loggedIn)
            {
                session.ClearAuthMessage();
            }
        }
    }
}