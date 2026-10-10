using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Codex;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class CodexProfileEditor : ProfileEditor
    {
        private readonly VisualElement _methodFields = new ();

        public CodexProfileEditor(ParleyProfile profile) : base(profile, "Codex (CLI app-server)")
        {
            EnumField method = new EnumField("Sign-in method", profile.CodexAuthMethod);
            method.RegisterValueChangedCallback(MethodChangedHandler);
            Add(method);
            _methodFields.AddToClassList("pl-profile__method");
            Add(_methodFields);
            RebuildMethodFields();
            TextField codexHome = Text(new TextFieldRequest("Config dir (CODEX_HOME)", profile.ConfigDir, value => Profile.ConfigDir = value, "Separate folder per account, e.g. ~/.codex-work"));
            TextField model = Text(new TextFieldRequest("Model", profile.Model, value => Profile.Model = value, "Empty = Codex default"));
            TextField effort = Text(new TextFieldRequest("Effort", profile.Effort, value => Profile.Effort = value, "minimal, low, medium, high, xhigh"));
            TextField arguments = Text(new TextFieldRequest("Extra CLI arguments", profile.ExtraArguments, value => Profile.ExtraArguments = value, "Appended to codex app-server"));
            VisualElement foldout = Advanced(codexHome, model, effort, arguments);

            Add(foldout);
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

                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
                
                default:
                {
                    _methodFields.Add(ParleyStyles.Text("Codex default (whatever the CLI is signed in with)", "pl-profile__method-title"));
                    AddNote("Uses the ChatGPT sign-in or API key Codex already has on this machine.");
                    _methodFields.Add(ButtonRow(
                        ("Sign in (Terminal)", () => RunInTerminal(CodexAuthCommands.LoginArguments(false))),
                        ("Sign in with device code (Terminal)", () => RunInTerminal(CodexAuthCommands.LoginArguments(true))),
                        ("Sign out (Terminal)", () => RunInTerminal(new List<string>
                        {
                            "logout"
                        })),
                        ("Check status", CheckStatus)));
                } break;
            }
        }

        private void AddNote(string text)
        {
            _methodFields.Add(ParleyStyles.Text(text, ParleyStyles.Muted));
        }

        private void RunInTerminal(List<string> arguments)
        {
            OpenTerminal(CodexAuthCommands.BuildTerminalCommandAsync(Profile, arguments));
        }

        private async void CheckStatus()
        {
            Status.text = "Checking…";
            CodexAuthStatus status = await CodexAuthCommands.GetStatusAsync(Profile);
            Status.text = status.Summary;
        }

        private void MethodChangedHandler(ChangeEvent<Enum> evt)
        {
            Profile.CodexAuthMethod = (CodexAuthMethod)evt.newValue;
            Persist();
            RebuildMethodFields();
        }
    }
}