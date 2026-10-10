using System.Collections.Generic;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Claude;
using DTech.Parley.Editor.Agents.Codex;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class McpWireTests
    {
        [Test]
        public void ClaudeSetServersKeepsUnitySdkServer()
        {
            JObject servers = ClaudeWire.McpServers(CreateConfiguration(), null, true);

            Assert.AreEqual("sdk", (string)servers["unity"]["type"]);
            Assert.AreEqual("stdio", (string)servers["echo"]["type"]);
            Assert.AreEqual("npx", (string)servers["echo"]["command"]);
            Assert.AreEqual("bar", (string)servers["echo"]["env"]["FOO"]);
            Assert.AreEqual("http", (string)servers["remote"]["type"]);
            Assert.AreEqual("Bearer x", (string)servers["remote"]["headers"]["Authorization"]);
            Assert.IsNull(ClaudeWire.McpServers(CreateConfiguration(), null, false)["unity"]);
        }

        [Test]
        public void ClaudeKeepsDynamicServersParleyDidNotAdd()
        {
            JObject status = JObject.Parse(@"{""mcpServers"":[
                {""name"":""mine"",""status"":""connected"",""scope"":""dynamic"",""source"":""dynamic"",""config"":{""type"":""stdio"",""command"":""node"",""scope"":""dynamic""}},
                {""name"":""unity"",""status"":""connected"",""scope"":""dynamic"",""source"":""sdk"",""config"":{""type"":""sdk"",""name"":""unity""}},
                {""name"":""docs"",""status"":""connected"",""scope"":""claudeai"",""config"":{""type"":""claudeai-proxy""}}]}");

            JObject passthrough = ClaudeWire.PassthroughMcpServers(status);
            JObject servers = ClaudeWire.McpServers(CreateConfiguration(), passthrough, true);

            Assert.AreEqual(1, passthrough.Count);
            Assert.AreEqual("node", (string)servers["mine"]["command"]);
            Assert.IsNull(servers["mine"]["scope"]);
            Assert.IsNull(servers["docs"]);
            Assert.AreEqual("sdk", (string)servers["unity"]["type"]);
        }

        [Test]
        public void ClaudeDenyRulesUseNormalizedNames()
        {
            Assert.AreEqual("mcp__claude_ai_Claude_Docs,mcp__github", ClaudeWire.McpDenyRules(new[] { "claude.ai Claude Docs", "github" }));
        }

        [Test]
        public void ClaudeStatusSplitsParleyAndExternalServers()
        {
            McpConfiguration configuration = CreateConfiguration();
            configuration.DisabledExternal.Add("github");
            JObject response = JObject.Parse(@"{""mcpServers"":[
                {""name"":""echo"",""status"":""connected"",""scope"":""dynamic"",""source"":""dynamic""},
                {""name"":""remote"",""status"":""failed"",""scope"":""dynamic"",""source"":""dynamic"",""error"":""boom""},
                {""name"":""github"",""status"":""connected"",""scope"":""user""},
                {""name"":""docs"",""status"":""needs-auth"",""scope"":""claudeai"",""source"":""claudeai""},
                {""name"":""unity"",""status"":""connected"",""scope"":""dynamic"",""source"":""sdk""}]}");

            List<McpServerStatus> statuses = ClaudeWire.ParseMcpStatus(response, configuration);

            Assert.AreEqual(4, statuses.Count);
            Assert.AreEqual(McpConnectionState.Connected, statuses[0].State);
            Assert.IsFalse(statuses[0].IsExternal);
            Assert.AreEqual(McpConnectionState.Failed, statuses[1].State);
            Assert.AreEqual("boom", statuses[1].Error);
            Assert.IsTrue(statuses[2].IsExternal);
            Assert.AreEqual(McpConnectionState.Disabled, statuses[2].State);
            Assert.AreEqual(McpConnectionState.NeedsAuth, statuses[3].State);
        }

        [Test]
        public void CodexConfigOverridesServersAndDisablesExternal()
        {
            McpConfiguration configuration = CreateConfiguration();
            configuration.DisabledExternal.Add("github");

            JObject config = CodexMcp.Config(configuration);

            Assert.AreEqual("npx", (string)config["mcp_servers.echo"]["command"]);
            Assert.AreEqual("-y", (string)config["mcp_servers.echo"]["args"][0]);
            Assert.AreEqual("https://example.com/mcp", (string)config["mcp_servers.remote"]["url"]);
            Assert.AreEqual("Bearer x", (string)config["mcp_servers.remote"]["http_headers"]["Authorization"]);
            Assert.IsFalse((bool)config["mcp_servers.github.enabled"]);
            Assert.IsNull(CodexMcp.Config(new McpConfiguration()));
        }

        [Test]
        public void CodexStatusMapsRuntimeStates()
        {
            JObject result = JObject.Parse(@"{""data"":[
                {""name"":""echo"",""runtimeStatus"":""connected"",""tools"":{""a"":{},""b"":{}}},
                {""name"":""github"",""runtimeStatus"":""disabled"",""tools"":{}},
                {""name"":""auth"",""runtimeStatus"":""authenticationRequired"",""tools"":{}},
                {""name"":""broken"",""runtimeStatus"":null,""tools"":{},""toolsError"":""nope""}]}");
            List<McpServerStatus> statuses = new ();

            CodexMcp.ParseStatus(result, CreateConfiguration(), statuses);

            Assert.AreEqual(4, statuses.Count);
            Assert.AreEqual(2, statuses[0].ToolCount);
            Assert.IsFalse(statuses[0].IsExternal);
            Assert.AreEqual(McpConnectionState.Disabled, statuses[1].State);
            Assert.IsTrue(statuses[1].IsExternal);
            Assert.AreEqual(McpConnectionState.NeedsAuth, statuses[2].State);
            Assert.AreEqual(McpConnectionState.Pending, statuses[3].State);
            Assert.AreEqual("nope", statuses[3].Error);
        }

        private static McpConfiguration CreateConfiguration()
        {
            McpConfiguration configuration = new McpConfiguration();
            McpServerLaunch echo = new McpServerLaunch { Name = "echo", Transport = McpTransport.Stdio, Command = "npx" };
            echo.Arguments.Add("-y");
            echo.Environment["FOO"] = "bar";
            McpServerLaunch remote = new McpServerLaunch { Name = "remote", Transport = McpTransport.Http, Url = "https://example.com/mcp" };
            remote.Headers["Authorization"] = "Bearer x";
            configuration.Servers.Add(echo);
            configuration.Servers.Add(remote);
            return configuration;
        }
    }
}
