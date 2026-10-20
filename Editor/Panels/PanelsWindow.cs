using System.Collections.Generic;
using DTech.Parley.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelsWindow : EditorWindow
	{
		private const string MenuPath = "Window/DTech/Parley Panels";
		private const string WindowTitle = "Parley Panels";
		private const float MinWidth = 520.0f;
		private const float MinHeight = 200.0f;

		private ScrollView _list;

		[MenuItem(MenuPath)]
		public static void Open()
		{
			PanelsWindow window = GetWindow<PanelsWindow>();
			window.titleContent = new GUIContent(WindowTitle);
			window.minSize = new Vector2(MinWidth, MinHeight);
			window.Show();
		}

		private void OnEnable()
		{
			PanelStore.Default.OnChanged -= StoreChangedHandler;
			PanelStore.Default.OnChanged += StoreChangedHandler;
		}

		private void OnDisable()
		{
			PanelStore.Default.OnChanged -= StoreChangedHandler;
		}

		private void OnFocus()
		{
			Refresh();
		}

		private void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			ParleyStyles.Apply(root);
			root.AddToClassList("pl-panel");
			VisualElement toolbar = new VisualElement();
			toolbar.AddToClassList("pl-panel__toolbar");
			toolbar.Add(ParleyStyles.Text("Saved in UserSettings/Parley/Panels. Ask the agent to build one with panel_open.", ParleyStyles.Muted));
			toolbar.Add(ParleyStyles.Spacer());
			toolbar.Add(ParleyStyles.Button("Refresh", Refresh, "pl-button--small"));
			root.Add(toolbar);
			_list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			_list.AddToClassList("pl-panel__scroll");
			root.Add(_list);
			Refresh();
		}

		private void Refresh()
		{
			if (_list == null)
			{
				return;
			}

			_list.Clear();
			PanelStore store = PanelStore.Default;
			IReadOnlyList<string> names = store.List();
			if (names.Count == 0)
			{
				_list.Add(ParleyStyles.Text("No saved panels yet.", ParleyStyles.Muted));
				return;
			}

			foreach (string name in names)
			{
				string title = store.TryLoad(name, out string json) ? PanelWindow.TitleOf(json) : null;
				VisualElement row = new VisualElement();
				row.AddToClassList("pl-panel__list-row");
				row.Add(ParleyStyles.Text(string.IsNullOrEmpty(title) ? name : title + "  ·  " + name, "pl-panel__list-name"));
				row.Add(ParleyStyles.Button("Open", () => PanelWindow.TryOpenSaved(name, out _), "pl-button--small"));
				row.Add(ParleyStyles.Button("Reveal", () => EditorUtility.RevealInFinder(store.PathFor(name)), "pl-button--small"));
				row.Add(ParleyStyles.Button("Delete", () => Delete(name), "pl-button--small"));
				_list.Add(row);
			}
		}

		private static void Delete(string name)
		{
			if (EditorUtility.DisplayDialog(WindowTitle, "Delete panel '" + name + "'?", "Delete", "Cancel"))
			{
				PanelStore.Default.Delete(name);
			}
		}

		private void StoreChangedHandler(string slug)
		{
			Refresh();
		}
	}
}
