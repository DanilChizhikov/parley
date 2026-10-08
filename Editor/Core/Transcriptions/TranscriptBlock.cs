using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal sealed class TranscriptBlock
    {
        [JsonProperty("id")] public string Id = Guid.NewGuid().ToString("N");
        [JsonProperty("kind")] public BlockKind Kind;
        [JsonProperty("text")] public string Text = string.Empty;
        [JsonProperty("parent")] public string ParentToolUseId;
        [JsonProperty("toolUseId")] public string ToolUseId;
        [JsonProperty("toolName")] public string ToolName;
        [JsonProperty("input")] public JObject Input;
        [JsonProperty("inputJson")] public string PartialInputJson;
        [JsonProperty("result")] public string Result;
        [JsonProperty("structured")] public JToken StructuredResult;
        [JsonProperty("isError")] public bool IsError;
        [JsonProperty("finished")] public bool IsFinished;
        [JsonProperty("mediaType")] public string MediaType;
        [JsonProperty("data")] public string Data;
        [JsonProperty("level")] public NoticeLevel Level;
        [JsonProperty("requestId")] public string RequestId;
        [JsonProperty("resolution")] public string Resolution;
    }
}