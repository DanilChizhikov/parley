using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEditor;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelRefreshTests
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
        public void IdleWhenNothingMarked()
        {
            PanelRefresh refresh = new PanelRefresh();
            PanelSpec spec = Spec(_assets.Create("Brain"), out PanelTargetResolver resolver);
            refresh.Capture(spec, resolver);

            Assert.AreEqual(PanelRefreshResult.Idle, refresh.Poll(spec, resolver, false));
        }

        [Test]
        public void KeepsFlagsWhileBusy()
        {
            PanelRefresh refresh = new PanelRefresh();
            string path = _assets.Create("Brain");
            PanelSpec spec = Spec(path, out PanelTargetResolver resolver);
            refresh.Capture(spec, resolver);
            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(path, "BrainRenamed"));
            refresh.MarkAssets();

            Assert.AreEqual(PanelRefreshResult.Idle, refresh.Poll(spec, resolver, true));
            Assert.AreEqual(PanelRefreshResult.Rebuild, refresh.Poll(spec, resolver, false));
        }

        [Test]
        public void IdleWhenWorldUnchanged()
        {
            PanelRefresh refresh = new PanelRefresh();
            PanelSpec spec = Spec(_assets.Create("Brain"), out PanelTargetResolver resolver);
            refresh.Capture(spec, resolver);
            resolver.AcceptChanges();
            refresh.MarkAssets();

            Assert.AreEqual(PanelRefreshResult.Idle, refresh.Poll(spec, resolver, false));
        }

        [Test]
        public void PersistWhenOnlyIdsCaptured()
        {
            PanelRefresh refresh = new PanelRefresh();
            PanelSpec spec = Spec(_assets.Create("Brain"), out PanelTargetResolver resolver);
            refresh.Capture(spec, resolver);
            refresh.MarkHierarchy();

            Assert.AreEqual(PanelRefreshResult.Persist, refresh.Poll(spec, resolver, false));
        }

        [Test]
        public void ClearsFlagsWhenSpecIsNull()
        {
            PanelRefresh refresh = new PanelRefresh();
            refresh.MarkAssets();

            Assert.AreEqual(PanelRefreshResult.Idle, refresh.Poll(null, null, false));
            Assert.AreEqual(PanelRefreshResult.Idle, refresh.Poll(null, null, false));
        }

        private static PanelSpec Spec(string reference, out PanelTargetResolver resolver)
        {
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = reference;
            resolver = new PanelTargetResolver(spec);
            return spec;
        }
    }
}
