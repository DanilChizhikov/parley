using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class LimitsPopup : PopupWindowContent
    {
        private const long TickMs = 1000;

        private static readonly Vector2 WindowSize = new (320.0f, 160.0f);

        private readonly Func<RateLimits> _source;

        private LimitsRing _ring;
        private LimitRow _session;
        private LimitRow _weekly;
        private Label _footer;

        public LimitsPopup(Func<RateLimits> source)
        {
            _source = source ?? (() => null);
        }

        public override Vector2 GetWindowSize()
        {
            return WindowSize;
        }

        public override void OnGUI(Rect rect)
        {
        }

        public override VisualElement CreateGUI()
        {
            VisualElement root = new VisualElement();
            ParleyStyles.Apply(root);
            root.AddToClassList("pl-limits-popup");
            root.Add(ParleyStyles.Text("Plan limits", "pl-limits-popup__title"));
            VisualElement body = new VisualElement();
            body.AddToClassList("pl-limits-popup__body");
            _ring = new LimitsRing();
            _ring.AddToClassList("pl-limits-ring--large");
            body.Add(_ring);
            VisualElement rows = new VisualElement();
            rows.AddToClassList("pl-limits-popup__rows");
            _session = new LimitRow(LimitsFormat.SessionTitle, "pl-limits__swatch--session");
            _weekly = new LimitRow(LimitsFormat.WeeklyTitle, "pl-limits__swatch--weekly");
            rows.Add(_session.Root);
            rows.Add(_weekly.Root);
            body.Add(rows);
            root.Add(body);
            _footer = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
            _footer.AddToClassList("pl-limits-popup__footer");
            root.Add(_footer);
            UpdateView();
            root.schedule.Execute(UpdateView).Every(TickMs);
            return root;
        }

        private void UpdateView()
        {
            RateLimits limits = _source();
            DateTimeOffset now = DateTimeOffset.Now;
            _ring.SetLimits(limits);
            bool known = limits != null && !limits.Unavailable;
            _session.Show(known ? limits.Session : null, now);
            _weekly.Show(known ? limits.Weekly : null, now);
            _footer.text = limits == null
                ? LimitsFormat.NotLoadedText
                : limits.Unavailable
                    ? LimitsFormat.UnavailableText
                    : "Updated " + limits.UpdatedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private sealed class LimitRow
        {
            private readonly Label _value;
            private readonly Label _detail;

            public VisualElement Root { get; }

            public LimitRow(string title, string swatchClass)
            {
                Root = new VisualElement();
                Root.AddToClassList("pl-limits-popup__row");
                VisualElement header = new VisualElement();
                header.AddToClassList("pl-limits-popup__row-header");
                VisualElement swatch = new VisualElement();
                swatch.AddToClassList("pl-limits__swatch");
                swatch.AddToClassList(swatchClass);
                header.Add(swatch);
                header.Add(ParleyStyles.Text(title, "pl-limits-popup__row-title"));
                header.Add(ParleyStyles.Spacer());
                _value = ParleyStyles.Text(string.Empty, "pl-limits-popup__value");
                header.Add(_value);
                Root.Add(header);
                _detail = ParleyStyles.Text(string.Empty, ParleyStyles.Muted);
                _detail.AddToClassList("pl-limits-popup__detail");
                Root.Add(_detail);
            }

            public void Show(RateLimitWindow? window, DateTimeOffset now)
            {
                _value.text = LimitsFormat.Remaining(window, now);
                _detail.text = window.HasValue ? LimitsFormat.Detail(window, now) : string.Empty;
            }
        }
    }
}
