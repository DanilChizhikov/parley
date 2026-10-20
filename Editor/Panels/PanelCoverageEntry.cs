namespace DTech.Parley.Editor.Panels
{
	internal readonly struct PanelCoverageEntry
	{
		public string TypeName { get; }
		public int Count { get; }
		public string FirstPath { get; }

		public PanelCoverageEntry(string typeName, int count, string firstPath)
		{
			TypeName = typeName;
			Count = count;
			FirstPath = firstPath;
		}
	}
}
