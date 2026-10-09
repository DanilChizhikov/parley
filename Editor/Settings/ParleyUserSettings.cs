using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor
{
	[FilePath("DTech/Parley/Settings.asset", FilePathAttribute.Location.PreferencesFolder)]
	internal sealed class ParleyUserSettings : ScriptableSingleton<ParleyUserSettings>
	{
		public event Action OnChanged;

		public IReadOnlyList<ParleyProfile> Profiles
		{
			get
			{
				EnsureDefaults();
				return _profiles;
			}
		}

		public ParleyProfile ActiveProfile
		{
			get
			{
				EnsureDefaults();
				return Find(_activeProfileId) ?? _profiles[0];
			}
		}

		public string CliPathOverride
		{
			get => _cliPathOverride;
			set => Set(ref _cliPathOverride, value);
		}

		public string CodexCliPathOverride
		{
			get => _codexCliPathOverride;
			set => Set(ref _codexCliPathOverride, value);
		}

		public PermissionMode DefaultMode
		{
			get => _defaultMode;
			set => Set(ref _defaultMode, value);
		}

		public bool LockReloadDuringTurn
		{
			get => _lockReloadDuringTurn;
			set => Set(ref _lockReloadDuringTurn, value);
		}

		public bool OpenLoginLinks
		{
			get => _openLoginLinks;
			set => Set(ref _openLoginLinks, value);
		}

		public bool ShowThinking
		{
			get => _showThinking;
			set => Set(ref _showThinking, value);
		}

		public bool SidePanelVisible
		{
			get => _sidePanelVisible;
			set => Set(ref _sidePanelVisible, value);
		}

		public float SidePanelWidth
		{
			get => _sidePanelWidth < 160.0f ? 260.0f : _sidePanelWidth;
			set => Set(ref _sidePanelWidth, value);
		}

		public string LastSessionId
		{
			get => _lastSessionId;
			set => Set(ref _lastSessionId, value);
		}

		[SerializeField] private List<ParleyProfile> _profiles = new ();
		[SerializeField] private string _activeProfileId;
		[SerializeField] private string _cliPathOverride;
		[SerializeField] private string _codexCliPathOverride;
		[SerializeField] private PermissionMode _defaultMode = PermissionMode.Default;
		[SerializeField] private bool _lockReloadDuringTurn = true;
		[SerializeField] private bool _openLoginLinks = true;
		[SerializeField] private bool _showThinking = true;
		[SerializeField] private bool _sidePanelVisible = true;
		[SerializeField] private float _sidePanelWidth = 260.0f;
		[SerializeField] private string _lastSessionId;
		[SerializeField] private List<AllowRule> _allowRules = new ();

		private bool _saveQueued;

		public ParleyProfile Find(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			foreach (ParleyProfile profile in _profiles)
			{
				if (profile.Id == id)
				{
					return profile;
				}
			}

			return null;
		}

		public void SetActive(ParleyProfile profile)
		{
			if (profile != null)
			{
				Set(ref _activeProfileId, profile.Id);
			}
		}

		public void Add(ParleyProfile profile)
		{
			_profiles.Add(profile);
			MarkDirty();
		}

		public void Remove(ParleyProfile profile)
		{
			if (_profiles.Count <= 1 || !_profiles.Remove(profile))
			{
				return;
			}

			if (_activeProfileId == profile.Id)
			{
				_activeProfileId = _profiles[0].Id;
			}

			MarkDirty();
		}

		public IEnumerable<AllowRule> RulesFor(string projectRoot)
		{
			foreach (AllowRule rule in _allowRules)
			{
				if (rule.ProjectRoot == projectRoot)
				{
					yield return rule;
				}
			}
		}

		public void AddRule(AllowRule rule)
		{
			foreach (AllowRule existing in _allowRules)
			{
				if (existing.ProjectRoot == rule.ProjectRoot && existing.Tool == rule.Tool && existing.Pattern == rule.Pattern)
				{
					return;
				}
			}

			_allowRules.Add(rule);
			MarkDirty();
		}

		public void RemoveRule(AllowRule rule)
		{
			if (_allowRules.Remove(rule))
			{
				MarkDirty();
			}
		}

		public void MarkDirty()
		{
			OnChanged?.Invoke();
			if (_saveQueued)
			{
				return;
			}

			_saveQueued = true;
			EditorApplication.delayCall += () =>
			{
				_saveQueued = false;
				Save(true);
			};
		}

		private void EnsureDefaults()
		{
			if (_profiles.Count > 0)
			{
				return;
			}

			_profiles.Add(ParleyProfile.CreateClaude("Claude Code", ClaudeAuthMethod.CliDefault));
			_profiles.Add(ParleyProfile.CreateCodex("Codex", CodexAuthMethod.CliDefault));
			_profiles.Add(ParleyProfile.CreateLocal("LM Studio", LocalPreset.LmStudio));
			_profiles.Add(ParleyProfile.CreateLocal("Ollama", LocalPreset.Ollama));
			_activeProfileId = _profiles[0].Id;
			MarkDirty();
		}

		private void Set<T>(ref T field, T value)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return;
			}

			field = value;
			MarkDirty();
		}
	}
}