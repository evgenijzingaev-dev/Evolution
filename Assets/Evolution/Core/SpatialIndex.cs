using System;
namespace Evolution.Core
{
    // Reused linked buckets on a torus. Queries bound both candidate work and duplicate buckets.
    internal sealed class SpatialIndex
    {
        readonly int[] heads, next;
        readonly int grid;
        readonly float width;
        public SpatialIndex(int capacity, float world)
        {
            grid = Math.Max(2, Math.Min(256, (int)(world / 3)));
            width = world / grid; heads = new int[grid * grid]; next = new int[capacity];
        }
        public void Build(Cell[] cells, int count, long tick)
        {
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            // Rotate priority so dense buckets do not permanently hide the youngest cells.
            for (int n = 0; n < count; n++) {
                int i = (int)((n + tick) % count);
                int tile = Bucket(cells[i].Z) * grid + Bucket(cells[i].X);
                next[i] = heads[tile]; heads[tile] = i;
            }
        }
        int Bucket(float p) => Math.Min(grid - 1, Math.Max(0, (int)(p / width)));
        public Query Nearby(Cell c, float radius) => new Query(this, c, radius);
        internal struct Query
        {
            readonly SpatialIndex index;
            readonly int x, z, radius, span;
            int bucket, candidate, examined;
            internal Query(SpatialIndex index, Cell cell, float distance)
            {
                this.index = index; x = index.Bucket(cell.X); z = index.Bucket(cell.Z);
                radius = (int)Math.Ceiling(distance / index.width); span = Math.Min(index.grid, radius * 2 + 1);
                bucket = 0; candidate = -1; examined = 0;
            }
            public bool Next(out int result)
            {
                while (candidate < 0 && bucket < span * span) {
                    int tx = ((x - radius + bucket % span) % index.grid + index.grid) % index.grid;
                    int tz = ((z - radius + bucket / span) % index.grid + index.grid) % index.grid;
                    candidate = index.heads[tz * index.grid + tx]; bucket++;
                }
                if (candidate < 0 || examined++ >= 96) { result = -1; return false; }
                result = candidate; candidate = index.next[candidate]; return true;
            }
        }
    }
}
