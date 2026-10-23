namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageTotals
	{
		public int Calls { get; private set; }
		public long Input { get; private set; }
		public long CacheWrite { get; private set; }
		public long CacheRead { get; private set; }
		public long Output { get; private set; }
		public long Reasoning { get; private set; }

		public long Total => Input + CacheWrite + CacheRead + Output;

		public void Add(UsageCall call)
		{
			Calls++;
			Input += call.Input;
			CacheWrite += call.CacheWrite;
			CacheRead += call.CacheRead;
			Output += call.Output;
			Reasoning += call.Reasoning;
		}
	}
}
