using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ClaudeWireTests
    {
        [Test]
        public void PermissionResultShapes()
        {
            JObject input = new JObject { ["command"] = "ls" };
            JObject allow = ClaudeWire.PermissionResult(Decision.AllowWith(null), input);
            Assert.AreEqual("allow", (string)allow["behavior"]);
            Assert.AreEqual("ls", (string)allow["updatedInput"]["command"]);

            JObject deny = ClaudeWire.PermissionResult(Decision.Deny("no", true), input);
            Assert.AreEqual("deny", (string)deny["behavior"]);
            Assert.AreEqual("no", (string)deny["message"]);
            Assert.IsTrue((bool)deny["interrupt"]);

            JObject response = ClaudeWire.ControlSuccess("r1", allow);
            Assert.AreEqual("control_response", (string)response["type"]);
            Assert.AreEqual("success", (string)response["response"]["subtype"]);
            Assert.AreEqual("r1", (string)response["response"]["request_id"]);
        }

        [Test]
        public void UserMessageCarriesAttachments()
        {
            UserTurn turn = new UserTurn { Text = "look" };
            turn.Attachments.Add(new ChatAttachment { Kind = AttachmentKind.Text, Label = "Selection", Text = "Player" });
            turn.Attachments.Add(new ChatAttachment { Kind = AttachmentKind.Image, Label = "shot", MediaType = "image/png", Base64 = "AAAA" });
            JObject message = ClaudeWire.UserMessage(turn, "s1");
            JArray content = (JArray)message["message"]["content"];
            Assert.AreEqual("user", (string)message["type"]);
            StringAssert.Contains("<attachment name=\"Selection\">", (string)content[0]["text"]);
            Assert.AreEqual("image", (string)content[1]["type"]);
            Assert.AreEqual("AAAA", (string)content[1]["source"]["data"]);
        }

        [Test]
        public void PermissionRequestIsParsed()
        {
            JObject request = JObject.Parse("{\"subtype\":\"can_use_tool\",\"tool_name\":\"AskUserQuestion\",\"input\":{\"questions\":[]},\"tool_use_id\":\"t9\",\"permission_suggestions\":[{\"type\":\"setMode\",\"mode\":\"acceptEdits\",\"destination\":\"session\"}]}");
            PendingRequest parsed = ClaudeWire.ParsePermissionRequest("r5", request);
            Assert.AreEqual(RequestKind.Question, parsed.Kind);
            Assert.AreEqual("t9", parsed.ToolUseId);
            Assert.AreEqual(1, parsed.Suggestions.Count);
        }

        [Test]
        public void MissingConversationIgnoresModelText()
        {
            JObject answer = new JObject { ["type"] = "result", ["is_error"] = false, ["result"] = ClaudeWire.MissingConversation + " means the session file is gone." };
            Assert.IsFalse(ClaudeWire.IsMissingConversation(answer));

            JObject failure = new JObject { ["type"] = "result", ["is_error"] = true, ["errors"] = new JArray(ClaudeWire.MissingConversation + ": abc") };
            Assert.IsTrue(ClaudeWire.IsMissingConversation(failure));
        }
    }
}