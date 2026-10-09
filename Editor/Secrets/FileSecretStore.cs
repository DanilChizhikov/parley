using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DTech.Parley.Editor.Secrets
{
	internal sealed class FileSecretStore : ISecretStore
	{
		private readonly string _path = Path.Combine(ProjectPaths.HomeFolder, ".config", "dtech-parley", "credentials.json");

		public string Description => "File " + _path + " (0600)";

		public bool TryGet(string key, out string secret)
		{
			return Load().TryGetValue(key, out secret) && !string.IsNullOrEmpty(secret);
		}

		public bool Set(string key, string secret, out string error)
		{
			Dictionary<string, string> values = Load();
			values[key] = secret;
			return Save(values, out error);
		}

		public bool Delete(string key)
		{
			Dictionary<string, string> values = Load();
			return values.Remove(key) && Save(values, out _);
		}

		private Dictionary<string, string> Load()
		{
			try
			{
				if (File.Exists(_path))
				{
					return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new Dictionary<string, string>();
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			return new Dictionary<string, string>();
		}

		private bool Save(Dictionary<string, string> values, out string error)
		{
			try
			{
				string directoryName = Path.GetDirectoryName(_path);
				Directory.CreateDirectory(directoryName);
				string valuesJson = JsonConvert.SerializeObject(values);
				File.WriteAllText(_path, valuesJson);
				string arguments = CommandLine.Join(new[] { "600", _path }, false);
				SecretProcess.Run("chmod", arguments, null, out _, out _);
				error = null;
				return true;
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
		}
	}
}