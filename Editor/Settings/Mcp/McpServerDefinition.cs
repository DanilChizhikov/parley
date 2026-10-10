using System;
using System.Collections.Generic;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class McpServerDefinition
    {
        private const string SecretOwnerPrefix = "mcp-";

        [field: SerializeField]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [field: SerializeField]
        public string Name { get; set; } = string.Empty;

        [field: SerializeField]
        public McpTransport Transport { get; set; }

        [field: SerializeField]
        public string Command { get; set; } = string.Empty;

        [field: SerializeField]
        public string Arguments { get; set; } = string.Empty;

        [field: SerializeField]
        public string Url { get; set; } = string.Empty;

        [field: SerializeField]
        public List<McpVariable> Environment { get; set; } = new ();

        [field: SerializeField]
        public List<McpVariable> Headers { get; set; } = new ();

        [field: SerializeField]
        public bool EnabledByDefault { get; set; }

        public string SecretOwner => SecretOwnerPrefix + Id;

        public McpServerDefinition Clone()
        {
            McpServerDefinition copy = new McpServerDefinition
            {
                Id = Id,
                Name = Name,
                Transport = Transport,
                Command = Command,
                Arguments = Arguments,
                Url = Url,
                EnabledByDefault = EnabledByDefault,
                Environment = new List<McpVariable>(),
                Headers = new List<McpVariable>(),
            };

            foreach (McpVariable variable in Environment)
            {
                copy.Environment.Add(variable.Clone());
            }

            foreach (McpVariable variable in Headers)
            {
                copy.Headers.Add(variable.Clone());
            }

            return copy;
        }

        public IEnumerable<McpVariable> ActiveVariables()
        {
            return Transport == McpTransport.Stdio ? Environment : Headers;
        }
    }
}
