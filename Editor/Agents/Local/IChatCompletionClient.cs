using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Local
{
    internal interface IChatCompletionClient
    {
        Task<List<string>> ListModelsAsync(CancellationToken cancellationToken);
        Task StreamChatAsync(JObject body, Action<JObject> onChunk, CancellationToken cancellationToken);
    }
}