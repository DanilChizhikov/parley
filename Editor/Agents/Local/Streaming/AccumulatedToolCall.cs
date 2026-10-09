using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal sealed class AccumulatedToolCall
    {
        public StringBuilder Arguments { get; } = new ();
        public int Index { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public JObject ParseArguments()
        {
            string arguments = Arguments.ToString();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return new JObject();
            }

            try
            {
                return JToken.Parse(arguments) as JObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}