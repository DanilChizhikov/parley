using System.Collections.Generic;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Tools.Builtin;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PermissionGateTests
    {
        [Test]
        public void AppliesModesAndRules()
        {
            List<AllowRule> rules = new ();
            string root = ProjectPaths.Root;
            PermissionGate gate = new PermissionGate(root, () => new string[0], () => rules);
            JObject edit = new JObject { ["file_path"] = "Assets/A.cs", ["old_string"] = "a", ["new_string"] = "b" };
            JObject outside = new JObject { ["file_path"] = "/etc/hosts" };
            Assert.AreEqual(GateVerdict.Ask, gate.Evaluate(new FileEditTool(), edit, PermissionMode.Default).Verdict);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new FileEditTool(), edit, PermissionMode.AcceptEdits).Verdict);
            Assert.AreEqual(GateVerdict.Deny, gate.Evaluate(new FileEditTool(), edit, PermissionMode.Plan).Verdict);
            Assert.AreEqual(GateVerdict.Deny, gate.Evaluate(new FileEditTool(), edit, PermissionMode.DontAsk).Verdict);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new FileReadTool(), new JObject { ["file_path"] = "Assets/A.cs" }, PermissionMode.Plan).Verdict);
            Assert.AreEqual(GateVerdict.Ask, gate.Evaluate(new FileReadTool(), outside, PermissionMode.Default).Verdict);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new BashTool(), new JObject { ["command"] = "git status" }, PermissionMode.Default).Verdict);
            Assert.AreEqual(GateVerdict.Ask, gate.Evaluate(new BashTool(), new JObject { ["command"] = "git status; rm -rf x" }, PermissionMode.Default).Verdict);

            rules.Add(PermissionGate.SuggestRule(new BashTool(), new JObject { ["command"] = "npm test --watch" }, root));
            Assert.AreEqual("npm test", rules[0].Pattern);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new BashTool(), new JObject { ["command"] = "npm test -- --ci" }, PermissionMode.Default).Verdict);
            Assert.AreEqual(GateVerdict.Ask, gate.Evaluate(new BashTool(), new JObject { ["command"] = "npm test && curl x" }, PermissionMode.Default).Verdict);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new AskUserQuestionTool(), new JObject(), PermissionMode.Plan).Verdict);
        }

        [Test]
        public void SuggestedEditRuleCoversAllEdits()
        {
            AllowRule rule = PermissionGate.SuggestRule(new FileEditTool(), new JObject(), ProjectPaths.Root);
            Assert.AreEqual(PermissionGate.FileEditRuleTool, rule.Tool);
            Assert.AreEqual("*", rule.Pattern);
            Assert.AreEqual(ProjectPaths.Root, rule.ProjectRoot);
        }

        [Test]
        public void SafeCommandsStayInsideWorkspace()
        {
            PermissionGate gate = new PermissionGate(ProjectPaths.Root, () => new string[0], () => new List<AllowRule>());
            Assert.AreEqual(GateVerdict.Allow, Bash(gate, "cat Assets/A.cs"));
            Assert.AreEqual(GateVerdict.Allow, Bash(gate, "grep -rn \"foo bar\" Assets"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "rg --pre=sh x ."));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "git diff --output=Assets/A.cs"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "git branch -D main"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "cat ~/.ssh/id_rsa"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "cat '/etc/hosts'"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "cat {/etc/hosts,a}"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "cat Assets/../../../etc/hosts"));
        }

        [Test]
        public void GitFolderEditsAlwaysAsk()
        {
            PermissionGate gate = new PermissionGate(ProjectPaths.Root, () => new string[0], () => new List<AllowRule>());
            JObject edit = new JObject { ["file_path"] = ".git/config", ["old_string"] = "a", ["new_string"] = "b" };
            Assert.AreEqual(GateVerdict.Ask, gate.Evaluate(new FileEditTool(), edit, PermissionMode.AcceptEdits).Verdict);
            Assert.AreEqual(GateVerdict.Allow, gate.Evaluate(new FileEditTool(), edit, PermissionMode.BypassPermissions).Verdict);
        }

        [Test]
        public void CommandRulesStayNarrow()
        {
            List<AllowRule> rules = new ();
            PermissionGate gate = new PermissionGate(ProjectPaths.Root, () => new string[0], () => rules);
            rules.Add(PermissionGate.SuggestRule(new BashTool(), new JObject { ["command"] = "npm test" }, ProjectPaths.Root));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "npm test > ~/.zshrc"));

            AllowRule python = PermissionGate.SuggestRule(new BashTool(), new JObject { ["command"] = "python tools/gen.py" }, ProjectPaths.Root);
            Assert.AreEqual("python tools/gen.py", python.Pattern);
            rules.Add(python);
            Assert.AreEqual(GateVerdict.Allow, Bash(gate, "python tools/gen.py"));
            Assert.AreEqual(GateVerdict.Ask, Bash(gate, "python -c \"print(1)\""));
            Assert.AreEqual("python tools/gen.py", (string)PermissionGate.ToSuggestions(python)[0]["rules"][0]["ruleContent"]);
        }

        private static GateVerdict Bash(PermissionGate gate, string command)
        {
            return gate.Evaluate(new BashTool(), new JObject { ["command"] = command }, PermissionMode.Default).Verdict;
        }
    }
}