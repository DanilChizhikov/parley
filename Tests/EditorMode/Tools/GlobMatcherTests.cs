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
    }
}