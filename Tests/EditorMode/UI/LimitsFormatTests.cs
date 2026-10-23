using System;
using DTech.Parley.Editor;
using DTech.Parley.Editor.UI;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class LimitsFormatTests
    {
        [Test]
        public void CountdownPicksTheLargestUnits()
        {
            Assert.AreEqual("<1m", LimitsFormat.Countdown(TimeSpan.FromSeconds(30)));
            Assert.AreEqual("13m", LimitsFormat.Countdown(TimeSpan.FromMinutes(13.5)));
            Assert.AreEqual("2h 13m", LimitsFormat.Countdown(new TimeSpan(2, 13, 40)));
            Assert.AreEqual("3d 4h", LimitsFormat.Countdown(new TimeSpan(3, 4, 59, 0)));
        }

        [Test]
        public void RemainingIsRoundedDown()
        {
            DateTimeOffset now = DateTimeOffset.Now;

            Assert.AreEqual("99% left", LimitsFormat.Remaining(new RateLimitWindow(0.4, now.AddHours(1)), now));
            Assert.AreEqual("0% left", LimitsFormat.Remaining(new RateLimitWindow(130.0, now.AddHours(1)), now));
            Assert.AreEqual("—", LimitsFormat.Remaining(null, now));
        }

        [Test]
        public void ResetTimeShowsWeekdayForOtherDays()
        {
            DateTimeOffset now = new DateTimeOffset(DateTime.Today.AddHours(10));

            Assert.AreEqual("14:30", LimitsFormat.ResetTime(now.AddHours(4.5), now));
            Assert.AreEqual(now.AddDays(2).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture) + " 10:00", LimitsFormat.ResetTime(now.AddDays(2), now));
        }
    }
}
