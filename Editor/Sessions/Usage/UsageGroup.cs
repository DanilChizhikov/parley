namespace DTech.Parley.Editor.Sessions
{
	internal sealed class UsageGroup
	{
		public string Key { get; }
		public string Label { get; }
		public UsageTotals Totals { get; } = new ();

		public UsageGroup(string key, string label)
		{
			Key = key;
			Label = label;
		}
	}
}
