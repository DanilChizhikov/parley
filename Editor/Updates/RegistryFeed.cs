using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace DTech.Parley.Editor.Updates
{
    internal sealed class RegistryFeed : IVersionFeed
    {
        private readonly string _packageName;

        public RegistryFeed(string packageName)
        {
            _packageName = packageName;
        }

        public async Task<Version> FetchLatestAsync(CancellationToken cancellationToken)
        {
            SearchRequest request = Client.Search(_packageName);
            await PackageRequests.WaitAsync(request, cancellationToken);
            if (request.Status != StatusCode.Success)
            {
                throw new InvalidOperationException(request.Error?.message ?? "Package search failed.");
            }

            Version latest = null;
            foreach (PackageInfo info in request.Result)
            {
                foreach (string version in info.versions.compatible)
                {
                    latest = VersionTags.Max(latest, VersionTags.Parse(version));
                }
            }

            return latest;
        }
    }
}
