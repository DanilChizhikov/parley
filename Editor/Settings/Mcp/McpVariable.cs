using System;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class McpVariable
    {
        private const string SecretFieldPrefix = "var-";

        [field: SerializeField]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [field: SerializeField]
        public string Key { get; set; } = string.Empty;

        [field: SerializeField]
        public string Value { get; set; } = string.Empty;

        [field: SerializeField]
        public bool IsSecret { get; set; }

        public string SecretField => SecretFieldPrefix + Id;

        public McpVariable Clone()
        {
            return new McpVariable { Id = Id, Key = Key, Value = Value, IsSecret = IsSecret };
        }
    }
}
