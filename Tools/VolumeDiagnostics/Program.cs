using System;
using System.Globalization;
using Evolution.Volume;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
// Fixed camera, no input, no new regions after initialization, no Player/Unity dependency.
Console.WriteLine("seed,seconds,alive,births,deaths,starved,predated,max_generation,substrate,organic,energy,intake,light,upkeep,movement,growth,attack,division,predation_loss,energy_blocked,growth_blocked,maturity_blocked,capacity_blocked,external_nutrients,solar,recycled,recycling_loss,decay_loss,assimilation_loss");
foreach(uint seed in new uint[]{42,7,123,2026}) {
    var ocean=new ChunkOcean(seed,new MemoryRegionArchive());
    for(int tick=0;tick<=12000;tick++) {
        if(tick%200==0) {
            var w=ocean.World;var s=VolumeSnapshot.Read(w);var d=w.Diagnostics;
            Console.WriteLine($"{seed},{tick/20},{s.Alive},{d.Births},{d.Deaths},{d.Starved},{d.Predated},{s.MaxGeneration},{s.Substrate:F6},{s.Organic:F6},{s.Energy:F6},{d.Intake:F6},{d.Light:F6},{d.Upkeep:F6},{d.Movement:F6},{d.Growth:F6},{d.Attack:F6},{d.Division:F6},{d.PredationLoss:F6},{s.EnergyBlocked},{s.GrowthBlocked},{s.MaturityBlocked},{s.CapacityBlocked},{d.ExternalNutrients:F6},{d.Solar:F6},{d.Recycled:F6},{d.RecyclingLoss:F6},{d.DecayLoss:F6},{d.AssimilationLoss:F6}");
        }
        if(tick==12000)break;
        ocean.World.Step();ocean.Stream(default);
    }
}
