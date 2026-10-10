using System;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class AutoSkill
    {
        [field: SerializeField]
        public string Name { get; set; } = string.Empty;

        [field: SerializeField]
        public string Arguments { get; set; } = string.Empty;
    }
}
