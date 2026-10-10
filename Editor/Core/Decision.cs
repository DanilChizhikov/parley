using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor
{
    internal sealed class Decision
    {
        public bool Allow { get; set; }
        public JObject UpdatedInput { get; set; }
        public JArray UpdatedPermissions { get; set; }
        public string Message { get; set; }
        public bool Interrupt { get; set; }
        public bool Remember { get; set; }

        public PermissionMode? NextMode { get; set; }

        public static Decision AllowWith(JObject updatedInput)
        {
            return new Decision { Allow = true, UpdatedInput = updatedInput };
        }

        public static Decision Deny(string message, bool interrupt = false)
        {
            return new Decision { Allow = false, Message = message, Interrupt = interrupt };
        }
    }
}