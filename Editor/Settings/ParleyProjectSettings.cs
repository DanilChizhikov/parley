using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Settings
{
	[FilePath("ProjectSettings/ParleySettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class ParleyProjectSettings : ScriptableSingleton<ParleyProjectSettings>
	{
		public string AppendSystemPrompt
		{
			get => _appendSystemPrompt;
			set
			{
				_appendSystemPrompt = value;
				Persist();
			}
		}

		public bool UnityToolsEnabled
		{
			get => _unityToolsEnabled;
			set
			{
				_unityToolsEnabled = value;
				Persist();
			}
		}

		public bool IncludeProjectInstructions
		{
			get => _includeProjectInstructions;
			set
			{
				_includeProjectInstructions = value;
				Persist();
			}
		}

		public IReadOnlyList<string> AdditionalDirectories => _additionalDirectories;

		public IReadOnlyList<string> DisabledUnityTools => _disabledUnityTools;

		[SerializeField] private string _appendSystemPrompt = string.Empty;
		[SerializeField] private bool _unityToolsEnabled = true;
		[SerializeField] private bool _includeProjectInstructions = true;
		[SerializeField] private List<string> _additionalDirectories = new ();
		[SerializeField] private List<string> _disabledUnityTools = new ();

		public void SetAdditionalDirectories(IEnumerable<string> directories)
		{
			_additionalDirectories = new List<string>(directories);
			Persist();
		}

		public void SetUnityToolEnabled(string name, bool enabled)
		{
			bool disabled = _disabledUnityTools.Contains(name);
			if (enabled && disabled)
			{
				_disabledUnityTools.Remove(name);
			}
			else if (!enabled && !disabled)
			{
				_disabledUnityTools.Add(name);
			}

			Persist();
		}

		public bool IsUnityToolEnabled(string name)
		{
			return _unityToolsEnabled && !_disabledUnityTools.Contains(name);
		}

		private void Persist()
		{
			Save(true);
		}
	}
}
