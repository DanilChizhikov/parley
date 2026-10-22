using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Agents.Local;
using DTech.Parley.Editor.Secrets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class LocalProfileEditor : ProfileEditor
    {
        private const float MinTemperature = 0.0f;
        private const float MaxTemperature = 2.0f;
        private const int MinOutputTokens = 256;
        private const int MinContextWindow = 2048;
        private const int MaxModelCalls = 500;
        private const int ListedModels = 3;

        private readonly TextField _baseUrl;
        private readonly TextField _model;
        private readonly Slider _temperature;
        private readonly Button _pick;

        public LocalProfileEditor(ParleyProfile profile) : base(profile, "Local model (OpenAI-compatible server, Parley's own agent)")
        {
            EnumField preset = new EnumField("Server", profile.Preset);
            preset.RegisterValueChangedCallback(PresetChangedHandler);
            Add(preset);
            _baseUrl = Text(new TextFieldRequest("Base URL", profile.LocalBaseUrl, value => Profile.LocalBaseUrl = value, "http://localhost:1234/v1"));
            Add(_baseUrl);
            Add(new SecretField("API key (optional)", profile.Id, SecretFields.LocalApiKey, "For servers that need one"));
            VisualElement modelRow = new VisualElement();
            modelRow.AddToClassList("pl-profile__row");
            _model = Text(new TextFieldRequest("Model", profile.Model, value => Profile.Model = value, "Empty = first model the server lists"));
            modelRow.Add(_model);
            _pick = SmallButton("Pick…", PickModel, "Choose from the models the server lists");
            modelRow.Add(_pick);
            Add(modelRow);
            _temperature = new Slider("Temperature", MinTemperature, MaxTemperature) { value = profile.Temperature, showInputField = true,
                tooltip = "0 = focused and repeatable, 2 = most random" };
            _temperature.RegisterValueChangedCallback(TemperatureChangedHandler);
            Add(_temperature);
            Add(Int("Max output tokens", profile.MaxTokens, MinOutputTokens, int.MaxValue, value => Profile.MaxTokens = value));
            Add(Int("Context window (tokens)", profile.ContextWindow, MinContextWindow, int.MaxValue, value => Profile.ContextWindow = value));
            Add(Int("Max model calls per turn", profile.MaxTurns, 1, MaxModelCalls, value => Profile.MaxTurns = value));
            Add(BoolField("Vision (send images)", profile.Vision, value => Profile.Vision = value));
            Toggle textTools = BoolField("Text tool calls (<tool_call> tags)", profile.TextToolCalls, value => Profile.TextToolCalls = value);
            textTools.tooltip = "For models or servers without native function calling.";
            Add(textTools);
            Add(ButtonRow(SmallButton("Test connection", TestConnection, "List the server's models to check the URL and key", true)));
            AddFooter(null);
        }

        private IntegerField Int(string label, int value, int min, int max, Action<int> apply)
        {
            string range = max == int.MaxValue ? "At least " + min : "From " + min + " to " + max;
            IntegerField field = new IntegerField(label) { value = value, isDelayed = true, tooltip = range };
            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, min, max);
                field.SetValueWithoutNotify(clamped);
                apply(clamped);
                Persist();
                if (clamped != evt.newValue)
                {
                    SetStatus(label + ": " + range.ToLowerInvariant() + ", set to " + clamped + ".");
                }
            });

            return field;
        }

        private Task<List<string>> ListModelsAsync()
        {
            string apiKey = SecretStores.Default.TryGet(SecretStores.Key(Profile.Id, SecretFields.LocalApiKey), out string key) ? key : null;
            return new OpenAiCompatClient(Profile.LocalBaseUrl, apiKey).ListModelsAsync(CancellationToken.None);
        }

        private static string DescribeModels(List<string> models)
        {
            if (models.Count == 0)
            {
                return "Connected · the server lists no models";
            }

            string listed = string.Join(", ", models.GetRange(0, Math.Min(ListedModels, models.Count)));
            return "Connected · " + models.Count + (models.Count == 1 ? " model (" : " models (") + listed + (models.Count > ListedModels ? ", …)" : ")");
        }

        private void TestConnection()
        {
            RunBusy(async () =>
            {
                SetStatus("Connecting…");
                try
                {
                    List<string> models = await ListModelsAsync();
                    SetStatus(DescribeModels(models), true);
                    Status.tooltip = string.Join("\n", models);
                }
                catch (Exception exception)
                {
                    SetStatus("Failed: " + exception.Message, false);
                }
            });
        }

        private async void PickModel()
        {
            List<string> models;
            _pick.text = "Loading…";
            _pick.SetEnabled(false);
            try
            {
                models = await ListModelsAsync();
            }
            catch (Exception exception)
            {
                SetStatus("Could not list models: " + exception.Message, false);
                return;
            }
            finally
            {
                _pick.text = "Pick…";
                _pick.SetEnabled(true);
            }

            GenericMenu menu = new GenericMenu();
            foreach (string model in models)
            {
                string captured = model;
                menu.AddItem(new GUIContent(model.Replace("/", "∕")), model == Profile.Model, () => SelectModel(captured));
            }

            if (models.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("The server lists no models"));
            }

            menu.DropDown(_model.worldBound);
        }

        private void SelectModel(string model)
        {
            Profile.Model = model;
            _model.SetValueWithoutNotify(model);
            Persist();
        }

        private void PresetChangedHandler(ChangeEvent<Enum> evt)
        {
            LocalPreset previous = (LocalPreset)evt.previousValue;
            Profile.Preset = (LocalPreset)evt.newValue;
            if (string.IsNullOrEmpty(Profile.LocalBaseUrl) || Profile.LocalBaseUrl == LocalPresets.DefaultBaseUrl(previous))
            {
                Profile.LocalBaseUrl = LocalPresets.DefaultBaseUrl(Profile.Preset);
                _baseUrl.SetValueWithoutNotify(Profile.LocalBaseUrl);
            }

            Persist();
        }

        private void TemperatureChangedHandler(ChangeEvent<float> evt)
        {
            Profile.Temperature = (float)Math.Round(Mathf.Clamp(evt.newValue, MinTemperature, MaxTemperature), 2);
            Persist();
        }
    }
}