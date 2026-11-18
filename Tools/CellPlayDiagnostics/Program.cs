using System;
using System.Globalization;
using Evolution.Core;
using Evolution.Volume;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
Console.WriteLine("seed,seconds,event,generation,direction,development_paid,parent_reserve,child_reserve,changes");
bool success=true;
foreach(uint seed in new uint[]{42,7,123,2026}) {
    var ocean=new ChunkOcean(seed,new MemoryRegionArchive());var play=new CellLineagePlay();var w=ocean.World;
    // Select an existing viable resident, using the same food/physiology inspection available to the HUD.
    int choice=-1;float best=float.NegativeInfinity;
    for(int i=0;i<w.Count;i++) {
        var a=w.Get(i);var r=PlayerReadout.Read(w,i);if(r.Food<0)continue;
        float gain=r.FoodRate-r.Need;
        if(gain>best){best=gain;choice=i;}
    }
    if(choice<0)throw new Exception("No real resident");play.TakeControl(w,choice);
    int births=0;
    for(int tick=0;tick<6000&&births<3;tick++) {
        w=ocean.World;if(w.Controlled<0)break;var a=w.Get(w.Controlled);
        var direction=(DevelopmentDirection)(births+1);
        if(!w.PlanDevelopment(direction))throw new Exception("Direction unavailable");
        var r=PlayerReadout.Read(w,w.Controlled);
        if(r.Food>=0){var f=w.GetFood(r.Food);var delta=f.Position-a.Position;w.Input=delta.Length>f.Radius*.4f?delta.Unit:default;}
        else w.Input=default;
        bool requested=w.RequestDivision();string parent=a.Identity;w.Step();play.RecordStep(w);
        if(requested) {
            var children=CellEvolution.Offspring(w,parent);
            if(children.Count>0) {
                var child=w.Get(children[0]);births++;
                Console.WriteLine($"{seed},{w.Time:F2},birth,{child.Biology.Generation},{child.InheritedDevelopment},{child.DevelopmentPaid:F6},{w.Get(w.Controlled).Biology.Energy:F6},{child.Biology.Energy:F6},\"{CellEvolution.Changes(child.Biology)}\"");
                if(!play.FollowOffspring(w,children[0]))throw new Exception("Cannot follow real child");
                w.Input=default;
            }
        }
        // Follow this real player's position; other organisms remain autonomous.
        var focus=w.Controlled>=0?w.Get(w.Controlled).Position:w.ActiveCenter;ocean.Stream(focus);
    }
    if(births<3) {
        if(births<2)success=false;
        var a=play.Fallen??(w.Controlled>=0?w.Get(w.Controlled):default);
        string reason=play.Fallen.HasValue?CellEvolution.DeathReason(a):"300-second budget exhausted";
        Console.WriteLine($"{seed},{w.Time:F2},{(play.Fallen.HasValue?"death":"timeout")},{a.Biology.Generation},{a.PlannedDevelopment},0,0,0,\"{reason}\"");
    }
}
if(!success)Environment.ExitCode=1;
