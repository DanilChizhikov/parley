using System.Diagnostics;
using DTech.Parley.Editor;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ProcessReaperTests
    {
        [Test]
        public void ChildProcessIsTrackedWhileRunning()
        {
            if (CommandLine.IsWindows)
            {
                Assert.Ignore("Uses /bin/sleep.");
            }

            ChildProcess process = new ChildProcess(new ProcessStartInfo("/bin/sleep", "60"));
            try
            {
                Assert.IsTrue(process.Start(out string error), error);
                Assert.IsTrue(ProcessReaper.IsTracked(process.ProcessId));
            }
            finally
            {
                process.Dispose();
            }

            Assert.IsFalse(ProcessReaper.IsTracked(process.ProcessId));
        }

        [Test]
        public void UntrackRemovesProcess()
        {
            if (CommandLine.IsWindows)
            {
                Assert.Ignore("Uses /bin/sleep.");
            }

            using Process process = Process.Start(new ProcessStartInfo("/bin/sleep", "60") { UseShellExecute = false });
            Assert.IsNotNull(process);
            try
            {
                ProcessReaper.Track(process);
                Assert.IsTrue(ProcessReaper.IsTracked(process.Id));
                ProcessReaper.Untrack(process);
                Assert.IsFalse(ProcessReaper.IsTracked(process.Id));
            }
            finally
            {
                ProcessReaper.Untrack(process);
                process.Kill();
            }
        }
    }
}
