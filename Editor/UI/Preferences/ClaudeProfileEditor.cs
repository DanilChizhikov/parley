using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Secrets;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ClaudeProfileEditor : ProfileEditor
    {
        private readonly VisualElement _methodFields = new ();
        private readonly CliCommandPanel _cli = new ();

        public ClaudeProfileEditor(ParleyProfile profile) : base(profile, "Claude Code (CLI)")
        {
            EnumField method = new EnumField("Sign-in method", profile.AuthMethod);
            method.RegisterValueChangedCallback(MethodChangedHandler);
            Add(method);
            _methodFields.AddToClassList("pl-profile__method");
            Add(_methodFields);
            RebuildMethodFields();
            TextField claudeHome = Text(new TextFieldRequest("Config dir (CLAUDE_CONFIG_DIR)", profile.ConfigDir, value => Profile.ConfigDir = value, "Separate folder per account, e.g. ~/.claude-work"));
            TextField model = Text(new TextFieldRequest("Model", profile.Model, value => Profile.Model = value, "opus, sonnet, fable or a full model id"));
            TextField effort = Text(new TextFieldRequest("Effort", profile.Effort, value => Profile.Effort = value, "low, medium, high, xhigh, max"));
            TextField arguments = Text(new TextFieldRequest("Extra CLI arguments", profile.ExtraArguments, value => Profile.ExtraArguments = value, "--fallback-model sonnet"));
            VisualElement foldout = Advanced(claudeHome, model, effort, arguments);

            Add(foldout);
            AddFooter(_cli);
        }

        private void RebuildMethodFields()
        {
            _methodFields.Clear();
            _methodFields.Add(ParleyStyles.Text(AuthEnvironmentBuilder.Describe(Profile.AuthMethod), "pl-profile__method-title"));
            string id = Profile.Id;
            switch (Profile.AuthMethod)
            {
                case ClaudeAuthMethod.CliDefault:
                {
                    AddNote("Uses whatever Claude Code is signed in with on this machine (your terminal login, environment variables, settings).");
                    AddLoginButtons(false);
                } break;
                
                case ClaudeAuthMethod.ClaudeAiLogin:
                case ClaudeAuthMethod.ConsoleLogin:
                case ClaudeAuthMethod.SsoLogin:
                {
                    AddField(new TextFieldRequest("Email (optional)", Profile.LoginEmail, value => Profile.LoginEmail = value));
                    AddNote("Signs in through the browser; Claude Code keeps the login in its own credential store. Credential environment variables are ignored for this profile.");
                    AddLoginButtons(true);
                } break;
                
                case ClaudeAuthMethod.OAuthToken:
                {
                    _methodFields.Add(new SecretField("OAuth token", id, SecretFields.OAuthToken, "sk-ant-oat01-…"));

                    AddNote("A one-year token for a Pro, Max, Team or Enterprise plan, passed as CLAUDE_CODE_OAUTH_TOKEN. " +
                        "Generating it in a terminal prints the token; paste it into the field above.");

                    _methodFields.Add(ButtonRow(
                        ("Generate token…", GenerateToken),
                        ("Generate in Terminal", () => RunInTerminal(new List<string>
                        {
                            "setup-token"
                        })),
                        ("Check status", CheckStatus)));
                } break;
                
                case ClaudeAuthMethod.ApiKey:
                {
                    _methodFields.Add(new SecretField("API key", id, SecretFields.ApiKey, "sk-ant-api03-…"));

                    AddNote("Claude Console API key, passed as ANTHROPIC_API_KEY (usage billing).");
                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
                
                case ClaudeAuthMethod.Gateway:
                {
                    AddField(new TextFieldRequest("Base URL", Profile.BaseUrl, value => Profile.BaseUrl = value,
                        "https://gateway.example.com"));

                    _methodFields.Add(new SecretField("Auth token", id, SecretFields.AuthToken,
                        "Bearer token (ANTHROPIC_AUTH_TOKEN)"));

                    AddField(new TextFieldRequest("Custom headers", Profile.CustomHeaders, value => Profile.CustomHeaders = value,
                        "Name: value (one per line)"));

                    AddNote("Also runs Claude Code against local servers that speak the Anthropic Messages API (Ollama 0.14+, LM Studio 0.4.1+).");
                    VisualElement buttonRow = ButtonRow(("Preset: Ollama", () => ApplyGatewayPreset("http://localhost:11434", "ollama")),
                        ("Preset: LM Studio", () => ApplyGatewayPreset("http://localhost:1234", "lmstudio")),
                        ("Check status", CheckStatus));

                    _methodFields.Add(buttonRow);
                } break;
                
                case ClaudeAuthMethod.ApiKeyHelper:
                {
                    AddField(new TextFieldRequest("Helper command", Profile.ApiKeyHelper, value => Profile.ApiKeyHelper = value, "~/bin/get-claude-key.sh"));

                    AddNote("A script that prints an API key; Claude Code reruns it when the key expires.");
                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
                
                case ClaudeAuthMethod.Bedrock:
                {
                    BuildBedrock();
                } break;
                
                case ClaudeAuthMethod.Vertex:
                {
                    AddField(new TextFieldRequest("Region (CLOUD_ML_REGION)", Profile.VertexRegion, value => Profile.VertexRegion = value,
                        "global or us-east5"));

                    AddField(new TextFieldRequest("Project id", Profile.VertexProjectId, value => Profile.VertexProjectId = value));
                    AddField(new TextFieldRequest("Credentials JSON (optional)", Profile.GoogleCredentialsPath,
                        value => Profile.GoogleCredentialsPath = value,
                        "Empty = gcloud application-default credentials"));

                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
                
                case ClaudeAuthMethod.Foundry:
                {
                    AddField(new TextFieldRequest("Resource name", Profile.FoundryResource, value => Profile.FoundryResource = value));
                    AddField(new TextFieldRequest("Base URL (instead of resource)", Profile.FoundryBaseUrl, value => Profile.FoundryBaseUrl = value));
                    _methodFields.Add(BoolField("Use Microsoft Entra ID", Profile.FoundryUseEntraId, EntraToggledHandler));
                    if (!Profile.FoundryUseEntraId)
                    {
                        _methodFields.Add(new SecretField("API key", id, SecretFields.FoundryApiKey));
                    }

                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
                
                case ClaudeAuthMethod.AnthropicProfile:
                {
                    AddField(new TextFieldRequest("Profile name (ANTHROPIC_PROFILE)", Profile.AnthropicProfile,
                        value => Profile.AnthropicProfile = value));

                    AddNote("A profile written by `ant auth login` or set up for Workload Identity Federation.");
                    _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
                } break;
            }
        }

        private void BuildBedrock()
        {
            AddField(new TextFieldRequest("AWS region", Profile.AwsRegion, value => Profile.AwsRegion = value, "us-east-1"));
            EnumField credentials = new EnumField("Credentials", Profile.BedrockCredentials);
            credentials.RegisterValueChangedCallback(BedrockCredentialsChangedHandler);
            _methodFields.Add(credentials);
            string id = Profile.Id;
            switch (Profile.BedrockCredentials)
            {
                case BedrockCredentials.Profile:
                    AddField(new TextFieldRequest("AWS profile", Profile.AwsProfile, value => Profile.AwsProfile = value));
                    break;
                case BedrockCredentials.AccessKeys:
                    _methodFields.Add(new SecretField("Access key id", id, SecretFields.AwsAccessKeyId));
                    _methodFields.Add(new SecretField("Secret access key", id, SecretFields.AwsSecretAccessKey));
                    _methodFields.Add(new SecretField("Session token (optional)", id, SecretFields.AwsSessionToken));
                    break;
                case BedrockCredentials.BearerToken:
                    _methodFields.Add(new SecretField("Bedrock API key", id, SecretFields.BedrockBearerToken));
                    break;
                default:
                    AddNote("Uses the default AWS credential chain (environment, ~/.aws, SSO).");
                    break;
            }

            AddField(new TextFieldRequest("Base URL (optional)", Profile.BedrockBaseUrl, value => Profile.BedrockBaseUrl = value, "ANTHROPIC_BEDROCK_BASE_URL"));
            _methodFields.Add(ButtonRow(("Check status", CheckStatus)));
        }

        private void AddField(TextFieldRequest request)
        {
            _methodFields.Add(Text(request));
        }

        private void AddNote(string text)
        {
            _methodFields.Add(ParleyStyles.Text(text, ParleyStyles.Muted));
        }

        private void AddLoginButtons(bool profileLogin)
        {
            List<(string label, Action action)> buttons = new List<(string label, Action action)>();
            if (profileLogin)
            {
                buttons.Add(("Sign in", SignIn));
                buttons.Add(("Sign in (Terminal)", () => RunInTerminal(ClaudeAuthCommands.LoginArguments(Profile))));
            }
            else
            {
                buttons.Add(("Sign in (Terminal)", () => RunInTerminal(new List<string> { "auth", "login" })));
            }

            buttons.Add(("Sign out", SignOut));
            buttons.Add(("Check status", CheckStatus));
            _methodFields.Add(ButtonRow(buttons.ToArray()));
        }

        private void RunInTerminal(List<string> arguments)
        {
            OpenTerminal(ClaudeAuthCommands.BuildTerminalCommandAsync(Profile, arguments));
        }

        private void SignIn()
        {
            _cli.Run(new CliCommandRequest(Profile, ClaudeAuthCommands.LoginArguments(Profile), null, CommandExitedHandler));
        }

        private void SignOut()
        {
            string scope = string.IsNullOrEmpty(Profile.ConfigDir) ? string.Empty : " for " + Profile.ConfigDir;
            if (!EditorUtility.DisplayDialog("Sign out", "Sign Claude Code out on this machine" + scope + "?", "Sign out", "Cancel"))
            {
                return;
            }

            _cli.Run(new CliCommandRequest(Profile, new List<string> { "auth", "logout" }, null, CommandExitedHandler));
        }

        private void GenerateToken()
        {
            Status.text = "Approve access in the browser; the token is saved to " + SecretStores.Default.Description + " automatically.";
            _cli.Run(new CliCommandRequest(Profile, new List<string> { "setup-token" }, TokenLineHandler));
        }

        private async void CheckStatus()
        {
            Status.text = "Checking…";
            AuthStatus status = await ClaudeAuthCommands.GetStatusAsync(Profile);
            Status.text = status.Summary;
        }

        private void ApplyGatewayPreset(string baseUrl, string token)
        {
            Profile.BaseUrl = baseUrl;
            Persist();
            if (!SecretStores.Default.Set(SecretStores.Key(Profile.Id, SecretFields.AuthToken), token, out string error))
            {
                Status.text = "Could not save the auth token: " + error;
                return;
            }

            RebuildMethodFields();
            Status.text = "Base URL set to " + baseUrl + ". Pick the model in Advanced (e.g. qwen3-coder).";
        }

        private void MethodChangedHandler(ChangeEvent<Enum> evt)
        {
            Profile.AuthMethod = (ClaudeAuthMethod)evt.newValue;
            Persist();
            RebuildMethodFields();
        }

        private void BedrockCredentialsChangedHandler(ChangeEvent<Enum> evt)
        {
            Profile.BedrockCredentials = (BedrockCredentials)evt.newValue;
            Persist();
            RebuildMethodFields();
        }

        private void EntraToggledHandler(bool value)
        {
            Profile.FoundryUseEntraId = value;
            RebuildMethodFields();
        }

        private void TokenLineHandler(string line)
        {
            string token = ClaudeAuthCommands.ExtractToken(line);
            if (token == null)
            {
                return;
            }

            Status.text = SecretStores.Default.Set(SecretStores.Key(Profile.Id, SecretFields.OAuthToken), token, out string error)
                ? "Token saved to " + SecretStores.Default.Description + "."
                : "Could not save the token: " + error;
            RebuildMethodFields();
        }

        private void CommandExitedHandler(int code)
        {
            CheckStatus();
        }
    }
}