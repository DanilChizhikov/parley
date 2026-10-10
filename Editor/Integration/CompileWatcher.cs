using UnityEditor;
using UnityEditor.Compilation;

namespace DTech.Parley.Editor
{
	[InitializeOnLoad]
	internal static class CompileWatcher
	{
		static CompileWatcher()
		{
			CompilationPipeline.compilationStarted += StartedHandler;
			CompilationPipeline.compilationFinished += FinishedHandler;
		}

		public static int StartedCount { get; private set; }

		public static int FinishedCount { get; private set; }

		public static bool IsCompiling => EditorApplication.isCompiling || StartedCount > FinishedCount;

		private static void StartedHandler(object context)
		{
			StartedCount++;
		}

		private static void FinishedHandler(object context)
		{
			FinishedCount++;
		}
	}
}