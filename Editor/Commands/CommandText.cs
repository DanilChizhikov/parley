using System;
using System.Collections.Generic;
using UnityEngine;

namespace DTech.Parley.Editor.Commands
{
    internal static class CommandText
    {
        private static readonly Dictionary<string, string> _cache = new (StringComparer.Ordinal);

        public static string Load(string resource)
        {
            if (string.IsNullOrEmpty(resource))
            {
                return null;
            }

            if (_cache.TryGetValue(resource, out string cached))
            {
                return cached;
            }

            TextAsset asset = Resources.Load<TextAsset>(resource);
            string text = asset != null ? asset.text : null;
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[Parley] Command text not found: " + resource);
                text = null;
            }

            _cache[resource] = text;
            return text;
        }
    }
}
