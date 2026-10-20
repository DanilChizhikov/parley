using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelTargetResolverTests
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
        public void CapturesIdOnFirstResolve()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            PanelTargetResolver resolver = new PanelTargetResolver(spec);

            Assert.IsNotNull(resolver.Resolve("cfg"));
            Assert.IsTrue(spec.Ids.ContainsKey("cfg"));
            Assert.IsTrue(GlobalObjectId.TryParse(spec.Ids["cfg"], out _));
            Assert.IsTrue(resolver.SpecChanged);
            Assert.AreEqual(PanelTargetStatus.Resolved, resolver.Status("cfg"));
        }

        [Test]
        public void HealsRenamedAsset()
        {
            string path = _assets.Create("Brain");
            PanelSpec spec = Spec(path);
            PanelTargetResolver resolver = new PanelTargetResolver(spec);
            Object target = resolver.Resolve("cfg");
            resolver.AcceptChanges();

            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(path, "BrainRenamed"));
            resolver.Invalidate();

            Assert.AreSame(target, resolver.Resolve("cfg"));
            Assert.AreEqual(PanelTargetStatus.Healed, resolver.Status("cfg"));
            Assert.AreEqual(PanelTempAssets.Root + "/BrainRenamed.asset", spec.Targets["cfg"]);
            Assert.IsTrue(resolver.SpecChanged);
        }

        [Test]
        public void HealsMovedAsset()
        {
            string path = _assets.Create("Brain");
            string folder = _assets.CreateFolder(PanelTempAssets.Root + "/Moved");
            PanelSpec spec = Spec(path);
            PanelTargetResolver resolver = new PanelTargetResolver(spec);
            Object target = resolver.Resolve("cfg");

            Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(path, folder + "/Brain.asset"));
            resolver.Invalidate();

            Assert.AreSame(target, resolver.Resolve("cfg"));
            Assert.AreEqual(folder + "/Brain.asset", spec.Targets["cfg"]);
        }

        [Test]
        public void KeepsIdAndReferenceWhenAssetDeleted()
        {
            string path = _assets.Create("Brain");
            PanelSpec spec = Spec(path);
            PanelTargetResolver resolver = new PanelTargetResolver(spec);
            resolver.Resolve("cfg");
            string id = spec.Ids["cfg"];

            Assert.IsTrue(AssetDatabase.DeleteAsset(path));
            resolver.Invalidate();

            Assert.IsNull(resolver.Resolve("cfg"));
            Assert.AreEqual(PanelTargetStatus.MissingWithId, resolver.Status("cfg"));
            Assert.AreEqual(path, spec.Targets["cfg"]);
            Assert.AreEqual(id, spec.Ids["cfg"]);
        }

        [Test]
        public void DoesNotCaptureIdForInMemoryObject()
        {
            PanelTestConfig config = ScriptableObject.CreateInstance<PanelTestConfig>();
            try
            {
                PanelSpec spec = Spec(config.GetInstanceID().ToString());
                PanelTargetResolver resolver = new PanelTargetResolver(spec);

                Assert.IsNotNull(resolver.Resolve("cfg"));
                Assert.AreEqual(0, spec.Ids.Count);
                Assert.IsFalse(resolver.SpecChanged);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void OverwritesUnparsableStoredId()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            spec.Ids["cfg"] = "not-an-id";
            PanelTargetResolver resolver = new PanelTargetResolver(spec);

            Assert.IsNotNull(resolver.Resolve("cfg"));
            Assert.IsTrue(GlobalObjectId.TryParse(spec.Ids["cfg"], out _));
        }

        [Test]
        public void CachesWithinAPass()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            PanelTargetResolver resolver = new PanelTargetResolver(spec);

            Assert.AreSame(resolver.Resolve("cfg"), resolver.Resolve("cfg"));
        }

        [Test]
        public void DoesNotHealWhenNotWritable()
        {
            string path = _assets.Create("Brain");
            PanelSpec spec = Spec(path);
            PanelTargetResolver resolver = new PanelTargetResolver(spec);
            resolver.Resolve("cfg");
            resolver.Writable = false;

            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(path, "BrainRenamed"));
            resolver.Invalidate();

            Assert.IsNotNull(resolver.Resolve("cfg"));
            Assert.AreEqual(path, spec.Targets["cfg"]);
        }

        [Test]
        public void MissingAliasIsMissing()
        {
            PanelSpec spec = Spec(_assets.Create("Brain"));
            PanelTargetResolver resolver = new PanelTargetResolver(spec);

            Assert.IsNull(resolver.Resolve("nope"));
            Assert.AreEqual(PanelTargetStatus.Missing, resolver.Status("nope"));
        }

        private static PanelSpec Spec(string reference)
        {
            PanelSpec spec = new PanelSpec { Title = "T" };
            spec.Targets["cfg"] = reference;
            return spec;
        }
    }
}
