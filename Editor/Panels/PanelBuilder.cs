using System;
using System.Collections.Generic;
using DTech.Parley.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelBuilder : IDisposable
	{
		public const int TableRowLimit = 200;

		private readonly PanelSpec _spec;
		private readonly Action _rebuild;
		private readonly Dictionary<Object, SerializedObject> _objects = new ();

		public PanelBuilder(PanelSpec spec, Action rebuild)
		{
			_spec = spec;
			_rebuild = rebuild;
		}

		public VisualElement Build()
		{
			VisualElement root = new VisualElement();
			root.AddToClassList("pl-panel__content");
			AddItems(root, _spec.Items);
			return root;
		}

		public void Dispose()
		{
			foreach (SerializedObject serializedObject in _objects.Values)
			{
				serializedObject.Dispose();
			}

			_objects.Clear();
		}

		private void AddItems(VisualElement parent, List<PanelItem> items)
		{
			foreach (PanelItem item in items)
			{
				parent.Add(BuildItem(item));
			}
		}

		private VisualElement BuildItem(PanelItem item)
		{
			switch (item.Type)
			{
				case PanelItemType.Header:
					return ParleyStyles.Text(item.Text, "pl-panel__header");
				case PanelItemType.Label:
					return ParleyStyles.Text(item.Text, "pl-panel__label");
				case PanelItemType.Help:
					return new HelpBox(item.Text, ToMessageType(item.Level));
				case PanelItemType.Separator:
					VisualElement separator = new VisualElement();
					separator.AddToClassList("pl-panel__separator");
					return separator;
				case PanelItemType.Field:
					return BuildField(item);
				case PanelItemType.Inspector:
					return BuildInspector(item);
				case PanelItemType.Group:
					return BuildGroup(item);
				case PanelItemType.Row:
					return BuildRow(item);
				case PanelItemType.Table:
					return BuildTable(item);
				case PanelItemType.Button:
					return ParleyStyles.Button(item.Label, () => RunAction(item), "pl-panel__button");
				default:
					return Error(item.Location + ": unsupported item.");
			}
		}

		private VisualElement BuildField(PanelItem item)
		{
			Object target = PanelTargets.Resolve(_spec, item.Target, item.Component);
			if (target == null)
			{
				return Missing(item);
			}

			SerializedObject serializedObject = Serialized(target);
			SerializedProperty property = serializedObject.FindProperty(item.Path);
			if (property == null)
			{
				return Error(PanelTargets.CheckProperty(target, item.Path));
			}

			PropertyField field = new PropertyField(property, item.Label);
			field.AddToClassList("pl-panel__field");
			field.Bind(serializedObject);
			return field;
		}

		private VisualElement BuildInspector(PanelItem item)
		{
			Object target = PanelTargets.Resolve(_spec, item.Target, item.Component);
			if (target == null)
			{
				return Missing(item);
			}

			if (target is not GameObject gameObject)
			{
				return new InspectorElement(target);
			}

			VisualElement container = new VisualElement();
			foreach (Component component in gameObject.GetComponents<Component>())
			{
				if (component == null)
				{
					container.Add(new HelpBox("Missing script", HelpBoxMessageType.Warning));
					continue;
				}

				Foldout foldout = new Foldout { text = component.GetType().Name, value = true };
				foldout.Add(new InspectorElement(component));
				container.Add(foldout);
			}

			return container;
		}

		private VisualElement BuildGroup(PanelItem item)
		{
			if (item.Foldout)
			{
				Foldout foldout = new Foldout { text = item.Label, value = true };
				foldout.AddToClassList("pl-panel__group");
				AddItems(foldout, item.Items);
				return foldout;
			}

			VisualElement group = new VisualElement();
			group.AddToClassList("pl-panel__group");
			group.Add(ParleyStyles.Text(item.Label, "pl-panel__group-title"));
			AddItems(group, item.Items);
			return group;
		}

		private VisualElement BuildRow(PanelItem item)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("pl-panel__row");
			foreach (PanelItem child in item.Items)
			{
				VisualElement element = BuildItem(child);
				element.AddToClassList("pl-panel__row-item");
				row.Add(element);
			}

			return row;
		}

		private VisualElement BuildTable(PanelItem item)
		{
			if (!string.IsNullOrEmpty(item.Folder) && !AssetDatabase.IsValidFolder(item.Folder))
			{
				return Error("Folder '" + item.Folder + "' does not exist.");
			}

			List<Object> assets = PanelTargets.FindAssets(item.Query, item.Folder);
			if (assets.Count == 0)
			{
				return new HelpBox("No assets match '" + item.Query + "'.", HelpBoxMessageType.Info);
			}

			VisualElement table = new VisualElement();
			table.AddToClassList("pl-panel__table");
			VisualElement header = TableRow(true);
			header.Add(ParleyStyles.Text("Asset", "pl-panel__table-name"));
			foreach (string column in item.Columns)
			{
				header.Add(ParleyStyles.Text(column, "pl-panel__table-cell"));
			}

			table.Add(header);
			int count = Math.Min(assets.Count, TableRowLimit);
			for (int i = 0; i < count; i++)
			{
				Object asset = assets[i];
				VisualElement row = TableRow(false);
				Button name = new Button(() => EditorGUIUtility.PingObject(asset)) { text = asset.name, tooltip = AssetDatabase.GetAssetPath(asset) };
				name.AddToClassList("pl-panel__table-name");
				row.Add(name);
				SerializedObject serializedObject = Serialized(asset);
				foreach (string column in item.Columns)
				{
					SerializedProperty property = serializedObject.FindProperty(column);
					if (property == null)
					{
						row.Add(ParleyStyles.Text("—", "pl-panel__table-cell"));
						continue;
					}

					PropertyField cell = new PropertyField(property, string.Empty);
					cell.AddToClassList("pl-panel__table-cell");
					cell.Bind(serializedObject);
					row.Add(cell);
				}

				table.Add(row);
			}

			if (assets.Count > count)
			{
				table.Add(ParleyStyles.Text("Showing first " + count + " of " + assets.Count + " assets.", ParleyStyles.Muted));
			}

			return table;
		}

		private static VisualElement TableRow(bool header)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("pl-panel__table-row");
			if (header)
			{
				row.AddToClassList("pl-panel__table-row--header");
			}

			return row;
		}

		private void RunAction(PanelItem item)
		{
			if (PanelActions.Run(_spec, item))
			{
				_rebuild?.Invoke();
			}
		}

		private SerializedObject Serialized(Object target)
		{
			if (!_objects.TryGetValue(target, out SerializedObject serializedObject))
			{
				serializedObject = new SerializedObject(target);
				_objects[target] = serializedObject;
			}

			return serializedObject;
		}

		private VisualElement Missing(PanelItem item)
		{
			string reference = item.Target != null && _spec.Targets.TryGetValue(item.Target, out string value) ? value : item.Target;
			string message = "Target '" + item.Target + "' (" + reference + ")" + (string.IsNullOrEmpty(item.Component) ? string.Empty : " component " + item.Component) + " not found.";
			VisualElement container = new VisualElement();
			container.AddToClassList("pl-panel__missing");
			container.Add(new HelpBox(message, HelpBoxMessageType.Warning));
			container.Add(ParleyStyles.Button("Retry", _rebuild, "pl-button--small"));
			return container;
		}

		private static VisualElement Error(string message)
		{
			return new HelpBox(message, HelpBoxMessageType.Error);
		}

		private static HelpBoxMessageType ToMessageType(PanelHelpLevel level)
		{
			return level switch
			{
				PanelHelpLevel.Warning => HelpBoxMessageType.Warning,
				PanelHelpLevel.Error => HelpBoxMessageType.Error,
				_ => HelpBoxMessageType.Info,
			};
		}
	}
}
