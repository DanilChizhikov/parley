using System;
using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.Panels;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelStoreTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "parley-panels-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void SlugsNames()
        {
            Assert.AreEqual("enemy-balance", PanelStore.Slug("  Enemy Balance! "));
            Assert.AreEqual("a-b_c", PanelStore.Slug("a..b_c"));
            Assert.AreEqual("level-2", PanelStore.Slug("../Level 2"));
            Assert.IsNull(PanelStore.Slug("!!!"));
            Assert.IsNull(PanelStore.Slug(null));
            Assert.AreEqual(PanelStore.MaxNameLength, PanelStore.Slug(new string('x', 200)).Length);
        }

        [Test]
        public void SavesListsLoadsAndDeletes()
        {
            PanelStore store = new PanelStore(_folder);
            List<string> changes = new ();
            store.OnChanged += changes.Add;

            CollectionAssert.IsEmpty(store.List());
            Assert.AreEqual("enemy-balance", store.Save("Enemy Balance", "{\"title\":\"A\"}"));
            store.Save("audio", "{}");

            CollectionAssert.AreEqual(new[] { "audio", "enemy-balance" }, store.List());
            Assert.IsTrue(store.TryLoad("Enemy Balance", out string json));
            Assert.AreEqual("{\"title\":\"A\"}", json);
            Assert.IsTrue(File.Exists(Path.Combine(_folder, "enemy-balance.json")));

            Assert.IsTrue(store.Delete("enemy-balance"));
            Assert.IsFalse(store.Delete("enemy-balance"));
            Assert.IsFalse(store.TryLoad("enemy-balance", out _));
            CollectionAssert.AreEqual(new[] { "enemy-balance", "audio", "enemy-balance" }, changes);
        }

        [Test]
        public void RejectsEmptyNames()
        {
            Assert.Throws<ArgumentException>(() => new PanelStore(_folder).Save("???", "{}"));
        }
    }
}
