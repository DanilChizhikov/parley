using System;
using UnityEngine;

namespace DTech.Parley.Editor
{
    [Serializable]
    internal sealed class AllowRule
    {
        [field: SerializeField]
        public string ProjectRoot { get; private set; }
        
        [field: SerializeField]
        public string Tool { get; private set; }
        
        [field: SerializeField]
        public string Pattern { get; private set; }

        public AllowRule(string projectRoot, string tool, string pattern)
        {
            ProjectRoot = projectRoot;
            Tool = tool;
            Pattern = pattern;
        }
    }
}