using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelSpecWriterTests
    {
        [Test]
        public void WritesIdsAndRepairedTargetsKeepingOtherFields()
        {
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/Old.asset"" }, ""items"": [], ""note"": ""keep"" }");
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = "Assets/New.asset";
            spec.Ids["cfg"] = "id-1";

            Assert.IsTrue(PanelSpecWriter.Apply(json, spec));

            Assert.AreEqual("keep", json["note"]?.ToString());
            Assert.AreEqual("Assets/New.asset", json["targets"]?["cfg"]?.ToString());
            Assert.AreEqual("id-1", json["ids"]?["cfg"]?.ToString());
            List<string> names = new ();
            foreach (JProperty property in json.Properties())
            {
                names.Add(property.Name);
            }

            Assert.AreEqual("ids", names[names.Count - 1]);
        }

        [Test]
        public void ReturnsFalseWhenNothingChanged()
        {
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/A.asset"" }, ""items"": [] }");
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = "Assets/A.asset";
            spec.Ids["cfg"] = "id-1";

            Assert.IsTrue(PanelSpecWriter.Apply(json, spec));
            Assert.IsFalse(PanelSpecWriter.Apply(json, spec));
        }

        [Test]
        public void KeepsIntegerReferenceAsInteger()
        {
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": 12345 }, ""items"": [] }");
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = "12345";

            Assert.IsFalse(PanelSpecWriter.Apply(json, spec));
            Assert.AreEqual(JTokenType.Integer, json["targets"]?["cfg"]?.Type);
        }

        [Test]
        public void RemovesIdsForDroppedAliases()
        {
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/A.asset"" }, ""items"": [], ""ids"": { ""gone"": ""id-0"" } }");
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = "Assets/A.asset";
            spec.Ids["cfg"] = "id-1";

            Assert.IsTrue(PanelSpecWriter.Apply(json, spec));

            Assert.IsNull(json["ids"]?["gone"]);
            Assert.AreEqual("id-1", json["ids"]?["cfg"]?.ToString());
        }

        [Test]
        public void MergeIdsKeepsIdsForUnchangedTargets()
        {
            JObject previous = JObject.Parse(@"{ ""targets"": { ""cfg"": ""Assets/A.asset"" }, ""ids"": { ""cfg"": ""id-1"" } }");
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/A.asset"" }, ""items"": [] }");

            PanelSpecWriter.MergeIds(json, previous);

            Assert.AreEqual("id-1", json["ids"]?["cfg"]?.ToString());
        }

        [Test]
        public void MergeIdsDropsIdsForRepointedTargets()
        {
            JObject previous = JObject.Parse(@"{ ""targets"": { ""cfg"": ""Assets/A.asset"" }, ""ids"": { ""cfg"": ""id-1"" } }");
            JObject json = JObject.Parse(@"{ ""title"": ""T"", ""targets"": { ""cfg"": ""Assets/B.asset"" }, ""items"": [] }");

            PanelSpecWriter.MergeIds(json, previous);

            Assert.IsNull(json["ids"]);
        }
    }
}
