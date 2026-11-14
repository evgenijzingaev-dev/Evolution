using System;
using System.Globalization;
using Evolution.Volume;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
Console.WriteLine("scenario,seed,seconds,alive,births,deaths,generation,photo_share,organic_share,hot_share,frugal_share,colonies,stored_energy,matter,energy_residual,matter_residual,oxygen_residual,solar,external_chemical,external_organic");
foreach(var scenario in new[]{EcologicalScenario.Sunlit,EcologicalScenario.Dark,EcologicalScenario.Hydrothermal,EcologicalScenario.Natural})foreach(uint seed in new uint[]{42,7}) {
    var w=NicheExperiment.Create(scenario,seed);bool closed=Array.IndexOf(args,"--closed")>=0;if(closed)w.NutrientSupply=false;
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
