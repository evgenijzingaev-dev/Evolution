using System;
using System.Diagnostics;
using System.Globalization;
using Evolution.Core;
using Evolution.Presentation;

CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
Console.WriteLine("cells,detailed,iterations,mean_cpu_ms,allocated_bytes_per_frame,checksum");
foreach(int count in new[] {256,512,2048}) {
    var states=new CellAnimation[count];var cells=new Cell[count];var poses=new WaterCellPose[count];
    var rng=new RandomSource(42);
    for(int i=0;i<count;i++) {
        var g=Genome.EcologicalFounder(Genome.Founder(ref rng),rng.State);
        cells[i]=new Cell {Genes=g,Id=i+1,Energy=Biology.Capacity(g)*.6f,Integrity=1,Growth=1,SwimSpeed=.8f};
        states[i]=new CellAnimation(i+1,AnimationTargets.From(cells[i],new OceanSample {Temperature=22},.8f,.05f));
    }
    double checksum=0;
    void Frame() {
        for(int i=0;i<count;i++) {
            states[i].Advance(1.0/60);var pose=WaterCellPose.From(states[i],cells[i],states[i].Phase);poses[i]=pose;
            checksum+=pose.Phase;
            if(i>=13)continue;
            for(int organelle=0;organelle<64;organelle++) {
                float x=.3f*(float)Math.Cos(organelle),y=.1f,z=.3f*(float)Math.Sin(organelle);
                pose.Deform(ref x,ref y,ref z);checksum+=x+y+z;
            }
            for(int segment=1;segment<=18;segment++) {
                MotorCurves.Flagellum(segment/18f,pose.Phase,pose.Drive,pose.Activity,pose.Turn,out float x,out float z);checksum+=x+z;
            }
        }
    }
    for(int f=0;f<100;f++)Frame();GC.Collect();
    var timer=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();timer.Start();
    for(int frame=0;frame<1000;frame++)Frame();timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
    Console.WriteLine($"{count},13,1000,{timer.Elapsed.TotalMilliseconds/1000:F4},{bytes/1000.0:F1},{checksum:F3}");
}
Console.Error.WriteLine(".NET CPU parameter projection only; excludes Unity, GPU, draw submission, cilia geometry, streaming and initialization. Not FPS.");
