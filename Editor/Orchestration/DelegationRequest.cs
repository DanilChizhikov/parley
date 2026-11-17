using System;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Orchestration
{
    internal sealed class DelegationRequest
    {
        public string RequestId { get; }
        public string TargetSkill { get; }
        public string TaskSummary { get; }
        public string ContextMode { get; }

        public DelegationRequest(string requestId, string targetSkill, string taskSummary, string contextMode)
        {
            RequestId = requestId;
            TargetSkill = targetSkill;
            TaskSummary = taskSummary;
            ContextMode = contextMode;
        }

        public static DelegationRequest Parse(JObject input, string requestId)
        {
            string Read(string key)
            {
                JToken value = input?[key];
                if (value?.Type != JTokenType.String)
                    throw new ArgumentException(key + " must be a string.");
                return ((string)value).Trim();
            }

            string target = Read("target_skill");
            string task = Read("task_summary");
            string mode = Read("context_mode");
            if (target.Length == 0 || target.Length > 128 || task.Length == 0 || task.Length > 32768)
                throw new ArgumentException("A target skill and a task summary (up to 32768 characters) are required.");
            if (mode != "summary" && mode != "last_messages")
                throw new ArgumentException("context_mode must be summary or last_messages.");
            string id = input?["request_id"]?.Type == JTokenType.String ? (string)input["request_id"] : requestId;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256)
                throw new ArgumentException("A stable request ID is required.");
            return new DelegationRequest(id, target, task, mode);
        }
    }
}
