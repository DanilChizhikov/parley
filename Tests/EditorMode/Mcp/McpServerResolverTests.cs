using DTech.Parley.Editor;
using DTech.Parley.Editor.Mcp;
using DTech.Parley.Editor.Secrets;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class McpServerResolverTests
    {
        [TestCase("github", true)]
        [TestCase("my-server_2", true)]
        [TestCase("", false)]
        [TestCase("unity", false)]
        [TestCase("has space", false)]
        [TestCase("dot.name", false)]
        public void ValidatesNames(string name, bool expected)
        {
            Assert.AreEqual(expected, McpServerResolver.IsValidName(name));
        }

        [Test]
        public void NormalizesToolPrefixLikeClaudeCode()
        {
            Assert.AreEqual("mcp__claude_ai_Claude_Docs__", McpServerResolver.ToolPrefix("claude.ai Claude Docs"));
            Assert.AreEqual("mcp__github__", McpServerResolver.ToolPrefix("github"));
        }

        [Test]
        public void ShortensLongToolNamesStably()
        {
            string longTool = new string('t', 80);
            string name = McpServerResolver.ToolName("server", longTool);

            Assert.AreEqual(64, name.Length);
            Assert.AreEqual(name, McpServerResolver.ToolName("server", longTool));
            Assert.AreNotEqual(name, McpServerResolver.ToolName("server", longTool + "x"));
            Assert.AreEqual("mcp__server__peek_value", McpServerResolver.ToolName("server", "peek.value"));
        }

        [Test]
        public void ResolvesStdioServerWithSecrets()
        {
            MemorySecretStore store = new MemorySecretStore();
            McpServerDefinition definition = new McpServerDefinition
            {
                Name = "fs",
                Transport = McpTransport.Stdio,
                Command = " npx ",
                Arguments = "-y \"@scope/server files\"",
            };

            McpVariable plain = new McpVariable { Key = "MODE", Value = "fast" };
            McpVariable secret = new McpVariable { Key = "TOKEN", IsSecret = true };
            definition.Environment.Add(plain);
            definition.Environment.Add(secret);
            definition.Environment.Add(new McpVariable { Key = " ", Value = "ignored" });
            definition.Headers.Add(new McpVariable { Key = "X-Ignored", Value = "1" });
            store.Values[SecretStores.Key(definition.SecretOwner, secret.SecretField)] = "s3cr3t";

            McpServerLaunch launch = McpServerResolver.Resolve(definition, store);

            Assert.AreEqual("npx", launch.Command);
            CollectionAssert.AreEqual(new[] { "-y", "@scope/server files" }, launch.Arguments);
            Assert.AreEqual("fast", launch.Environment["MODE"]);
            Assert.AreEqual("s3cr3t", launch.Environment["TOKEN"]);
            Assert.AreEqual(2, launch.Environment.Count);
            Assert.AreEqual(0, launch.Headers.Count);
        }

        [Test]
        public void ResolvesHttpServerHeadersOnly()
        {
            McpServerDefinition definition = new McpServerDefinition { Name = "remote", Transport = McpTransport.Http, Url = " https://example.com/mcp " };
            definition.Headers.Add(new McpVariable { Key = "Authorization", Value = "Bearer x" });
            definition.Environment.Add(new McpVariable { Key = "IGNORED", Value = "1" });

            McpServerLaunch launch = McpServerResolver.Resolve(definition, new MemorySecretStore());

            Assert.AreEqual("https://example.com/mcp", launch.Url);
            Assert.AreEqual("Bearer x", launch.Headers["Authorization"]);
            Assert.AreEqual(0, launch.Environment.Count);
        }

        [Test]
        public void FingerprintChangesWithConfiguration()
        {
            McpConfiguration first = new McpConfiguration();
            first.Servers.Add(new McpServerLaunch { Name = "a", Command = "x" });
            McpConfiguration second = new McpConfiguration();
            second.Servers.Add(new McpServerLaunch { Name = "a", Command = "x" });
            Assert.AreEqual(McpServerResolver.Fingerprint(first), McpServerResolver.Fingerprint(second));

            second.DisabledExternal.Add("github");
            Assert.AreNotEqual(McpServerResolver.Fingerprint(first), McpServerResolver.Fingerprint(second));
        }
    }
}
