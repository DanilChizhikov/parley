using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Updates
{
    internal static class ReleaseNotesLoader
    {
        private const string ReleasesPath = "/releases?per_page=100";
        private const string FullChangelogPrefix = "**Full Changelog**";

        public static async Task<List<ReleaseNote>> LoadAsync(Version current, Version latest, CancellationToken cancellationToken)
        {
            JArray releases = await GitHubApi.GetArrayAsync(ReleasesPath, cancellationToken);
            List<ReleaseNote> notes = new ();
            foreach (JToken release in releases)
            {
                if ((bool?)release["draft"] == true || (bool?)release["prerelease"] == true)
                {
                    continue;
                }

                Version version = VersionTags.Parse((string)release["tag_name"]);
                if (version == null || (current != null && version <= current) || (latest != null && version > latest))
                {
                    continue;
                }

                notes.Add(new ReleaseNote(version, (string)release["name"], CleanBody((string)release["body"]), (string)release["html_url"]));
            }

            notes.Sort(NewestFirst);
            return notes;
        }

        internal static string CleanBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder builder = new StringBuilder(body.Length);
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith(FullChangelogPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Append(line).Append('\n');
            }

            return builder.ToString().Trim();
        }

        private static int NewestFirst(ReleaseNote left, ReleaseNote right)
        {
            return right.Version.CompareTo(left.Version);
        }
    }
}
