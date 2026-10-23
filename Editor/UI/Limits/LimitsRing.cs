using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Parley.Editor.UI
{
    internal sealed class LimitsRing : VisualElement
    {
        private const float BottomAngle = 90.0f;
        private const float HalfTurn = 180.0f;
        private const float FullTurn = 360.0f;
        private const float MinSweep = 0.5f;
        private const double FullPercent = 100.0;

        private static readonly CustomStyleProperty<Color> SessionColorProperty = new ("--pl-ring-session");
        private static readonly CustomStyleProperty<Color> WeeklyColorProperty = new ("--pl-ring-weekly");
        private static readonly CustomStyleProperty<Color> TrackColorProperty = new ("--pl-ring-track");

        private Color _sessionColor = new (0.30f, 0.55f, 0.96f);
        private Color _weeklyColor = new (0.61f, 0.50f, 0.94f);
        private Color _trackColor = new (0.5f, 0.5f, 0.5f, 0.35f);
        private RateLimits _limits;

        private Vector2 Center => contentRect.center;

        private float Radius => Mathf.Min(contentRect.width, contentRect.height) * 0.5f;

        public LimitsRing()
        {
            AddToClassList("pl-limits-ring");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += GenerateHandler;
            RegisterCallback<CustomStyleResolvedEvent>(CustomStyleResolvedHandler);
        }

        public void SetLimits(RateLimits limits)
        {
            _limits = limits;
            MarkDirtyRepaint();
        }

        private static float Sweep(RateLimitWindow? window, DateTimeOffset now)
        {
            return window.HasValue ? (float)(HalfTurn * window.Value.RemainingPercent(now) / FullPercent) : 0.0f;
        }

        private void GenerateHandler(MeshGenerationContext context)
        {
            if (Radius <= 0.0f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            painter.fillColor = _trackColor;
            painter.BeginPath();
            painter.Arc(Center, Radius, Angle.Degrees(0.0f), Angle.Degrees(FullTurn), ArcDirection.Clockwise);
            painter.ClosePath();
            painter.Fill();
            if (_limits == null || _limits.Unavailable)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            float session = Sweep(_limits.Session, now);
            if (session >= MinSweep)
            {
                painter.fillColor = _sessionColor;
                FillSector(painter, session, ArcDirection.Clockwise);
            }

            float weekly = Sweep(_limits.Weekly, now);
            if (weekly >= MinSweep)
            {
                painter.fillColor = _weeklyColor;
                FillSector(painter, weekly, ArcDirection.CounterClockwise);
            }
        }

        private void FillSector(Painter2D painter, float sweep, ArcDirection direction)
        {
            float endAngle = direction == ArcDirection.Clockwise ? BottomAngle + sweep : BottomAngle - sweep;
            painter.BeginPath();
            painter.MoveTo(Center);
            painter.Arc(Center, Radius, Angle.Degrees(BottomAngle), Angle.Degrees(endAngle), direction);
            painter.ClosePath();
            painter.Fill();
        }

        private void CustomStyleResolvedHandler(CustomStyleResolvedEvent evt)
        {
            ICustomStyle style = evt.customStyle;
            if (style.TryGetValue(SessionColorProperty, out Color session))
            {
                _sessionColor = session;
            }

            if (style.TryGetValue(WeeklyColorProperty, out Color weekly))
            {
                _weeklyColor = weekly;
            }

            if (style.TryGetValue(TrackColorProperty, out Color track))
            {
                _trackColor = track;
            }

            MarkDirtyRepaint();
        }
    }
}
