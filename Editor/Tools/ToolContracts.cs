using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools
{
	internal enum ToolKind
	{
		ReadOnly = 0,
		FileEdit = 1,
		Execute = 2,
		Network = 3,
		Interactive = 4,
	}

	internal sealed class ToolResult
	{
		public string Text = string.Empty;
		public bool IsError;
		public JToken Structured;

		public static ToolResult Ok(string text, JToken structured = null)
		{
			return new ToolResult { Text = text ?? string.Empty, Structured = structured };
		}

		public static ToolResult Error(string text)
		{
			return new ToolResult { Text = text ?? string.Empty, IsError = true };
		}
	}

	internal sealed class ToolContext
	{
		public readonly HashSet<string> ReadFiles = new (StringComparer.OrdinalIgnoreCase);

		public string ProjectRoot = ProjectPaths.Root;
		public IReadOnlyList<string> AdditionalDirectories = Array.Empty<string>();
		public Func<PermissionMode> GetMode = () => PermissionMode.Default;
		public Action<PermissionMode> SetMode = _ => { };
		public Func<PendingRequest, CancellationToken, Task<Decision>> AskUser;
		public string ToolUseId;
	}

	internal interface IParleyTool
	{
		string Name { get; }
		string Description { get; }
		JObject InputSchema { get; }
		ToolKind Kind { get; }

		string Summarize(JObject input);
		string TargetPath(JObject input);
		Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken);
	}

	internal sealed class SchemaBuilder
	{
		private readonly JObject _properties = new ();
		private readonly JArray _required = new ();

		public SchemaBuilder String(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "string", ["description"] = description }, required);
		}

		public SchemaBuilder Integer(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "integer", ["description"] = description }, required);
		}

		public SchemaBuilder Boolean(string name, string description, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "boolean", ["description"] = description }, required);
		}

		public SchemaBuilder Enum(string name, string description, string[] values, bool required = false)
		{
			return Add(name, new JObject { ["type"] = "string", ["description"] = description, ["enum"] = new JArray(values) }, required);
		}

		public SchemaBuilder Add(string name, JObject schema, bool required = false)
		{
			_properties[name] = schema;
			if (required)
			{
				_required.Add(name);
			}

			return this;
		}

		public JObject Build()
		{
			JObject schema = new JObject
			{
				["type"] = "object",
				["properties"] = _properties,
			};

			if (_required.Count > 0)
			{
				schema["required"] = _required;
			}

			return schema;
		}
	}

	internal static class ToolInput
	{
		public static string String(JObject input, string name, string fallback = null)
		{
			JToken token = input?[name];
			return token == null || token.Type == JTokenType.Null ? fallback : token.Type == JTokenType.String ? (string)token : token.ToString();
		}

		public static int Int(JObject input, string name, int fallback)
		{
			JToken token = input?[name];
			if (token == null)
			{
				return fallback;
			}

			if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
			{
				return (int)token;
			}

			return int.TryParse(token.ToString(), out int value) ? value : fallback;
		}

		public static bool Bool(JObject input, string name, bool fallback)
		{
			JToken token = input?[name];
			if (token == null)
			{
				return fallback;
			}

			if (token.Type == JTokenType.Boolean)
			{
				return (bool)token;
			}

			return bool.TryParse(token.ToString(), out bool value) ? value : fallback;
		}
	}
}
