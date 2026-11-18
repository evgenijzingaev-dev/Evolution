using System;
using Evolution.Core;

namespace Evolution.Presentation
{
    public static class VisualMotion
    {
        public static float Heading(float previous,float target,double seconds) =>
            previous+CellMotion.AngleDelta(target-previous)*(float)(1-Math.Exp(-16*Math.Max(0,seconds)));
        public static float Depth(int organismId,int tissueSlot,double time) =>
            (float)(.16*Math.Sin(organismId*2.39996)+.035*Math.Sin(time*1.3+organismId*.7+tissueSlot*.2));
    }
}
