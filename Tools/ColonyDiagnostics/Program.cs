using System;
using System.Globalization;
using Evolution.Core;
using Evolution.Volume;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
if(Array.IndexOf(args,"--profile")>=0) {
    var sample=new ChunkOcean(42,new MemoryRegionArchive()).World;
    for(int i=0;i<100;i++)sample.Step();
    var watch=new System.Diagnostics.Stopwatch();double[] times=new double[200];long bytes=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<times.Length;i++){watch.Restart();sample.Step();watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
    bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;Array.Sort(times);
    Console.WriteLine("agents,bytes_per_step,median_ms,p95_ms,max_ms");Console.WriteLine($"{sample.Count},{bytes/200.0:F1},{times[100]:F4},{times[190]:F4},{times[199]:F4}");return;
}
Console.WriteLine("seed,seconds,event,identity,cells,roles,energy,contact,transport_loss");
foreach(uint seed in new uint[]{42,7,123,2026}) {
    var ocean=new ChunkOcean(seed,new MemoryRegionArchive());var w=ocean.World;
    int chosen=-1;float best=-1;
    for(int i=0;i<w.Count;i++){var a=w.Get(i);var g=a.Biology.Genes;if(g.Adhesion<.8f||g.ColonySize<.3f||g.Branching<.5f||g.Differentiation<.2f)continue;
        float value=0;for(int f=0;f<w.FoodCount;f++)if((w.GetFood(f).Position-a.Position).Length<w.GetFood(f).Radius)value+=w.FoodPotential(f,a.Biology);
        value-=Biology.Cost(g,w.Temperature(a.Position));if(value>best){best=value;chosen=i;}}
    if(chosen<0)continue;
    string identity=w.Get(chosen).Identity;w.Controlled=chosen;w.ControlColony=true;w.ActiveCenter=w.Get(chosen).Position;
    int previous=1;bool lost=false;double lostAt=0;int initialRoot=chosen;
    void Log(string what,int index) {
        int size=w.OrganismSize(index),root=w.OrganismRoot(index);int mask=0;float energy=0;
        for(int i=0;i<w.Count;i++)if(w.Get(i).Alive&&w.OrganismRoot(i)==root){var c=w.Get(i).Biology;energy+=c.Energy;if(c.Specialization>.1f)mask|=1<<(int)c.Role;}
        Console.WriteLine($"{seed},{w.Time:F2},{what},{w.Get(index).Identity},{size},{mask},{energy:F4},{w.Diagnostics.Contact:F6},{w.Diagnostics.TransportLoss:F6}");
    }
    Log("single",chosen);
    for(int tick=0;tick<6000;tick++) {
        if(!w.Get(initialRoot).Alive){Log("root-death",initialRoot);break;}
        int root=w.OrganismRoot(initialRoot);
        if(!lost)for(int i=0;i<w.Count;i++)if(w.Get(i).Alive&&w.OrganismRoot(i)==root){w.Controlled=i;w.PlanConnection(BirthConnection.Retain);if(w.Get(i).PlannedConnection==BirthConnection.Retain&&w.RequestDivision()){chosen=i;break;}}
        w.Controlled=chosen;w.ControlColony=true;w.Input=default;
        // Stay with the real nutrient cloud using ordinary paid movement, never a position/resource edit.
        float score=float.NegativeInfinity;V3 target=w.Get(chosen).Position;
        for(int f=0;f<w.FoodCount;f++){float distance=(w.GetFood(f).Position-w.Get(chosen).Position).Length;float value=w.FoodPotential(f,w.Get(chosen).Biology)-.15f*distance;if(value>score){score=value;target=w.GetFood(f).Position;}}
        var delta=target-w.Get(chosen).Position;if(delta.Length>1.1f)w.Input=delta.Unit*.35f;
        w.Step();int size=w.OrganismSize(initialRoot);
        if(size!=previous){Log(size>previous?"paid-growth":"part-lost",initialRoot);previous=size;}
        if(size>=4&&!lost&&w.Time>30) {
            int mask=0;for(int i=0;i<w.Count;i++)if(w.Get(i).Alive&&w.OrganismRoot(i)==w.OrganismRoot(initialRoot)&&w.Get(i).Biology.Specialization>.1f)mask|=1<<(int)w.Get(i).Biology.Role;
            if((mask&(mask-1))!=0){Log("four-specialized-whole-control",initialRoot);for(int i=w.Count-1;i>=0;i--)if(w.Get(i).Alive&&w.OrganismRoot(i)==w.OrganismRoot(initialRoot)&&w.BondParent(i)>=0){w.Detach(i);lost=true;lostAt=w.Time;chosen=initialRoot;Log("detach-live-part",initialRoot);break;}}
        }
        if(lost&&w.Time-lostAt>=5){Log("continued-life",initialRoot);break;}
    }
}
