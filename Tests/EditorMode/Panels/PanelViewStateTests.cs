using DTech.Parley.Editor.Panels;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class PanelViewStateTests
    {
        [Test]
        public void RestoresFoldoutValueAcrossBuilds()
        {
            PanelViewState state = new PanelViewState();
            Foldout first = new Foldout();
            state.Bind(first, "items[0]");
            first.value = false;

            state.BeginBuild();
            Foldout second = new Foldout();
            state.Bind(second, "items[0]");

            Assert.IsFalse(second.value);
        }

        [Test]
        public void PrunesKeysNotSeenInTheLastBuild()
        {
            PanelViewState state = new PanelViewState();
            Foldout first = new Foldout();
            state.Bind(first, "items[0]");
            first.value = false;

            state.BeginBuild();
            state.BeginBuild();
            Foldout second = new Foldout();
            state.Bind(second, "items[0]");

            Assert.IsTrue(second.value);
        }

        [Test]
        public void NestedFoldoutDoesNotOverwriteParent()
        {
            PanelViewState state = new PanelViewState();
            Foldout parent = new Foldout();
            Foldout child = new Foldout();
            parent.Add(child);
            state.Bind(parent, "items[0]");
            state.Bind(child, "items[0].items[0]");

            child.value = false;

            state.BeginBuild();
            Foldout newParent = new Foldout();
            Foldout newChild = new Foldout();
            newParent.Add(newChild);
            state.Bind(newParent, "items[0]");
            state.Bind(newChild, "items[0].items[0]");

            Assert.IsTrue(newParent.value);
            Assert.IsFalse(newChild.value);
        }

        [Test]
        public void KeysDistinguishDuplicates()
        {
            Assert.AreNotEqual(
                PanelViewState.ComponentKey("items[0]", "BoxCollider", 0),
                PanelViewState.ComponentKey("items[0]", "BoxCollider", 1));
            Assert.AreNotEqual(
                PanelViewState.RepeatKey("items[0]", "guid-a"),
                PanelViewState.RepeatKey("items[0]", "guid-b"));
        }
    }
}
