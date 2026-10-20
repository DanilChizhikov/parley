using System;
using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelWindow : EditorWindow
	{
		private const string DefaultTitle = "Panel";
		private const string SavedPrefix = "saved:";
		private const string TemporaryPrefix = "temp:";
		private const float MinWidth = 320.0f;
		private const float MinHeight = 200.0f;
		private const long RefreshMs = 100;

		[SerializeField] private string _key;
		[SerializeField] private string _panelName;
		[SerializeField] private string _specJson;
		[SerializeField] private string _title;

		private readonly PanelViewState _view = new ();
		private readonly PanelRefresh _refresh = new ();

		private PanelBuilder _builder;
		private JObject _document;
		private PanelSpec _spec;
		private PanelTargetResolver _targets;
		private bool _selfSave;
		private ScrollView _scroll;
		private VisualElement _banner;
		private TextField _nameField;
		private Button _saveButton;
		private Button _revealButton;
		private Label _savedLabel;

		public static PanelWindow Open(string panelName, string specJson, string title)
		{
			string key = panelName != null ? SavedPrefix + panelName : TemporaryPrefix + title;
			PanelWindow window = Find(key);
			if (window == null)
			{
				window = CreateWindow<PanelWindow>();
				window.minSize = new Vector2(MinWidth, MinHeight);
			}

			window._key = key;
			window._panelName = panelName;
			window._specJson = specJson;
			window._title = string.IsNullOrWhiteSpace(title) ? panelName ?? DefaultTitle : title;
			window.titleContent = new GUIContent(window._title);
			window.Show();
			window.Focus();
			window.Rebuild();
			return window;
		}

		public static bool TryOpenSaved(string name, out PanelWindow window)
		{
			window = null;
			string slug = PanelStore.Slug(name);
			if (slug == null || !PanelStore.Default.TryLoad(slug, out string json))
			{
				return false;
			}

			window = Open(slug, json, TitleOf(json) ?? slug);
			return true;
		}

		public static string TitleOf(string json)
		{
			try
			{
				return (JToken.Parse(json) as JObject)?["title"]?.ToString();
			}
			catch (JsonException)
			{
				return null;
			}
		}

		private static PanelWindow Find(string key)
		{
			foreach (PanelWindow window in Resources.FindObjectsOfTypeAll<PanelWindow>())
			{
				if (window._key == key)
				{
					return window;
				}
			}

			return null;
		}

		private void OnEnable()
		{
			PanelStore.Default.OnChanged -= StoreChangedHandler;
			PanelStore.Default.OnChanged += StoreChangedHandler;
			EditorApplication.projectChanged -= ProjectChangedHandler;
			EditorApplication.projectChanged += ProjectChangedHandler;
			EditorApplication.hierarchyChanged -= HierarchyChangedHandler;
			EditorApplication.hierarchyChanged += HierarchyChangedHandler;
			Undo.undoRedoPerformed -= ProjectChangedHandler;
			Undo.undoRedoPerformed += ProjectChangedHandler;
		}

		private void OnDisable()
		{
			PanelStore.Default.OnChanged -= StoreChangedHandler;
			EditorApplication.projectChanged -= ProjectChangedHandler;
			EditorApplication.hierarchyChanged -= HierarchyChangedHandler;
			Undo.undoRedoPerformed -= ProjectChangedHandler;
			_builder?.Dispose();
			_builder = null;
		}

		private void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			ParleyStyles.Apply(root);
			root.AddToClassList("pl-panel");
			VisualElement toolbar = new VisualElement();
			toolbar.AddToClassList("pl-panel__toolbar");
			toolbar.Add(ParleyStyles.Button("Reload", Reload, "pl-button--small"));
			toolbar.Add(ParleyStyles.Spacer());
			_savedLabel = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
			toolbar.Add(_savedLabel);
			_nameField = new TextField { tooltip = "Name to save this panel under (UserSettings/Parley/Panels)." };
			_nameField.AddToClassList("pl-panel__name");
			_nameField.textEdition.placeholder = "panel name";
			toolbar.Add(_nameField);
			_saveButton = ParleyStyles.Button("Save", Save, "pl-button--small");
			toolbar.Add(_saveButton);
			_revealButton = ParleyStyles.Button("Reveal", Reveal, "pl-button--small");
			toolbar.Add(_revealButton);
			root.Add(toolbar);
			_banner = new VisualElement();
			_banner.AddToClassList("pl-panel__banner");
			root.Add(_banner);
			_scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
			_scroll.AddToClassList("pl-panel__scroll");
			root.Add(_scroll);
			root.schedule.Execute(RefreshIfDirty).Every(RefreshMs);
			Rebuild();
		}

		private void Rebuild()
		{
			if (_scroll == null)
			{
				return;
			}

			List<PanelIssue> issues = new ();
			_document = PanelParser.ReadDocument(_specJson, issues);
			_spec = _document == null ? null : PanelParser.Parse(_document, issues);
			if (_spec == null || PanelIssue.HasErrors(issues))
			{
				_spec = null;
				_targets = null;
				_scroll.Clear();
				_banner.Clear();
				_builder?.Dispose();
				_builder = null;
				UpdateToolbar();
				foreach (PanelIssue issue in issues)
				{
					_scroll.Add(new HelpBox(issue.ToString(), issue.Severity == PanelIssueSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
				}

				return;
			}

			_targets = new PanelTargetResolver(_spec);
			Render();
		}

		private void Render()
		{
			if (_scroll == null || _spec == null || _targets == null)
			{
				return;
			}

			_view.Scroll = _scroll.scrollOffset;
			_scroll.Clear();
			_builder?.Dispose();
			_builder = null;
			_title = _spec.Title;
			titleContent = new GUIContent(_title);
			UpdateToolbar();
			_targets.Writable = !EditorApplication.isPlayingOrWillChangePlaymode;
			_refresh.Capture(_spec, _targets);
			RenderBanner();
			_view.BeginBuild();
			_builder = new PanelBuilder(new PanelBuildRequest(_spec, _targets, _view, Rebuild));
			_scroll.Add(_builder.Build());
			_scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollGeometryChangedHandler);
			PersistIfChanged();
		}

		private void RenderBanner()
		{
			_banner.Clear();
			PanelCoverage coverage = _refresh.Coverage;
			if (coverage.Missing.Count == 0)
			{
				return;
			}

			foreach (PanelCoverageEntry entry in coverage.Missing)
			{
				VisualElement row = new VisualElement();
				row.AddToClassList("pl-panel__banner-row");
				string assets = entry.Count == 1 ? " asset is" : " assets are";
				row.Add(new HelpBox(entry.Count + " " + entry.TypeName + assets + " not in this panel. Ask Parley to add them, or use a query item so the panel keeps up by itself.",
					HelpBoxMessageType.Info));
				if (!string.IsNullOrEmpty(entry.FirstPath))
				{
					string path = entry.FirstPath;
					row.Add(ParleyStyles.Button("Ping", () => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path)), "pl-button--small"));
				}

				_banner.Add(row);
			}
		}

		private void RefreshIfDirty()
		{
			if (_spec == null || _targets == null)
			{
				return;
			}

			_targets.Writable = !EditorApplication.isPlayingOrWillChangePlaymode;
			switch (_refresh.Poll(_spec, _targets, CompileWatcher.IsCompiling))
			{
				case PanelRefreshResult.Rebuild:
					Render();
					break;
				case PanelRefreshResult.Persist:
					PersistIfChanged();
					break;
			}
		}

		private void PersistIfChanged()
		{
			if (_targets == null || !_targets.SpecChanged || _document == null)
			{
				return;
			}

			if (!PanelSpecWriter.Apply(_document, _spec))
			{
				_targets.AcceptChanges();
				return;
			}

			_specJson = _document.ToString(Formatting.Indented);
			if (!string.IsNullOrEmpty(_panelName))
			{
				_selfSave = true;
				try
				{
					PanelStore.Default.Save(_panelName, _specJson);
				}
				catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
				{
					Debug.LogWarning("[Parley] Could not update panel '" + _panelName + "': " + exception.Message);
				}
				finally
				{
					_selfSave = false;
				}
			}

			_targets.AcceptChanges();
		}

		private void UpdateToolbar()
		{
			if (_nameField == null)
			{
				return;
			}

			bool saved = !string.IsNullOrEmpty(_panelName);
			ParleyStyles.SetVisible(_nameField, !saved);
			ParleyStyles.SetVisible(_saveButton, !saved);
			ParleyStyles.SetVisible(_revealButton, saved);
			_savedLabel.text = saved ? _panelName : "not saved";
			if (!saved && string.IsNullOrEmpty(_nameField.value))
			{
				_nameField.SetValueWithoutNotify(PanelStore.Slug(_title) ?? string.Empty);
			}
		}

		private void Reload()
		{
			if (!string.IsNullOrEmpty(_panelName) && PanelStore.Default.TryLoad(_panelName, out string json))
			{
				_specJson = json;
			}

			Rebuild();
		}

		private void Save()
		{
			string slug = PanelStore.Slug(_nameField.value);
			if (slug == null)
			{
				EditorUtility.DisplayDialog(DefaultTitle, "Enter a name with letters or digits.", "OK");
				return;
			}

			if (PanelStore.Default.Exists(slug) && !EditorUtility.DisplayDialog(DefaultTitle, "Panel '" + slug + "' exists. Overwrite?", "Overwrite", "Cancel"))
			{
				return;
			}

			string json = _specJson;
			try
			{
				json = JToken.Parse(_specJson).ToString(Formatting.Indented);
			}
			catch (JsonException)
			{
				// Keep the raw text: the window already shows the parse error.
			}

			_panelName = slug;
			_key = SavedPrefix + slug;
			_selfSave = true;
			try
			{
				PanelStore.Default.Save(slug, json);
			}
			finally
			{
				_selfSave = false;
			}

			UpdateToolbar();
		}

		private void Reveal()
		{
			string path = PanelStore.Default.PathFor(_panelName);
			if (path != null)
			{
				EditorUtility.RevealInFinder(path);
			}
		}

		private void ProjectChangedHandler()
		{
			_refresh.MarkAssets();
		}

		private void HierarchyChangedHandler()
		{
			_refresh.MarkHierarchy();
		}

		private void ScrollGeometryChangedHandler(GeometryChangedEvent evt)
		{
			_scroll.contentContainer.UnregisterCallback<GeometryChangedEvent>(ScrollGeometryChangedHandler);
			_scroll.scrollOffset = _view.Scroll;
		}

		private void StoreChangedHandler(string slug)
		{
			if (_selfSave || string.IsNullOrEmpty(_panelName) || slug != _panelName)
			{
				return;
			}

			if (PanelStore.Default.TryLoad(slug, out string json))
			{
				_specJson = json;
				Rebuild();
				return;
			}

			_panelName = null;
			_key = TemporaryPrefix + _title;
			UpdateToolbar();
		}
	}
}
