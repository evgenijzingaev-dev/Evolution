using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Evolution.Tests
{
    public sealed class FriendlyOceanHudInputTests
    {
        static readonly Rect Toast = new Rect(1130, 88, 292, 162);

        // Resolve the production class from its actual Unity asmdef assembly.
        // Resolve the actual production implementation, never a test-only copy.
        static bool NoticeHit(Vector2 point, Rect area, int count, bool journal, bool covered)
        {
            var type = Type.GetType("Evolution.Runtime.FriendlyOceanHud, Evolution.Runtime");
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
            Assert.IsFalse(NoticeHit(new Vector2(1150, 168), Toast, 1, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 190), Toast, 1, false, false));
        }

        [Test]
        public void TwoNoticesDoNotBlockGapOrOutside()
        {
            Assert.IsTrue(NoticeHit(new Vector2(1150, 174), Toast, 2, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 168), Toast, 2, false, false));
            Assert.IsFalse(NoticeHit(new Vector2(1150, 252), Toast, 2, false, false));
        }

        [Test]
        public void JournalBlocksWholePanelAndSettingsTakePriority()
        {
            var gap = new Vector2(1150, 168);
            Assert.IsTrue(NoticeHit(gap, Toast, 0, true, false));
            Assert.IsFalse(NoticeHit(gap, Toast, 2, true, true));
        }

        [Test]
        public void MoreThanTwoNoticesNeverExpandInputRegion()
        {
            Assert.IsFalse(NoticeHit(new Vector2(1150, 252), Toast, 99, false, false));
        }

        static bool Marker(Vector3 p,Rect viewport,Rect[] blockers,out Rect label,out Vector2 anchor)
        {
            var type=Type.GetType("Evolution.Runtime.FriendlyOceanHud, Evolution.Runtime");
            var args=new object[] {p,viewport,blockers,null,null};
            bool result=(bool)type.GetMethod("TryWorldLabel").Invoke(null,args);
            label=(Rect)args[3];anchor=(Vector2)args[4];return result;
        }

        [TestCase(-1,300,1)] [TestCase(1500,300,1)]
        [TestCase(700,-1,1)] [TestCase(700,850,1)] [TestCase(700,300,-1)]
        public void HiddenTargetsNeverBecomeClampedMarkers(float x,float y,float depth)
        {
            Assert.IsFalse(Marker(new Vector3(x,y,depth),new Rect(18,80,1404,668),new Rect[0],out _,out _));
        }

        [TestCase(1366,768)] [TestCase(1920,1080)] [TestCase(2560,1440)]
        public void MarkerSweepKeepsActualEndpointAndShortConnectorAwayFromPanels(int width,int height)
        {
            float scale=Mathf.Min(width/1440f,height/810f),w=width/scale,h=height/scale;
            var viewport=new Rect(18,80,w-36,h-152);
            var blockers=new[] {new Rect(18,90,272,170),new Rect(w-310,264,292,300),new Rect(w-310,88,292,162),new Rect(w/2-224,h-208,448,46)};
            int visible=0;
            for(float x=20;x<w;x+=41)for(float y=82;y<h;y+=37) {
                var p=new Vector3(x,y,1);
                if(!Marker(p,viewport,blockers,out var label,out var anchor))continue;
                visible++;
                Assert.That(Vector2.Distance(anchor,new Vector2(x,y)),Is.EqualTo(18).Within(.001));
                Assert.GreaterOrEqual(label.xMin,viewport.xMin);Assert.LessOrEqual(label.xMax,viewport.xMax);
                Assert.GreaterOrEqual(label.yMin,viewport.yMin);Assert.LessOrEqual(label.yMax,viewport.yMax);
                foreach(var box in blockers){Assert.IsFalse(label.Overlaps(box));Assert.IsFalse(box.Contains(new Vector2(x,y)));}
            }
            Assert.Greater(visible,50);
        }

        [Test]
        public void TargetUnderPanelIsHiddenAndBlockedPreferredLabelCanUseAnotherSide()
        {
            var viewport=new Rect(18,80,1404,668);var p=new Vector3(700,400,1);
            Assert.IsFalse(Marker(p,viewport,new[] {new Rect(690,390,20,20)},out _,out _));
            Assert.IsTrue(Marker(p,viewport,new[] {new Rect(588,340,224,42)},out var label,out _));
            Assert.Greater(label.yMin,p.y);
        }
    }
}
