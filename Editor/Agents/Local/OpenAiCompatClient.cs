using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DTech.Parley.Editor.Agents.Local
{
	internal sealed class OpenAiCompatClient : IChatCompletionClient
	{
		private readonly string _baseUrl;
		private readonly string _apiKey;

		public OpenAiCompatClient(string baseUrl, string apiKey)
		{
			_baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
			_apiKey = apiKey;
		}

		public async Task<List<string>> ListModelsAsync(CancellationToken cancellationToken)
		{
			using UnityWebRequest request = UnityWebRequest.Get(_baseUrl + "/models");
			request.timeout = 10;
			ApplyHeaders(request);
			await WebRequests.SendAsync(request, cancellationToken);
			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new InvalidOperationException("GET " + _baseUrl + "/models failed: " + request.error + Describe(request.downloadHandler?.text));
			}

			List<string> models = new ();
			JObject json = JObject.Parse(request.downloadHandler.text);
			if (json["data"] is JArray data)
			{
				foreach (JToken model in data)
				{
					string id = (string)model["id"];
					if (!string.IsNullOrEmpty(id))
					{
						models.Add(id);
					}
				}
			}

			return models;
		}

		public async Task StreamChatAsync(JObject body, Action<JObject> onChunk, CancellationToken cancellationToken)
		{
			SseParser parser = new SseParser();
			parser.OnData += data => HandleData(data, onChunk);
			byte[] payload = new UTF8Encoding(false).GetBytes(body.ToString(Formatting.None));
			using UnityWebRequest request = new UnityWebRequest(_baseUrl + "/chat/completions", UnityWebRequest.kHttpVerbPOST)
			{
				uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" },
				downloadHandler = new StreamingDownloadHandler(parser.Feed),
				timeout = 0,
			};

			request.SetRequestHeader("Content-Type", "application/json");
			request.SetRequestHeader("Accept", "text/event-stream");
			ApplyHeaders(request);
			await WebRequests.SendAsync(request, cancellationToken);
			parser.Flush();
			cancellationToken.ThrowIfCancellationRequested();
			if (request.result != UnityWebRequest.Result.Success || request.responseCode >= 400)
			{
				throw new InvalidOperationException("Chat request failed (HTTP " + request.responseCode + "): " + request.error + Describe(parser.RawPrefix));
			}
		}

		private static void HandleData(string data, Action<JObject> onChunk)
		{
			JObject chunk;
			try
			{
				chunk = JObject.Parse(data);
			}
			catch (JsonException)
			{
				return;
			}

			try
			{
				onChunk(chunk);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Parley] Failed to handle a stream chunk: " + exception.Message);
			}
		}

		private static string Describe(string body)
		{
			if (string.IsNullOrWhiteSpace(body))
			{
				return string.Empty;
			}

			string message = ReadErrorMessage(body);
			if (!string.IsNullOrEmpty(message))
			{
				return " — " + message;
			}

			return " — " + (body.Length > 400 ? body.Substring(0, 400) + "…" : body);
		}

		private static string ReadErrorMessage(string body)
		{
			try
			{
				JObject json = JObject.Parse(body);
				return (string)json["error"]?["message"] ?? (string)json["error"] ?? (string)json["message"];
			}
			catch (Exception)
			{
				return null;
			}
		}

		private void ApplyHeaders(UnityWebRequest request)
		{
			if (!string.IsNullOrEmpty(_apiKey))
			{
				request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
			}
		}

		private sealed class StreamingDownloadHandler : DownloadHandlerScript
		{
			private readonly Action<string> _onText;
			private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();

			private char[] _chars = new char[16384];

			public StreamingDownloadHandler(Action<string> onText) : base(new byte[16384])
			{
				_onText = onText;
			}

			protected override bool ReceiveData(byte[] data, int dataLength)
			{
				if (data == null || dataLength <= 0)
				{
					return true;
				}

				if (dataLength + 4 > _chars.Length)
				{
					_chars = new char[dataLength + 4];
				}

				int count = _decoder.GetChars(data, 0, dataLength, _chars, 0);
				if (count > 0)
				{
					_onText(new string(_chars, 0, count));
				}

				return true;
			}
		}
	}
}