using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DTech.Parley.Editor.Sessions
{
	internal static class SessionStore
	{
		private const string IndexFile = "index.json";
		private const string TempSuffix = ".tmp";
		private const int MaxTitleLength = 60;

		private static readonly JsonSerializerSettings _settings = new ()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None,
		};

		private static readonly UTF8Encoding _encoding = new (false);

		private static string IndexPath => Path.Combine(ProjectPaths.SessionsFolder, IndexFile);

		public static string MakeTitle(string text)
		{
			string title = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
			if (title.Length == 0)
			{
				return SessionRecord.DefaultTitle;
			}

			return title.Length > MaxTitleLength ? title.Substring(0, MaxTitleLength - 1) + "…" : title;
		}

		public static void Save(SessionRecord record)
		{
			if (record == null || record.Entries.Count == 0)
			{
				return;
			}

			record.UpdatedUtc = DateTime.UtcNow;
			try
			{
				WriteAtomic(PathFor(record.Id), JsonConvert.SerializeObject(record, _settings));
				List<SessionSummary> index = LoadIndex();
				index.RemoveAll(summary => summary.Id == record.Id);
				index.Insert(0, Summarize(record));
				SaveIndex(index);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning("[Parley] Failed to save session: " + exception.Message);
			}
		}

		public static SessionRecord Load(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			try
			{
				string path = PathFor(id);
				return File.Exists(path) ? JsonConvert.DeserializeObject<SessionRecord>(File.ReadAllText(path), _settings) : null;
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning("[Parley] Failed to load session " + id + ": " + exception.Message);
				return null;
			}
		}

		public static List<SessionSummary> List()
		{
			List<SessionSummary> index = LoadIndex();
			index.Sort((left, right) => right.UpdatedUtc.CompareTo(left.UpdatedUtc));
			return index;
		}

		public static void Delete(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return;
			}

			try
			{
				string path = PathFor(id);
				if (File.Exists(path))
				{
					File.Delete(path);
				}

				List<SessionSummary> index = LoadIndex();
				if (index.RemoveAll(summary => summary.Id == id) > 0)
				{
					SaveIndex(index);
				}
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogWarning("[Parley] Failed to delete session " + id + ": " + exception.Message);
			}
		}

		private static SessionSummary Summarize(SessionRecord record)
		{
			return new SessionSummary { Id = record.Id, Title = record.Title, ProfileId = record.ProfileId, UpdatedUtc = record.UpdatedUtc };
		}

		private static string PathFor(string id)
		{
			StringBuilder safe = new StringBuilder();
			foreach (char character in id)
			{
				if (char.IsLetterOrDigit(character) || character == '-')
				{
					safe.Append(character);
				}
			}

			return Path.Combine(ProjectPaths.SessionsFolder, safe + ".json");
		}

		private static List<SessionSummary> LoadIndex()
		{
			string path = IndexPath;
			if (File.Exists(path))
			{
				try
				{
					return JsonConvert.DeserializeObject<List<SessionSummary>>(File.ReadAllText(path)) ?? new List<SessionSummary>();
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogWarning("[Parley] Session index is unreadable, rebuilding it: " + exception.Message);
				}
			}

			List<SessionSummary> index = RebuildIndex();
			if (index.Count > 0)
			{
				try
				{
					SaveIndex(index);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogWarning("[Parley] Failed to save rebuilt session index: " + exception.Message);
				}
			}

			return index;
		}

		private static List<SessionSummary> RebuildIndex()
		{
			List<SessionSummary> index = new ();
			foreach (string file in Directory.GetFiles(ProjectPaths.SessionsFolder, "*.json"))
			{
				if (Path.GetFileName(file) == IndexFile)
				{
					continue;
				}

				SessionRecord record = Load(Path.GetFileNameWithoutExtension(file));
				if (record != null)
				{
					index.Add(Summarize(record));
				}
			}

			return index;
		}

		private static void SaveIndex(List<SessionSummary> index)
		{
			WriteAtomic(IndexPath, JsonConvert.SerializeObject(index, Formatting.Indented));
		}

		private static void WriteAtomic(string path, string content)
		{
			string temp = path + TempSuffix;
			File.WriteAllText(temp, content, _encoding);
			if (File.Exists(path))
			{
				File.Replace(temp, path, null);
			}
			else
			{
				File.Move(temp, path);
			}
		}
	}
}