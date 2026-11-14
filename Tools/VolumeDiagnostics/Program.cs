using System;
using System.Globalization;
using System.Linq;
using Evolution.Volume;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
uint[] seeds=args.FirstOrDefault(a=>a.StartsWith("--seeds="))?.Substring(8).Split(',').Select(uint.Parse).ToArray()??new uint[]{42,7,123,2026};
int seconds=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--seconds="))?.Substring(10)??"600");
// Fixed camera, no input, no new regions after initialization, no Player/Unity dependency.
Console.WriteLine("seed,seconds,alive,births,deaths,starved,predated,max_generation,substrate,organic,energy,intake,light,upkeep,movement,growth,attack,division,predation_loss,energy_blocked,growth_blocked,maturity_blocked,capacity_blocked,external_nutrients,solar,recycled,recycling_loss,decay_loss,assimilation_loss,stored,external_input,dissipated,balance_error,archived_alive,archived_substrate,archived_organic,historical_generation,birth_overhead,predation_dissipation,unpaid");
foreach(uint seed in seeds) {
    var ocean=new ChunkOcean(seed,new MemoryRegionArchive());
    ocean.World.NutrientSupply=!args.Contains("--no-supply");
    double initial=VolumeSnapshot.Read(ocean.World).Stored;
    for(int tick=0;tick<=seconds*20;tick++) {
        if(tick%200==0) {
            var w=ocean.World;var s=ocean.Snapshot(out var archived);var d=w.Diagnostics;
            Console.WriteLine($"{seed},{tick/20},{s.Alive},{d.Births},{d.Deaths},{d.Starved},{d.Predated},{s.MaxGeneration},{s.Substrate:F6},{s.Organic:F6},{s.Energy:F6},{d.Intake:F6},{d.Light:F6},{d.Upkeep:F6},{d.Movement:F6},{d.Growth:F6},{d.Attack:F6},{d.Division:F6},{d.PredationLoss:F6},{s.EnergyBlocked},{s.GrowthBlocked},{s.MaturityBlocked},{s.CapacityBlocked},{d.ExternalNutrients:F6},{d.Solar:F6},{d.Recycled:F6},{d.RecyclingLoss:F6},{d.DecayLoss:F6},{d.AssimilationLoss:F6},{s.Stored:F6},{d.ExternalNutrients+d.Solar:F6},{d.Dissipated:F6},{s.Stored-initial-d.ExternalNutrients-d.Solar+d.Dissipated:F6},{archived.Alive},{archived.Substrate:F6},{archived.Organic:F6},{d.MaxGeneration},{d.BirthOverhead:F6},{d.PredationDissipation:F6},{d.Unpaid:F6}");
        }
        if(tick==seconds*20)break;
        ocean.World.Step();ocean.Stream(default);
    }
}
