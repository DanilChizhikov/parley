using System;
using DTech.Parley.Editor.Updates;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class UpdateParsingTests
    {
        [Test]
        public void ParsesReleaseTags()
        {
            Assert.AreEqual(new Version(1, 2, 3), VersionTags.Parse("v1.2.3"));
            Assert.AreEqual(new Version(1, 2, 3), VersionTags.Parse(" 1.2.3 "));
            Assert.AreEqual(new Version(1, 2, 0), VersionTags.Parse("V1.2"));
        }

        [Test]
        public void RejectsPreReleaseAndGarbage()
        {
            Assert.IsNull(VersionTags.Parse("v1.2.3-preview.1"));
            Assert.IsNull(VersionTags.Parse("latest"));
            Assert.IsNull(VersionTags.Parse(string.Empty));
            Assert.IsNull(VersionTags.Parse(null));
        }

        [Test]
        public void MaxKeepsHighestVersion()
        {
            Assert.AreEqual(new Version(0, 2, 0), VersionTags.Max(null, new Version(0, 2, 0)));
            Assert.AreEqual(new Version(0, 2, 0), VersionTags.Max(new Version(0, 2, 0), null));
            Assert.AreEqual(new Version(0, 10, 0), VersionTags.Max(new Version(0, 9, 0), new Version(0, 10, 0)));
        }

        [Test]
        public void ExtractsGitUrlFromPackageId()
        {
            Assert.AreEqual("https://github.com/DanilChizhikov/parley.git",
                InstalledPackage.GitUrlFromPackageId("com.dtech.parley@https://github.com/DanilChizhikov/parley.git#v0.1.0"));
            Assert.AreEqual("git@github.com:DanilChizhikov/parley.git",
                InstalledPackage.GitUrlFromPackageId("com.dtech.parley@git@github.com:DanilChizhikov/parley.git#v0.1.0"));
            Assert.AreEqual("https://github.com/DanilChizhikov/parley.git?path=/Packages/com.dtech.parley",
                InstalledPackage.GitUrlFromPackageId("com.dtech.parley@https://github.com/DanilChizhikov/parley.git?path=/Packages/com.dtech.parley"));
        }

        [Test]
        public void BuildsInstallIdentifierPerSource()
        {
            Version version = new Version(0, 2, 0);
            InstalledPackage git = new InstalledPackage("com.dtech.parley", new Version(0, 1, 0), PackageSource.Git, "https://github.com/DanilChizhikov/parley.git");
            InstalledPackage registry = new InstalledPackage("com.dtech.parley", new Version(0, 1, 0), PackageSource.Registry, string.Empty);
            Assert.AreEqual("https://github.com/DanilChizhikov/parley.git#v0.2.0", PackageInstaller.Identifier(git, version));
            Assert.AreEqual("com.dtech.parley@0.2.0", PackageInstaller.Identifier(registry, version));
        }

        [Test]
        public void CleanBodyDropsFullChangelogLine()
        {
            string body = "## Added\r\n- Update button\r\n\r\n**Full Changelog**: https://github.com/x/y/compare/v0.1.0...v0.2.0";
            Assert.AreEqual("## Added\n- Update button", ReleaseNotesLoader.CleanBody(body));
            Assert.AreEqual(string.Empty, ReleaseNotesLoader.CleanBody(null));
        }
    }
}
