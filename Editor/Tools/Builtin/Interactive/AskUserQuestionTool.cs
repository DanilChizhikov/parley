using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class AskUserQuestionTool : IParleyTool
    {
        public string Name => PendingRequest.AskUserQuestionTool;

        public string Description =>
            "Ask the user 1-4 multiple-choice questions when a decision is genuinely theirs (requirements, trade-offs). Each question has 2-4 options; the user can also type their own answer.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .Add("questions", new JObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = 4,
                ["items"] = new SchemaBuilder()
                    .String("question", "The full question, ending with a question mark.", true)
                    .String("header", "Very short label (max 12 characters).", true)
                    .Boolean("multiSelect", "Allow several options.", true)
                    .Add("options", new JObject
                    {
                        ["type"] = "array",
                        ["minItems"] = 2,
                        ["maxItems"] = 4,
                        ["items"] = new SchemaBuilder()
                            .String("label", "Option text (1-5 words).", true)
                            .String("description", "What choosing this option means.", true)
                            .Build(),
                    }, true)
                    .Build(),
            }, true)
            .Build();

        public ToolKind Kind => ToolKind.Interactive;

        public static string FormatAnswers(JObject answersInput)
        {
            string response = ToolInput.String(answersInput, "response");
            if (!string.IsNullOrWhiteSpace(response))
            {
                return "The user responded: " + response;
            }

            StringBuilder builder = new StringBuilder("User has answered your questions: ");
            bool first = true;
            if (answersInput?["answers"] is JObject answers)
            {
                foreach (JProperty answer in answers.Properties())
                {
                    string value = answer.Value is JArray array ? string.Join(", ", array.ToObject<string[]>()) : answer.Value.ToString();
                    builder.Append(first ? string.Empty : ", ").Append('"').Append(answer.Name).Append("\"=\"").Append(value).Append('"');
                    first = false;
                }
            }

            builder.Append(". You can now continue with the user's answers in mind.");
            return builder.ToString();
        }

        public string Summarize(JObject input) => "Ask the user";

        public string TargetPath(JObject input) => null;

        public async Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            if (!(input["questions"] is JArray questions) || questions.Count == 0)
            {
                return ToolResult.Error("questions must be a non-empty array.");
            }

            Decision decision = await Ask(context, input, cancellationToken);
            if (!decision.Allow)
            {
                return ToolResult.Error("The user declined to answer." + (string.IsNullOrEmpty(decision.Message) ? string.Empty : " " + decision.Message));
            }

            return ToolResult.Ok(FormatAnswers(decision.UpdatedInput), decision.UpdatedInput);
        }

        internal static Task<Decision> Ask(ToolContext context, JObject input, CancellationToken cancellationToken)
        {
            PendingRequest request = new PendingRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ToolName = PendingRequest.AskUserQuestionTool,
                Input = input,
                ToolUseId = context.ToolUseId,
            };

            return context.AskUser(request, cancellationToken);
        }
    }
}