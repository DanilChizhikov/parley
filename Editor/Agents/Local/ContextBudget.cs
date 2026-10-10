using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
	internal sealed class ContextBudget
	{
		public const string ElidedText = "[older tool output removed to save context]";
		public const double DefaultCharsPerToken = 3.5;

		private const double MinCharsPerToken = 1.5;
		private const double MaxCharsPerToken = 6.0;
		private const int ImageChars = 2800;
		private const int MessageOverheadChars = 8;
		private const int KeepRecentMessages = 6;

		public double CharsPerToken { get; private set; } = DefaultCharsPerToken;

		public static int EstimateChars(JObject message)
		{
			int chars = MessageOverheadChars;
			JToken content = message["content"];
			if (content != null && content.Type == JTokenType.String)
			{
				chars += ((string)content).Length;
			}
			else if (content is JArray parts)
			{
				foreach (JToken part in parts)
				{
					if ((string)part["type"] == "image_url")
					{
						chars += ImageChars;
					}
					else
					{
						chars += ((string)part["text"] ?? string.Empty).Length;
					}
				}
			}

			if (message["tool_calls"] is JArray calls)
			{
				chars += calls.ToString().Length;
			}

			return chars;
		}

		public void Calibrate(long promptChars, long promptTokens)
		{
			if (promptChars <= 0 || promptTokens <= 0)
			{
				return;
			}

			CharsPerToken = Math.Max(MinCharsPerToken, Math.Min(MaxCharsPerToken, (double)promptChars / promptTokens));
		}

		public int Tokens(int chars)
		{
			return (int)(chars / CharsPerToken);
		}

		public int Estimate(JObject message)
		{
			return Tokens(EstimateChars(message));
		}

		public int Estimate(IEnumerable<JObject> messages)
		{
			int total = 0;
			foreach (JObject message in messages)
			{
				total += Estimate(message);
			}

			return total;
		}

		public List<JObject> Fit(IReadOnlyList<JObject> history, int budget, out int estimate)
		{
			List<JObject> messages = new (history);
			estimate = Estimate(messages);
			if (estimate <= budget)
			{
				return messages;
			}

			for (int i = 0; i < messages.Count - KeepRecentMessages && estimate > budget; i++)
			{
				JObject message = messages[i];
				if ((string)message["role"] != "tool" || (string)message["content"] == ElidedText)
				{
					continue;
				}

				int before = Estimate(message);
				JObject elided = (JObject)message.DeepClone();
				elided["content"] = ElidedText;
				messages[i] = elided;
				estimate += Estimate(elided) - before;
			}

			int firstUser = messages.FindIndex(message => (string)message["role"] == "user");
			int start = firstUser + 1;
			while (estimate > budget && start < messages.Count - KeepRecentMessages)
			{
				int end = start + 1;
				while (end < messages.Count && (string)messages[end]["role"] == "tool")
				{
					end++;
				}

				for (int i = start; i < end; i++)
				{
					estimate -= Estimate(messages[i]);
				}

				messages.RemoveRange(start, end - start);
			}

			return messages;
		}
	}
}
