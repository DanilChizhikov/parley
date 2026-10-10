using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Settings
{
	[FilePath("ProjectSettings/ParleySettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class ParleyProjectSettings : ScriptableSingleton<ParleyProjectSettings>
	{
		private const char FieldSeparator = '\u0001';
		private const char ItemSeparator = '\u0002';

		public string AppendSystemPrompt => IsTrusted ? _appendSystemPrompt : string.Empty;

		public string ConfiguredAppendSystemPrompt
		{
			get => _appendSystemPrompt;
			set
			{
				_appendSystemPrompt = value;
				PersistTrusted();
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

		public IReadOnlyList<string> AdditionalDirectories => IsTrusted ? (IReadOnlyList<string>)_additionalDirectories : Array.Empty<string>();

		public IReadOnlyList<string> ConfiguredAdditionalDirectories => _additionalDirectories;

		public IReadOnlyList<string> InstructionFiles => IsTrusted ? (IReadOnlyList<string>)_instructionFiles : Array.Empty<string>();

		public IReadOnlyList<string> ConfiguredInstructionFiles => _instructionFiles;

		public IReadOnlyList<string> DisabledUnityTools => _disabledUnityTools;

		public bool IsTrusted => Fingerprint.Length == 0 || ParleyUserSettings.instance.IsTrusted(ProjectPaths.Root, Fingerprint);

		public string Fingerprint => _fingerprint ??= ComputeFingerprint();

		[SerializeField] private string _appendSystemPrompt = string.Empty;
		[SerializeField] private bool _unityToolsEnabled = true;
		[SerializeField] private bool _includeProjectInstructions = true;
		[SerializeField] private List<string> _additionalDirectories = new ();
		[SerializeField] private List<string> _instructionFiles = new ();
		[SerializeField] private List<string> _disabledUnityTools = new ();

		[NonSerialized] private string _fingerprint;

		public void SetAdditionalDirectories(IEnumerable<string> directories)
		{
			_additionalDirectories = new List<string>(directories);
			PersistTrusted();
		}

		public void SetInstructionFiles(IEnumerable<string> files)
		{
			_instructionFiles = new List<string>(files);
			PersistTrusted();
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

		public void Trust()
		{
			ParleyUserSettings.instance.Trust(ProjectPaths.Root, Fingerprint);
		}

		private static void AppendItems(StringBuilder builder, List<string> items)
		{
			builder.Append(FieldSeparator);
			foreach (string item in items)
			{
				builder.Append(item).Append(ItemSeparator);
			}
		}

		private void OnEnable()
		{
			_fingerprint = null;
		}

		private void Persist()
		{
			_fingerprint = null;
			Save(true);
		}

		private void PersistTrusted()
		{
			Persist();
			Trust();
		}

		private string ComputeFingerprint()
		{
			if (string.IsNullOrWhiteSpace(_appendSystemPrompt) && _additionalDirectories.Count == 0 && _instructionFiles.Count == 0)
			{
				return string.Empty;
			}

			StringBuilder builder = new StringBuilder(_appendSystemPrompt ?? string.Empty);
			AppendItems(builder, _additionalDirectories);
			AppendItems(builder, _instructionFiles);
			using SHA256 sha = SHA256.Create();
			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
			StringBuilder hex = new StringBuilder(hash.Length * 2);
			foreach (byte value in hash)
			{
				hex.Append(value.ToString("x2"));
			}

			return hex.ToString();
		}
	}
}
