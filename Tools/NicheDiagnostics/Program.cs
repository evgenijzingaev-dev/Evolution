using System;
using System.Globalization;
using Evolution.Volume;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
if(Array.IndexOf(args,"--profile")>=0) {
    var w=new ChunkOcean(42,new MemoryRegionArchive(),EcologicalScenario.Natural).World;
    for(int i=0;i<100;i++)w.Step();int start=VolumeSnapshot.Read(w).Alive;
    var times=new double[300];var watch=new System.Diagnostics.Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<times.Length;i++){watch.Restart();w.Step();watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
    long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(times);
    Console.WriteLine("resident_slots,alive_start,alive_end,median_step_ms,p95_step_ms,allocated_bytes_per_step");
    Console.WriteLine($"{w.Count},{start},{VolumeSnapshot.Read(w).Alive},{times[150]:F5},{times[285]:F5},{allocated/300.0:F2}");
    Console.Error.WriteLine(".NET biological step only; excludes Unity, render, streaming and observation; not FPS.");return;
}
if(Array.IndexOf(args,"--world")>=0) {
    Console.WriteLine("seed,seconds,active_alive,total_alive,births,deaths,generation,energy_residual,matter_residual,oxygen_residual,initial_energy,throughput");
    foreach(uint seed in new uint[]{42,7,123,2026}) {
        var ocean=new ChunkOcean(seed,new MemoryRegionArchive(),EcologicalScenario.Natural);
        for(int tick=0;tick<=12000;tick++) {
            if(tick%200==0) {
                var s=ocean.Snapshot(out _);var d=ocean.World.Diagnostics;double energy=d.InitialEnergy+d.ExternalNutrients+d.ExternalOrganic+d.Solar;
                Console.WriteLine($"{seed},{ocean.World.Time:F2},{VolumeSnapshot.Read(ocean.World).Alive},{s.Alive},{d.Births},{d.Deaths},{s.MaxGeneration},{energy-d.Dissipated-s.Stored:F6},{d.InitialMatter+d.ExternalMatter-d.MatterLost-s.Matter:F6},{d.InitialOxygen+d.OxygenIn+d.OxygenProduced-d.OxygenOut-d.OxygenUsed-s.Oxygen:F6},{d.InitialEnergy:F6},{energy:F6}");
            }
            if(tick<12000){ocean.World.Step();if(tick%20==0)ocean.Stream(default);}
        }
    }
    return;
}
Console.WriteLine("scenario,seed,seconds,alive,births,deaths,generation,photo_share,organic_share,hot_share,frugal_share,colonies,stored_energy,matter,energy_residual,matter_residual,oxygen_residual,solar,external_chemical,external_organic");
foreach(var scenario in new[]{EcologicalScenario.Sunlit,EcologicalScenario.Dark,EcologicalScenario.Hydrothermal,EcologicalScenario.Natural})foreach(uint seed in new uint[]{42,7}) {
    var w=NicheExperiment.Create(scenario,seed);bool closed=Array.IndexOf(args,"--no-resource-input")>=0;if(closed)w.NutrientSupply=false;
    var initial=VolumeSnapshot.Read(w);
    for(int tick=0;tick<=12000;tick++) {
        if(tick%200==0) {
            var s=VolumeSnapshot.Read(w);var d=w.Diagnostics;int photo=0,organic=0,hot=0,frugal=0,colonies=0;
            for(int i=0;i<w.Count;i++){var a=w.Get(i);if(!a.Alive)continue;var g=a.Biology.Genes;if(g.Photosynthesis>.6f)photo++;if(g.OrganicSpecialization>.6f)organic++;if(g.PreferredTemperature>32)hot++;if(g.EnergyEfficiency>.7f)frugal++;if(w.OrganismRoot(i)==i&&w.OrganismSize(i)>1)colonies++;}
            double energy=initial.Stored+d.ExternalNutrients+d.ExternalOrganic+d.Solar-d.Dissipated-s.Stored;
            double matter=d.InitialMatter+d.ExternalMatter-d.MatterLost-s.Matter;
            double oxygen=d.InitialOxygen+d.OxygenIn+d.OxygenProduced-d.OxygenOut-d.OxygenUsed-s.Oxygen;
            double n=Math.Max(1,s.Alive);
            Console.WriteLine($"{scenario},{seed},{w.Time:F2},{s.Alive},{d.Births},{d.Deaths},{s.MaxGeneration},{photo/n:F5},{organic/n:F5},{hot/n:F5},{frugal/n:F5},{colonies},{s.Stored:F6},{s.Matter:F6},{energy:F6},{matter:F6},{oxygen:F6},{d.Solar:F6},{d.ExternalNutrients:F6},{d.ExternalOrganic:F6}");
        }
        if(tick<12000)NicheExperiment.Step(w);
    }
}
