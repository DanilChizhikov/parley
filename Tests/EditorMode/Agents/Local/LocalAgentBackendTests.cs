using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Tools;
using DTech.Parley.Editor.Tools.Builtin;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DTech.Parley.Tests.EditorMode
{
	public sealed class LocalAgentBackendTests
	{
		private string _file;

		[SetUp]
		public void SetUp()
		{
			_file = Path.Combine(Path.GetTempPath(), "parley-test-" + Guid.NewGuid().ToString("N") + ".txt");
			File.WriteAllText(_file, "alpha\nbeta\n");
		}

		[TearDown]
		public void TearDown()
		{
			if (File.Exists(_file))
			{
				File.Delete(_file);
			}
		}

		[Test]
		public void RunsToolCallsAndFinishesWithText()
		{
			ScriptedClient client = new ScriptedClient();
			client.Rounds.Enqueue(new[]
			{
				Chunk("{\"choices\":[{\"delta\":{\"reasoning_content\":\"look at file\"}}]}"),
				Chunk("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c1\",\"function\":{\"name\":\"Read\",\"arguments\":\"{\\\"file_path\\\":\\\"" + Escape(_file) + "\\\"}\"}}]}}]}"),
			});
			client.Rounds.Enqueue(new[]
			{
				Chunk("{\"choices\":[{\"delta\":{\"content\":\"The file has two lines.\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}"),
			});

			RecordingSink sink = new RecordingSink();
			LocalAgentBackend backend = CreateBackend(sink, client, PermissionMode.BypassPermissions);
			Task send = backend.SendAsync(new UserTurn { Text = "read it" }, CancellationToken.None);

			Assert.IsTrue(send.IsCompleted);
			Assert.AreEqual(1, sink.Turns.Count, string.Join("\n", sink.Notices));
			Assert.AreEqual(1, sink.ToolResults.Count);
			StringAssert.Contains("alpha", sink.ToolResults[0].text);
			Assert.IsFalse(sink.ToolResults[0].error);
			Assert.AreEqual(2, client.Bodies.Count);
			JArray secondMessages = (JArray)client.Bodies[1]["messages"];
			Assert.AreEqual("tool", (string)secondMessages[secondMessages.Count - 1]["role"]);
			Assert.AreEqual("c1", (string)secondMessages[secondMessages.Count - 1]["tool_call_id"]);
			Assert.IsNotNull(client.Bodies[0]["tools"]);
			Assert.AreEqual(5, sink.Turns[0].OutputTokens);
		}

		[UnityTest]
		public IEnumerator AskModeRaisesRequestAndDeniedToolReportsError()
		{
			ScriptedClient client = new ScriptedClient();
			client.Rounds.Enqueue(new[]
			{
				Chunk("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c2\",\"function\":{\"name\":\"Bash\",\"arguments\":\"{\\\"command\\\":\\\"make build\\\"}\"}}]}}]}"),
			});
			client.Rounds.Enqueue(new[] { Chunk("{\"choices\":[{\"delta\":{\"content\":\"OK, I won't.\"}}]}") });

			RecordingSink sink = new RecordingSink();
			LocalAgentBackend backend = CreateBackend(sink, client, PermissionMode.Default);
			sink.OnRequest = request => backend.Respond(request, Decision.Deny("not now"));
			backend.SendAsync(new UserTurn { Text = "build" }, CancellationToken.None);

			for (int frame = 0; frame < 300 && sink.Turns.Count == 0; frame++)
			{
				yield return null;
			}

			Assert.AreEqual(1, sink.Requests.Count);
			Assert.AreEqual("Bash", sink.Requests[0].ToolName);
			Assert.AreEqual(1, sink.Turns.Count);
			Assert.IsTrue(sink.ToolResults[0].error);
			StringAssert.Contains("not now", sink.ToolResults[0].text);
		}

		[Test]
		public void PlanModeBlocksEditsWithoutAsking()
		{
			ScriptedClient client = new ScriptedClient();
			client.Rounds.Enqueue(new[]
			{
				Chunk("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c3\",\"function\":{\"name\":\"Write\",\"arguments\":\"{\\\"file_path\\\":\\\"x.txt\\\",\\\"content\\\":\\\"y\\\"}\"}}]}}]}"),
			});
			client.Rounds.Enqueue(new[] { Chunk("{\"choices\":[{\"delta\":{\"content\":\"Plan instead.\"}}]}") });

			RecordingSink sink = new RecordingSink();
			LocalAgentBackend backend = CreateBackend(sink, client, PermissionMode.Plan);
			backend.SendAsync(new UserTurn { Text = "write" }, CancellationToken.None);

			Assert.AreEqual(0, sink.Requests.Count);
			Assert.IsTrue(sink.ToolResults[0].error);
			StringAssert.Contains("Plan mode", sink.ToolResults[0].text);
			StringAssert.Contains("Plan mode is active", (string)client.Bodies[0]["messages"][0]["content"]);
		}

		[Test]
		public void TextToolCallFallback()
		{
			ScriptedClient client = new ScriptedClient();
			client.Rounds.Enqueue(new[]
			{
				Chunk("{\"choices\":[{\"delta\":{\"content\":\"<tool_call>{\\\"name\\\":\\\"Read\\\",\\\"arguments\\\":{\\\"file_path\\\":\\\"" + Escape(_file) + "\\\"}}</tool_call>\"}}]}"),
			});
			client.Rounds.Enqueue(new[] { Chunk("{\"choices\":[{\"delta\":{\"content\":\"done\"}}]}") });

			RecordingSink sink = new RecordingSink();
			ParleyProfile profile = ParleyProfile.CreateLocal("t", LocalPreset.Custom);
			profile.TextToolCalls = true;
			profile.Model = "m";
			LocalAgentBackend backend = new LocalAgentBackend(profile, sink, Catalog(), null, null, PermissionMode.BypassPermissions, (_, _) => client);
			backend.SendAsync(new UserTurn { Text = "go" }, CancellationToken.None);

			Assert.AreEqual(1, sink.ToolResults.Count);
			Assert.IsNull(client.Bodies[0]["tools"]);
			JArray second = (JArray)client.Bodies[1]["messages"];
			StringAssert.Contains("<tool_response name=\"Read\">", (string)second[second.Count - 1]["content"]);
			StringAssert.Contains("<tool_call>", (string)second[second.Count - 2]["content"]);
		}

		private static LocalAgentBackend CreateBackend(RecordingSink sink, ScriptedClient client, PermissionMode mode)
		{
			ParleyProfile profile = ParleyProfile.CreateLocal("t", LocalPreset.Custom);
			profile.Model = "m";
			return new LocalAgentBackend(profile, sink, Catalog(), null, null, mode, (_, _) => client);
		}

		private static ToolCatalog Catalog()
		{
			return new ToolCatalog(new IParleyTool[] { new FileReadTool(), new FileWriteTool(), new BashTool(), new AskUserQuestionTool(), new ExitPlanModeTool() });
		}

		private static JObject Chunk(string json)
		{
			return JObject.Parse(json);
		}

		private static string Escape(string path)
		{
			return path.Replace("\\", "\\\\\\\\");
		}

		private sealed class ScriptedClient : IChatCompletionClient
		{
			public Queue<JObject[]> Rounds { get; } = new ();
			public List<JObject> Bodies { get; } = new ();

			public Task<List<string>> ListModelsAsync(CancellationToken cancellationToken)
			{
				return Task.FromResult(new List<string> { "m" });
			}

			public Task StreamChatAsync(JObject body, Action<JObject> onChunk, CancellationToken cancellationToken)
			{
				Bodies.Add((JObject)body.DeepClone());
				if (Rounds.Count == 0)
				{
					return Task.CompletedTask;
				}

				foreach (JObject chunk in Rounds.Dequeue())
				{
					onChunk(chunk);
				}

				return Task.CompletedTask;
			}
		}
	}
}