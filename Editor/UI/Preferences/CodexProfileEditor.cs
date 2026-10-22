using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Codex;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class CodexProfileEditor : ProfileEditor
    {
        private static readonly string[] Efforts = { "minimal", "low", "medium", "high", "xhigh" };

        private readonly VisualElement _methodFields = new ();

        public CodexProfileEditor(ParleyProfile profile) : base(profile, "Codex (CLI app-server)")
        {
            EnumField method = new EnumField("Sign-in method", profile.CodexAuthMethod);
            method.RegisterValueChangedCallback(MethodChangedHandler);
            Add(method);
            _methodFields.AddToClassList("pl-profile__method");
            Add(_methodFields);
            RebuildMethodFields();
            Add(Text(new TextFieldRequest("Model", profile.Model, value => Profile.Model = value, "Empty = Codex default")));
            Add(Choice("Effort", profile.Effort, Efforts, value => Profile.Effort = value, "Reasoning effort. Default uses the Codex setting."));
            TextField codexHome = Text(new TextFieldRequest("Config dir", profile.ConfigDir, value => Profile.ConfigDir = value, "Separate folder per account, e.g. ~/.codex-work",
                "CODEX_HOME. Use a separate folder to keep this profile's login apart from your terminal's."));
            TextField arguments = Text(new TextFieldRequest("Extra CLI arguments", profile.ExtraArguments, value => Profile.ExtraArguments = value, "Appended to codex app-server"));
            Add(Advanced(codexHome, arguments));
            AddFooter(null);
        }

        private void RebuildMethodFields()
        {
            _methodFields.Clear();
            switch (Profile.CodexAuthMethod)
            {
                case CodexAuthMethod.ApiKey:
                {
                    _methodFields.Add(ParleyStyles.Text("OpenAI API key", "pl-profile__method-title"));
                    _methodFields.Add(new SecretField("API key", Profile.Id, SecretFields.OpenAiApiKey, "sk-…"));

                    AddNote("Codex runs with its own home folder (Library/Parley/Codex/<profile id>) unless Config dir is set, so your terminal login stays untouched.");

                    _methodFields.Add(ButtonRow(CheckStatusButton()));
                } break;
                
                default:
                {
                    _methodFields.Add(ParleyStyles.Text("Codex default (whatever the CLI is signed in with)", "pl-profile__method-title"));
                    AddNote("Uses the ChatGPT sign-in or API key Codex already has on this machine.");
                    Button signOut = SmallButton("Sign out (Terminal)", SignOut, "Run codex logout in a terminal window");
                    signOut.AddToClassList("pl-button--danger");
                    _methodFields.Add(ButtonRow(
                        SmallButton("Sign in (Terminal)", () => RunInTerminal(CodexAuthCommands.LoginArguments(false)), "Run codex login in a terminal window", true),
                        SmallButton("Device code…", () => RunInTerminal(CodexAuthCommands.LoginArguments(true)),
                            "Sign in with a device code in a terminal window (for machines without a browser)"),
                        signOut,
                        CheckStatusButton()));
                } break;
            }

            _methodFields.Add(Status);
        }

        private Button CheckStatusButton()
        {
            return SmallButton("Check status", CheckStatus, "Ask Codex which account this profile uses");
        }

        private void SignOut()
        {
            if (!EditorUtility.DisplayDialog("Sign out", "Sign Codex out on this machine?", "Sign out", "Cancel"))
            {
                return;
            }

            RunInTerminal(new List<string> { "logout" });
        }

        private void AddNote(string text)
        {
            _methodFields.Add(ParleyStyles.Text(text, ParleyStyles.Muted));
        }

        private void RunInTerminal(List<string> arguments)
        {
            OpenTerminal(CodexAuthCommands.BuildTerminalCommandAsync(Profile, arguments));
        }

        private void CheckStatus()
        {
            RunBusy(async () =>
            {
                SetStatus("Checking…");
                CodexAuthStatus status = await CodexAuthCommands.GetStatusAsync(Profile);
                SetStatus(status.Summary, string.IsNullOrEmpty(status.Error) && status.LoggedIn);
            });
        }

        private void MethodChangedHandler(ChangeEvent<Enum> evt)
        {
            Profile.CodexAuthMethod = (CodexAuthMethod)evt.newValue;
            Persist();
            RebuildMethodFields();
        }
    }
}