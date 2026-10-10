using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager.Requests;

namespace DTech.Parley.Editor
{
    internal static class PackageRequests
    {
        public static Task WaitAsync(Request request, CancellationToken cancellationToken)
        {
            RequestPoll poll = new RequestPoll(request, cancellationToken);
            EditorApplication.update += poll.Execute;
            return poll.Task;
        }

        private sealed class RequestPoll
        {
            private readonly Request _request;
            private readonly CancellationToken _cancellationToken;
            private readonly TaskCompletionSource<bool> _completion = new ();

            public Task Task => _completion.Task;

            public RequestPoll(Request request, CancellationToken cancellationToken)
            {
                _request = request;
                _cancellationToken = cancellationToken;
            }

            public void Execute()
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    EditorApplication.update -= Execute;
                    _completion.TrySetCanceled();
                    return;
                }

                if (!_request.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= Execute;
                _completion.TrySetResult(true);
            }
        }
    }
}
