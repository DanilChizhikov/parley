using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEngine;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelTargetsTests
    {
        private PanelTestConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PanelTestConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AcceptsExistingPropertyPaths()
        {
            List<PanelIssue> issues = Validate(Field("_health"), Field("_stats._armor"));
            CollectionAssert.IsEmpty(issues, PanelIssue.Format(issues));
        }

        [Test]
        public void SuggestsSimilarPropertyPaths()
        {
            List<PanelIssue> issues = Validate(Field("_healt"));

            Assert.IsTrue(PanelIssue.HasErrors(issues));
            StringAssert.Contains("Did you mean: _health", issues[0].Message);
            Assert.AreEqual("items[0]", issues[0].Location);
        }

        [Test]
        public void ListsAvailablePathsWhenNothingIsSimilar()
        {
            List<PanelIssue> issues = Validate(Field("completelyUnrelatedName"));

            StringAssert.Contains("Available:", issues[0].Message);
            StringAssert.Contains("_speed", issues[0].Message);
        }

        [Test]
        public void MissingAssetIsErrorButMissingSceneObjectIsWarning()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = Parse(@"{ ""title"": ""T"", ""targets"": { ""a"": ""Assets/DoesNotExist.asset"", ""s"": ""NoScene:NoRoot/Child"" },
                ""items"": [ { ""type"": ""inspector"", ""target"": ""a"" }, { ""type"": ""inspector"", ""target"": ""s"" } ] }");
            PanelTargets.Validate(spec, issues);

            Assert.AreEqual(2, issues.Count);
            Assert.AreEqual(PanelIssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual("targets.a", issues[0].Location);
            Assert.AreEqual(PanelIssueSeverity.Warning, issues[1].Severity);
        }

        [Test]
        public void ComponentOnNonGameObjectIsError()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + _config.GetInstanceID() + @""" },
                ""items"": [ { ""type"": ""inspector"", ""target"": ""cfg"", ""component"": ""Rigidbody"" } ] }");
            PanelTargets.Validate(spec, issues);

            StringAssert.Contains("Component 'Rigidbody' not found", PanelIssue.Format(issues));
        }

        [Test]
        public void FindsComponentsByName()
        {
            GameObject gameObject = new GameObject("PanelTarget", typeof(BoxCollider));
            try
            {
                Assert.IsInstanceOf<BoxCollider>(PanelTargets.WithComponent(gameObject, "BoxCollider"));
                Assert.IsInstanceOf<BoxCollider>(PanelTargets.WithComponent(gameObject, "collider"));
                Assert.IsNull(PanelTargets.WithComponent(gameObject, "Rigidbody"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UnknownScriptableObjectTypeIsError()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = Parse(@"{ ""title"": ""T"", ""items"": [
                { ""type"": ""button"", ""label"": ""New"", ""action"": ""create_asset"", ""asset_type"": ""NoSuchConfigType"", ""asset_path"": ""Assets/x.asset"" } ] }");
            PanelTargets.Validate(spec, issues);

            StringAssert.Contains("'NoSuchConfigType' not found", PanelIssue.Format(issues));
            Assert.AreEqual(typeof(PanelTestConfig), PanelTargets.FindScriptableObjectType(nameof(PanelTestConfig)));
        }

        [Test]
        public void DistanceIsCaseInsensitiveEditDistance()
        {
            Assert.AreEqual(0, PanelTargets.Distance("Health", "health"));
            Assert.AreEqual(1, PanelTargets.Distance("healt", "health"));
            Assert.AreEqual(3, PanelTargets.Distance("", "abc"));
        }

        [Test]
        public void ValidateCapturesIdsIntoSpec()
        {
            PanelTempAssets assets = new PanelTempAssets();
            try
            {
                List<PanelIssue> issues = new ();
                PanelSpec spec = Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + assets.Create("Brain") + @""" },
                    ""items"": [ { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_health"" } ] }");

                PanelTargets.Validate(spec, issues);

                CollectionAssert.IsEmpty(issues, PanelIssue.Format(issues));
                Assert.AreEqual(1, spec.Ids.Count);
            }
            finally
            {
                assets.Delete();
            }
        }

        [Test]
        public void MissingKnownTargetMentionsReconnect()
        {
            PanelTempAssets assets = new PanelTempAssets();
            try
            {
                string path = assets.Create("Brain");
                PanelSpec spec = Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + path + @""" },
                    ""items"": [ { ""type"": ""inspector"", ""target"": ""cfg"" } ] }");
                PanelTargets.Validate(spec, new List<PanelIssue>());
                Assert.IsTrue(UnityEditor.AssetDatabase.DeleteAsset(path));

                List<PanelIssue> issues = new ();
                PanelTargets.Validate(spec, issues);

                Assert.IsTrue(PanelIssue.HasErrors(issues));
                StringAssert.Contains("reconnect", issues[0].Message);
            }
            finally
            {
                assets.Delete();
            }
        }

        private string Field(string path)
        {
            return @"{ ""type"": ""field"", ""target"": ""cfg"", ""path"": """ + path + @""" }";
        }

        private List<PanelIssue> Validate(params string[] items)
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + _config.GetInstanceID() + @""" }, ""items"": [" + string.Join(",", items) + "] }");
            PanelTargets.Validate(spec, issues);
            return issues;
        }

        private static PanelSpec Parse(string json)
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = PanelParser.Parse(json, issues);
            Assert.IsFalse(PanelIssue.HasErrors(issues), PanelIssue.Format(issues));
            return spec;
        }
    }
}
