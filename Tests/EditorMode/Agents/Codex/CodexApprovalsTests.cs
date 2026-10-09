using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexApprovalsTests
    {
        [Test]
        public void CommandApprovalBecomesBashRequest()
        {
            CodexServerRequest server = Server(CodexApprovals.CommandApproval, "{\"itemId\":\"c1\",\"command\":\"rm -rf build\",\"cwd\":\"/p\",\"reason\":\"needs write\",\"proposedExecpolicyAmendment\":[\"rm\",\"-rf\"]}");
            PendingRequest request = CodexApprovals.Parse(server, null);
            Assert.AreEqual("7", request.Id);
            Assert.AreEqual(CodexApprovals.BashTool, request.ToolName);
            Assert.AreEqual(RequestKind.ToolPermission, request.Kind);
            Assert.AreEqual("rm -rf build", (string)request.Input["command"]);
            Assert.AreEqual("needs write", request.DecisionReason);
            Assert.AreEqual("c1", request.ToolUseId);
        }

        [Test]
        public void CommandDecisionsMapToCodexValues()
        {
            CodexServerRequest server = Server(CodexApprovals.CommandApproval, "{\"itemId\":\"c1\",\"proposedExecpolicyAmendment\":[\"rm\",\"-rf\"]}");
            Assert.AreEqual("accept", (string)CodexApprovals.Result(server, Decision.AllowWith(null))["decision"]);
            Assert.AreEqual("decline", (string)CodexApprovals.Result(server, Decision.Deny("no"))["decision"]);
            Assert.AreEqual("cancel", (string)CodexApprovals.Result(server, Decision.Deny("stop", true))["decision"]);

            JObject remembered = CodexApprovals.Result(server, new Decision { Allow = true, Remember = true });
            Assert.AreEqual("rm", (string)remembered["decision"]["acceptWithExecpolicyAmendment"]["execpolicy_amendment"][0]);

            CodexServerRequest plain = Server(CodexApprovals.CommandApproval, "{\"itemId\":\"c2\"}");
            Assert.AreEqual("acceptForSession", (string)CodexApprovals.Result(plain, new Decision { Allow = true, Remember = true })["decision"]);
        }

        [Test]
        public void FileChangeUsesStartedItemChanges()
        {
            CodexServerRequest server = Server(CodexApprovals.FileChangeApproval, "{\"itemId\":\"f1\",\"grantRoot\":\"/p/Assets\"}");
            JObject item = JObject.Parse("{\"type\":\"fileChange\",\"id\":\"f1\",\"changes\":[{\"path\":\"a.cs\",\"kind\":{\"type\":\"update\"},\"diff\":\"@@\"}],\"status\":\"inProgress\"}");
            PendingRequest request = CodexApprovals.Parse(server, item);
            Assert.AreEqual(CodexApprovals.ApplyPatchTool, request.ToolName);
            Assert.AreEqual("a.cs", (string)request.Input["changes"][0]["path"]);
            Assert.AreEqual("/p/Assets", request.BlockedPath);
            Assert.AreEqual("acceptForSession", (string)CodexApprovals.Result(server, new Decision { Allow = true, Remember = true })["decision"]);
        }

        [Test]
        public void UserInputRoundTripsThroughAskUserQuestion()
        {
            string questions = "[{\"id\":\"q1\",\"header\":\"Mode\",\"question\":\"Which mode?\",\"options\":[{\"label\":\"Fast\",\"description\":\"f\"},{\"label\":\"Safe\",\"description\":\"s\"}]},"
                + "{\"id\":\"q2\",\"header\":\"Name\",\"question\":\"Name?\",\"options\":null}]";
            CodexServerRequest server = Server(CodexApprovals.UserInputRequest, "{\"itemId\":\"u1\",\"isBlocking\":true,\"questions\":" + questions + "}");
            PendingRequest request = CodexApprovals.Parse(server, null);
            Assert.AreEqual(RequestKind.Question, request.Kind);
            JArray parsed = (JArray)request.Input["questions"];
            Assert.AreEqual("Which mode?", (string)parsed[0]["question"]);
            Assert.AreEqual(2, ((JArray)parsed[0]["options"]).Count);
            Assert.AreEqual(0, ((JArray)parsed[1]["options"]).Count);

            JObject answered = CodexApprovals.Result(server, Decision.AllowWith(JObject.Parse("{\"answers\":{\"Which mode?\":[\"Fast\",\"Safe\"]},\"response\":\"Bob\"}")));
            CollectionAssert.AreEqual(new[] { "Fast", "Safe" }, answered["answers"]["q1"]["answers"].ToObject<string[]>());
            CollectionAssert.AreEqual(new[] { "Bob" }, answered["answers"]["q2"]["answers"].ToObject<string[]>());

            JObject declined = CodexApprovals.Result(server, Decision.Deny("later"));
            Assert.AreEqual(0, ((JObject)declined["answers"]).Count);
        }

        [Test]
        public void PermissionsGrantEchoesRequest()
        {
            CodexServerRequest server = Server(CodexApprovals.PermissionsApproval, "{\"itemId\":\"p1\",\"cwd\":\"/p\",\"permissions\":{\"network\":{\"enabled\":true}}}");
            Assert.AreEqual(CodexApprovals.PermissionsTool, CodexApprovals.Parse(server, null).ToolName);

            JObject session = CodexApprovals.Result(server, new Decision { Allow = true, Remember = true });
            Assert.IsTrue((bool)session["permissions"]["network"]["enabled"]);
            Assert.AreEqual("session", (string)session["scope"]);
            Assert.AreEqual("turn", (string)CodexApprovals.Result(server, Decision.AllowWith(null))["scope"]);

            JObject denied = CodexApprovals.Result(server, Decision.Deny("no"));
            Assert.AreEqual(0, ((JObject)denied["permissions"]).Count);
            Assert.IsNull(denied["scope"]);
        }

        [Test]
        public void OnlyKnownRequestsAreSupported()
        {
            Assert.IsTrue(CodexApprovals.IsSupported(CodexApprovals.CommandApproval));
            Assert.IsTrue(CodexApprovals.IsSupported(CodexApprovals.FileChangeApproval));
            Assert.IsTrue(CodexApprovals.IsSupported(CodexApprovals.PermissionsApproval));
            Assert.IsTrue(CodexApprovals.IsSupported(CodexApprovals.UserInputRequest));
            Assert.IsFalse(CodexApprovals.IsSupported("mcpServer/elicitation/request"));
        }

        private static CodexServerRequest Server(string method, string parameters)
        {
            return new CodexServerRequest { RpcId = 7, Method = method, Params = JObject.Parse(parameters) };
        }
    }
}