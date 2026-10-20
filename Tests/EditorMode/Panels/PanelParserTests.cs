using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelParserTests
    {
        [Test]
        public void ParsesNestedItems()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = PanelParser.Parse(@"{
                ""title"": ""Balance"",
                ""targets"": { ""cfg"": ""Assets/Config.asset"" },
                ""items"": [
                    { ""type"": ""header"", ""text"": ""Stats"" },
                    { ""type"": ""group"", ""label"": ""Main"", ""foldout"": true, ""items"": [
                        { ""type"": ""field"", ""target"": ""cfg"", ""path"": ""_health"", ""label"": ""HP"" },
                        { ""type"": ""row"", ""items"": [ { ""type"": ""button"", ""label"": ""Ping"", ""action"": ""ping"", ""target"": ""cfg"" } ] }
                    ] },
                    { ""type"": ""table"", ""query"": ""t:Config"", ""columns"": [""_health"", ""_speed""] },
                    { ""type"": ""help"", ""text"": ""Careful"", ""level"": ""warning"" }
                ]
            }", issues);

            CollectionAssert.IsEmpty(issues);
            Assert.AreEqual("Balance", spec.Title);
            Assert.AreEqual("Assets/Config.asset", spec.Targets["cfg"]);
            Assert.AreEqual(7, spec.CountItems());
            PanelItem group = spec.Items[1];
            Assert.AreEqual(PanelItemType.Group, group.Type);
            Assert.IsTrue(group.Foldout);
            Assert.AreEqual("HP", group.Items[0].Label);
            Assert.AreEqual(PanelAction.Ping, group.Items[1].Items[0].Action);
            Assert.AreEqual("items[1].items[1].items[0]", group.Items[1].Items[0].Location);
            CollectionAssert.AreEqual(new[] { "_health", "_speed" }, spec.Items[2].Columns);
            Assert.AreEqual(PanelHelpLevel.Warning, spec.Items[3].Level);
        }

        [Test]
        public void ReportsUnknownTypesAndActions()
        {
            List<PanelIssue> issues = new ();
            PanelParser.Parse(@"{ ""title"": ""T"", ""items"": [
                { ""type"": ""slider"" },
                { ""type"": ""button"", ""label"": ""Run"", ""action"": ""execute_code"" }
            ] }", issues);

            Assert.IsTrue(PanelIssue.HasErrors(issues));
            Assert.AreEqual(2, issues.Count);
            StringAssert.Contains("Unknown type 'slider'", issues[0].Message);
            StringAssert.Contains("Unknown action 'execute_code'", issues[1].Message);
            Assert.AreEqual("items[1]", issues[1].Location);
        }

        [Test]
        public void ReportsMissingFieldsAndUnknownAliases()
        {
            List<PanelIssue> issues = new ();
            PanelParser.Parse(@"{ ""items"": [
                { ""type"": ""field"", ""target"": ""nope"" },
                { ""type"": ""group"", ""label"": ""G"" },
                { ""type"": ""table"", ""query"": ""t:X"" },
                { ""type"": ""button"", ""label"": ""New"", ""action"": ""create_asset"", ""asset_type"": ""X"", ""asset_path"": ""Configs/x.asset"" }
            ] }", issues);

            string all = PanelIssue.Format(issues);
            StringAssert.Contains("title", all);
            StringAssert.Contains("Unknown target alias 'nope'", all);
            StringAssert.Contains("'path' is required", all);
            StringAssert.Contains("'items' array is required", all);
            StringAssert.Contains("'columns' must list", all);
            StringAssert.Contains("asset_path must look like", all);
        }

        [Test]
        public void LimitsNesting()
        {
            JObject root = new JObject { ["title"] = "Deep" };
            JArray items = new JArray();
            root["items"] = items;
            for (int i = 0; i < PanelParser.MaxDepth + 1; i++)
            {
                JArray children = new JArray();
                items.Add(new JObject { ["type"] = "row", ["items"] = children });
                items = children;
            }

            List<PanelIssue> issues = new ();
            PanelParser.Parse(root, issues);

            Assert.IsTrue(PanelIssue.HasErrors(issues));
            StringAssert.Contains("Nested too deep", PanelIssue.Format(issues));
        }

        [Test]
        public void UnknownHelpLevelIsWarning()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = PanelParser.Parse(@"{ ""title"": ""T"", ""items"": [ { ""type"": ""help"", ""text"": ""x"", ""level"": ""loud"" } ] }", issues);

            Assert.IsFalse(PanelIssue.HasErrors(issues));
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(PanelHelpLevel.Info, spec.Items[0].Level);
        }

        [Test]
        public void ParsesIdsForKnownAliasesOnly()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = PanelParser.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/A.asset"" },
                ""ids"": { ""cfg"": ""id-1"", ""gone"": ""id-2"", ""bad"": 5 },
                ""items"": [ { ""type"": ""label"", ""text"": ""x"" } ] }", issues);

            Assert.IsFalse(PanelIssue.HasErrors(issues), PanelIssue.Format(issues));
            Assert.AreEqual(1, spec.Ids.Count);
            Assert.AreEqual("id-1", spec.Ids["cfg"]);
        }

        [Test]
        public void CollectsQueryItems()
        {
            List<PanelIssue> issues = new ();
            PanelSpec spec = PanelParser.Parse(@"{ ""title"": ""T"", ""targets"": {}, ""items"": [
                { ""type"": ""table"", ""query"": ""t:PanelTestConfig"", ""columns"": [ ""_health"" ] },
                { ""type"": ""repeat"", ""query"": ""t:PanelTestConfig"", ""items"": [ { ""type"": ""field"", ""path"": ""_health"" } ] }
            ] }", issues);

            Assert.IsFalse(PanelIssue.HasErrors(issues), PanelIssue.Format(issues));
            Assert.AreEqual(2, spec.Queries.Count);
        }

        [Test]
        public void ReportsInvalidJson()
        {
            List<PanelIssue> issues = new ();
            Assert.IsNull(PanelParser.Parse("{ not json", issues));
            StringAssert.Contains("Invalid JSON", issues[0].Message);
        }

        [Test]
        public void ReadSpecAcceptsObjectsAndJsonStrings()
        {
            Assert.IsNotNull(PanelParser.ReadSpec(new JObject { ["title"] = "A" }, out string error));
            Assert.IsNull(error);

            JObject fromString = PanelParser.ReadSpec(new JValue("{\"title\":\"B\"}"), out error);
            Assert.AreEqual("B", (string)fromString["title"]);

            Assert.IsNull(PanelParser.ReadSpec(new JArray(), out error));
            StringAssert.Contains("JSON object", error);

            Assert.IsNull(PanelParser.ReadSpec(null, out error));
            Assert.IsNull(error);
        }
    }
}
