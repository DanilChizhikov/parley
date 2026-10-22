using System;
using System.Collections.Generic;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class ChatPreset
    {
        [field: SerializeField]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [field: SerializeField]
        public string Name { get; set; } = string.Empty;

        [field: SerializeField]
        public List<string> EnabledMcpServerIds { get; set; } = new ();

        [field: SerializeField]
        public List<string> DisabledExternalMcpServers { get; set; } = new ();

        [field: SerializeField]
        public List<string> EnabledLibrarySkills { get; set; } = new ();

        [field: SerializeField]
        public List<string> DisabledExternalSkills { get; set; } = new ();

        [field: SerializeField]
        public List<AutoSkill> AutoSkills { get; set; } = new ();
    }
}
