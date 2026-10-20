using System.Collections.Generic;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelItem
	{
		public PanelItemType Type { get; set; }
		public string Location { get; set; }
		public string Text { get; set; }
		public string Label { get; set; }
		public PanelHelpLevel Level { get; set; }
		public string Target { get; set; }
		public string Component { get; set; }
		public string Path { get; set; }
		public bool Foldout { get; set; }
		public List<PanelItem> Items { get; } = new ();
		public string Query { get; set; }
		public string Folder { get; set; }
		public List<string> Columns { get; } = new ();
		public PanelAction Action { get; set; }
		public string Menu { get; set; }
		public string AssetType { get; set; }
		public string AssetPath { get; set; }
	}
}
