using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace DTech.Parley.Editor.Updates
{
    internal static class GitHubApi
    {
        public const string ReleasesPage = "https://github.com/DanilChizhikov/parley/releases";

        private const string RepositoryUrl = "https://api.github.com/repos/DanilChizhikov/parley";
        private const int TimeoutSeconds = 15;

        public static async Task<JArray> GetArrayAsync(string path, CancellationToken cancellationToken)
        {
            using UnityWebRequest request = UnityWebRequest.Get(RepositoryUrl + path);
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "DTech.Parley");
            await WebRequests.SendAsync(request, cancellationToken);
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException("GET " + path + " failed: " + request.error);
            }

            return JArray.Parse(request.downloadHandler.text);
        }
    }
}
