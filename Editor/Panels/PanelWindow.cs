using System;
using System.Collections.Generic;
using System.IO;
using DTech.Parley.Editor.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.Panels
{
	internal sealed class PanelWindow : EditorWindow
	{
		private const string DefaultTitle = "Panel";
		private const string SavedPrefix = "saved:";
		private const string TemporaryPrefix = "temp:";
		private const string DialogTitle = "Parley";
		private const float MinWidth = 400.0f;
		private const double NotificationSeconds = 1.5;
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
		private List<PanelIssue> _warnings = new ();
		private bool _bannerDismissed;
		private TextField _nameField;
		private ToolbarButton _saveButton;
		private ToolbarButton _revealButton;
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
			window.ApplyTitle();
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

		private static Texture Icon()
		{
			return EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_UnityEditor.InspectorWindow" : "UnityEditor.InspectorWindow").image;
		}

		private void ApplyTitle()
		{
			titleContent = new GUIContent(_title ?? DefaultTitle, Icon(), "Parley panel");
		}

		private void OnEnable()
		{
			minSize = new Vector2(MinWidth, MinHeight);
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
			Toolbar toolbar = new Toolbar();
			toolbar.AddToClassList("pl-panel__toolbar");
			toolbar.Add(new ToolbarButton(Reload) { text = "Reload", tooltip = "Read the panel again and refresh every value" });
			toolbar.Add(new ToolbarSpacer { flex = true });
			_savedLabel = ParleyStyles.Text(string.Empty, "pl-panel__saved");
			toolbar.Add(_savedLabel);
			_nameField = new TextField { tooltip = "Name to save this panel under (UserSettings/Parley/Panels). Press Enter to save." };
			_nameField.AddToClassList("pl-panel__name");
			_nameField.textEdition.placeholder = "panel name";
			_nameField.RegisterCallback<KeyDownEvent>(NameKeyDownHandler, TrickleDown.TrickleDown);
			toolbar.Add(_nameField);
			_saveButton = new ToolbarButton(Save) { text = "Save", tooltip = "Keep this panel so you can open it later from Window > DTech > Parley Panels" };
			toolbar.Add(_saveButton);
			_revealButton = new ToolbarButton(Reveal) { text = "Reveal", tooltip = "Show the panel file" };
			toolbar.Add(_revealButton);
			root.Add(toolbar);
			_banner = new VisualElement();
			_banner.AddToClassList("pl-panel__banner");
			root.Add(_banner);
			_scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { horizontalScrollerVisibility = ScrollerVisibility.Auto };
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
				VisualElement content = new VisualElement();
				content.AddToClassList("pl-panel__content");
				content.Add(ParleyStyles.Text("This panel can't be shown because its description has problems. Ask Parley to fix them, then press Reload.",
					"pl-panel__summary"));
				AddIssues(content, issues);
				_scroll.Add(content);
				return;
			}

			_warnings = issues;
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
			ApplyTitle();
			UpdateToolbar();
			_targets.Writable = !EditorApplication.isPlayingOrWillChangePlaymode;
			_refresh.Capture(_spec, _targets);
			RenderBanner();
			_view.BeginBuild();
			_builder = new PanelBuilder(new PanelBuildRequest(_spec, _targets, _view, Rebuild));
			if (_warnings.Count > 0)
			{
				VisualElement warnings = new VisualElement();
				warnings.AddToClassList("pl-panel__warnings");
				AddIssues(warnings, _warnings);
				_scroll.Add(warnings);
			}

			VisualElement built = _builder.Build();
			_scroll.Add(built);
			if (_spec.Items.Count == 0)
			{
				VisualElement empty = new VisualElement();
				empty.AddToClassList("pl-empty");
				empty.Add(ParleyStyles.Text("This panel is empty", "pl-empty__title"));
				empty.Add(ParleyStyles.Text("Ask Parley what it should show.", "pl-empty__text"));
				_scroll.Add(empty);
			}

			_scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(ScrollGeometryChangedHandler);
			PersistIfChanged();
		}

		private static void AddIssues(VisualElement parent, List<PanelIssue> issues)
		{
			foreach (PanelIssue issue in issues)
			{
				parent.Add(new HelpBox(issue.ToDisplayString(), issue.Severity == PanelIssueSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
			}
		}

		private void RenderBanner()
		{
			_banner.Clear();
			PanelCoverage coverage = _refresh.Coverage;
			if (coverage.Missing.Count == 0 || _bannerDismissed)
			{
				return;
			}

			PanelCoverageEntry entry = coverage.Missing[0];
			VisualElement row = new VisualElement();
			row.AddToClassList("pl-panel__banner-row");
			string assets = entry.Count == 1 ? " asset is" : " assets are";
			string more = coverage.Missing.Count > 1 ? " (and " + (coverage.Missing.Count - 1) + " more types)" : string.Empty;
			HelpBox box = new HelpBox(entry.Count + " " + entry.TypeName + assets + " not shown here" + more + ". Ask Parley to add them, or to list them with a search so new ones appear on their own.",
				HelpBoxMessageType.Info);
			List<string> lines = new List<string>();
			foreach (PanelCoverageEntry missing in coverage.Missing)
			{
				lines.Add(missing.Count + " × " + missing.TypeName);
			}

			box.tooltip = string.Join("\n", lines);
			row.Add(box);
			if (!string.IsNullOrEmpty(entry.FirstPath))
			{
				string path = entry.FirstPath;
				Button ping = ParleyStyles.Button("Ping", () => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path)), "pl-button--small");
				ping.tooltip = "Highlight one of them in the Project window";
				row.Add(ping);
			}

			Button dismiss = ParleyStyles.Button("✕", DismissBanner, "pl-button--small");
			dismiss.AddToClassList("pl-button--ghost");
			dismiss.tooltip = "Hide this note for this panel";
			row.Add(dismiss);
			_banner.Add(row);
		}

		private void DismissBanner()
		{
			_bannerDismissed = true;
			_banner.Clear();
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
			bool valid = _spec != null;
			ParleyStyles.SetVisible(_nameField, !saved && valid);
			ParleyStyles.SetVisible(_saveButton, !saved && valid);
			ParleyStyles.SetVisible(_revealButton, saved);
			_savedLabel.text = saved ? "Saved as " + _panelName : valid ? "Unsaved" : string.Empty;
			_savedLabel.tooltip = saved ? PanelStore.Default.PathFor(_panelName) : "This panel closes for good unless you save it";
			_savedLabel.EnableInClassList("pl-panel__saved--unsaved", !saved);
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
				ShowNotification(new GUIContent("Enter a name with letters or digits"), NotificationSeconds);
				_nameField.Focus();
				return;
			}

			if (PanelStore.Default.Exists(slug) && !EditorUtility.DisplayDialog(DialogTitle, "A panel called '" + slug + "' already exists. Overwrite it?", "Overwrite", "Cancel"))
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

			_selfSave = true;
			try
			{
				PanelStore.Default.Save(slug, json);
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				EditorUtility.DisplayDialog(DialogTitle, "Could not save the panel: " + exception.Message, "OK");
				return;
			}
			finally
			{
				_selfSave = false;
			}

			_panelName = slug;
			_key = SavedPrefix + slug;
			UpdateToolbar();
			ShowNotification(new GUIContent("Saved"), NotificationSeconds);
		}

		private void Reveal()
		{
			string path = PanelStore.Default.PathFor(_panelName);
			if (path != null)
			{
				EditorUtility.RevealInFinder(path);
			}
		}

		private void NameKeyDownHandler(KeyDownEvent evt)
		{
			if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
			{
				return;
			}

			Save();
			evt.StopPropagation();
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
