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

        private readonly TextField _baseUrl;
        private readonly TextField _model;
        private readonly FloatField _temperature;

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
            _model.style.flexGrow = 1.0f;
            modelRow.Add(_model);
            modelRow.Add(ParleyStyles.Button("Pick…", PickModel, "pl-button--small"));
            Add(modelRow);
            _temperature = new FloatField("Temperature") { value = profile.Temperature };
            _temperature.RegisterValueChangedCallback(TemperatureChangedHandler);
            Add(_temperature);
            Add(Int("Max output tokens", profile.MaxTokens, value => Profile.MaxTokens = Mathf.Max(MinOutputTokens, value)));
            Add(Int("Context window (tokens)", profile.ContextWindow, value => Profile.ContextWindow = Mathf.Max(MinContextWindow, value)));
            Add(Int("Max model calls per turn", profile.MaxTurns, value => Profile.MaxTurns = Mathf.Clamp(value, 1, MaxModelCalls)));
            Add(BoolField("Vision (send images)", profile.Vision, value => Profile.Vision = value));
            Toggle textTools = BoolField("Text tool calls (<tool_call> tags)", profile.TextToolCalls, value => Profile.TextToolCalls = value);
            textTools.tooltip = "For models or servers without native function calling.";
            Add(textTools);
            Add(ButtonRow(("Test connection", TestConnection)));
            AddFooter(null);
        }

        private static IntegerField Int(string label, int value, Func<int, int> apply)
        {
            IntegerField field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(apply(evt.newValue));
                Persist();
            });

            return field;
        }

        private Task<List<string>> ListModelsAsync()
        {
            string apiKey = SecretStores.Default.TryGet(SecretStores.Key(Profile.Id, SecretFields.LocalApiKey), out string key) ? key : null;
            return new OpenAiCompatClient(Profile.LocalBaseUrl, apiKey).ListModelsAsync(CancellationToken.None);
        }

        private async void TestConnection()
        {
            Status.text = "Connecting…";
            try
            {
                List<string> models = await ListModelsAsync();
                Status.text = "Connected. " + models.Count + " model(s): " + string.Join(", ", models);
            }
            catch (Exception exception)
            {
                Status.text = "Failed: " + exception.Message;
            }
        }

        private async void PickModel()
        {
            List<string> models;
            try
            {
                models = await ListModelsAsync();
            }
            catch (Exception exception)
            {
                Status.text = "Could not list models: " + exception.Message;
                return;
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
            Profile.Temperature = Mathf.Clamp(evt.newValue, MinTemperature, MaxTemperature);
            if (Profile.Temperature != evt.newValue)
            {
                _temperature.SetValueWithoutNotify(Profile.Temperature);
            }

            Persist();
        }
    }
}