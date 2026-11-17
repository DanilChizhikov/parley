using System;
using System.Collections.Generic;
using DTech.Parley.Editor.Skills;

namespace DTech.Parley.Editor.Sessions
{
	internal static class ChatPresets
	{
        public static bool TryCreateRole(SkillDocument skill, IReadOnlyList<ParleyProfile> profiles,
            ParleyProfile current, PermissionMode mode, out ParleyProfile profile, out SessionRecord record,
            out string error)
        {
            profile = null;
            record = null;
            error = null;
            if (skill.Diagnostics.Count > 0 || string.IsNullOrWhiteSpace(skill.Body))
                error = "The role has invalid or empty instructions.";
            else if (string.IsNullOrWhiteSpace(skill.PreferredProvider) || string.IsNullOrWhiteSpace(skill.Model))
                error = "The role requires preferred_provider and model. Use 'current' explicitly to inherit the current profile.";
            else if (skill.PreferredProvider == "current") profile = current;
            else
            {
                foreach (ParleyProfile candidate in profiles)
                {
                    if (!string.Equals(candidate.Id, skill.PreferredProvider, StringComparison.Ordinal)
                        && !string.Equals(candidate.Name, skill.PreferredProvider, StringComparison.OrdinalIgnoreCase)
                        && !(skill.PreferredProvider == "claude" && candidate.Kind == ProfileKind.ClaudeCode)
                        && !(skill.PreferredProvider == "codex" && candidate.Kind == ProfileKind.Codex)
                        && !(skill.PreferredProvider == "local" && candidate.Kind == ProfileKind.Local)) continue;
                    if (profile != null)
                    {
                        error = "Multiple profiles match '" + skill.PreferredProvider + "'. Use a unique profile name or ID.";
                        break;
                    }
                    profile = candidate;
                }
            }
            if (error == null && profile == null) error = "Provider profile '" + skill.PreferredProvider + "' is unavailable.";
            string model = skill.Model == "current" ? profile?.Model : skill.Model;
            if (error == null && string.IsNullOrWhiteSpace(model))
                error = "The selected profile has no model. Choose a model before opening this role.";
            if (error != null) return false;
            record = new SessionRecord
            {
                Title = skill.Name,
                ProfileId = profile.Id,
                Kind = profile.Kind,
                Model = model,
                Effort = profile.Effort,
                HasModelSelection = true,
                Mode = mode,
                RoleSkillId = skill.Id,
                RoleInstructions = SkillRenderer.Render(skill.Body, skill.FolderPath, string.Empty),
                RoleHandoffMode = skill.HandoffMode,
                RoleAllowedTargets = new List<string>(skill.AllowedTargets),
            };
            return true;
        }

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
