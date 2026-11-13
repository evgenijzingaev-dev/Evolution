using Evolution.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Evolution.Tests
{
    public sealed class FriendlyOceanHudInputTests
    {
        static readonly Rect Toast = new Rect(1130, 88, 292, 150);

        [Test]
        public void NoNoticeLeavesOceanClickable()
        {
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 100), Toast, 0, false, false));
        }

        [Test]
        public void OneNoticeBlocksOnlyItsVisibleCard()
        {
            Assert.IsTrue(FriendlyOceanHud.NoticeHit(new Vector2(1150, 100), Toast, 1, false, false));
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 162), Toast, 1, false, false));
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 190), Toast, 1, false, false));
        }

        [Test]
        public void TwoNoticesDoNotBlockGapOrOutside()
        {
            Assert.IsTrue(FriendlyOceanHud.NoticeHit(new Vector2(1150, 170), Toast, 2, false, false));
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 162), Toast, 2, false, false));
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 240), Toast, 2, false, false));
        }

        [Test]
        public void JournalBlocksWholePanelAndSettingsTakePriority()
        {
            var gap = new Vector2(1150, 162);
            Assert.IsTrue(FriendlyOceanHud.NoticeHit(gap, Toast, 0, true, false));
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(gap, Toast, 2, true, true));
        }

        [Test]
        public void MoreThanTwoNoticesNeverExpandInputRegion()
        {
            Assert.IsFalse(FriendlyOceanHud.NoticeHit(new Vector2(1150, 240), Toast, 99, false, false));
        }
    }
}
