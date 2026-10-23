using System;

namespace DTech.Parley.Editor
{
	internal readonly struct CliCandidate
	{
		public readonly string Path;
		public readonly Version Version;

		public CliCandidate(string path, Version version)
		{
			Path = path;
			Version = version;
		}
	}
}
