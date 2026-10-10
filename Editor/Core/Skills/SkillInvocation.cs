using Newtonsoft.Json;

namespace DTech.Parley.Editor
{
    internal sealed class SkillInvocation
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("args")]
        public string Arguments { get; set; } = string.Empty;

        public string ToCommand()
        {
            string arguments = (Arguments ?? string.Empty).Trim();
            return arguments.Length == 0 ? "/" + Name : "/" + Name + " " + arguments;
        }
    }
}
