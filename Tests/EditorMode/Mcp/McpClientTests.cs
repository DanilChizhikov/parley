using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Mcp;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class McpClientTests
    {
        [Test]
        public void ConnectsAndListsToolsAsParleyTools()
        {
            ScriptedMcpTransport transport = CreateTransport();
            McpClient client = new McpClient(new McpServerLaunch { Name = "echo" }, transport);

            client.Connect();

            Assert.IsTrue(client.Connecting.IsCompleted);
            Assert.AreEqual(McpConnectionState.Connected, client.State, client.Error);
            Assert.IsTrue(transport.Started);
            Assert.AreEqual("2025-06-18", transport.ProtocolVersion);
            Assert.AreEqual("initialize", (string)transport.Requests[0]["method"]);
            Assert.AreEqual("notifications/initialized", (string)transport.Notifications[0]["method"]);
            Assert.AreEqual(2, client.Tools.Count);
            Assert.AreEqual("mcp__echo__say", client.Tools[0].Name);
            Assert.AreEqual(ToolKind.External, client.Tools[0].Kind);
            Assert.AreEqual("mcp__echo__peek_value", client.Tools[1].Name);
            Assert.AreEqual(ToolKind.External, client.Tools[1].Kind);
        }

        [Test]
        public void CallsToolsByOriginalName()
        {
            ScriptedMcpTransport transport = CreateTransport();
            McpClient client = new McpClient(new McpServerLaunch { Name = "echo" }, transport);
            client.Connect();

            Task<ToolResult> call = client.Tools[1].ExecuteAsync(new JObject { ["text"] = "hi" }, new ToolContext(), CancellationToken.None);

            Assert.IsTrue(call.IsCompleted);
            Assert.IsFalse(call.Result.IsError);
            Assert.AreEqual("peek.value:hi\n[image]", call.Result.Text);
        }

        [Test]
        public void ReportsToolErrorsAndClosedTransport()
        {
            ScriptedMcpTransport transport = CreateTransport();
            transport.Handlers["tools/call"] = _ => new JObject
            {
                ["isError"] = true,
                ["content"] = new JArray { new JObject { ["type"] = "text", ["text"] = "bad input" } },
            };

            McpClient client = new McpClient(new McpServerLaunch { Name = "echo" }, transport);
            client.Connect();
            ToolResult error = client.CallToolAsync("say", new JObject(), CancellationToken.None).Result;
            Assert.IsTrue(error.IsError);
            Assert.AreEqual("bad input", error.Text);

            transport.Close("Exited with code 1.");
            Assert.AreEqual(McpConnectionState.Failed, client.State);
            Assert.AreEqual(0, client.Tools.Count);
            StringAssert.Contains("not connected", client.CallToolAsync("say", new JObject(), CancellationToken.None).Result.Text);
        }

        [Test]
        public void FailsWhenInitializeReturnsError()
        {
            ScriptedMcpTransport transport = CreateTransport();
            transport.Handlers["initialize"] = _ => new JObject { ["__error"] = "unsupported protocol" };
            McpClient client = new McpClient(new McpServerLaunch { Name = "echo" }, transport);

            client.Connect();

            Assert.AreEqual(McpConnectionState.Failed, client.State);
            StringAssert.Contains("unsupported protocol", client.Error);
        }

        private static ScriptedMcpTransport CreateTransport()
        {
            ScriptedMcpTransport transport = new ScriptedMcpTransport();
            transport.Handlers["initialize"] = _ => new JObject { ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JObject() };
            transport.Handlers["tools/list"] = _ => new JObject
            {
                ["tools"] = new JArray
                {
                    new JObject { ["name"] = "say", ["inputSchema"] = new JObject { ["type"] = "object" } },
                    new JObject { ["name"] = "peek.value", ["annotations"] = new JObject { ["readOnlyHint"] = true } },
                },
            };
            transport.Handlers["tools/call"] = parameters => new JObject
            {
                ["content"] = new JArray
                {
                    new JObject { ["type"] = "text", ["text"] = (string)parameters["name"] + ":" + (string)parameters["arguments"]["text"] },
                    new JObject { ["type"] = "image", ["data"] = "AAAA" },
                },
            };

            return transport;
        }
    }
}
