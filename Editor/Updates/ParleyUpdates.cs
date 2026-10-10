using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace DTech.Parley.Editor.Updates
{
    internal static class ParleyUpdates
    {
        public static event Action OnChanged;

        private const string CheckedKey = "DTech.Parley.Updates.Checked";
        private const string LatestKey = "DTech.Parley.Updates.Latest";

        public static UpdateStatus Status { get; private set; }

        public static Version Current => _package.Current;

        public static Version Latest { get; private set; }

        private static InstalledPackage _package;

        public static void EnsureChecked()
        {
            if (Status != UpdateStatus.Unknown)
            {
                return;
            }

            IVersionFeed feed = InstalledPackage.TryResolve(out _package) ? CreateFeed(_package) : null;
            if (feed == null)
            {
                SetStatus(UpdateStatus.Unsupported);
                return;
            }

            if (SessionState.GetBool(CheckedKey, false))
            {
                Apply(VersionTags.Parse(SessionState.GetString(LatestKey, string.Empty)));
                return;
            }

            _ = CheckAsync(feed);
        }

        public static async Task InstallAsync()
        {
            if (Status != UpdateStatus.Available)
            {
                return;
            }

            SetStatus(UpdateStatus.Installing);
            try
            {
                await PackageInstaller.InstallAsync(_package, Latest);
                SetStatus(UpdateStatus.UpToDate);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Parley] Update to v" + Latest + " failed: " + exception.Message);
                SetStatus(UpdateStatus.Available);
            }
        }

        private static IVersionFeed CreateFeed(InstalledPackage package)
        {
            switch (package.Source)
            {
                case PackageSource.Git:
                    return string.IsNullOrEmpty(package.GitUrl) ? null : new GitTagFeed();
                case PackageSource.Registry:
                    return new RegistryFeed(package.Name);
                default:
                    return null;
            }
        }

        private static async Task CheckAsync(IVersionFeed feed)
        {
            SetStatus(UpdateStatus.Checking);
            try
            {
                Version latest = await feed.FetchLatestAsync(CancellationToken.None);
                SessionState.SetBool(CheckedKey, true);
                SessionState.SetString(LatestKey, latest == null ? string.Empty : latest.ToString());
                Apply(latest);
            }
            catch (Exception exception)
            {
                SessionState.SetBool(CheckedKey, true);
                Debug.LogWarning("[Parley] Update check failed: " + exception.Message);
                SetStatus(UpdateStatus.Failed);
            }
        }

        private static void Apply(Version latest)
        {
            Latest = latest;
            SetStatus(latest != null && latest > _package.Current ? UpdateStatus.Available : UpdateStatus.UpToDate);
        }

        private static void SetStatus(UpdateStatus status)
        {
            Status = status;
            OnChanged?.Invoke();
        }
    }
}
