using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DTech.Parley.Editor.Secrets
{
	internal sealed class FileSecretStore : ISecretStore
	{
		private const string ChmodTool = "/bin/chmod";
		private const string PrivateFolderMode = "700";
		private const string PrivateFileMode = "600";
		private const string TempSuffix = ".tmp";

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

		private static bool Chmod(string mode, string path)
		{
			return SecretProcess.Run(ChmodTool, CommandLine.Join(new[] { mode, path }, false), null).ExitCode == 0;
		}

		private static void DeleteQuietly(string path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				Debug.LogWarning("[Parley] Could not delete " + path + ": " + exception.Message);
			}
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
			string temp = _path + TempSuffix;
			try
			{
				string directoryName = Path.GetDirectoryName(_path);
				Directory.CreateDirectory(directoryName);
				Chmod(PrivateFolderMode, directoryName);
				File.WriteAllText(temp, string.Empty);
				if (!Chmod(PrivateFileMode, temp))
				{
					DeleteQuietly(temp);
					error = "Could not restrict permissions of " + temp + ".";
					return false;
				}

				File.WriteAllText(temp, JsonConvert.SerializeObject(values));
				if (File.Exists(_path))
				{
					File.Replace(temp, _path, null);
				}
				else
				{
					File.Move(temp, _path);
				}

				error = null;
				return true;
			}
			catch (Exception exception)
			{
				DeleteQuietly(temp);
				error = exception.Message;
				return false;
			}
		}
	}
}
