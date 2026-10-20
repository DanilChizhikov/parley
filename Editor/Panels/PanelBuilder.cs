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
		public const int RepeatLimit = 50;

		private readonly PanelSpec _spec;
		private readonly PanelTargetResolver _targets;
		private readonly PanelViewState _view;
		private readonly Action _rebuild;
		private readonly Dictionary<Object, SerializedObject> _objects = new ();

		public PanelBuilder(in PanelBuildRequest request)
		{
			_spec = request.Spec;
			_targets = request.Targets;
			_view = request.View ?? new PanelViewState();
			_rebuild = request.Rebuild;
		}

		public VisualElement Build()
		{
			VisualElement root = new VisualElement();
			root.AddToClassList("pl-panel__content");
			AddItems(root, _spec.Items, null);
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

		private void AddItems(VisualElement parent, List<PanelItem> items, Object scope)
		{
			foreach (PanelItem item in items)
			{
				parent.Add(BuildItem(item, scope));
			}
		}

		private VisualElement BuildItem(PanelItem item, Object scope)
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
					return BuildField(item, scope);
				case PanelItemType.Inspector:
					return BuildInspector(item, scope);
				case PanelItemType.Group:
					return BuildGroup(item, scope);
				case PanelItemType.Row:
					return BuildRow(item, scope);
				case PanelItemType.Table:
					return BuildTable(item);
				case PanelItemType.Repeat:
					return BuildRepeat(item);
				case PanelItemType.Button:
					return ParleyStyles.Button(item.Label, () => RunAction(item, scope), "pl-panel__button");
				default:
					return Error(item.Location + ": unsupported item.");
			}
		}

		private VisualElement BuildField(PanelItem item, Object scope)
		{
			Object target = TargetOf(item, scope);
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

		private VisualElement BuildInspector(PanelItem item, Object scope)
		{
			Object target = TargetOf(item, scope);
			if (target == null)
			{
				return Missing(item);
			}

			if (target is not GameObject gameObject)
			{
				return new InspectorElement(target);
			}

			VisualElement container = new VisualElement();
			Component[] components = gameObject.GetComponents<Component>();
			for (int i = 0; i < components.Length; i++)
			{
				Component component = components[i];
				if (component == null)
				{
					container.Add(new HelpBox("Missing script", HelpBoxMessageType.Warning));
					continue;
				}

				string typeName = component.GetType().Name;
				Foldout foldout = new Foldout { text = typeName };
				_view.Bind(foldout, PanelViewState.ComponentKey(item.Location, typeName, i));
				foldout.Add(new InspectorElement(component));
				container.Add(foldout);
			}

			return container;
		}

		private VisualElement BuildGroup(PanelItem item, Object scope)
		{
			if (item.Foldout)
			{
				Foldout foldout = new Foldout { text = item.Label };
				foldout.AddToClassList("pl-panel__group");
				_view.Bind(foldout, item.Location);
				AddItems(foldout, item.Items, scope);
				return foldout;
			}

			VisualElement group = new VisualElement();
			group.AddToClassList("pl-panel__group");
			group.Add(ParleyStyles.Text(item.Label, "pl-panel__group-title"));
			AddItems(group, item.Items, scope);
			return group;
		}

		private VisualElement BuildRow(PanelItem item, Object scope)
		{
			VisualElement row = new VisualElement();
			row.AddToClassList("pl-panel__row");
			foreach (PanelItem child in item.Items)
			{
				VisualElement element = BuildItem(child, scope);
				element.AddToClassList("pl-panel__row-item");
				row.Add(element);
			}

			return row;
		}

		private VisualElement BuildRepeat(PanelItem item)
		{
			if (!PanelQuery.HasValidFolder(item))
			{
				return Error("Folder '" + item.Folder + "' does not exist.");
			}

			VisualElement container = new VisualElement();
			container.AddToClassList("pl-panel__group");
			if (!string.IsNullOrEmpty(item.Label))
			{
				container.Add(ParleyStyles.Text(item.Label, "pl-panel__group-title"));
			}

			List<Object> assets = PanelQuery.Assets(item);
			if (assets.Count == 0)
			{
				container.Add(new HelpBox("No assets match '" + item.Query + "'.", HelpBoxMessageType.Info));
				return container;
			}

			int count = Math.Min(assets.Count, RepeatLimit);
			for (int i = 0; i < count; i++)
			{
				Object asset = assets[i];
				Foldout foldout = new Foldout { text = asset.name, tooltip = AssetDatabase.GetAssetPath(asset) };
				foldout.AddToClassList("pl-panel__group");
				_view.Bind(foldout, PanelViewState.RepeatKey(item.Location, PanelQuery.KeyOf(asset)));
				AddItems(foldout, item.Items, asset);
				container.Add(foldout);
			}

			if (assets.Count > count)
			{
				container.Add(ParleyStyles.Text("Showing first " + count + " of " + assets.Count + " assets.", ParleyStyles.Muted));
			}

			return container;
		}

		private VisualElement BuildTable(PanelItem item)
		{
			if (!PanelQuery.HasValidFolder(item))
			{
				return Error("Folder '" + item.Folder + "' does not exist.");
			}

			List<Object> assets = PanelQuery.Assets(item);
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

		private Object TargetOf(PanelItem item, Object scope)
		{
			return string.IsNullOrEmpty(item.Target)
				? PanelTargets.WithComponent(scope, item.Component)
				: _targets.Resolve(item.Target, item.Component);
		}

		private void RunAction(PanelItem item, Object scope)
		{
			if (PanelActions.Run(_targets, item, scope))
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
			string component = string.IsNullOrEmpty(item.Component) ? string.Empty : " component " + item.Component;
			string message = _targets.Status(item.Target) == PanelTargetStatus.MissingWithId
				? "Target '" + item.Target + "' (" + reference + ")" + component
					+ " is not available right now. Parley remembered its identity and will reconnect it automatically when the asset or its scene comes back."
				: "Target '" + item.Target + "' (" + reference + ")" + component + " not found. Open the scene or check the path.";
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
