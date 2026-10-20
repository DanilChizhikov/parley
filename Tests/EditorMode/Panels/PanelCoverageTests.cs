using DTech.Parley.Editor.Panels;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelCoverageTests
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
        public void ReportsNewAssetsOfTheSameType()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            _assets.Create("Second");
            PanelCoverage coverage = PanelCoverage.Of(spec, new PanelTargetResolver(spec));

            Assert.AreEqual(1, coverage.Missing.Count);
            Assert.AreEqual(nameof(PanelTestConfig), coverage.Missing[0].TypeName);
            Assert.AreEqual(1, coverage.Missing[0].Count);
            StringAssert.Contains("Second", coverage.Missing[0].FirstPath);
        }

        [Test]
        public void IgnoresTypesCoveredByQuery()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            _assets.Create("Second");
            spec.Queries.Add(new PanelItem
            {
                Type = PanelItemType.Repeat,
                Location = "items[1]",
                Query = "t:PanelTestConfig",
            });

            Assert.AreEqual(0, PanelCoverage.Of(spec, new PanelTargetResolver(spec)).Missing.Count);
        }

        [Test]
        public void EmptyWhenNothingNew()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));

            Assert.AreEqual(0, PanelCoverage.Of(spec, new PanelTargetResolver(spec)).Missing.Count);
        }

        private static PanelSpec Spec(string reference)
        {
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = reference;
            return spec;
        }
    }
}
