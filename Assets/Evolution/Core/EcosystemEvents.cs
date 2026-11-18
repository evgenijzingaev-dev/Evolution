using System;
using System.Collections.Generic;
namespace Evolution.Core
{
    public enum NaturalEvent { None, Cooling, Warming, NutrientShortage, ToxicPulse, CurrentShift, ResourcePulse, PlanktonBloom }
    public enum EvolutionRecordKind { Climate, ClimateEnded, StrategyAppeared, Adaptation, LineageGrowth, LineageExtinct, PopulationBoom, PopulationDecline, Predation }
    public struct EvolutionRecord
    {
        public long Tick;
        public EvolutionRecordKind Kind;
        public int Subject, Value;
    }
    public sealed class EvolutionJournal
    {
        readonly EvolutionRecord[] entries = new EvolutionRecord[96];
        int cursor;
        public int Count { get; private set; }
        public EvolutionRecord GetRecent(int offset)
        {
            if (offset < 0 || offset >= Count) throw new ArgumentOutOfRangeException(nameof(offset));
            return entries[(cursor - 1 - offset + entries.Length) % entries.Length];
        }
        internal void Add(long tick, EvolutionRecordKind kind, int subject = 0, int value = 0)
        {
            entries[cursor] = new EvolutionRecord { Tick = tick, Kind = kind, Subject = subject, Value = value };
            cursor = (cursor + 1) % entries.Length; Count = Math.Min(Count + 1, entries.Length);
        }
    }
    public sealed partial class Simulation
    {
        public EvolutionJournal Journal { get; } = new EvolutionJournal();
        public NaturalEvent ActiveEvent { get; private set; }
        public float EventSecondsRemaining => Math.Max(0, (eventEndTick - Tick) * config.StepSeconds);
        public long PredationDeaths { get; private set; }
        public float SunlightEnergy { get; private set; }
        public int PredatorCount { get; private set; }
        public int LivingLineages => lineageCounts.Count;
        readonly Dictionary<int, int> lineageCounts = new Dictionary<int, int>();
        readonly Dictionary<int, int> censusCounts = new Dictionary<int, int>();
        readonly Dictionary<int, int> adaptationMasks = new Dictionary<int, int>();
        RandomSource climateRandom;
        long eventStartTick, eventEndTick, nextEventTick, nextCensusTick;
        int seenStrategies, lastPopulation;
        float EventEnvelope => ActiveEvent == NaturalEvent.None ? 0 : Math.Max(0, Math.Min(1,
            Math.Min((Tick - eventStartTick + 1) * config.StepSeconds / 4, (eventEndTick - Tick) * config.StepSeconds / 4)));
        internal float CurrentScale => ActiveEvent == NaturalEvent.CurrentShift ? 1 - 2.5f * EventEnvelope : 1;
        void InitializeEcology(uint seed)
        {
            climateRandom = new RandomSource(seed ^ 0xc2b2ae35u);
            nextEventTick = Math.Max(1, (long)(45 / config.StepSeconds));
            Census();
        }
        void UpdateNaturalEvents()
        {
            if (ActiveEvent != NaturalEvent.None && Tick >= eventEndTick) {
                Journal.Add(Tick, EvolutionRecordKind.ClimateEnded, (int)ActiveEvent); ActiveEvent = NaturalEvent.None;
            }
            if (!config.NaturalEvents || Tick < nextEventTick) return;
            ActiveEvent = (NaturalEvent)(1 + (int)(climateRandom.Next() * 7));
            eventStartTick = Tick;
            eventEndTick = Tick + Math.Max(1, (long)(climateRandom.Range(20, 32) / config.StepSeconds));
            nextEventTick = eventEndTick + Math.Max(1, (long)(climateRandom.Range(28, 50) / config.StepSeconds));
            Journal.Add(Tick, EvolutionRecordKind.Climate, (int)ActiveEvent);
        }
        OceanSample ApplyEcology(OceanSample sample, float x, float z)
        {
            if (!config.Ecosystem) return sample;
            double a = (x + z) / config.WorldSize * Math.PI * 2;
            float vent = config.OceanZones ? (float)Math.Pow(.5 + .5 * Math.Cos(a), 12) : 0;
            sample.Temperature += 7 * vent;
            sample.Fertility += .7f * vent;
            float strength = EventEnvelope;
            switch (ActiveEvent) {
                case NaturalEvent.Cooling: sample.Temperature -= 7 * strength; break;
                case NaturalEvent.Warming: sample.Temperature += 7 * strength; break;
                case NaturalEvent.NutrientShortage: sample.Fertility *= 1 - .85f * strength; break;
                case NaturalEvent.ToxicPulse: sample.Toxicity += .7f * strength; break;
                case NaturalEvent.ResourcePulse: sample.Fertility *= 1 + 2.5f * strength; break;
                case NaturalEvent.PlanktonBloom:
                    sample.Light *= 1 + .6f * strength; sample.Fertility *= 1 + 1.5f * strength; break;
            }
            sample.CurrentX *= CurrentScale; sample.CurrentZ *= CurrentScale;
            return sample;
        }
        void Census()
        {
            censusCounts.Clear(); PredatorCount = 0;
            for (int i = 0; i < Count; i++) {
                Cell c = cells[i]; int id = c.LineageId;
                censusCounts[id] = censusCounts.TryGetValue(id, out int n) ? n + 1 : 1;
                if (c.Genes.Predation > .55f) PredatorCount++;
                int strategy = (int)Ecology.Strategy(c);
                if ((seenStrategies & (1 << strategy)) == 0) {
                    seenStrategies |= 1 << strategy; Journal.Add(Tick, EvolutionRecordKind.StrategyAppeared, strategy);
                }
            }
            foreach (var previous in lineageCounts) {
                if (!censusCounts.TryGetValue(previous.Key, out int n)) {
                    Journal.Add(Tick, EvolutionRecordKind.LineageExtinct, previous.Key); adaptationMasks.Remove(previous.Key);
                } else if (n >= Math.Max(4, previous.Value * 2)) Journal.Add(Tick, EvolutionRecordKind.LineageGrowth, previous.Key, n);
            }
            if (lastPopulation > 0) {
                if (Count >= lastPopulation * 1.5f) Journal.Add(Tick, EvolutionRecordKind.PopulationBoom, 0, Count);
                if (Count < lastPopulation * .65f) Journal.Add(Tick, EvolutionRecordKind.PopulationDecline, 0, Count);
            }
            lastPopulation = Count; lineageCounts.Clear();
            foreach (var item in censusCounts) lineageCounts.Add(item.Key, item.Value);
            nextCensusTick = Tick + Math.Max(1, (long)(5 / config.StepSeconds));
        }
        void RecordAdaptation(Cell parent, Cell child)
        {
            // Report first threshold crossings in each surviving lineage, not every tiny mutation.
            int mask = 0;
            if (parent.Genes.Photosynthesis < .55f && child.Genes.Photosynthesis >= .55f) mask |= 1 << 11;
            if (parent.Genes.Predation < .55f && child.Genes.Predation >= .55f) mask |= 1 << 12;
            if (parent.Genes.Membrane < .7f && child.Genes.Membrane >= .7f) mask |= 1 << 13;
            if (parent.Genes.ThermalTolerance < .6f && child.Genes.ThermalTolerance >= .6f) mask |= 1 << 15;
            if (parent.Genes.ToxinResistance < .6f && child.Genes.ToxinResistance >= .6f) mask |= 1 << 16;
            adaptationMasks.TryGetValue(child.LineageId, out int previous); mask &= ~previous;
            if (mask == 0) return;
            adaptationMasks[child.LineageId] = previous | mask;
            Journal.Add(Tick + 1, EvolutionRecordKind.Adaptation, child.LineageId, mask);
        }
    }
}
