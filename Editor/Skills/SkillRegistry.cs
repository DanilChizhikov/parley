using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace DTech.Parley.Editor.Skills
{
    internal sealed class SkillRegistry
    {
        private static SkillRegistry _current;
        private static string _customDirectory;
        private readonly SortedDictionary<string, SkillDocument> _skills = new(StringComparer.Ordinal);

        public IReadOnlyCollection<SkillDocument> Skills => _skills.Values;
        public List<string> Diagnostics { get; } = new();

        public static SkillRegistry Current
        {
            get
            {
                string directory = ParleyUserSettings.instance.CustomSkillsDirectory;
                if (_current == null || _customDirectory != directory) Reload();
                return _current;
            }
        }

        public static void Reload()
        {
            SkillRegistry registry = new();
            foreach (string guid in AssetDatabase.FindAssets("SkillRegistry t:MonoScript"))
            {
                string script = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(script) != "SkillRegistry.cs") continue;
                UnityEditor.PackageManager.PackageInfo package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssetPath(script);
                string source = package == null ? Path.Combine(ProjectPaths.Root, script)
                    : Path.Combine(package.resolvedPath, script.Substring(package.assetPath.Length + 1));
                registry.LoadDirectory(Path.Combine(Path.GetDirectoryName(source), "Builtin"));
                break;
            }
            foreach (SkillDocument document in SkillLibrary.List()) registry.Add(document, document.FolderPath);
            _customDirectory = ParleyUserSettings.instance.CustomSkillsDirectory;
            registry.LoadDirectory(_customDirectory);
            _current = registry;
        }

        public void LoadDirectory(string directory)
        {
            foreach (SkillDocument document in SkillScanner.ScanDocuments(directory, Diagnostics))
                Add(document, document.FolderPath);
        }

        public void Add(SkillDocument document, string source)
        {
            if (!SkillLibrary.IsValidName(document.Name) || string.IsNullOrWhiteSpace(document.Body))
            {
                Diagnostics.Add(source + ": skill requires a valid name and non-empty instructions.");
                return;
            }
            if (document.Diagnostics.Count > 0)
            {
                foreach (string diagnostic in document.Diagnostics) Diagnostics.Add(source + ": " + diagnostic);
                return;
            }
            if (_skills.ContainsKey(document.Id)) Diagnostics.Add(source + ": overrides " + document.Id + ".");
            _skills[document.Id] = document;
        }
    }
}
