using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Updates
{
    internal sealed class GitTagFeed : IVersionFeed
    {
        private const string TagsPath = "/tags?per_page=100";

        public async Task<Version> FetchLatestAsync(CancellationToken cancellationToken)
        {
            JArray tags = await GitHubApi.GetArrayAsync(TagsPath, cancellationToken);
            Version latest = null;
            foreach (JToken tag in tags)
            {
                latest = VersionTags.Max(latest, VersionTags.Parse((string)tag["name"]));
            }

            return latest;
        }
    }
}
