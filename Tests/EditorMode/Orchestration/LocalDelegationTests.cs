using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Orchestration;
using DTech.Parley.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class LocalDelegationTests
    {
        [Test]
        public async Task NativeLocalLoopRegistersToolAndPreservesCallIdInPlanMode()
        {
            ParleyProfile profile = ParleyProfile.CreateLocal("Delegation test", LocalPreset.LmStudio);
            profile.Model = "m";
            DelegationRequest received = null;
            string receivedTurn = null;
            ToolCatalog tools = new(new IParleyTool[]
            {
                new DelegateToAgentTool((request, turn) =>
                {
                    received = request;
                    receivedTurn = turn;
                    return "queued receipt";
                }),
            });
            Client client = new();
            RecordingSink sink = new();
            using LocalAgentBackend backend = new(profile, sink, tools, null, null, PermissionMode.Plan,
                (_, _) => client);
            await backend.SendAsync(new UserTurn { Text = "delegate", CorrelationId = "local-turn" }, CancellationToken.None);
            Assert.IsNotNull(received);
            Assert.AreEqual("local-call", received.RequestId);
            Assert.AreEqual("worker", received.TargetSkill);
            Assert.AreEqual("local-turn", receivedTurn);
            Assert.AreEqual("delegate_to_agent", (string)client.Bodies[0]["tools"][0]["function"]["name"]);
            JArray messages = (JArray)client.Bodies[1]["messages"];
            Assert.IsTrue(System.Linq.Enumerable.Any(messages, item => (string)item["role"] == "tool" &&
                (string)item["content"] == "queued receipt"));
            Assert.AreEqual(PermissionMode.Plan, backend.Mode);
        }

        private sealed class Client : IChatCompletionClient
        {
            public readonly List<JObject> Bodies = new();
            public Task<List<string>> ListModelsAsync(CancellationToken token) =>
                Task.FromResult(new List<string> { "m" });
            public Task StreamChatAsync(JObject body, Action<JObject> onChunk, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Bodies.Add((JObject)body.DeepClone());
                JObject delta = Bodies.Count == 1 ? new JObject
                {
                    ["tool_calls"] = new JArray(new JObject
                    {
                        ["index"] = 0, ["id"] = "local-call",
                        ["function"] = new JObject
                        {
                            ["name"] = "delegate_to_agent",
                            ["arguments"] = new JObject
                            {
                                ["target_skill"] = "worker", ["task_summary"] = "solve",
                                ["context_mode"] = "last_messages",
                            }.ToString(Newtonsoft.Json.Formatting.None),
                        },
                    }),
                } : new JObject { ["content"] = "Waiting for child." };
                onChunk(new JObject { ["choices"] = new JArray(new JObject { ["delta"] = delta }) });
                return Task.CompletedTask;
            }
        }
    }
}
