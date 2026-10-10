using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.Parley.Editor.Updates
{
    internal interface IVersionFeed
    {
        Task<Version> FetchLatestAsync(CancellationToken cancellationToken);
    }
}
