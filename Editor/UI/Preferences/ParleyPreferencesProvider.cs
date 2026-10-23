using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Secrets;
using DTech.Parley.Editor.Settings;
using DTech.Parley.Editor.Tools;
using UnityEditor;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal static class ParleyPreferencesProvider
    {
        private const long TrustPollMs = 500;

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(ParleyWindow.PreferencesPath, SettingsScope.User)
            {
                label = "Parley",
                activateHandler = (_, root) => Build(root),
                keywords = new HashSet<string> { "parley", "ai", "claude", "codex", "chat", "llm", "lm studio", "ollama", "api key", "login" },
            };
        }

        private static void Build(VisualElement root)
        {
            ParleyStyles.Apply(root);
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pl-prefs");
            root.Add(scroll);
            scroll.Add(ParleyStyles.Text("Parley", "pl-prefs__title"));
            scroll.Add(BuildGeneral());
            scroll.Add(BuildProject());
            VisualElement profiles = Section("Profiles", "Secrets are stored in " + SecretStores.Default.Description + ", never in project files.");
            profiles.Add(new ProfileListSection());
            scroll.Add(profiles);
        }

        private static VisualElement BuildGeneral()
        {
            ParleyUserSettings settings = ParleyUserSettings.instance;
            VisualElement section = Section("General");
            section.Add(new CliPathField("Claude Code CLI path", settings.CliPathOverride, value => settings.CliPathOverride = value, DetectClaudeAsync));
            section.Add(new CliPathField("Codex CLI path", settings.CodexCliPathOverride, value => settings.CodexCliPathOverride = value, DetectCodexAsync));
            EnumField mode = new EnumField("Default permission mode", settings.DefaultMode)
            {
                tooltip = "Mode new chats start in. You can still switch it from the Parley toolbar.",
            };
            mode.RegisterValueChangedCallback(evt =>
            {
                PermissionMode selected = (PermissionMode)evt.newValue;
                if (selected == PermissionMode.BypassPermissions && !ParleyWindow.ConfirmBypass())
                {
                    mode.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                settings.DefaultMode = selected;
            });
            section.Add(mode);
            Toggle reload = BoolField("Defer script reload while the agent works", settings.LockReloadDuringTurn, value => settings.LockReloadDuringTurn = value);
            reload.tooltip = "Keeps Unity from reloading assemblies mid-turn, which would stop the agent. Compilation still runs; the reload happens when the turn ends.";
            section.Add(reload);
            Toggle thinking = BoolField("Show thinking", settings.ShowThinking, value => settings.ShowThinking = value);
            thinking.tooltip = "Show the model's reasoning blocks in the chat.";
            section.Add(thinking);
            Toggle links = BoolField("Open sign-in links automatically", settings.OpenLoginLinks, value => settings.OpenLoginLinks = value);
            links.tooltip = "Open the browser as soon as a sign-in command prints a link.";
            section.Add(links);
            return section;
        }

        private static VisualElement BuildProject()
        {
            ParleyProjectSettings project = ParleyProjectSettings.instance;
            VisualElement section = Section("This project", "Shared with your team through ProjectSettings/ParleySettings.asset.");
            section.Add(BuildTrustRow(project));
            TextField prompt = new TextField("Extra instructions")
            {
                value = project.ConfiguredAppendSystemPrompt ?? string.Empty,
                multiline = true,
                isDelayed = true,
                tooltip = "Appended to the agent's system prompt in every chat for this project.",
            };
            prompt.AddToClassList("pl-prefs__multiline");
            prompt.RegisterValueChangedCallback(evt => project.ConfiguredAppendSystemPrompt = evt.newValue);
            section.Add(prompt);
            TextField directories = ListField("Extra directories", project.ConfiguredAdditionalDirectories, project.SetAdditionalDirectories);
            directories.textEdition.placeholder = "Comma-separated folders the agent may access";
            section.Add(directories);
            Toggle instructions = BoolField("Project instruction files", project.IncludeProjectInstructions, value => project.IncludeProjectInstructions = value);
            instructions.tooltip = "Give local models the project's instruction files (CLAUDE.md, AGENTS.md, CODEX.md, …). Claude Code and Codex read them on their own.";
            section.Add(instructions);
            TextField instructionFiles = ListField("Extra instruction files", project.ConfiguredInstructionFiles, project.SetInstructionFiles);
            instructionFiles.textEdition.placeholder = "Comma-separated files added to the local agent's instructions";
            section.Add(instructionFiles);
            Toggle unityTools = BoolField("Unity editor tools", project.UnityToolsEnabled, value => project.UnityToolsEnabled = value);
            unityTools.tooltip = "Claude Code sees them as " + SdkMcpBridge.ToolPrefix + "*, Codex and local models as " + ToolCatalog.UnityLocalPrefix + "*.";
            section.Add(unityTools);
            VisualElement children = new VisualElement();
            children.AddToClassList("pl-prefs__children");
            foreach (IParleyTool tool in ToolCatalog.CreateUnityTools())
            {
                string name = tool.Name;
                Toggle toggle = BoolField(name, !Contains(project.DisabledUnityTools, name), value => project.SetUnityToolEnabled(name, value));
                toggle.tooltip = tool.Description;
                children.Add(toggle);
            }

            children.SetEnabled(project.UnityToolsEnabled);
            unityTools.RegisterValueChangedCallback(evt => children.SetEnabled(evt.newValue));
            section.Add(children);
            return section;
        }

        private static VisualElement BuildTrustRow(ParleyProjectSettings project)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("pl-banner");
            row.AddToClassList("pl-banner--warning");
            row.Add(ParleyStyles.Text("Not trusted: these values were changed outside Parley (for example by a pull) and are ignored until you review and trust them.",
                "pl-banner__text"));
            Button trust = ParleyStyles.Button("Trust", project.Trust, "pl-button--small");
            trust.tooltip = "Use the values below in this project's chats";
            row.Add(trust);
            ParleyStyles.SetVisible(row, !project.IsTrusted);
            row.schedule.Execute(() => ParleyStyles.SetVisible(row, !project.IsTrusted)).Every(TrustPollMs);
            return row;
        }

        private static async Task<string> DetectClaudeAsync()
        {
            string path = await ClaudeCliLocator.LocateAsync(ParleyUserSettings.instance.CliPathOverride);
            if (path == null)
            {
                return "Not found. Install Claude Code or set the path.";
            }

            string version = await ClaudeCliLocator.GetVersionAsync(path, await ShellEnvironment.GetLoginPathAsync());
            string description = DescribeExecutable(path, version);
            return ClaudeCliLocator.IsOutdated(CliVersions.ParseVersion(version))
                ? description + " · outdated (needs " + ClaudeCliLocator.MinimumVersion + "+), run 'claude update' or remove this install"
                : description;
        }

        private static async Task<string> DetectCodexAsync()
        {
            string path = await CodexCliLocator.LocateAsync(ParleyUserSettings.instance.CodexCliPathOverride);
            if (path == null)
            {
                return "Not found. Install Codex (npm install -g @openai/codex or brew install codex) or set the path.";
            }

            string version = await CodexCliLocator.GetVersionAsync(path, await ShellEnvironment.GetLoginPathAsync());
            return DescribeExecutable(path, version);
        }

        private static string DescribeExecutable(string path, string version)
        {
            return path + (string.IsNullOrEmpty(version) ? string.Empty : " · " + version);
        }

        private static VisualElement Section(string title, string subtitle = null)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("pl-prefs__section");
            section.Add(ParleyStyles.Text(title, "pl-prefs__section-title"));
            if (subtitle != null)
            {
                section.Add(ParleyStyles.Text(subtitle, ParleyStyles.Muted));
            }

            return section;
        }

        private static Toggle BoolField(string label, bool value, Action<bool> apply)
        {
            Toggle toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(evt => apply(evt.newValue));
            return toggle;
        }

        private static TextField ListField(string label, IReadOnlyList<string> values, Action<IEnumerable<string>> apply)
        {
            TextField field = new TextField(label) { value = string.Join(", ", values), isDelayed = true };
            field.RegisterValueChangedCallback(evt => apply(SplitList(evt.newValue)));
            return field;
        }

        private static List<string> SplitList(string text)
        {
            List<string> parts = new List<string>();
            foreach (string part in (text ?? string.Empty).Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    parts.Add(trimmed);
                }
            }

            return parts;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (string item in values)
            {
                if (item == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}