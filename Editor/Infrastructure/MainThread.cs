using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class MainThread
	{
		private const int DrainBudget = 4000;

		private static readonly ConcurrentQueue<Action> Queue = new ();
		private static readonly int MainThreadId;
		
		public static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == MainThreadId;

		static MainThread()
		{
			MainThreadId = Thread.CurrentThread.ManagedThreadId;
			EditorApplication.update += Drain;
		}

		public static void Post(Action action)
		{
			if (action != null)
			{
				Queue.Enqueue(action);
			}
		}

		private static void Drain()
		{
			int budget = DrainBudget;
			while (budget-- > 0 && Queue.TryDequeue(out Action action))
			{
				try
				{
					action();
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}
	}
}