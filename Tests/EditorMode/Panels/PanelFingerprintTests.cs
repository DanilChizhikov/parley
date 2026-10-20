using System.Collections.Generic;
using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEditor;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelFingerprintTests
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
        public void TargetsHashChangesOnRename()
        {
            string path = _assets.Create("Brain");
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = path;
            PanelTargetResolver resolver = new PanelTargetResolver(spec);
            ulong before = PanelFingerprint.HashTargets(spec, resolver);

            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(path, "BrainRenamed"));
            resolver.Invalidate();

            Assert.AreNotEqual(before, PanelFingerprint.HashTargets(spec, resolver));
        }

        [Test]
        public void TargetsHashStableWithoutChanges()
        {
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = _assets.Create("Brain");
            PanelTargetResolver resolver = new PanelTargetResolver(spec);

            ulong first = PanelFingerprint.HashTargets(spec, resolver);
            resolver.Invalidate();

            Assert.AreEqual(first, PanelFingerprint.HashTargets(spec, resolver));
        }

        [Test]
        public void QueriesHashChangesWhenAssetAddedRenamedAndDeleted()
        {
            _assets.Create("Brain");
            List<PanelItem> queries = new () { Query() };
            ulong start = PanelFingerprint.HashQueries(queries);

            string added = _assets.Create("Second");
            ulong afterAdd = PanelFingerprint.HashQueries(queries);
            Assert.AreNotEqual(start, afterAdd);

            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(added, "SecondRenamed"));
            ulong afterRename = PanelFingerprint.HashQueries(queries);
            Assert.AreNotEqual(afterAdd, afterRename);

            Assert.IsTrue(AssetDatabase.DeleteAsset(PanelTempAssets.Root + "/SecondRenamed.asset"));
            Assert.AreEqual(start, PanelFingerprint.HashQueries(queries));
        }

        [Test]
        public void QueriesHashIgnoresAssetOutsideFolder()
        {
            _assets.Create("Brain");
            List<PanelItem> queries = new () { Query() };
            ulong before = PanelFingerprint.HashQueries(queries);

            string outside = _assets.CreateFolder(PanelTempAssets.Root + "/Other");
            _assets.Create(outside, "Ignored");

            Assert.AreEqual(before, PanelFingerprint.HashQueries(queries));
        }

        private static PanelItem Query()
        {
            return new PanelItem
            {
                Type = PanelItemType.Table,
                Location = "items[0]",
                Query = "t:PanelTestConfig",
                Folder = PanelTempAssets.Root,
            };
        }
    }
}
