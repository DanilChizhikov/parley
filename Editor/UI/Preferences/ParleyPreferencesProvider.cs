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
            VisualElement profiles = Section("Profiles");
            profiles.Add(new ProfileListSection());
            scroll.Add(profiles);
        }

        private static VisualElement BuildGeneral()
        {
            ParleyUserSettings settings = ParleyUserSettings.instance;
            VisualElement section = Section("General");
            section.Add(new CliPathField("Claude Code CLI path", settings.CliPathOverride, value => settings.CliPathOverride = value, DetectClaudeAsync));
            section.Add(new CliPathField("Codex CLI path", settings.CodexCliPathOverride, value => settings.CodexCliPathOverride = value, DetectCodexAsync));
            EnumField mode = new EnumField("Default permission mode", settings.DefaultMode);
            mode.RegisterValueChangedCallback(evt => settings.DefaultMode = (PermissionMode)evt.newValue);
            section.Add(mode);
            Toggle reload = BoolField("Defer script reload while the agent works", settings.LockReloadDuringTurn, value => settings.LockReloadDuringTurn = value);
            reload.tooltip = "Keeps Unity from reloading assemblies mid-turn, which would stop the agent. Compilation still runs; the reload happens when the turn ends.";
            section.Add(reload);
            section.Add(BoolField("Show thinking", settings.ShowThinking, value => settings.ShowThinking = value));
            section.Add(BoolField("Open sign-in links automatically", settings.OpenLoginLinks, value => settings.OpenLoginLinks = value));
            section.Add(ParleyStyles.Text("Secrets are stored in " + SecretStores.Default.Description + ", never in project files.", ParleyStyles.Muted));
            return section;
        }

        private static VisualElement BuildProject()
        {
            ParleyProjectSettings project = ParleyProjectSettings.instance;
            VisualElement section = Section("This project (ProjectSettings/ParleySettings.asset)");
            TextField prompt = new TextField("Extra instructions") { value = project.AppendSystemPrompt ?? string.Empty, multiline = true, isDelayed = true };
            prompt.AddToClassList("pl-prefs__multiline");
            prompt.RegisterValueChangedCallback(evt => project.AppendSystemPrompt = evt.newValue);
            section.Add(prompt);
            TextField directories = ListField("Extra directories", project.AdditionalDirectories, project.SetAdditionalDirectories);
            directories.textEdition.placeholder = "Comma-separated folders the agent may access";
            section.Add(directories);
            section.Add(BoolField("Include project instruction files (CLAUDE.md, AGENTS.md, CODEX.md, …) for local models", project.IncludeProjectInstructions,
                value => project.IncludeProjectInstructions = value));
            TextField instructionFiles = ListField("Extra instruction files", project.InstructionFiles, project.SetInstructionFiles);
            instructionFiles.textEdition.placeholder = "Comma-separated files added to the local agent's instructions";
            section.Add(instructionFiles);
            Toggle unityTools = BoolField("Unity editor tools", project.UnityToolsEnabled, value => project.UnityToolsEnabled = value);
            unityTools.tooltip = "Claude Code sees them as " + SdkMcpBridge.ToolPrefix + "*, Codex and local models as " + ToolCatalog.UnityLocalPrefix + "*.";
            section.Add(unityTools);
            foreach (IParleyTool tool in ToolCatalog.CreateUnityTools())
            {
                string name = tool.Name;
                Toggle toggle = BoolField("    " + name, !Contains(project.DisabledUnityTools, name), value => project.SetUnityToolEnabled(name, value));
                toggle.tooltip = tool.Description;
                section.Add(toggle);
            }

            return section;
        }

        private static async Task<string> DetectClaudeAsync()
        {
            string path = await ClaudeCliLocator.LocateAsync(ParleyUserSettings.instance.CliPathOverride);
            if (path == null)
            {
                return "Not found. Install Claude Code or set the path.";
            }

            string version = await ClaudeCliLocator.GetVersionAsync(path, await ShellEnvironment.GetLoginPathAsync());
            return DescribeExecutable(path, version);
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

        private static VisualElement Section(string title)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("pl-prefs__section");
            section.Add(ParleyStyles.Text(title, "pl-prefs__section-title"));
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