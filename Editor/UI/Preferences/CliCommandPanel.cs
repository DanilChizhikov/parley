using System;
using System.Text;
using DTech.Parley.Editor.Agents.Claude;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class CliCommandPanel : VisualElement
    {
        private const int MaxLogChars = 12000;
        private const int VisibleTokenLength = 16;

        private readonly Label _log;
        private readonly Button _openUrl;
        private readonly Button _cancel;
        private readonly FeedbackRow _codeRow;
        private readonly StringBuilder _output = new ();

        private ClaudeCliCommand _command;
        private string _url;
        private Action<string> _onLine;
        private Action<int> _onExit;
        private int _generation;

        public CliCommandPanel()
        {
            AddToClassList("pl-cli");
            _log = ParleyStyles.Text(string.Empty, "pl-cli__log");
            _log.selection.isSelectable = true;
            ParleyStyles.UseMonospace(_log);
            Add(_log);
            VisualElement buttons = new VisualElement();
            buttons.AddToClassList("pl-request__buttons");
            _openUrl = ParleyStyles.Button("Open sign-in page", () => Application.OpenURL(_url), "pl-button--primary");
            _cancel = ParleyStyles.Button("Cancel", Cancel, "pl-button--ghost");
            buttons.Add(_openUrl);
            buttons.Add(_cancel);
            Add(buttons);
            _codeRow = new FeedbackRow("Paste the code from the browser if asked", "Submit code", SubmitCode);
            Add(_codeRow);
            RegisterCallback<DetachFromPanelEvent>(DetachedHandler);
            SetRunning(false);
            ParleyStyles.SetVisible(this, false);
        }

        public async void Run(CliCommandRequest request)
        {
            Cancel();
            int generation = _generation;
            _output.Clear();
            _url = null;
            _onLine = request.OnLine;
            _onExit = request.OnExit;
            ParleyStyles.SetVisible(this, true);
            Append("$ claude " + string.Join(" ", request.Arguments));
            ClaudeCliCommand command = await ClaudeAuthCommands.StartAsync(request.Profile, request.Arguments);
            if (generation != _generation)
            {
                command?.Dispose();
                return;
            }

            if (command == null)
            {
                Append("Claude Code CLI was not found.");
                return;
            }

            _command = command;
            command.OnOutput += OutputHandler;
            command.OnExited += ExitedHandler;
            if (!command.Start(out string error))
            {
                Append("Failed to start: " + error);
                Release();
                return;
            }

            SetRunning(true);
        }

        public void Cancel()
        {
            _generation++;
            if (_command == null)
            {
                return;
            }

            _command.Cancel();
            Release();
        }

        private void Release()
        {
            if (_command != null)
            {
                _command.OnOutput -= OutputHandler;
                _command.OnExited -= ExitedHandler;
                _command.Dispose();
                _command = null;
            }

            SetRunning(false);
        }

        private void SubmitCode(string code)
        {
            if (_command == null)
            {
                return;
            }

            _command.Send(code);
            Append("(code sent)");
        }

        private void SetRunning(bool running)
        {
            ParleyStyles.SetVisible(_cancel, running);
            ParleyStyles.SetVisible(_codeRow, running);
            ParleyStyles.SetVisible(_openUrl, running && !string.IsNullOrEmpty(_url));
        }

        private void Append(string line)
        {
            _output.AppendLine(line);
            if (_output.Length > MaxLogChars)
            {
                _output.Remove(0, _output.Length - MaxLogChars);
            }

            _log.text = _output.ToString().TrimEnd();
        }

        private void OutputHandler(string line)
        {
            string clean = ClaudeAuthCommands.StripAnsi(line);
            string url = ClaudeAuthCommands.ExtractUrl(clean);
            if (url != null && _url == null)
            {
                _url = url;
                SetRunning(true);
                if (ParleyUserSettings.instance.OpenLoginLinks)
                {
                    Application.OpenURL(url);
                }
            }

            string token = ClaudeAuthCommands.ExtractToken(clean);
            Append(token == null ? clean : clean.Replace(token, token.Substring(0, Math.Min(VisibleTokenLength, token.Length)) + "…"));
            _onLine?.Invoke(clean);
        }

        private void ExitedHandler(int code)
        {
            Append("(exited with code " + code + ")");
            Release();
            _onExit?.Invoke(code);
        }

        private void DetachedHandler(DetachFromPanelEvent evt)
        {
            Cancel();
        }
    }
}