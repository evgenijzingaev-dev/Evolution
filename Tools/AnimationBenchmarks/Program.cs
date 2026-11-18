using System;
using System.Diagnostics;
using Evolution.Core;
using Evolution.Presentation;

// CPU-only parameter/curve workload. Excludes Unity draw submission, GPU, shader and scene costs.
foreach (int count in new[] { 512,1440,4096 }) {
    var simulation = new Simulation(new SimulationConfig { InitialPopulation=count,Capacity=count,Ecosystem=true,OceanZones=true },42);
    var states = new CellAnimation[count];
    for(int i=0;i<count;i++) {
        var c=simulation.GetCell(i);
        states[i]=new CellAnimation(c.Id,AnimationTargets.From(c,simulation.EnvironmentAt(c.X,c.Z),0,.05f));
    }
    double checksum=0;
    void Frame(int frame) {
        for(int i=0;i<count;i++) {
            var a=states[i]; a.Advance(1.0/60);
            var pose=MembranePose.From(a,frame/60.0,(float)a.Phase);
            checksum+=pose.Amplitude;
            if(i>=64) continue;
            for(int j=0;j<7;j++) {
                pose.Interior(.2f*(float)Math.Cos(j),.02f,.2f*(float)Math.Sin(j),out float x,out _,out float z); checksum+=x+z;
            }
            if(a.Target.Flagellum) for(int j=1;j<=12;j++) {
                MotorCurves.Flagellum(j/12f,(float)a.Phase,a.Drive,a.Activity,a.Turn,out float x,out float z); checksum+=x+z;
            }
            if(a.Target.Cilia) for(int j=0;j<12;j++) for(int k=1;k<=3;k++) {
                MotorCurves.Cilium(k/3f,j*(float)Math.PI/6,(float)a.Phase,a.Drive,a.Activity,a.Turn,out float x,out float z); checksum+=x+z;
            }
        }
    }
    for(int f=0;f<60;f++) Frame(f);
    var timer=new Stopwatch(); GC.Collect(); long allocated=GC.GetAllocatedBytesForCurrentThread(); timer.Start();
    for(int f=0;f<300;f++) Frame(f);
    timer.Stop(); allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
    Console.WriteLine($"animation_parameters cells={count} detailed=64 iterations=300 mean_ms={timer.Elapsed.TotalMilliseconds/300:F4} allocated_bytes={allocated} checksum={checksum:F3} runtime=net8 no_unity_no_gpu=true");
}
