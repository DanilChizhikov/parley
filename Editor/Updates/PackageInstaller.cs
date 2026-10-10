using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace DTech.Parley.Editor.Updates
{
    internal static class PackageInstaller
    {
        public static async Task InstallAsync(InstalledPackage package, Version version)
        {
            AddRequest request = Client.Add(Identifier(package, version));
            await PackageRequests.WaitAsync(request, CancellationToken.None);
            if (request.Status != StatusCode.Success)
            {
                throw new InvalidOperationException(request.Error?.message ?? "Package update failed.");
            }

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }

        internal static string Identifier(InstalledPackage package, Version version)
        {
            return package.Source == PackageSource.Git
                ? package.GitUrl + "#v" + version
                : package.Name + "@" + version;
        }
    }
}
