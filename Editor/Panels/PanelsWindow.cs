using System.Collections.Generic;
using DTech.Parley.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelsWindow : EditorWindow
	{
		private const string MenuPath = "Window/DTech/Parley/Panels";
		private const string WindowTitle = "Parley Panels";
		private const string DialogTitle = "Parley";
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
			minSize = new Vector2(MinWidth, MinHeight);
			PanelStore.Default.OnChanged -= StoreChangedHandler;
			PanelStore.Default.OnChanged += StoreChangedHandler;
		}

		private void OnDisable()
		{
			PanelStore.Default.OnChanged -= StoreChangedHandler;
		}

		private void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			ParleyStyles.Apply(root);
			root.AddToClassList("pl-panel");
			Toolbar toolbar = new Toolbar();
			toolbar.AddToClassList("pl-panel__toolbar");
			Label hint = ParleyStyles.Text("Saved panels", "pl-panel__toolbar-title");
			hint.tooltip = "Stored in UserSettings/Parley/Panels";
			toolbar.Add(hint);
			toolbar.Add(new ToolbarSpacer { flex = true });
			toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh", tooltip = "Read the panels folder again" });
			root.Add(toolbar);
			_list = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			_list.AddToClassList("pl-panel__scroll");
			_list.AddToClassList("pl-panel__list");
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
				VisualElement empty = new VisualElement();
				empty.AddToClassList("pl-empty");
				empty.Add(ParleyStyles.Text("No saved panels yet", "pl-empty__title"));
				empty.Add(ParleyStyles.Text("In the Parley chat, type /create-parley-panel and describe what you want to tune. Save the panel it opens to see it here.",
					"pl-empty__text"));
				_list.Add(empty);
				return;
			}

			foreach (string name in names)
			{
				string title = store.TryLoad(name, out string json) ? PanelWindow.TitleOf(json) : null;
				VisualElement row = new VisualElement();
				row.AddToClassList("pl-panel__list-row");
				row.tooltip = "Double-click to open";
				row.RegisterCallback<ClickEvent>(evt =>
				{
					if (evt.clickCount == 2 && !(evt.target is Button))
					{
						OpenSaved(name);
					}
				});
				VisualElement text = new VisualElement();
				text.AddToClassList("pl-panel__list-text");
				text.Add(ParleyStyles.Text(string.IsNullOrEmpty(title) ? name : title, "pl-panel__list-name"));
				if (!string.IsNullOrEmpty(title))
				{
					text.Add(ParleyStyles.Text(name, "pl-panel__list-slug"));
				}

				row.Add(text);
				Button open = ParleyStyles.Button("Open", () => OpenSaved(name), "pl-button--small");
				open.AddToClassList("pl-button--primary");
				open.tooltip = "Open this panel";
				row.Add(open);
				Button reveal = ParleyStyles.Button("Reveal", () => EditorUtility.RevealInFinder(store.PathFor(name)), "pl-button--small");
				reveal.tooltip = "Show the panel file";
				row.Add(reveal);
				Button delete = ParleyStyles.Button("Delete", () => Delete(name), "pl-button--small");
				delete.AddToClassList("pl-button--danger");
				delete.tooltip = "Delete this panel";
				row.Add(delete);
				_list.Add(row);
			}
		}

		private static void OpenSaved(string name)
		{
			if (!PanelWindow.TryOpenSaved(name, out _))
			{
				EditorUtility.DisplayDialog(DialogTitle, "Could not open panel '" + name + "'. Its file may have been moved or deleted.", "OK");
			}
		}

		private static void Delete(string name)
		{
			if (EditorUtility.DisplayDialog(DialogTitle, "Delete panel '" + name + "'? This cannot be undone.", "Delete", "Cancel"))
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
