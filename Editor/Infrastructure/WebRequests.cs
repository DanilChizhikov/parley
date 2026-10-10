using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DTech.Parley.Editor
{
	internal static class WebRequests
	{
		public static Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
		{
			UnityWebRequestAsyncOperation operation = request.SendWebRequest();
			CancellationTokenRegistration registration = default;

			registration = cancellationToken.Register(() => MainThread.Post(() =>
			{
				try
				{
					request.Abort();
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}));

			WebRequestPoll poll = new WebRequestPoll(operation, registration, cancellationToken);

			EditorApplication.update += poll.Execute;
			return poll.Task;
		}

		private sealed class WebRequestPoll
		{
			private readonly UnityWebRequestAsyncOperation _operation;
			private readonly CancellationTokenRegistration _registration;
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _completion = new ();

			public Task Task => _completion.Task;

			public WebRequestPoll(UnityWebRequestAsyncOperation operation, CancellationTokenRegistration registration, CancellationToken cancellationToken)
			{
				_operation = operation;
				_registration = registration;
				_cancellationToken = cancellationToken;
			}

			public void Execute()
			{
				if (!_operation.isDone)
				{
					return;
				}

				EditorApplication.update -= Execute;
				_registration.Dispose();
				if (_cancellationToken.IsCancellationRequested)
				{
					_completion.TrySetCanceled();
				}
				else
				{
					_completion.TrySetResult(true);
				}
			}
		}
	}
}