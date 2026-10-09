using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace DTech.Parley.Editor.Tools.Builtin
{
	internal sealed class WebFetchTool : IParleyTool
	{
		private const int MaxChars = 40000;

		private static readonly Regex ScriptOrStyle = new ("<(script|style|noscript)[^>]*>.*?</\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
		private static readonly Regex BlockTag = new ("<(br|p|div|li|tr|h[1-6]|section|article|pre)[^>]*>", RegexOptions.IgnoreCase);
		private static readonly Regex AnyTag = new ("<[^>]+>");
		private static readonly Regex Blank = new ("\\n\\s*\\n\\s*\\n+");

		public string Name => "WebFetch";

		public string Description => "Fetch a URL over HTTP(S) and return its text content (HTML converted to plain text).";

		public JObject InputSchema { get; } = new SchemaBuilder()
			.String("url", "Absolute http(s) URL.", true)
			.Build();

		public ToolKind Kind => ToolKind.Network;

		public static string HtmlToText(string html)
		{
			string text = ScriptOrStyle.Replace(html, string.Empty);
			text = BlockTag.Replace(text, "\n");
			text = AnyTag.Replace(text, string.Empty);
			text = WebUtility.HtmlDecode(text);
			return Blank.Replace(text.Replace("\r", string.Empty), "\n\n").Trim();
		}

		public string Summarize(JObject input) => "Fetch " + ToolInput.String(input, "url");

		public string TargetPath(JObject input) => null;

		public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
		{
			string url = ToolInput.String(input, "url", string.Empty).Trim();
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
			{
				return ToolResult.Error("url must be an absolute http(s) URL.");
			}

			using UnityWebRequest request = UnityWebRequest.Get(uri);
			request.timeout = 30;
			request.SetRequestHeader("User-Agent", "Parley/0.1 (Unity Editor)");
			await WebRequests.SendAsync(request, cancellationToken);
			if (request.result != UnityWebRequest.Result.Success)
			{
				return ToolResult.Error("Request failed: " + request.error + " (HTTP " + request.responseCode + ")");
			}

			string body = request.downloadHandler.text ?? string.Empty;
			string contentType = request.GetResponseHeader("Content-Type") ?? string.Empty;
			string text = contentType.Contains("html") ? HtmlToText(body) : body;
			if (text.Length > MaxChars)
			{
				text = text.Substring(0, MaxChars) + "\n... [truncated]";
			}

			return ToolResult.Ok("HTTP " + request.responseCode + " " + uri + "\n\n" + text);
		}
	}
}