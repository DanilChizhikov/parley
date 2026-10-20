using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelBuilderTests
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
        public void BuildsFieldsInsideContainers()
        {
            using PanelBuilder builder = new PanelBuilder(Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + _config.GetInstanceID() + @""" }, ""items"": [
                { ""type"": ""header"", ""text"": ""Stats"" },
                { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_health"", ""label"": ""HP"" },
                { ""type"": ""group"", ""label"": ""More"", ""foldout"": true, ""items"": [
                    { ""type"": ""row"", ""items"": [
                        { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_speed"" },
                        { ""type"": ""button"", ""label"": ""Ping"", ""action"": ""ping"", ""target"": ""cfg"" }
                    ] }
                ] },
                { ""type"": ""help"", ""text"": ""Tip"" },
                { ""type"": ""separator"" }
            ] }"), null);

            VisualElement root = builder.Build();

            List<PropertyField> fields = root.Query<PropertyField>().ToList();
            Assert.AreEqual(2, fields.Count);
            Assert.AreEqual("HP", fields[0].label);
            Assert.AreEqual(1, root.Query<Foldout>().ToList().Count);
            Assert.AreEqual(1, root.Query<HelpBox>().ToList().Count);
            Assert.IsNotNull(root.Q<Button>(className: "pl-panel__button"));
            Assert.IsNotNull(root.Q(className: "pl-panel__row"));
        }

        [Test]
        public void ShowsPlaceholderForMissingTargets()
        {
            using PanelBuilder builder = new PanelBuilder(Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/DoesNotExist.asset"" }, ""items"": [
                { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_health"" }
            ] }"), null);

            VisualElement root = builder.Build();

            Assert.AreEqual(0, root.Query<PropertyField>().ToList().Count);
            Assert.IsNotNull(root.Q(className: "pl-panel__missing"));
        }

        [Test]
        public void ShowsErrorForUnknownPropertyPath()
        {
            using PanelBuilder builder = new PanelBuilder(Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": """ + _config.GetInstanceID() + @""" }, ""items"": [
                { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_nope"" }
            ] }"), null);

            VisualElement root = builder.Build();

            HelpBox help = root.Q<HelpBox>();
            Assert.IsNotNull(help);
            StringAssert.Contains("'_nope' not found", help.text);
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
