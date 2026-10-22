using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.Sessions
{
	internal static class ChatPresets
	{
		public static ChatPreset Capture(SessionRecord record, string name)
		{
			ChatPreset preset = new ChatPreset { Name = (name ?? string.Empty).Trim() };
			CopyInto(record, preset);
			return preset;
		}

		public static void CopyInto(SessionRecord record, ChatPreset preset)
		{
			preset.EnabledMcpServerIds = Copy(record.EnabledMcpServerIds);
			preset.DisabledExternalMcpServers = Copy(record.DisabledExternalMcpServers);
			preset.EnabledLibrarySkills = Copy(record.EnabledLibrarySkills);
			preset.DisabledExternalSkills = Copy(record.DisabledExternalSkills);
			preset.AutoSkills = new List<AutoSkill>();
			if (record.AutoSkills == null)
			{
				return;
			}

			foreach (SkillInvocation skill in record.AutoSkills)
			{
				preset.AutoSkills.Add(new AutoSkill { Name = skill.Name, Arguments = skill.Arguments ?? string.Empty });
			}
		}

		public static void ApplyTo(SessionRecord record, ChatPreset preset)
		{
			record.EnabledMcpServerIds = Copy(preset.EnabledMcpServerIds);
			record.DisabledExternalMcpServers = Copy(preset.DisabledExternalMcpServers);
			record.EnabledLibrarySkills = Copy(preset.EnabledLibrarySkills);
			record.DisabledExternalSkills = Copy(preset.DisabledExternalSkills);
			record.AutoSkills = ToInvocations(preset.AutoSkills);
			record.PendingAutoSkills = ToInvocations(preset.AutoSkills);
		}

		public static bool Matches(SessionRecord record, ChatPreset preset)
		{
			return SameSet(record.EnabledMcpServerIds, preset.EnabledMcpServerIds)
				&& SameSet(record.DisabledExternalMcpServers, preset.DisabledExternalMcpServers)
				&& SameSet(record.EnabledLibrarySkills, preset.EnabledLibrarySkills)
				&& SameSet(record.DisabledExternalSkills, preset.DisabledExternalSkills)
				&& SameAutoSkills(record.AutoSkills, preset.AutoSkills);
		}

		public static ChatPreset FindMatch(SessionRecord record, IReadOnlyList<ChatPreset> presets)
		{
			foreach (ChatPreset preset in presets)
			{
				if (Matches(record, preset))
				{
					return preset;
				}
			}

			return null;
		}

		private static List<string> Copy(List<string> source)
		{
			return source == null ? new List<string>() : new List<string>(source);
		}

		private static List<SkillInvocation> ToInvocations(List<AutoSkill> skills)
		{
			List<SkillInvocation> invocations = new ();
			if (skills == null)
			{
				return invocations;
			}

			foreach (AutoSkill skill in skills)
			{
				if (!string.IsNullOrWhiteSpace(skill.Name))
				{
					invocations.Add(new SkillInvocation { Name = skill.Name.Trim(), Arguments = skill.Arguments ?? string.Empty });
				}
			}

			return invocations;
		}

		private static bool SameSet(List<string> left, List<string> right)
		{
			HashSet<string> set = new (left ?? new List<string>(), StringComparer.Ordinal);
			return set.SetEquals(right ?? new List<string>());
		}

		private static bool SameAutoSkills(List<SkillInvocation> left, List<AutoSkill> right)
		{
			int leftCount = left?.Count ?? 0;
			int rightCount = right?.Count ?? 0;
			if (leftCount != rightCount)
			{
				return false;
			}

			for (int i = 0; i < leftCount; i++)
			{
				if (left[i].Name != right[i].Name || (left[i].Arguments ?? string.Empty) != (right[i].Arguments ?? string.Empty))
				{
					return false;
				}
			}

			return true;
		}
	}
}
