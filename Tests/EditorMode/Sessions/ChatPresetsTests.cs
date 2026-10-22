using System.Collections.Generic;
using DTech.Parley.Editor;
using DTech.Parley.Editor.Sessions;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ChatPresetsTests
    {
        private static SessionRecord Record()
        {
            return new SessionRecord
            {
                EnabledMcpServerIds = new List<string> { "server-a" },
                DisabledExternalMcpServers = new List<string> { "figma" },
                EnabledLibrarySkills = new List<string> { "level-design" },
                DisabledExternalSkills = new List<string> { "pdf", "docx" },
                AutoSkills = new List<SkillInvocation> { new SkillInvocation { Name = "caveman", Arguments = "ultra" } },
                PendingAutoSkills = new List<SkillInvocation>(),
            };
        }

        [Test]
        public void CaptureCopiesLists()
        {
            SessionRecord record = Record();
            ChatPreset preset = ChatPresets.Capture(record, "  Unity  ");
            Assert.AreEqual("Unity", preset.Name);
            CollectionAssert.AreEqual(record.DisabledExternalSkills, preset.DisabledExternalSkills);
            Assert.AreNotSame(record.DisabledExternalSkills, preset.DisabledExternalSkills);
            Assert.AreEqual(1, preset.AutoSkills.Count);
            Assert.AreEqual("caveman", preset.AutoSkills[0].Name);
            Assert.AreEqual("ultra", preset.AutoSkills[0].Arguments);
        }

        [Test]
        public void ApplyReplacesListsAndQueuesAutoSkills()
        {
            ChatPreset preset = ChatPresets.Capture(Record(), "Unity");
            SessionRecord target = new SessionRecord
            {
                EnabledMcpServerIds = new List<string> { "server-b" },
                DisabledExternalMcpServers = new List<string>(),
                EnabledLibrarySkills = new List<string>(),
                DisabledExternalSkills = new List<string> { "xlsx" },
            };

            ChatPresets.ApplyTo(target, preset);
            CollectionAssert.AreEquivalent(new[] { "server-a" }, target.EnabledMcpServerIds);
            CollectionAssert.AreEquivalent(new[] { "pdf", "docx" }, target.DisabledExternalSkills);
            Assert.AreNotSame(preset.DisabledExternalSkills, target.DisabledExternalSkills);
            Assert.AreEqual(1, target.PendingAutoSkills.Count);
            Assert.AreEqual("/caveman ultra", target.PendingAutoSkills[0].ToCommand());
            Assert.AreEqual(1, target.AutoSkills.Count);
        }

        [Test]
        public void MatchesIgnoresOrderButNotContent()
        {
            SessionRecord record = Record();
            ChatPreset preset = ChatPresets.Capture(record, "Unity");
            record.DisabledExternalSkills.Reverse();
            Assert.IsTrue(ChatPresets.Matches(record, preset));
            record.DisabledExternalSkills.Remove("pdf");
            Assert.IsFalse(ChatPresets.Matches(record, preset));
        }

        [Test]
        public void MatchesChecksAutoSkillArguments()
        {
            SessionRecord record = Record();
            ChatPreset preset = ChatPresets.Capture(record, "Unity");
            record.AutoSkills[0].Arguments = "lite";
            Assert.IsFalse(ChatPresets.Matches(record, preset));
        }

        [Test]
        public void FindMatchReturnsMatchingPreset()
        {
            SessionRecord record = Record();
            ChatPreset other = new ChatPreset { Name = "Empty" };
            ChatPreset unity = ChatPresets.Capture(record, "Unity");
            Assert.AreSame(unity, ChatPresets.FindMatch(record, new List<ChatPreset> { other, unity }));
            Assert.IsNull(ChatPresets.FindMatch(new SessionRecord(), new List<ChatPreset> { unity }));
        }

        [Test]
        public void NullRecordListsMatchEmptyPreset()
        {
            Assert.IsTrue(ChatPresets.Matches(new SessionRecord(), new ChatPreset()));
        }
    }
}
