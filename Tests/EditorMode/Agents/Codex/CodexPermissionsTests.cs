using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexPermissionsTests
    {
        [Test]
        public void MapsModesToApprovalAndSandbox()
        {
            Assert.AreEqual("on-request", CodexPermissions.ApprovalPolicy(PermissionMode.Default));
            Assert.AreEqual("read-only", CodexPermissions.SandboxMode(PermissionMode.Default));
            Assert.AreEqual("on-request", CodexPermissions.ApprovalPolicy(PermissionMode.AcceptEdits));
            Assert.AreEqual("workspace-write", CodexPermissions.SandboxMode(PermissionMode.AcceptEdits));
            Assert.AreEqual("on-request", CodexPermissions.ApprovalPolicy(PermissionMode.Plan));
            Assert.AreEqual("read-only", CodexPermissions.SandboxMode(PermissionMode.Plan));
            Assert.AreEqual("never", CodexPermissions.ApprovalPolicy(PermissionMode.DontAsk));
            Assert.AreEqual("read-only", CodexPermissions.SandboxMode(PermissionMode.DontAsk));
            Assert.AreEqual("never", CodexPermissions.ApprovalPolicy(PermissionMode.BypassPermissions));
            Assert.AreEqual("danger-full-access", CodexPermissions.SandboxMode(PermissionMode.BypassPermissions));
        }

        [Test]
        public void SandboxPolicyIncludesExtraWritableRoots()
        {
            JObject write = CodexPermissions.SandboxPolicy(PermissionMode.AcceptEdits, new[] { "Packages/Foo", " " });
            Assert.AreEqual("workspaceWrite", (string)write["type"]);
            Assert.AreEqual(1, ((JArray)write["writableRoots"]).Count);
            Assert.AreEqual(ProjectPaths.Resolve("Packages/Foo"), (string)write["writableRoots"][0]);
            Assert.IsFalse((bool)write["networkAccess"]);
            Assert.AreEqual("readOnly", (string)CodexPermissions.SandboxPolicy(PermissionMode.Default, new string[0])["type"]);
            Assert.AreEqual("dangerFullAccess", (string)CodexPermissions.SandboxPolicy(PermissionMode.BypassPermissions, new string[0])["type"]);
        }

        [Test]
        public void AutoIsNotOfferedAndPlanUsesCollaborationMode()
        {
            CollectionAssert.DoesNotContain(CodexPermissions.Supported, PermissionMode.Auto);
            Assert.AreEqual(PermissionMode.Default, CodexPermissions.Normalize(PermissionMode.Auto));
            Assert.AreEqual(CodexPermissions.PlanCollaboration, CodexPermissions.CollaborationMode(PermissionMode.Plan));
            Assert.AreEqual(CodexPermissions.DefaultCollaboration, CodexPermissions.CollaborationMode(PermissionMode.AcceptEdits));
        }
    }
}