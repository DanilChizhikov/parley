using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class QuestionCard : VisualElement
    {
        private readonly PendingRequest _request;
        private readonly Action<Decision, string> _respond;
        private readonly List<QuestionState> _questions = new ();
        private readonly Button _submit;

        public QuestionCard(PendingRequest request, Action<Decision, string> respond)
        {
            _request = request;
            _respond = respond;
            AddToClassList("pl-question");
            Add(ParleyStyles.Text("The agent has questions", "pl-request__title"));
            if (request.Input["questions"] is JArray questions)
            {
                foreach (JToken question in questions)
                {
                    if (question is JObject item)
                    {
                        QuestionState state = new QuestionState(item, UpdateSubmit);
                        _questions.Add(state);
                        Add(state.Root);
                    }
                }
            }

            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-request__buttons");
            _submit = ParleyStyles.Button("Submit answers", Submit, "pl-button--primary");
            buttons.Add(_submit);
            buttons.Add(ParleyStyles.Button("Dismiss", () => _respond(Decision.Deny("The user dismissed the questions."), "Dismissed")));
            Add(buttons);
            Add(new FeedbackRow("Or reply in your own words instead…", "Send reply", Reply));
            UpdateSubmit();
        }

        private void UpdateSubmit()
        {
            bool ready = _questions.Count > 0;
            foreach (QuestionState question in _questions)
            {
                ready &= question.HasAnswer;
            }

            _submit?.SetEnabled(ready);
        }

        private void Submit()
        {
            JObject answers = new JObject();
            StringBuilder summary = new StringBuilder("Answered: ");
            foreach (QuestionState question in _questions)
            {
                answers[question.Text] = question.Answer();
                summary.Append(question.Header).Append(" → ").Append(question.AnswerText()).Append("; ");
            }

            JObject updated = new JObject { ["questions"] = _request.Input["questions"], ["answers"] = answers };
            _respond(Decision.AllowWith(updated), summary.ToString().TrimEnd(' ', ';'));
        }

        private void Reply(string text)
        {
            JObject updated = new JObject { ["questions"] = _request.Input["questions"], ["answers"] = new JObject(), ["response"] = text };
            _respond(Decision.AllowWith(updated), "Replied: " + text);
        }

        private sealed class QuestionState
        {
            private readonly bool _multi;
            private readonly List<(Toggle toggle, string label)> _options = new ();
            private readonly Toggle _otherToggle;
            private readonly TextField _otherField;
            private readonly MarkdownView _preview;
            private readonly Action _changed;

            public VisualElement Root { get; } = new ();
            public string Text { get; }
            public string Header { get; }

            public bool HasAnswer
            {
                get
                {
                    foreach ((Toggle toggle, string _) in _options)
                    {
                        if (toggle.value)
                        {
                            return true;
                        }
                    }

                    return _otherToggle.value && !string.IsNullOrWhiteSpace(_otherField.value);
                }
            }

            public QuestionState(JObject question, Action changed)
            {
                _changed = changed;
                Text = (string)question["question"] ?? string.Empty;
                Header = (string)question["header"] ?? "Question";
                _multi = (bool?)question["multiSelect"] == true;
                Root.AddToClassList("pl-question__item");
                VisualElement title = new VisualElement();
                title.AddToClassList("pl-question__title");
                title.Add(ParleyStyles.Text(Header, "pl-chip"));
                title.Add(ParleyStyles.Text(Text, "pl-question__text"));
                Root.Add(title);
                if (_multi)
                {
                    Root.Add(ParleyStyles.Text("Select all that apply", ParleyStyles.Muted));
                }

                VisualElement content = new VisualElement();
                content.AddToClassList("pl-question__content");
                VisualElement options = new VisualElement();
                options.AddToClassList("pl-question__options");
                content.Add(options);
                string firstPreview = AddOptions(options, question["options"] as JArray);
                VisualElement other = new VisualElement();
                other.AddToClassList("pl-question__other");
                _otherToggle = new Toggle { text = "Other" };
                _otherField = new TextField();
                _otherField.textEdition.placeholder = "Type your own answer";
                _otherField.AddToClassList("pl-question__other-field");
                _otherToggle.RegisterValueChangedCallback(evt => OnToggled(_otherToggle, evt.newValue, null));
                _otherField.RegisterValueChangedCallback(OtherChangedHandler);
                other.Add(_otherToggle);
                other.Add(_otherField);
                options.Add(other);
                if (firstPreview != null)
                {
                    _preview = new MarkdownView();
                    _preview.AddToClassList("pl-question__preview");
                    content.AddToClassList("pl-question__content--preview");
                    content.Add(_preview);
                    ShowPreview(firstPreview);
                }

                Root.Add(content);
            }

            public JToken Answer()
            {
                List<string> labels = Selected();
                if (_multi)
                {
                    return new JArray(labels.ToArray());
                }

                return labels.Count > 0 ? labels[0] : string.Empty;
            }

            public string AnswerText()
            {
                return string.Join(", ", Selected());
            }

            private string AddOptions(VisualElement options, JArray list)
            {
                if (list == null)
                {
                    return null;
                }

                string firstPreview = null;
                foreach (JToken option in list)
                {
                    string label = (string)option["label"] ?? string.Empty;
                    string description = (string)option["description"];
                    string preview = (string)option["preview"];
                    if (firstPreview == null && !string.IsNullOrEmpty(preview))
                    {
                        firstPreview = preview;
                    }
                    VisualElement row = new VisualElement();
                    row.AddToClassList("pl-question__option");
                    Toggle toggle = new Toggle { text = label };
                    toggle.RegisterValueChangedCallback(evt => OnToggled(toggle, evt.newValue, preview));
                    row.Add(toggle);
                    if (!string.IsNullOrEmpty(description))
                    {
                        row.Add(ParleyStyles.Text(description, "pl-question__description"));
                    }

                    row.RegisterCallback<PointerEnterEvent>(_ => ShowPreview(preview));
                    options.Add(row);
                    _options.Add((toggle, label));
                }

                return firstPreview;
            }

            private List<string> Selected()
            {
                List<string> labels = new List<string>();
                foreach ((Toggle toggle, string label) in _options)
                {
                    if (toggle.value)
                    {
                        labels.Add(label);
                    }
                }

                if (_otherToggle.value && !string.IsNullOrWhiteSpace(_otherField.value))
                {
                    labels.Add(_otherField.value.Trim());
                }

                return labels;
            }

            private void OnToggled(Toggle source, bool value, string preview)
            {
                if (value && !_multi)
                {
                    foreach ((Toggle toggle, string _) in _options)
                    {
                        if (toggle != source)
                        {
                            toggle.SetValueWithoutNotify(false);
                        }
                    }

                    if (source != _otherToggle)
                    {
                        _otherToggle.SetValueWithoutNotify(false);
                    }
                }

                if (value)
                {
                    ShowPreview(preview);
                }

                _changed();
            }

            private void OtherChangedHandler(ChangeEvent<string> evt)
            {
                if (!string.IsNullOrEmpty(evt.newValue) && !_otherToggle.value)
                {
                    _otherToggle.value = true;
                }

                _changed();
            }

            private void ShowPreview(string preview)
            {
                if (_preview != null && !string.IsNullOrEmpty(preview))
                {
                    _preview.SetMarkdown(preview);
                }
            }
        }
    }
}