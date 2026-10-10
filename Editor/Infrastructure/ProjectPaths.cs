using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DTech.Parley.Editor
{
	internal static class ProjectPaths
	{
		public static string Root => _root ??= Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		public static string LibraryFolder => Ensure(Path.Combine(Root, "Library", "Parley"));
		public static string RunFolder => Ensure(Path.Combine(LibraryFolder, "run"));
		public static string SessionsFolder => Ensure(Path.Combine(LibraryFolder, "Sessions"));
		public static string HomeFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		private static string _root;
		
		public static string Normalize(string path)
		{
			return string.IsNullOrEmpty(path) ? path : Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
		}

		public static string Resolve(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return Root;
			}

			if (path.StartsWith("~/", StringComparison.Ordinal))
			{
				path = Path.Combine(HomeFolder, path.Substring(2));
			}

			return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));
		}

		public static bool IsInside(string path, string folder)
		{
			string normalizedPath = Normalize(path);
			string normalizedFolder = Normalize(folder);
			if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(normalizedFolder))
			{
				return false;
			}

			StringComparison comparison = CommandLine.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return normalizedPath.Equals(normalizedFolder, comparison)
				|| normalizedPath.StartsWith(normalizedFolder + "/", comparison);
		}

		public static bool IsInsideWorkspace(string path, string root, IReadOnlyList<string> additionalDirectories)
		{
			if (IsInside(path, root))
			{
				return true;
			}

			foreach (string directory in additionalDirectories)
			{
				if (!string.IsNullOrWhiteSpace(directory) && IsInside(path, Resolve(directory)))
				{
					return true;
				}
			}

			return false;
		}

		public static string ToProjectRelative(string path)
		{
			string normalized = Normalize(path);
			string root = Normalize(Root);
			if (normalized != null && normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
			{
				return normalized.Substring(root.Length + 1);
			}

			return normalized;
		}

		private static string Ensure(string folder)
		{
			Directory.CreateDirectory(folder);
			return folder;
		}
	}
}