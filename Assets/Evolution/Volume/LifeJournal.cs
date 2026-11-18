using System;

namespace Evolution.Volume
{
    public enum LifeEventKind { Division, Death }
    // Only causes observable in the current physiology. There is no invented age-death rule.
    public enum DeathAppearance { Exhaustion, Damage }
    public struct LifeEvent
    {
        public long Sequence;
        public double Time;
        public LifeEventKind Kind;
        public DeathAppearance Cause;
        public VolumeAgent Agent,Child;
    }
    // Transient bounded notification channel, outside the save/energy/PRNG state.
    // Reused across active streaming rebuilds; archive catch-up uses a separate channel.
    public sealed class LifeJournal
    {
        public const int Capacity=256;
        readonly LifeEvent[] events=new LifeEvent[Capacity];
        public long Latest {get;private set;}
        public long Oldest=>Math.Max(1,Latest-Capacity+1);
        internal void Record(LifeEvent value)
        {value.Sequence=++Latest;events[(int)((Latest-1)%Capacity)]=value;}
        public bool TryRead(long sequence,out LifeEvent value)
        {
            if(sequence<Oldest||sequence>Latest){value=default;return false;}
            value=events[(int)((sequence-1)%Capacity)];return true;
        }
    }
}
