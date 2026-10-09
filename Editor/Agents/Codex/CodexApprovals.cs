using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Codex
{
    internal static class CodexApprovals
    {
        public const string CommandApproval = "item/commandExecution/requestApproval";
        public const string FileChangeApproval = "item/fileChange/requestApproval";
        public const string PermissionsApproval = "item/permissions/requestApproval";
        public const string UserInputRequest = "item/tool/requestUserInput";
        public const string BashTool = "Bash";
        public const string ApplyPatchTool = "ApplyPatch";
        public const string PermissionsTool = "RequestPermissions";

        public static bool IsSupported(string method)
        {
            return method == CommandApproval || method == FileChangeApproval || method == PermissionsApproval || method == UserInputRequest;
        }

        public static PendingRequest Parse(CodexServerRequest server, JObject item)
        {
            JObject parameters = server.Params;
            PendingRequest request = new PendingRequest
            {
                Id = CodexWire.RequestKey(server.RpcId),
                ToolUseId = (string)parameters["itemId"],
                DecisionReason = (string)parameters["reason"],
            };

            switch (server.Method)
            {
                case CommandApproval:
                    request.ToolName = BashTool;
                    request.Input = new JObject
                    {
                        ["command"] = (string)parameters["command"] ?? (string)item?["command"],
                        ["cwd"] = (string)parameters["cwd"],
                    };
                    break;
                case FileChangeApproval:
                    request.ToolName = ApplyPatchTool;
                    request.Input = new JObject { ["changes"] = item?["changes"]?.DeepClone() ?? new JArray() };
                    request.BlockedPath = (string)parameters["grantRoot"];
                    break;
                case PermissionsApproval:
                    request.ToolName = PermissionsTool;
                    request.Input = new JObject
                    {
                        ["permissions"] = parameters["permissions"]?.DeepClone() ?? new JObject(),
                        ["cwd"] = (string)parameters["cwd"],
                    };
                    break;
                default:
                    request.ToolName = PendingRequest.AskUserQuestionTool;
                    request.Input = new JObject { ["questions"] = Questions(parameters["questions"] as JArray) };
                    break;
            }

            return request;
        }

        public static JObject Result(CodexServerRequest server, Decision decision)
        {
            switch (server.Method)
            {
                case CommandApproval:
                    return new JObject { ["decision"] = CommandDecision(server.Params, decision) };
                case FileChangeApproval:
                    return new JObject { ["decision"] = Decide(decision) };
                case PermissionsApproval:
                    return PermissionsResult(server.Params, decision);
                default:
                    return new JObject { ["answers"] = Answers(server.Params["questions"] as JArray, decision) };
            }
        }

        private static string Decide(Decision decision)
        {
            if (decision.Allow)
            {
                return decision.Remember ? "acceptForSession" : "accept";
            }

            return decision.Interrupt ? "cancel" : "decline";
        }

        private static JToken CommandDecision(JObject parameters, Decision decision)
        {
            if (decision.Allow && decision.Remember && parameters["proposedExecpolicyAmendment"] is JArray amendment && amendment.Count > 0)
            {
                return new JObject
                {
                    ["acceptWithExecpolicyAmendment"] = new JObject { ["execpolicy_amendment"] = amendment.DeepClone() },
                };
            }

            return Decide(decision);
        }

        private static JObject PermissionsResult(JObject parameters, Decision decision)
        {
            if (!decision.Allow)
            {
                return new JObject { ["permissions"] = new JObject() };
            }

            return new JObject
            {
                ["permissions"] = parameters["permissions"]?.DeepClone() ?? new JObject(),
                ["scope"] = decision.Remember ? "session" : "turn",
            };
        }

        private static JArray Questions(JArray questions)
        {
            JArray result = new JArray();
            if (questions == null)
            {
                return result;
            }

            foreach (JToken question in questions)
            {
                JArray options = new JArray();
                if (question["options"] is JArray sourceOptions)
                {
                    foreach (JToken option in sourceOptions)
                    {
                        options.Add(new JObject { ["label"] = (string)option["label"], ["description"] = (string)option["description"] });
                    }
                }

                result.Add(new JObject
                {
                    ["id"] = (string)question["id"],
                    ["question"] = (string)question["question"],
                    ["header"] = (string)question["header"],
                    ["multiSelect"] = false,
                    ["options"] = options,
                });
            }

            return result;
        }

        private static JObject Answers(JArray questions, Decision decision)
        {
            JObject result = new JObject();
            if (questions == null || !decision.Allow)
            {
                return result;
            }

            JObject answers = decision.UpdatedInput?["answers"] as JObject;
            string response = (string)decision.UpdatedInput?["response"];
            foreach (JToken question in questions)
            {
                string id = (string)question["id"];
                JToken value = answers?[(string)question["question"] ?? string.Empty] ?? answers?[id ?? string.Empty];
                JArray values = new JArray();
                if (value is JArray array)
                {
                    foreach (JToken entry in array)
                    {
                        values.Add(entry.ToString());
                    }
                }
                else if (value != null && value.Type != JTokenType.Null)
                {
                    values.Add(value.ToString());
                }
                else if (!string.IsNullOrWhiteSpace(response))
                {
                    values.Add(response);
                }

                result[id ?? string.Empty] = new JObject { ["answers"] = values };
            }

            return result;
        }
    }
}