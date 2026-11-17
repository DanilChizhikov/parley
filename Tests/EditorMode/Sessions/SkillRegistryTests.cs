using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Skills;
using DTech.Parley.Editor.Tools;
using DTech.Parley.Editor.UI;
using DTech.Parley.Tests.EditorMode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Editor.Sessions
{
    internal sealed class SkillRegistryTests
    {
        [Test]
        public void PackageIncludesBuiltinReviewer()
        {
            SkillRegistry.Reload();
            Assert.That(SkillRegistry.Current.Skills, Has.Some.Matches<SkillDocument>(skill => skill.Name == "reviewer"));
        }

        [Test]
        public void ParsedOrchestrationMetadataSurvivesFormatting()
        {
            SkillDocument skill = SkillMarkdown.Parse("---\nname: reviewer\ndescription: >\n  Review code\n  carefully\npreferred_provider: 'local'\nmodel: \"model:one\" # comment\nhandoff_mode: auto\nallowed_targets:\n  - developer\n  - 'qa'\nunknown: keep\n---\nInstructions");
            Assert.That(skill.Description, Is.EqualTo("Review code carefully"));
            Assert.That(skill.Model, Is.EqualTo("model:one"));
            Assert.That(skill.AllowedTargets, Is.EqualTo(new[] { "developer", "qa" }));
            SkillDocument reparsed = SkillMarkdown.Parse(SkillMarkdown.Format(skill));
            Assert.That(reparsed.PreferredProvider, Is.EqualTo("local"));
            Assert.That(reparsed.Model, Is.EqualTo(skill.Model));
            Assert.That(reparsed.HandoffMode, Is.EqualTo("auto"));
            Assert.That(reparsed.AllowedTargets, Is.EqualTo(skill.AllowedTargets));
            Assert.That(reparsed.ExtraFrontmatter, Does.Contain("unknown: keep"));
            Assert.That(reparsed.Body, Is.EqualTo("Instructions"));
        }

        [Test]
        public void FlowTargetsRespectQuotedCommasAndComments()
        {
            SkillDocument skill = SkillMarkdown.Parse("---\nallowed_targets: [developer, 'qa,team', \"hash#tag\"] # targets\n---\nbody");
            Assert.That(skill.AllowedTargets, Is.EqualTo(new[] { "developer", "qa,team", "hash#tag" }));
        }

        [TestCase("handoff_mode: unsafe")]
        [TestCase("allowed_targets: developer")]
        public void MalformedMetadataHasDiagnostics(string metadata)
        {
            SkillDocument skill = SkillMarkdown.Parse("---\nname: reviewer\n" + metadata + "\n---\nbody");
            Assert.That(skill.Diagnostics, Is.Not.Empty);
        }

        [Test]
        public void CustomSkillOverridesBuiltinWithStableIdAndReloadedBody()
        {
            string directory = Path.Combine(Path.GetTempPath(), "parley-skills-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                SkillRegistry registry = new();
                registry.Add(new SkillDocument { Name = "reviewer", Body = "builtin" }, "builtin");
                string file = Path.Combine(directory, "review.md");
                File.WriteAllText(file, "---\nname: reviewer\n---\ncustom");
                registry.LoadDirectory(directory);
                SkillDocument loaded = new List<SkillDocument>(registry.Skills)[0];
                Assert.That(loaded.Id, Is.EqualTo("skill:reviewer"));
                Assert.That(loaded.Body, Is.EqualTo("custom"));
                Assert.That(registry.Diagnostics, Has.Some.Contains("overrides"));
                File.WriteAllText(file, "---\nname: reviewer\n---\nupdated");
                registry.LoadDirectory(directory);
                Assert.That(new List<SkillDocument>(registry.Skills)[0].Body, Is.EqualTo("updated"));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void RoleRecordSnapshotsInstructionsAndDoesNotChangeSharedProfile()
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("codex-profile", CodexAuthMethod.CliDefault);
            profile.Model = "original";
            SkillDocument role = new()
            {
                Name = "reviewer", Body = "Review instructions", PreferredProvider = "codex", Model = "role-model",
                HandoffMode = "auto", AllowedTargets = new List<string> { "developer" },
            };
            Assert.That(ChatPresets.TryCreateRole(role, new[] { profile }, profile, PermissionMode.Plan,
                out ParleyProfile selected, out SessionRecord record, out string error), Is.True, error);
            Assert.That(selected, Is.SameAs(profile));
            Assert.That(profile.Model, Is.EqualTo("original"));
            role.Body = "changed";
            role.AllowedTargets.Clear();
            SessionRecord restored = JsonConvert.DeserializeObject<SessionRecord>(JsonConvert.SerializeObject(record));
            Assert.That(restored.Model, Is.EqualTo("role-model"));
            Assert.That(restored.RoleInstructions, Is.EqualTo("Review instructions"));
            Assert.That(restored.RoleSkillId, Is.EqualTo("skill:reviewer"));
            Assert.That(restored.RoleAllowedTargets, Is.EqualTo(new[] { "developer" }));
            Assert.That(restored.Mode, Is.EqualTo(PermissionMode.Plan));
        }

        [TestCase("", "model")]
        [TestCase("codex", "")]
        [TestCase("missing-provider", "model")]
        public void MissingProviderOrModelProducesActionableError(string provider, string model)
        {
            ParleyProfile profile = ParleyProfile.CreateCodex("Codex", CodexAuthMethod.CliDefault);
            SkillDocument role = new() { Name = "reviewer", Body = "Review", PreferredProvider = provider, Model = model };
            Assert.That(ChatPresets.TryCreateRole(role, new[] { profile }, profile, PermissionMode.Default,
                out _, out SessionRecord record, out string error), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void CurrentFallbackIsExplicitAndAmbiguousProviderIsRejected()
        {
            ParleyProfile first = ParleyProfile.CreateLocal("First", LocalPreset.LmStudio);
            first.Model = "configured-model";
            ParleyProfile second = ParleyProfile.CreateLocal("Second", LocalPreset.Ollama);
            SkillDocument role = new() { Name = "reviewer", Body = "Review", PreferredProvider = "local", Model = "current" };
            Assert.That(ChatPresets.TryCreateRole(role, new[] { first, second }, first, PermissionMode.Default,
                out _, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("Multiple profiles"));
            role.PreferredProvider = "current";
            Assert.That(ChatPresets.TryCreateRole(role, new[] { first, second }, first, PermissionMode.Default,
                out ParleyProfile selected, out SessionRecord record, out error), Is.True, error);
            Assert.That(selected, Is.SameAs(first));
            Assert.That(record.Model, Is.EqualTo(first.Model));
        }

        [TestCase(ProfileKind.Local)]
        [TestCase(ProfileKind.ClaudeCode)]
        [TestCase(ProfileKind.Codex)]
        public void BackendRequestContainsSessionRoleInstructions(ProfileKind kind)
        {
            ParleyProfile profile = new() { Kind = kind, Model = "role-model" };
            ToolCatalog tools = new(Array.Empty<IParleyTool>());
            RecordingSink sink = new();
            using IAgentBackend backend = kind switch
            {
                ProfileKind.ClaudeCode => new ClaudeCodeBackend(profile, sink, tools, null, PermissionMode.Default),
                ProfileKind.Codex => new CodexBackend(profile, sink, tools, null, PermissionMode.Default),
                _ => new LocalAgentBackend(profile, sink, tools, null, null, PermissionMode.Default),
            };
            backend.SetSkillConfiguration(new SkillConfiguration { RoleInstructions = "Unique session role instructions" });
            string method = kind == ProfileKind.Local ? "BuildBody" : kind == ProfileKind.ClaudeCode
                ? "BuildSystemPromptAppendix" : "BuildDeveloperInstructions";
            MethodInfo builder = backend.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            object request = builder.Invoke(backend, kind == ProfileKind.Local ? new object[] { false } : null);
            string instructions = kind == ProfileKind.Local
                ? (string)((JObject)request)["messages"][0]["content"] : (string)request;
            Assert.That(instructions, Does.Contain("Unique session role instructions"));
        }

        [Test]
        public void EditingLibrarySkillPreservesRoleMetadata()
        {
            SkillDocument original = new()
            {
                Name = "reviewer", Description = "Review code", Body = "Instructions",
                PreferredProvider = "codex", Model = "role-model", HandoffMode = "auto",
                AllowedTargets = new List<string> { "developer" },
            };
            SkillDocument saved = null;
            SkillForm form = new(original, (draft, _, _) => saved = draft, () => { });
            typeof(SkillForm).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
            Assert.That(saved, Is.Not.Null);
            SkillDocument reparsed = SkillMarkdown.Parse(SkillMarkdown.Format(saved));
            Assert.That(reparsed.PreferredProvider, Is.EqualTo("codex"));
            Assert.That(reparsed.Model, Is.EqualTo("role-model"));
            Assert.That(reparsed.HandoffMode, Is.EqualTo("auto"));
            Assert.That(reparsed.AllowedTargets, Is.EqualTo(new[] { "developer" }));
        }

        [TestCase("", "model")]
        [TestCase("current", "")]
        [TestCase("missing", "model")]
        public void MissingRoleProviderOrModelFailsVisibly(string provider, string model)
        {
            ParleyProfile current = ParleyProfile.CreateCodex("codex", CodexAuthMethod.CliDefault);
            SkillDocument role = new() { Body = "instructions", PreferredProvider = provider, Model = model };
            Assert.That(ChatPresets.TryCreateRole(role, new[] { current }, current, PermissionMode.Default,
                out _, out SessionRecord record, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(record, Is.Null);
        }

        [Test]
        public void CurrentProfileFallbackMustBeExplicitAndHaveAModel()
        {
            ParleyProfile current = ParleyProfile.CreateLocal("local", LocalPreset.Ollama);
            SkillDocument role = new() { Body = "instructions", PreferredProvider = "current", Model = "current" };
            Assert.That(ChatPresets.TryCreateRole(role, new[] { current }, current, PermissionMode.Default,
                out _, out _, out _), Is.False);
            current.Model = "selected-model";
            Assert.That(ChatPresets.TryCreateRole(role, new[] { current }, current, PermissionMode.Default,
                out _, out SessionRecord record, out _), Is.True);
            Assert.That(record.Model, Is.EqualTo("selected-model"));
        }
    }
}
