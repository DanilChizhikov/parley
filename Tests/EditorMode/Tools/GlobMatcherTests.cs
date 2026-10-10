using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DTech.Parley.Editor.Tools.Builtin;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class GlobMatcherTests
    {
        [Test]
        public void PatternsMatchRelativePaths()
        {
            Assert.IsTrue(GlobMatcher.ToRegex("**/*.cs", false).IsMatch("Assets/Scripts/Player.cs"));
            Assert.IsTrue(GlobMatcher.ToRegex("**/*.cs", false).IsMatch("Player.cs"));
            Assert.IsFalse(GlobMatcher.ToRegex("*.cs", false).IsMatch("Assets/Player.cs"));
            Assert.IsTrue(GlobMatcher.ToRegex("Assets/**/*.{uss,uxml}", false).IsMatch("Assets/UI/Main.uxml"));
        }

        [Test]
        public void SkipsUnityFoldersOnlyAtProjectRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "parley-glob-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Library"));
                Directory.CreateDirectory(Path.Combine(root, "Assets", "Library"));
                File.WriteAllText(Path.Combine(root, "Library", "a.cs"), "a");
                File.WriteAllText(Path.Combine(root, "Assets", "Library", "b.cs"), "b");

                List<string> names = new ();
                foreach (string file in GlobMatcher.EnumerateFiles(root, root, CancellationToken.None))
                {
                    names.Add(Path.GetFileName(file));
                }

                CollectionAssert.AreEqual(new[] { "b.cs" }, names);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
