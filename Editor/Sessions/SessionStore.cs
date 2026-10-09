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
		private const int MaxTitleLength = 60;

		private static readonly JsonSerializerSettings _settings = new ()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None,
		};

		public static string MakeTitle(string text)
		{
			string title = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
			if (title.Length == 0)
			{
				return "New chat";
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
				string path = PathFor(record.Id);
				string temp = path + ".tmp";
				File.WriteAllText(temp, JsonConvert.SerializeObject(record, _settings), new UTF8Encoding(false));
				if (File.Exists(path))
				{
					File.Delete(path);
				}

				File.Move(temp, path);
				List<SessionSummary> index = LoadIndex();
				index.RemoveAll(summary => summary.Id == record.Id);
				index.Insert(0, new SessionSummary { Id = record.Id, Title = record.Title, ProfileId = record.ProfileId, UpdatedUtc = record.UpdatedUtc });
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
			try
			{
				string path = PathFor(id);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception)
			{
			}

			List<SessionSummary> index = LoadIndex();
			if (index.RemoveAll(summary => summary.Id == id) > 0)
			{
				SaveIndex(index);
			}
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
			string path = Path.Combine(ProjectPaths.SessionsFolder, IndexFile);
			try
			{
				if (File.Exists(path))
				{
					return JsonConvert.DeserializeObject<List<SessionSummary>>(File.ReadAllText(path)) ?? new List<SessionSummary>();
				}
			}
			catch (Exception)
			{
			}

			return new List<SessionSummary>();
		}

		private static void SaveIndex(List<SessionSummary> index)
		{
			File.WriteAllText(Path.Combine(ProjectPaths.SessionsFolder, IndexFile), JsonConvert.SerializeObject(index, Formatting.Indented), new UTF8Encoding(false));
		}
	}
}