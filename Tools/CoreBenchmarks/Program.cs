using System;
using System.Diagnostics;
using Evolution.Core;

// Core throughput only: this cannot measure Unity rendering, frame time or Mono/IL2CPP.
foreach (int count in new[] { 1000, 4096, 10000 }) {
    var config = new SimulationConfig { Capacity = count, InitialPopulation = count, GridSize = 64,
        InitialNutrients = 8, NutrientRegeneration = .5f, MaxAge = 10000 };
    var world = new Simulation(config, 42);
    for (int i = 0; i < 100; i++) world.Step();
    var watch = new Stopwatch(); GC.Collect();
    long before = GC.GetAllocatedBytesForCurrentThread();
    watch.Start();
    for (int i = 0; i < 500; i++) world.Step();
    watch.Stop();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"initial={count} final={world.Count} mean_ms_per_step={watch.Elapsed.TotalMilliseconds / 500:F4} allocated_bytes={allocated} ticks=500 seed=42 grid=64 warmup=100");
}
