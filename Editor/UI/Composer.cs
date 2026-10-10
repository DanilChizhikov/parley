using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DTech.Parley.Editor.UI
{
    internal sealed class Composer : VisualElement
    {
        public event Action<string, List<ChatAttachment>> OnSubmit;
        public event Action OnStop;

        private const int MaxSuggestions = 12;
        private const int MinAssetQueryLength = 2;

        private static readonly HashSet<string> _imageExtensions = new (StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        private readonly TextField _input;
        private readonly VisualElement _chips;
        private readonly ScrollView _suggestions;
        private readonly Button _send;
        private readonly Button _stop;
        private readonly List<ChatAttachment> _attachments = new ();
        private readonly List<(string insert, string name, string detail)> _suggestionItems = new ();
        private readonly HashSet<string> _suggestedNames = new (StringComparer.OrdinalIgnoreCase);

        private Func<IReadOnlyList<SlashCommandInfo>> _commands = () => Array.Empty<SlashCommandInfo>();
        private int _selectedSuggestion;

        public Composer()
        {
            AddToClassList("pl-composer");
            _suggestions = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            _suggestions.AddToClassList("pl-suggestions");
            ParleyStyles.SetVisible(_suggestions, false);
            Add(_suggestions);
            _chips = new VisualElement();
            _chips.AddToClassList("pl-chips");
            Add(_chips);
            _input = new TextField { multiline = true };
            _input.AddToClassList("pl-composer__input");
            _input.textEdition.placeholder = "Ask Parley…  (Enter to send, Shift+Enter for a new line)";
            _input.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _input.RegisterCallback<KeyDownEvent>(KeyDownHandler, TrickleDown.TrickleDown);
            _input.RegisterValueChangedCallback(InputChangedHandler);
            Add(_input);
            VisualElement bar = new VisualElement();
            bar.AddToClassList("pl-composer__bar");
            Button attach = ParleyStyles.Button("＋ Attach", ShowAttachMenu, "pl-button--ghost");
            attach.tooltip = "Attach the selection, console errors, a screenshot or a file";
            bar.Add(attach);
            bar.Add(ParleyStyles.Spacer());
            _stop = ParleyStyles.Button("■ Stop", () => OnStop?.Invoke(), "pl-button--danger");
            _send = ParleyStyles.Button("Send ↵", Submit, "pl-button--primary");
            bar.Add(_stop);
            bar.Add(_send);
            Add(bar);
            RegisterCallback<DragUpdatedEvent>(DragUpdatedHandler);
            RegisterCallback<DragPerformEvent>(DragPerformHandler);
            SetBusy(false);
        }

        public void SetCommandSource(Func<IReadOnlyList<SlashCommandInfo>> commands)
        {
            _commands = commands ?? (() => Array.Empty<SlashCommandInfo>());
        }

        public void SetBusy(bool busy)
        {
            ParleyStyles.SetVisible(_stop, busy);
            _send.text = busy ? "Queue ↵" : "Send ↵";
        }

        public void FocusInput()
        {
            _input.Focus();
        }

        public void AddAttachment(ChatAttachment attachment)
        {
            if (attachment == null)
            {
                return;
            }

            _attachments.Add(attachment);
            RebuildChips();
        }

        public void InsertText(string text)
        {
            string current = _input.value ?? string.Empty;
            bool separated = current.Length == 0 || current.EndsWith(" ", StringComparison.Ordinal) || current.EndsWith("\n", StringComparison.Ordinal);
            _input.value = separated ? current + text : current + " " + text;
            _input.Focus();
        }

        private static bool IsCommandQuery(string text, string beforeCursor)
        {
            return text.StartsWith("/", StringComparison.Ordinal) && beforeCursor.IndexOf(' ') < 0 && beforeCursor.IndexOf('\n') < 0;
        }

        private void Submit()
        {
            string text = (_input.value ?? string.Empty).Trim();
            if (text.Length == 0 && _attachments.Count == 0)
            {
                return;
            }

            List<ChatAttachment> attachments = new List<ChatAttachment>(_attachments);
            _attachments.Clear();
            RebuildChips();
            _input.value = string.Empty;
            HideSuggestions();
            OnSubmit?.Invoke(text, attachments);
        }

        private void RebuildChips()
        {
            _chips.Clear();
            foreach (ChatAttachment attachment in _attachments)
            {
                VisualElement chip = new VisualElement();
                chip.AddToClassList("pl-chip-removable");
                chip.Add(ParleyStyles.Text((attachment.Kind == AttachmentKind.Image ? "🖼 " : "📎 ") + attachment.Label, "pl-chip-removable__label"));
                ChatAttachment captured = attachment;
                chip.Add(ParleyStyles.Button("✕", () => RemoveAttachment(captured), "pl-button--chip"));
                _chips.Add(chip);
            }

            ParleyStyles.SetVisible(_chips, _attachments.Count > 0);
        }

        private void RemoveAttachment(ChatAttachment attachment)
        {
            _attachments.Remove(attachment);
            RebuildChips();
        }

        private void ShowAttachMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Selection"), false, () => AddOrWarn(Attachments.FromSelection(), "Nothing is selected."));
            menu.AddItem(new GUIContent("Console errors"), false, () => AddOrWarn(Attachments.FromConsoleErrors(), "The console has no errors."));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Scene view screenshot"), false, () => AddOrWarn(Attachments.FromSceneView(), "No Scene view is open."));
            menu.AddItem(new GUIContent("Game camera screenshot"), false, () => AddOrWarn(Attachments.FromGameCamera(), "No camera found."));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("File…"), false, AttachFile);
            menu.DropDown(new Rect(worldBound.x, worldBound.y, 0.0f, 0.0f));
        }

        private void AttachFile()
        {
            string path = EditorUtility.OpenFilePanel("Attach file", ProjectPaths.Root, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                AddOrWarn(Attachments.FromPath(path), "Could not read " + path);
            }
        }

        private void AddOrWarn(ChatAttachment attachment, string warning)
        {
            if (attachment == null)
            {
                EditorUtility.DisplayDialog("Parley", warning, "OK");
                return;
            }

            AddAttachment(attachment);
        }

        private void UpdateSuggestions()
        {
            _suggestionItems.Clear();
            string text = _input.value ?? string.Empty;
            int cursor = Mathf.Clamp(_input.cursorIndex, 0, text.Length);
            string before = text.Substring(0, cursor);
            if (IsCommandQuery(text, before))
            {
                AddCommandSuggestions(before.Substring(1));
            }
            else
            {
                int at = before.LastIndexOf('@');
                if (at >= 0 && (at == 0 || char.IsWhiteSpace(before[at - 1])) && before.IndexOf(' ', at) < 0 && cursor - at > MinAssetQueryLength)
                {
                    AddAssetSuggestions(before.Substring(at + 1));
                }
            }

            _selectedSuggestion = 0;
            RenderSuggestions();
        }

        private void AddCommandSuggestions(string query)
        {
            _suggestedNames.Clear();
            foreach (SlashCommandInfo command in _commands())
            {
                if (command.Name == null || !command.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) || !_suggestedNames.Add(command.Name))
                {
                    continue;
                }

                string name = "/" + command.Name + (string.IsNullOrEmpty(command.ArgumentHint) ? string.Empty : " " + command.ArgumentHint);
                _suggestionItems.Add(("/" + command.Name + " ", name, command.Description));
                if (_suggestionItems.Count >= MaxSuggestions)
                {
                    return;
                }
            }
        }

        private void AddAssetSuggestions(string query)
        {
            foreach (string guid in AssetDatabase.FindAssets(query))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || Directory.Exists(path))
                {
                    continue;
                }

                _suggestionItems.Add((path + " ", path, null));
                if (_suggestionItems.Count >= MaxSuggestions)
                {
                    return;
                }
            }
        }

        private void RenderSuggestions()
        {
            _suggestions.Clear();
            VisualElement selected = null;
            for (int i = 0; i < _suggestionItems.Count; i++)
            {
                int index = i;
                string name = _suggestionItems[i].name;
                string detail = _suggestionItems[i].detail;
                VisualElement row = new VisualElement { tooltip = detail };
                row.AddToClassList("pl-suggestion");
                row.EnableInClassList("pl-suggestion--selected", i == _selectedSuggestion);
                Label nameLabel = new Label(name) { enableRichText = false };
                nameLabel.AddToClassList("pl-suggestion__name");
                row.Add(nameLabel);
                if (!string.IsNullOrEmpty(detail))
                {
                    Label detailLabel = new Label(detail) { enableRichText = false };
                    detailLabel.AddToClassList("pl-suggestion__detail");
                    row.Add(detailLabel);
                }

                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    ApplySuggestion(index);
                    evt.StopPropagation();
                });

                _suggestions.Add(row);
                if (i == _selectedSuggestion)
                {
                    selected = row;
                }
            }

            ParleyStyles.SetVisible(_suggestions, _suggestionItems.Count > 0);
            if (selected != null)
            {
                schedule.Execute(() => _suggestions.ScrollTo(selected));
            }
        }

        private void HideSuggestions()
        {
            _suggestionItems.Clear();
            RenderSuggestions();
        }

        private void ApplySuggestion(int index)
        {
            if (index < 0 || index >= _suggestionItems.Count)
            {
                return;
            }

            string insert = _suggestionItems[index].insert;
            string text = _input.value ?? string.Empty;
            int cursor = Mathf.Clamp(_input.cursorIndex, 0, text.Length);
            string before = text.Substring(0, cursor);
            string after = text.Substring(cursor);
            int start = text.StartsWith("/", StringComparison.Ordinal) && before.IndexOf(' ') < 0 ? 0 : before.LastIndexOf('@');
            if (start < 0)
            {
                start = cursor;
            }

            HideSuggestions();
            _input.SetValueWithoutNotify(before.Substring(0, start) + insert + after);
            int caret = start + insert.Length;
            _input.SelectRange(caret, caret);
            _input.Focus();
        }

        private void MoveSelection(int delta)
        {
            _selectedSuggestion = (_selectedSuggestion + delta + _suggestionItems.Count) % _suggestionItems.Count;
            RenderSuggestions();
        }

        private bool HandleSuggestionKey(KeyDownEvent evt, bool enter)
        {
            if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
            {
                MoveSelection(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                return true;
            }

            if (evt.keyCode == KeyCode.Tab || evt.character == '\t' || (enter && !evt.shiftKey))
            {
                if (evt.keyCode != KeyCode.None)
                {
                    ApplySuggestion(_selectedSuggestion);
                }

                return true;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                HideSuggestions();
                return true;
            }

            return false;
        }

        private void InputChangedHandler(ChangeEvent<string> evt)
        {
            schedule.Execute(UpdateSuggestions);
        }

        private void KeyDownHandler(KeyDownEvent evt)
        {
            bool enter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.character == '\n' || evt.character == '\r';
            if (_suggestionItems.Count > 0 && HandleSuggestionKey(evt, enter))
            {
                evt.StopImmediatePropagation();
                return;
            }

            if (!enter)
            {
                return;
            }

            if (evt.keyCode != KeyCode.None)
            {
                if (evt.shiftKey)
                {
                    InsertNewLine();
                }
                else
                {
                    Submit();
                }
            }

            evt.StopImmediatePropagation();
        }

        private void InsertNewLine()
        {
            string text = _input.value ?? string.Empty;
            int start = Mathf.Clamp(Mathf.Min(_input.cursorIndex, _input.selectIndex), 0, text.Length);
            int end = Mathf.Clamp(Mathf.Max(_input.cursorIndex, _input.selectIndex), start, text.Length);
            _input.value = text.Remove(start, end - start).Insert(start, "\n");
            _input.SelectRange(start + 1, start + 1);
        }

        private void DragUpdatedHandler(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length > 0 || DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private void DragPerformHandler(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            foreach (string path in DragAndDrop.paths)
            {
                if (_imageExtensions.Contains(Path.GetExtension(path)))
                {
                    AddAttachment(Attachments.FromPath(path));
                    continue;
                }

                InsertText("`" + ProjectPaths.ToProjectRelative(ProjectPaths.Resolve(path)) + "`");
            }

            foreach (Object target in DragAndDrop.objectReferences)
            {
                string assetPath = AssetDatabase.GetAssetPath(target);
                if (string.IsNullOrEmpty(assetPath))
                {
                    InsertText("`" + UnityObjectPaths.Describe(target) + "`");
                }
            }
        }
    }
}