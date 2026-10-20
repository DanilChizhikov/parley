using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelRepeatTests
    {
        private PanelTempAssets _assets;

        [SetUp]
        public void SetUp()
        {
            _assets = new PanelTempAssets();
        }

        [TearDown]
        public void TearDown()
        {
            _assets.Delete();
        }

        [Test]
        public void ParsesRepeatAndCollectsItAsQuery()
        {
            PanelSpec spec = Parse(Spec());

            Assert.AreEqual(1, spec.Queries.Count);
            Assert.AreEqual(PanelItemType.Repeat, spec.Items[0].Type);
            Assert.IsNull(spec.Items[0].Items[0].Target);
        }

        [Test]
        public void RejectsEmptyTargetOutsideRepeat()
        {
            List<PanelIssue> issues = new ();
            PanelParser.Parse(@"{ ""title"": ""T"", ""targets"": {}, ""items"": [ { ""type"": ""field"", ""path"": ""_health"" } ] }", issues);

            Assert.IsTrue(PanelIssue.HasErrors(issues));
            StringAssert.Contains("'target' is required", issues[0].Message);
        }

        [Test]
        public void BuildsOneSectionPerAssetAndFollowsNewAssets()
        {
            _assets.Create("Brain");
            PanelViewState view = new PanelViewState();

            Assert.AreEqual(1, Sections(Spec(), view));

            _assets.Create("Second");

            Assert.AreEqual(2, Sections(Spec(), view));
        }

        [Test]
        public void ScopedFieldsBindToEachAsset()
        {
            _assets.Create("Brain");
            _assets.Create("Second");
            PanelSpec spec = Parse(Spec());
            using PanelBuilder builder = new PanelBuilder(new PanelBuildRequest(spec, new PanelTargetResolver(spec), new PanelViewState(), null));

            VisualElement root = builder.Build();

            Assert.AreEqual(2, root.Query<PropertyField>().ToList().Count);
        }

        [Test]
        public void ValidatesNestedPropertyPathsAgainstFirstAsset()
        {
            _assets.Create("Brain");
            PanelSpec spec = Parse(Spec("_nope"));
            List<PanelIssue> issues = new ();

            PanelTargets.Validate(spec, issues);

            Assert.IsTrue(PanelIssue.HasErrors(issues));
            StringAssert.Contains("'_nope' not found", issues[0].Message);
        }

        private static int Sections(string json, PanelViewState view)
        {
            PanelSpec spec = Parse(json);
            using PanelBuilder builder = new PanelBuilder(new PanelBuildRequest(spec, new PanelTargetResolver(spec), view, null));
            return builder.Build().Query<Foldout>().ToList().Count;
        }

        private static string Spec(string path = "_health")
        {
            return @"{ ""title"": ""T"", ""targets"": {}, ""items"": [
                { ""type"": ""repeat"", ""query"": ""t:PanelTestConfig"", ""folder"": """ + PanelTempAssets.Root + @""", ""label"": ""Configs"", ""items"": [
                    { ""type"": ""field"", ""path"": """ + path + @""" }
                ] }
            ] }";
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
