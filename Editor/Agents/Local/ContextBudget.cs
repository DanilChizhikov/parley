using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
	internal static class ContextBudget
	{
		public const string ElidedText = "[older tool output removed to save context]";

		private const double CharsPerToken = 3.5;
		private const int ImageTokens = 800;
		private const int KeepRecentMessages = 6;

		public static int Estimate(JObject message)
		{
			int chars = 8;
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
						chars += (int)(ImageTokens * CharsPerToken);
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

			return (int)(chars / CharsPerToken);
		}

		public static int Estimate(IEnumerable<JObject> messages)
		{
			int total = 0;
			foreach (JObject message in messages)
			{
				total += Estimate(message);
			}

			return total;
		}

		public static List<JObject> Fit(IReadOnlyList<JObject> history, int budget, out int estimate)
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