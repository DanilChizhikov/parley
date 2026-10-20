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
			_previous.Clear();
			foreach (KeyValuePair<string, bool> pair in _values)
			{
				_previous[pair.Key] = pair.Value;
			}

			_values.Clear();
		}

		public void Bind(Foldout foldout, string key)
		{
			bool open = !_previous.TryGetValue(key, out bool stored) || stored;
			foldout.value = open;
			_values[key] = open;
			foldout.RegisterValueChangedCallback(evt =>
			{
				if (evt.target == foldout)
				{
					_values[key] = evt.newValue;
				}
			});
		}
	}
}
