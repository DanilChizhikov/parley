using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class PanelTempAssets
    {
        public const string Root = "Assets/ParleyTempTests";

        public string Folder { get; }

        private readonly List<string> _folders = new ();

        public PanelTempAssets()
        {
            Folder = CreateFolder(Root);
        }

        public string Create(string name)
        {
            return Create(Folder, name);
        }

        public string Create(string folder, string name)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PanelTestConfig>(), path);
            return path;
        }

        public string CreateFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                int slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }

            _folders.Add(path);
            return path;
        }

        public void Delete()
        {
            for (int i = _folders.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(_folders[i]);
            }

            _folders.Clear();
        }
    }
}
