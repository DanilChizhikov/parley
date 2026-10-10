using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class CodexWireTests
    {
        [Test]
        public void MessagesHaveNoJsonRpcField()
        {
            JObject request = CodexWire.Request(3, "turn/start", null);
            Assert.AreEqual(3, (long)request["id"]);
            Assert.AreEqual("turn/start", (string)request["method"]);
            Assert.AreEqual(0, ((JObject)request["params"]).Count);
            Assert.IsNull(request["jsonrpc"]);

            JObject notification = CodexWire.Notification("initialized");
            Assert.AreEqual("{\"method\":\"initialized\"}", CodexWire.Serialize(notification));
        }

        [Test]
        public void ResponsesEchoIdOfAnyType()
        {
            Assert.AreEqual(0, (long)CodexWire.Response(new JValue(0), new JObject())["id"]);
            Assert.AreEqual("abc", (string)CodexWire.Response(new JValue("abc"), null)["id"]);
            JObject error = CodexWire.Error(new JValue(4), -32601, "nope");
            Assert.AreEqual(-32601, (int)error["error"]["code"]);
            Assert.AreEqual("nope", (string)error["error"]["message"]);
        }

        [Test]
        public void RequestKeyHandlesNumbersAndStrings()
        {
            Assert.AreEqual("0", CodexWire.RequestKey(new JValue(0)));
            Assert.AreEqual("abc", CodexWire.RequestKey(new JValue("abc")));
            Assert.IsNull(CodexWire.RequestKey(null));
        }

        [Test]
        public void UserInputCarriesAttachments()
        {
            UserTurn turn = new UserTurn { Text = "look" };
            turn.Attachments.Add(new ChatAttachment { Kind = AttachmentKind.Text, Label = "Selection", Text = "Player" });
            turn.Attachments.Add(new ChatAttachment { Kind = AttachmentKind.Image, Label = "shot", MediaType = "image/png", Base64 = "AAAA" });
            JArray input = CodexWire.UserInput(turn);
            Assert.AreEqual("text", (string)input[0]["type"]);
            StringAssert.Contains("<attachment name=\"Selection\">", (string)input[0]["text"]);
            Assert.AreEqual("image", (string)input[1]["type"]);
            Assert.AreEqual("data:image/png;base64,AAAA", (string)input[1]["url"]);
        }

        [Test]
        public void ImageOnlyTurnHasNoTextItem()
        {
            UserTurn turn = new UserTurn();
            turn.Attachments.Add(new ChatAttachment { Kind = AttachmentKind.Image, Base64 = "BBBB" });
            JArray input = CodexWire.UserInput(turn);
            Assert.AreEqual(1, input.Count);
            Assert.AreEqual("data:image/png;base64,BBBB", (string)input[0]["url"]);
        }
    }
}