using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Updates;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ReleaseNotesWindow : EditorWindow
    {
        private const string WindowTitle = "Parley Update";
        private const float WindowWidth = 700.0f;
        private const float WindowHeight = 600.0f;
        private const float MinWidth = 420.0f;
        private const float MinHeight = 360.0f;

        private static bool _openingPreview;

        [SerializeField] private bool _preview;

        private Label _headline;
        private Label _currentLabel;
        private Label _latestLabel;
        private ScrollView _notes;
        private Button _updateButton;
        private CancellationTokenSource _loading;

        public static void Open()
        {
            ReleaseNotesWindow window = GetWindow<ReleaseNotesWindow>(true, WindowTitle);
            window.minSize = new Vector2(MinWidth, MinHeight);
            Rect position = window.position;
            if (position.width < WindowWidth || position.height < WindowHeight)
            {
                window.position = new Rect(position.x, position.y, Mathf.Max(position.width, WindowWidth), Mathf.Max(position.height, WindowHeight));
            }

            window.Show();
        }

        public static void OpenPreview()
        {
            _openingPreview = true;
            try
            {
                ReleaseNotesWindow window = CreateInstance<ReleaseNotesWindow>();
                window.titleContent = new GUIContent(WindowTitle + " (Preview)");
                window.minSize = new Vector2(MinWidth, MinHeight);
                window.position = new Rect(window.position.x, window.position.y, WindowWidth, WindowHeight);
                window.ShowUtility();
            }
            finally
            {
                _openingPreview = false;
            }
        }

        private static List<ReleaseNote> PreviewNotes()
        {
            return new List<ReleaseNote>
            {
                new ReleaseNote(new Version(1, 3, 0), "Preview release", "### Added\n- **Debug preview** window with scripted scenarios.\n- Panel item `repeat`.\n\n### Fixed\n- Status bar text no longer overflows.", null),
                new ReleaseNote(new Version(1, 2, 1), null, "- Smaller fixes.\n- `Copy` button on code blocks.", null),
                new ReleaseNote(new Version(1, 2, 0), "Skills", string.Empty, null),
            };
        }

        private static string Describe(Version version)
        {
            return version == null ? "—" : "v" + version;
        }

        private void OnEnable()
        {
            if (_openingPreview)
            {
                _preview = true;
            }

            ParleyUpdates.OnChanged -= ChangedHandler;
            ParleyUpdates.OnChanged += ChangedHandler;
        }

        private void OnDisable()
        {
            ParleyUpdates.OnChanged -= ChangedHandler;
            CancelLoading();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            ParleyStyles.Apply(root);
            root.AddToClassList("pl-release");
            root.Add(ParleyStyles.Text("Parley", "pl-release__brand"));
            _headline = ParleyStyles.Text(string.Empty, "pl-release__headline");
            root.Add(_headline);
            _currentLabel = ParleyStyles.Text(string.Empty, "pl-release__version");
            root.Add(_currentLabel);
            _latestLabel = ParleyStyles.Text(string.Empty, "pl-release__version");
            root.Add(_latestLabel);
            root.Add(ParleyStyles.Text("What's new", "pl-release__section"));
            _notes = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _notes.AddToClassList("pl-release__notes");
            root.Add(_notes);
            VisualElement footer = new VisualElement();
            footer.AddToClassList("pl-release__footer");
            footer.Add(ParleyStyles.Spacer());
            footer.Add(ParleyStyles.Button("Close", Close));
            _updateButton = ParleyStyles.Button("Update", ParleyWindow.RunUpdate, "pl-button--primary");
            _updateButton.tooltip = "Install the new version. Unity recompiles scripts afterwards.";
            footer.Add(_updateButton);
            root.Add(footer);
            if (_preview)
            {
                RefreshPreview();
                return;
            }

            ParleyUpdates.EnsureChecked();
            Refresh();
        }

        private void RefreshPreview()
        {
            _headline.text = "Parley v1.3.0 is available";
            _currentLabel.text = "Installed: v1.2.0";
            _latestLabel.text = "Latest: v1.3.0";
            _updateButton.SetEnabled(false);
            _updateButton.tooltip = "Disabled in preview";
            ShowNotes(PreviewNotes());
            Button failure = ParleyStyles.Button("Show the error state", () => ShowFailure("Failed to load release notes: preview error."), "pl-release__open");
            _notes.Add(failure);
        }

        private void Refresh()
        {
            if (_notes == null || _preview)
            {
                return;
            }

            UpdateStatus status = ParleyUpdates.Status;
            if (status != UpdateStatus.Checking && status != UpdateStatus.Available && status != UpdateStatus.Installing && status != UpdateStatus.Failed)
            {
                EditorApplication.delayCall += CloseIfOpen;
                return;
            }

            _headline.text = status switch
            {
                UpdateStatus.Checking => "Checking for updates…",
                UpdateStatus.Installing => "Updating Parley to " + Describe(ParleyUpdates.Latest) + "…",
                UpdateStatus.Failed => "The update check failed",
                _ => "Parley " + Describe(ParleyUpdates.Latest) + " is available",
            };
            _currentLabel.text = "Installed: " + Describe(ParleyUpdates.Current);
            _latestLabel.text = "Latest: " + Describe(ParleyUpdates.Latest);
            _updateButton.text = status == UpdateStatus.Installing ? "Updating…" : "Update";
            _updateButton.SetEnabled(status == UpdateStatus.Available);
            if (status == UpdateStatus.Failed)
            {
                CancelLoading();
                ShowFailure("Parley could not reach the releases page. Check your connection, or open the page in a browser.");
                return;
            }

            if (status == UpdateStatus.Checking && _loading == null)
            {
                _notes.Clear();
                _notes.Add(ParleyStyles.Text("Checking…", "pl-release__info"));
            }
            if (_loading == null && status == UpdateStatus.Available)
            {
                _ = LoadNotesAsync();
            }
        }

        private async Task LoadNotesAsync()
        {
            _loading = new CancellationTokenSource();
            CancellationToken cancellationToken = _loading.Token;
            _notes.Clear();
            _notes.Add(ParleyStyles.Text("Loading release notes…", "pl-release__info"));
            try
            {
                List<ReleaseNote> notes = await ReleaseNotesLoader.LoadAsync(ParleyUpdates.Current, ParleyUpdates.Latest, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowNotes(notes);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ShowFailure("Failed to load release notes: " + exception.Message);
                }
            }
        }

        private void ShowNotes(List<ReleaseNote> notes)
        {
            if (notes.Count == 0)
            {
                ShowFailure("No release notes are published for these versions.");
                return;
            }

            _notes.Clear();
            foreach (ReleaseNote note in notes)
            {
                string title = "v" + note.Version;
                if (!string.IsNullOrWhiteSpace(note.Title) && note.Title.Trim() != title)
                {
                    title += "  —  " + note.Title.Trim();
                }

                _notes.Add(ParleyStyles.Text(title, "pl-release__title"));
                MarkdownView body = new MarkdownView();
                body.SetMarkdown(string.IsNullOrEmpty(note.Body) ? "_No description._" : note.Body);
                _notes.Add(body);
            }
        }

        private void ShowFailure(string message)
        {
            _notes.Clear();
            _notes.Add(ParleyStyles.Text(message, "pl-release__error"));
            Button open = ParleyStyles.Button("Open releases page", OpenReleasesPage, "pl-release__open");
            open.tooltip = GitHubApi.ReleasesPage;
            _notes.Add(open);
        }

        private void OpenReleasesPage()
        {
            Application.OpenURL(GitHubApi.ReleasesPage);
        }

        private void CancelLoading()
        {
            if (_loading == null)
            {
                return;
            }

            _loading.Cancel();
            _loading.Dispose();
            _loading = null;
        }

        private void CloseIfOpen()
        {
            if (this != null)
            {
                Close();
            }
        }

        private void ChangedHandler()
        {
            Refresh();
        }
    }
}