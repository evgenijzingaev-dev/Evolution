using System;
using Evolution.Core;
namespace Evolution.Volume
{
    // Explicit diagnostic fixture: identical initial genetic mixture for every environment.
    // Not used to spawn game residents. No resources, genes or positions are edited after construction.
    public static class NicheExperiment
    {
        public static VolumeWorld Create(EcologicalScenario scenario,uint seed=42)
        {
            var reference=new VolumeWorld(1,seed);var founders=new VolumeAgent[48];var rng=new RandomSource(seed);
            for(int i=0;i<founders.Length;i++) {
                int type=i%4;
                var g=new Genome {Size=.8f,Speed=type==3?.35f:.65f,Uptake=type==3?.85f:1.2f,PreferredTemperature=type==2?43:type==1?16:23,
                    ThermalTolerance=type==2?.8f:.25f,EnergyEfficiency=type==3?.9f:.35f,Membrane=.15f,Sensitivity=.65f,
                    Photosynthesis=type==0?.9f:type==3?.3f:.03f,OrganicSpecialization=type==1?.9f:type==3?.4f:.08f,
                    Adhesion=.5f+.45f*rng.Next(),ColonySize=.8f,Branching=.8f,Differentiation=.5f,Transport=.65f,RolePattern=rng.Next()};
                var f=reference.GetFood((i/4)%reference.FoodCount);
                float angle=type*(float)Math.PI/2;
                founders[i]=new VolumeAgent {Position=f.Position+new V3((float)Math.Cos(angle)*.65f,.2f,(float)Math.Sin(angle)*.65f),Direction=new V3(1,0,0),
                    Biology=new Cell {Genes=g,Energy=Biology.Capacity(g)*.65f,Age=8,Growth=1,Integrity=1}};
            }
            return new VolumeWorld(48,seed,founders,scenario);
        }
        public static void Step(VolumeWorld world){world.Step();world.CompactArchived();}
    }
}
