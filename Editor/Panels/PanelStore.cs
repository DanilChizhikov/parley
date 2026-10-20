using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelStore
	{
		public event Action<string> OnChanged;
		
		public const string Extension = ".json";
		public const int MaxNameLength = 64;

		public static PanelStore Default => _default ??= new PanelStore(ProjectPaths.PanelsFolder);
		
		private static PanelStore _default;
		
		public string Folder { get; }

		public PanelStore(string folder)
		{
			Folder = folder;
		}

		public static string Slug(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			StringBuilder builder = new StringBuilder();
			foreach (char character in name.Trim().ToLowerInvariant())
			{
				if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_')
				{
					builder.Append(character);
				}
				else if ((character == '-' || char.IsWhiteSpace(character) || character == '.') && builder.Length > 0 && builder[builder.Length - 1] != '-')
				{
					builder.Append('-');
				}

				if (builder.Length >= MaxNameLength)
				{
					break;
				}
			}

			string slug = builder.ToString().Trim('-');
			return slug.Length == 0 ? null : slug;
		}

		public string PathFor(string name)
		{
			string slug = Slug(name);
			return slug == null ? null : Path.Combine(Folder, slug + Extension);
		}

		public bool Exists(string name)
		{
			string path = PathFor(name);
			return path != null && File.Exists(path);
		}

		public IReadOnlyList<string> List()
		{
			List<string> names = new ();
			if (!Directory.Exists(Folder))
			{
				return names;
			}

			foreach (string file in Directory.GetFiles(Folder, "*" + Extension))
			{
				names.Add(Path.GetFileNameWithoutExtension(file));
			}

			names.Sort(StringComparer.Ordinal);
			return names;
		}

		public bool TryLoad(string name, out string json)
		{
			json = null;
			string path = PathFor(name);
			if (path == null || !File.Exists(path))
			{
				return false;
			}

			json = File.ReadAllText(path);
			return true;
		}

		public string Save(string name, string json)
		{
			string slug = Slug(name);
			if (slug == null)
			{
				throw new ArgumentException("Panel name must contain letters or digits.", nameof(name));
			}

			Directory.CreateDirectory(Folder);
			File.WriteAllText(Path.Combine(Folder, slug + Extension), json ?? string.Empty);
			OnChanged?.Invoke(slug);
			return slug;
		}

		public bool Delete(string name)
		{
			string path = PathFor(name);
			if (path == null || !File.Exists(path))
			{
				return false;
			}

			File.Delete(path);
			OnChanged?.Invoke(Slug(name));
			return true;
		}
	}
}
