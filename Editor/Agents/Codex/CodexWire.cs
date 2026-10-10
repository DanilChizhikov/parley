using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexWire
    {
        public static string Serialize(JObject message)
        {
            return message.ToString(Formatting.None);
        }

        public static JObject Request(long id, string method, JObject parameters)
        {
            return new JObject
            {
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters ?? new JObject(),
            };
        }

        public static JObject Notification(string method)
        {
            return new JObject { ["method"] = method };
        }

        public static JObject Response(JToken id, JToken result)
        {
            return new JObject
            {
                ["id"] = id?.DeepClone(),
                ["result"] = result ?? new JObject(),
            };
        }

        public static JObject Error(JToken id, int code, string message)
        {
            return new JObject
            {
                ["id"] = id?.DeepClone(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
            };
        }

        public static string RequestKey(JToken id)
        {
            return id == null ? null : id.Type == JTokenType.String ? (string)id : id.ToString(Formatting.None);
        }

        public static JArray TextInput(string text)
        {
            return new JArray { new JObject { ["type"] = "text", ["text"] = text ?? string.Empty } };
        }

        public static JArray UserInput(UserTurn turn, IReadOnlyList<SkillInfo> skills)
        {
            JArray input = UserInput(turn);
            for (int i = skills.Count - 1; i >= 0; i--)
            {
                input.Insert(0, new JObject { ["type"] = "skill", ["name"] = skills[i].Name, ["path"] = skills[i].Path });
            }

            return input;
        }

        public static JArray UserInput(UserTurn turn)
        {
            StringBuilder text = new StringBuilder(turn.Text ?? string.Empty);
            foreach (ChatAttachment attachment in turn.Attachments)
            {
                if (attachment.Kind == AttachmentKind.Text)
                {
                    text.Append("\n\n<attachment name=\"").Append(attachment.Label).Append("\">\n").Append(attachment.Text).Append("\n</attachment>");
                }
            }

            JArray input = text.Length > 0 ? TextInput(text.ToString()) : new JArray();
            foreach (ChatAttachment attachment in turn.Attachments)
            {
                if (attachment.Kind == AttachmentKind.Image)
                {
                    string url = "data:" + (attachment.MediaType ?? "image/png") + ";base64," + attachment.Base64;
                    input.Add(new JObject { ["type"] = "image", ["url"] = url });
                }
            }

            return input;
        }
    }
}