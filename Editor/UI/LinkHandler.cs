using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.UI
{
    internal static class LinkHandler
    {
        public const string FileScheme = "file:";

        private static readonly HashSet<string> _openableExtensions = new (StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".shader", ".hlsl", ".cginc", ".compute", ".uss", ".uxml", ".tss", ".json", ".asmdef", ".asmref",
            ".txt", ".md", ".xml", ".yaml", ".yml", ".csv", ".log",
        };

        public static void Open(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return;
            }

            if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Application.OpenURL(link);
                return;
            }

            OpenFile(link.StartsWith(FileScheme, StringComparison.Ordinal) ? link.Substring(FileScheme.Length) : link);
        }

        private static void OpenFile(string reference)
        {
            string path = reference;
            int line = 1;
            int colon = reference.LastIndexOf(':');
            if (colon > 1 && int.TryParse(reference.Substring(colon + 1), out int parsed))
            {
                path = reference.Substring(0, colon);
                line = Mathf.Max(1, parsed);
            }

            string full = ProjectPaths.Resolve(path);
            if (!File.Exists(full))
            {
                Debug.LogWarning("[Parley] File not found: " + full);
                return;
            }

            if (!ProjectPaths.IsInside(full, ProjectPaths.Root) || !_openableExtensions.Contains(Path.GetExtension(full)))
            {
                EditorUtility.RevealInFinder(full);
                return;
            }

            string relative = ProjectPaths.ToProjectRelative(full);
            if (relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.StartsWith("Packages/", StringComparison.Ordinal))
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(relative);
                if (asset != null && AssetDatabase.OpenAsset(asset, line))
                {
                    return;
                }
            }

            InternalEditorUtility.OpenFileAtLineExternal(full, line);
        }
    }
}