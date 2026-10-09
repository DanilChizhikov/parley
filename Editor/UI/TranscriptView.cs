using System.Collections.Generic;
using DTech.Parley.Editor.Sessions;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class TranscriptView : VisualElement
    {
        private const long RefreshIntervalMs = 50;
        private const long ScrollDelayMs = 10;
        private const float StickThreshold = 24.0f;

        private readonly ScrollView _scroll;
        private readonly VisualElement _empty;
        private readonly Dictionary<TranscriptBlock, BlockView> _views = new ();
        private readonly Dictionary<string, ToolCardView> _toolCards = new ();
        private readonly Dictionary<TranscriptEntry, VisualElement> _entries = new ();
        private readonly HashSet<BlockView> _dirty = new ();

        private ChatSession _session;
        private bool _stickToBottom = true;
        private bool _scrollQueued;

        public TranscriptView()
        {
            AddToClassList("pl-transcript");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("pl-transcript__scroll");
            _scroll.verticalScroller.valueChanged += ScrolledHandler;
            _scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(ContentGeometryChangedHandler);
            Add(_scroll);
            _empty = new VisualElement();
            _empty.AddToClassList("pl-empty");
            _empty.Add(ParleyStyles.Text("Parley", "pl-empty__title"));
            _empty.Add(ParleyStyles.Text("Ask about your project, request changes, or attach the selection, console errors or a screenshot. "
                + "Shift+Enter adds a new line, / opens commands.", "pl-empty__text"));
            _scroll.Add(_empty);
            schedule.Execute(FlushDirty).Every(RefreshIntervalMs);
        }

        public void Bind(ChatSession session)
        {
            Unbind();
            _session = session;
            _session.OnEntryAdded += EntryAddedHandler;
            _session.OnBlockAdded += BlockAddedHandler;
            _session.OnBlockChanged += BlockChangedHandler;
            foreach (TranscriptEntry entry in session.Record.Entries)
            {
                AddEntry(entry);
            }

            UpdateEmpty();
            ScrollToBottom();
        }

        public void Unbind()
        {
            if (_session != null)
            {
                _session.OnEntryAdded -= EntryAddedHandler;
                _session.OnBlockAdded -= BlockAddedHandler;
                _session.OnBlockChanged -= BlockChangedHandler;
                _session = null;
            }

            foreach (VisualElement entry in _entries.Values)
            {
                entry.RemoveFromHierarchy();
            }

            _entries.Clear();
            _views.Clear();
            _toolCards.Clear();
            _dirty.Clear();
            UpdateEmpty();
        }

        public void ScrollToBottom()
        {
            _stickToBottom = true;
            if (_scrollQueued)
            {
                return;
            }

            _scrollQueued = true;
            schedule.Execute(ScrollToEnd).ExecuteLater(ScrollDelayMs);
        }

        private void AddEntry(TranscriptEntry entry)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList("pl-entry");
            element.AddToClassList("pl-entry--" + entry.Role.ToString().ToLowerInvariant());
            _entries[entry] = element;
            _scroll.Add(element);
            foreach (TranscriptBlock block in entry.Blocks)
            {
                AddBlock(entry, block);
            }
        }

        private void AddBlock(TranscriptEntry entry, TranscriptBlock block)
        {
            if (!_entries.TryGetValue(entry, out VisualElement container) || _views.ContainsKey(block))
            {
                return;
            }

            BlockView view = CreateView(entry, block);
            if (view == null)
            {
                return;
            }

            _views[block] = view;
            if (view is ToolCardView card && !string.IsNullOrEmpty(block.ToolUseId))
            {
                _toolCards[block.ToolUseId] = card;
            }

            if (!string.IsNullOrEmpty(block.ParentToolUseId) && _toolCards.TryGetValue(block.ParentToolUseId, out ToolCardView parent))
            {
                parent.Nested.Add(view);
                return;
            }

            container.Add(view);
        }

        private BlockView CreateView(TranscriptEntry entry, TranscriptBlock block)
        {
            switch (block.Kind)
            {
                case BlockKind.Text:
                    return new TextBlockView(block, entry.Role == EntryRole.User);
                case BlockKind.Thinking:
                    return ParleyUserSettings.instance.ShowThinking ? new ThinkingBlockView(block) : null;
                case BlockKind.ToolUse:
                    return new ToolCardView(block);
                case BlockKind.Image:
                    return new ImageBlockView(block);
                case BlockKind.Request:
                    return _session == null ? null : new RequestCardView(block, _session);
                default:
                    return new NoticeBlockView(block);
            }
        }

        private void UpdateEmpty()
        {
            ParleyStyles.SetVisible(_empty, _entries.Count == 0);
        }

        private void FlushDirty()
        {
            if (_dirty.Count == 0)
            {
                return;
            }

            foreach (BlockView view in _dirty)
            {
                view.Refresh();
            }

            _dirty.Clear();
        }

        private void ScrollToEnd()
        {
            _scrollQueued = false;
            _scroll.scrollOffset = new Vector2(_scroll.scrollOffset.x, float.MaxValue);
        }

        private void ContentGeometryChangedHandler(GeometryChangedEvent evt)
        {
            if (_stickToBottom)
            {
                ScrollToBottom();
            }
        }

        private void ScrolledHandler(float value)
        {
            _stickToBottom = value >= _scroll.verticalScroller.highValue - StickThreshold;
        }

        private void EntryAddedHandler(TranscriptEntry entry)
        {
            AddEntry(entry);
            UpdateEmpty();
            if (entry.Role == EntryRole.User)
            {
                ScrollToBottom();
            }
        }

        private void BlockAddedHandler(TranscriptEntry entry, TranscriptBlock block)
        {
            AddBlock(entry, block);
        }

        private void BlockChangedHandler(TranscriptBlock block)
        {
            if (_views.TryGetValue(block, out BlockView view))
            {
                _dirty.Add(view);
            }
        }
    }
}