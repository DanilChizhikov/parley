using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelViewState
	{
		public Vector2 Scroll { get; set; }

		private readonly Dictionary<string, bool> _values = new (StringComparer.Ordinal);
		private readonly Dictionary<string, bool> _previous = new (StringComparer.Ordinal);
		private readonly Dictionary<string, Foldout> _bound = new (StringComparer.Ordinal);

		public static string ComponentKey(string location, string typeName, int index)
		{
			return location + "/" + typeName + "#" + index;
		}

		public static string RepeatKey(string location, string assetKey)
		{
			return location + "/" + assetKey;
		}

		public void BeginBuild()
		{
			// Read the live foldouts instead of listening for change events: a Foldout raises its
			// ChangeEvent from the inner toggle and the event bubbles through ancestor foldouts.
			foreach (KeyValuePair<string, Foldout> pair in _bound)
			{
				if (pair.Value != null)
				{
					_values[pair.Key] = pair.Value.value;
				}
			}

			_previous.Clear();
			foreach (KeyValuePair<string, bool> pair in _values)
			{
				_previous[pair.Key] = pair.Value;
			}

			_values.Clear();
			_bound.Clear();
		}

		public void Bind(Foldout foldout, string key)
		{
			bool open = !_previous.TryGetValue(key, out bool stored) || stored;
			foldout.value = open;
			_values[key] = open;
			_bound[key] = foldout;
		}
	}
}
