using System;

namespace Evolution.Presentation
{
    public static class WaterOptics
    {
        // Smooth transition over physical coordinates, unlike Mathf.SmoothStep(from,to,t)
        // whose third argument is already normalized and whose result is in [from,to].
        public static float SmoothRange(float from,float to,float value)
        {
            if(to<=from)return value>=to?1:0;
            float t=Math.Max(0,Math.Min(1,(value-from)/(to-from)));
            return t*t*(3-2*t);
        }
    }
}
