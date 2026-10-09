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
    }
}