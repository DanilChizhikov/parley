using System;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace DTech.Parley.Editor.Updates
{
    internal readonly struct InstalledPackage
    {
        public string Name { get; }

        public Version Current { get; }

        public PackageSource Source { get; }

        public string GitUrl { get; }

        public InstalledPackage(string name, Version current, PackageSource source, string gitUrl)
        {
            Name = name;
            Current = current;
            Source = source;
            GitUrl = gitUrl;
        }

        public static bool TryResolve(out InstalledPackage package)
        {
            package = default;
            PackageInfo info = PackageInfo.FindForAssembly(typeof(InstalledPackage).Assembly);
            if (info == null)
            {
                return false;
            }

            Version current = VersionTags.Parse(info.version);
            if (current == null)
            {
                return false;
            }

            string gitUrl = info.source == PackageSource.Git ? GitUrlFromPackageId(info.packageId) : string.Empty;
            package = new InstalledPackage(info.name, current, info.source, gitUrl);
            return true;
        }

        internal static string GitUrlFromPackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return string.Empty;
            }

            int at = packageId.IndexOf('@');
            string url = at >= 0 ? packageId.Substring(at + 1) : packageId;
            int fragment = url.LastIndexOf('#');
            return fragment >= 0 ? url.Substring(0, fragment) : url;
        }
    }
}
