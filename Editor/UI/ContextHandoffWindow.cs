using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Sessions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class ContextHandoffWindow : EditorWindow
    {
        private IChatTabManager _tabs;
        private ChatSession _source;
        private TranscriptEntry _cutoff;
        private bool _returningResult;
        private Action _saveDraft;
        private Action<ChatSession> _prepared;
        private readonly List<ChatSession> _targets = new();
        private readonly List<ParleyProfile> _profiles = new();
        private DropdownField _target;
        private DropdownField _profile;
        private DropdownField _mode;
        private DropdownField _summarizer;
        private IntegerField _count;
        private TextField _preview;
        private Label _status;
        private Button _prepare;
        private CancellationTokenSource _lifetime;

        public static void Show(Rect anchor, IChatTabManager tabs, ChatSession source, TranscriptEntry cutoff,
            bool returningResult, Action saveDraft, Action<ChatSession> prepared)
        {
            ContextHandoffWindow window = CreateInstance<ContextHandoffWindow>();
            window._tabs = tabs;
            window._source = source;
            window._cutoff = cutoff;
            window._returningResult = returningResult;
            window._saveDraft = saveDraft;
            window._prepared = prepared;
            window._lifetime = new CancellationTokenSource();
            window.ShowAsDropDown(anchor, new Vector2(460, 510));
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            ParleyStyles.Apply(root);
            root.AddToClassList("pl-mcp__form");
            root.Add(new Label(_returningResult ? "Send Result Back" : "Send Context →"));
            root.Add(new Label("Prepares a message. Review it and press Send in the target tab.")
                { style = { whiteSpace = WhiteSpace.Normal } });

            List<string> targets = new();
            if (!_returningResult)
            {
                _targets.Add(null);
                targets.Add("Create new tab");
            }

            int tabNumber = 0;
            foreach (ChatSession tab in _tabs.Tabs)
            {
                tabNumber++;
                if (tab != _source && (!_returningResult || tab.SessionId == _source.Record.ParentSessionId))
                {
                    _targets.Add(tab);
                    targets.Add("Tab #" + tabNumber + ": " + tab.Title + " (" + tab.Profile.Name + ")" +
                        (tab.HasActiveWork ? " — busy" : ""));
                }
            }

            if (targets.Count == 0)
            {
                root.Add(new Label("The parent tab is closed. Open it from History first."));
                return;
            }

            _target = new DropdownField("Target", targets, 0);
            root.Add(_target);
            List<string> profiles = new();
            _profiles.Add(_source.Profile);
            profiles.Add(_source.Profile.Name + " (current)");
            foreach (ParleyProfile profile in ParleyUserSettings.instance.Profiles)
            {
                if (profile.Id != _source.Profile.Id)
                {
                    _profiles.Add(profile);
                    profiles.Add(profile.Name);
                }
            }

            _profile = new DropdownField("New tab profile", profiles, 0);
            root.Add(_profile);
            _mode = new DropdownField("Snapshot", new List<string> { "Last N messages", "AI Summary" }, 0);
            root.Add(_mode);
            _count = new IntegerField("Last N") { value = _returningResult ? 1 : 10 };
            root.Add(_count);
            _summarizer = new DropdownField("Summary model", profiles, 0);
            root.Add(_summarizer);
            _preview = new TextField("Snapshot preview") { multiline = true, isReadOnly = true };
            _preview.style.flexGrow = 1;
            _preview.style.minHeight = 120;
            root.Add(_preview);
            _status = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            root.Add(_status);
            _prepare = new Button(() => { _ = PrepareAsync(_lifetime.Token); }) { text = "Prepare message" };
            root.Add(_prepare);
            root.Add(new Button(Close) { text = "Cancel" });
            _target.RegisterValueChangedCallback(_ => RefreshPreview());
            _mode.RegisterValueChangedCallback(_ => RefreshPreview());
            _count.RegisterValueChangedCallback(_ => RefreshPreview());
            RefreshPreview();
        }

        private ContextSnapshot Capture()
        {
            return ContextHandoffService.Capture(_source.Record,
                _mode.index == 1 ? int.MaxValue : _count.value, _cutoff, !_returningResult);
        }

        private void RefreshPreview()
        {
            ParleyStyles.SetVisible(_profile, _targets[_target.index] == null);
            ParleyStyles.SetVisible(_count, _mode.index == 0);
            ParleyStyles.SetVisible(_summarizer, _mode.index == 1);
            try
            {
                ContextSnapshot snapshot = Capture();
                _preview.value = snapshot.Markdown;
                ChatSession target = _targets[_target.index];
                _status.text = target?.HasActiveWork == true
                    ? "The target tab is busy. Wait or choose another tab."
                    : snapshot.Characters + " characters · ~" + snapshot.EstimatedTokens + " tokens";
                _prepare.SetEnabled(target?.HasActiveWork != true);
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
                _prepare.SetEnabled(false);
            }
        }

        private async Task PrepareAsync(CancellationToken cancellationToken)
        {
            _prepare.SetEnabled(false);
            _target.SetEnabled(false);
            _profile.SetEnabled(false);
            _mode.SetEnabled(false);
            _count.SetEnabled(false);
            _summarizer.SetEnabled(false);
            ReloadGuard.Acquire(this);
            try
            {
                ContextSnapshot snapshot = Capture();
                if (_mode.index == 1)
                {
                    _status.text = "Generating summary…";
                    ParleyProfile summarizer = JsonUtility.FromJson<ParleyProfile>(
                        JsonUtility.ToJson(_profiles[_summarizer.index]));
                    if (_summarizer.index == 0)
                    {
                        summarizer.Model = _source.Model;
                        summarizer.Effort = _source.Effort;
                    }

                    ContextHandoffService service = new(new ContextSummaryGenerator());
                    snapshot = await service.SummarizeAsync(snapshot, summarizer, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _saveDraft();
                ChatSession target = ManualContextHandoff.Prepare(_tabs, _source, _targets[_target.index],
                    _profiles[_profile.index], snapshot, _returningResult);
                _prepared(target);
                Close();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _status.text = exception.Message;
                _prepare.SetEnabled(true);
            }
            finally
            {
                ReloadGuard.Release(this);
                if (this != null)
                {
                    _target.SetEnabled(true);
                    _profile.SetEnabled(true);
                    _mode.SetEnabled(true);
                    _count.SetEnabled(true);
                    _summarizer.SetEnabled(true);
                }
            }
        }

        private void OnDisable()
        {
            _lifetime?.Cancel();
        }

        private void OnDestroy()
        {
            _lifetime?.Dispose();
        }
    }
}
