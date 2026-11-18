using System;
using System.Collections.Generic;
namespace Evolution.Core
{
    public struct LineageSnapshot
    {
        public int Id, ParentId, LineageId, Generation, MutationMask;
        public long BornAtTick, DiedAtTick;
        public Genome Genes;
    }
    // A bounded archive of actual committed births; older entries are explicitly evicted.
    public sealed class LineageHistory
    {
        readonly LineageSnapshot[] records;
        readonly Dictionary<int,int> slots;
        int cursor;
        public int Count { get; private set; }
        public int Capacity => records.Length;
        public LineageHistory(int capacity = 512)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            records = new LineageSnapshot[capacity]; slots = new Dictionary<int,int>(capacity);
        }
        public bool TryFind(int id, out LineageSnapshot snapshot)
        {
            if (slots.TryGetValue(id,out int slot)) { snapshot = records[slot]; return true; }
            snapshot = default; return false;
        }
        public LineageSnapshot Recent(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return records[(cursor - index - 1 + records.Length) % records.Length];
        }
        internal void Birth(Cell c, long tick)
        {
            if (Count == records.Length) slots.Remove(records[cursor].Id);
            else Count++;
            records[cursor] = new LineageSnapshot { Id = c.Id, ParentId = c.ParentId, LineageId = c.LineageId,
                Generation = c.Generation, Genes = c.Genes, MutationMask = c.BirthMutationMask, BornAtTick = tick, DiedAtTick = -1 };
            slots[c.Id] = cursor; cursor = (cursor + 1) % records.Length;
        }
        internal void Death(int id, long tick)
        { if (slots.TryGetValue(id,out int slot)) records[slot].DiedAtTick = tick; }
    }
}
