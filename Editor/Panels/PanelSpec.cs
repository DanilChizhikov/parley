using System;
using System.Collections.Generic;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelSpec
	{
		public string Title { get; set; }
		public Dictionary<string, string> Targets { get; } = new (StringComparer.Ordinal);
		public Dictionary<string, string> Ids { get; } = new (StringComparer.Ordinal);
		public List<PanelItem> Items { get; } = new ();
		public List<PanelItem> Queries { get; } = new ();

		public int CountItems()
		{
			return Count(Items);
		}

		private static int Count(List<PanelItem> items)
		{
			int count = items.Count;
			foreach (PanelItem item in items)
			{
				count += Count(item.Items);
			}

			return count;
		}
	}
}
