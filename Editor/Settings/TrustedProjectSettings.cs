using System;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class TrustedProjectSettings
    {
        [field: SerializeField]
        public string ProjectRoot { get; private set; }

        [field: SerializeField]
        public string Fingerprint { get; private set; }

        public TrustedProjectSettings(string projectRoot, string fingerprint)
        {
            ProjectRoot = projectRoot;
            Fingerprint = fingerprint;
        }
    }
}
