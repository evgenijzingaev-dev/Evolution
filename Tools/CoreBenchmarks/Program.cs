using System;
using System.Diagnostics;
using Evolution.Core;

// A short stage E workload, separate from the older throughput/stress scenarios below.
if (Array.IndexOf(args, "--stage-e") >= 0) {
    foreach (int count in new[] { 1440, 4096 }) {
        var cfg = new SimulationConfig { Ecosystem = true, OceanZones = true, NaturalEvents = true,
            WorldSize = 120, GridSize = 96, Capacity = 4096, InitialPopulation = count };
        var sim = new Simulation(cfg,42);
        for (int i = 0; i < 20; i++) sim.Step();
        GC.Collect(); long startBytes = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) sim.Step();
        timer.Stop();
        Console.WriteLine($"stage_e initial={count} final={sim.Count} mean_ms_per_step={timer.Elapsed.TotalMilliseconds/100:F4} allocated_bytes={GC.GetAllocatedBytesForCurrentThread()-startBytes} ticks=100 warmup=20 world=120 grid=96 seed=42");
    }
    return;
}

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
    Console.WriteLine($"initial={count} final={world.Count} mean_ms_per_step={watch.Elapsed.TotalMilliseconds / 500:F4} allocated_bytes={allocated} colonies={world.ColonyCount} colonial_cells={world.ColonialCells} largest={world.LargestColony} ticks=500 seed=42 grid=64 warmup=100");
}

// Qualitative default-world smoke run, separate from throughput timings above.
var defaultWorld = new Simulation(new SimulationConfig(), 42);
for (int tick = 0; tick < 2000; tick++) defaultWorld.Step();
Console.WriteLine($"default_seed=42 ticks=2000 population={defaultWorld.Count} births={defaultWorld.Births} deaths={defaultWorld.Deaths} colonies={defaultWorld.ColonyCount} colonial_cells={defaultWorld.ColonialCells} largest={defaultWorld.LargestColony}");
