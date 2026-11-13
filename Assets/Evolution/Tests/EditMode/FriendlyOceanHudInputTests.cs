using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Evolution.Tests
{
    public sealed class FriendlyOceanHudInputTests
    {
        static readonly Rect Toast = new Rect(1130, 88, 292, 150);

        // Unity test assemblies do not automatically reference Assembly-CSharp.
        // Resolve the actual production implementation, never a test-only copy.
        static bool NoticeHit(Vector2 point, Rect area, int count, bool journal, bool covered)
        {
            var type = Type.GetType("Evolution.Runtime.FriendlyOceanHud, Assembly-CSharp");
            Assert.IsNotNull(type, "Production FriendlyOceanHud must be present");
            var method = type.GetMethod("NoticeHit", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "Production NoticeHit method must remain public");
            return (bool)method.Invoke(null, new object[] { point, area, count, journal, covered });
        }

        [Test]
        public void NoNoticeLeavesOceanClickable()
        {
            Assert.IsFalse(NoticeHit(new Vector2(1150, 100), Toast, 0, false, false));
        }

        [Test]
        public void OneNoticeBlocksOnlyItsVisibleCard()
        {
            Assert.IsTrue(NoticeHit(new Vector2(1150, 100), Toast, 1, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 162), Toast, 1, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 190), Toast, 1, false, false));
        }

        [Test]
        public void TwoNoticesDoNotBlockGapOrOutside()
        {
            Assert.IsTrue(NoticeHit(new Vector2(1150, 170), Toast, 2, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 162), Toast, 2, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 240), Toast, 2, false, false));
        }

        [Test]
        public void JournalBlocksWholePanelAndSettingsTakePriority()
        {
            var gap = new Vector2(1150, 162);
            Assert.IsTrue(NoticeHit(gap, Toast, 0, true, false));
            Assert.IsFalse(NoticeHit(gap, Toast, 2, true, true));
        }

        [Test]
        public void MoreThanTwoNoticesNeverExpandInputRegion()
        {
            Assert.IsFalse(NoticeHit(new Vector2(1150, 240), Toast, 99, false, false));
        }
    }
}
