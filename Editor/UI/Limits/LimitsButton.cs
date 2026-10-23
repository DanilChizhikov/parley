using System;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class LimitsButton : Button
    {
        private const long TickMs = 30000;

        private readonly LimitsRing _ring;

        private Func<RateLimits> _source = () => null;
        private Action _refresh;

        public LimitsButton()
        {
            AddToClassList("pl-button");
            AddToClassList("pl-button--ghost");
            AddToClassList("pl-limits-button");
            clicked += ClickedHandler;
            _ring = new LimitsRing();
            _ring.AddToClassList("pl-limits-ring--small");
            Add(_ring);
            schedule.Execute(UpdateView).Every(TickMs);
            UpdateView();
        }

        public void SetSource(Func<RateLimits> source, Action refresh)
        {
            _source = source ?? (() => null);
            _refresh = refresh;
            UpdateView();
        }

        public void UpdateView()
        {
            RateLimits limits = _source();
            _ring.SetLimits(limits);
            tooltip = LimitsFormat.Tooltip(limits, DateTimeOffset.Now);
        }

        private void ClickedHandler()
        {
            _refresh?.Invoke();
            UnityEditor.PopupWindow.Show(worldBound, new LimitsPopup(_source));
        }
    }
}
