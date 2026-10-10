using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Claude
{
    internal sealed class AuthEnvironment
    {
        public Dictionary<string, string> Variables { get; } = new (StringComparer.Ordinal);
        public List<string> Removed { get; } = new ();
        public List<string> Problems { get; } = new ();
        public JObject Settings { get; } = new ();
        public bool IsValid => Problems.Count == 0;

        public void Require(string variable, string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Problems.Add(label + " is not set.");
                return;
            }

            Variables[variable] = value.Trim();
        }

        public void Optional(string variable, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Variables[variable] = value.Trim();
            }
        }
    }
}