using System;
using System.Collections.Generic;
using Evolution.Core;

namespace Evolution.Volume
{
    public enum BirthConnection { Natural, Separate, Retain }

    // Stable identity edges, never list indices. A birth can only attach to its own older parent.
    public sealed partial class VolumeWorld
    {
        public const int MaxColonyCells=12;
        public bool ControlColony;
        readonly Dictionary<string,int> colonyIndices=new Dictionary<string,int>();
        int[] bondParent=Array.Empty<int>(),colonyRoot=Array.Empty<int>(),colonySize=Array.Empty<int>(),sockets=Array.Empty<int>();
        float[] colonyMass=Array.Empty<float>(),colonyMotor=Array.Empty<float>(),colonyDefense=Array.Empty<float>();
        bool[] colonyNear=Array.Empty<bool>();
        V3[] colonyVelocity=Array.Empty<V3>(),colonyDirection=Array.Empty<V3>();
        public int OrganismRoot(int index)=>index>=0&&index<Count?colonyRoot[index]:-1;
        public int OrganismSize(int index)=>index>=0&&index<Count?colonySize[colonyRoot[index]]:0;
        public int BondParent(int index)=>index>=0&&index<Count?bondParent[index]:-1;
        public float Protection(int index)=>colonyDefense[colonyRoot[index]]/Math.Max(1,OrganismSize(index))*.8f;
        public ChunkKey OwnerChunk(int index)=>ChunkKey.At(agents[OrganismRoot(index)].Position);
        public void RefreshColonies()
        {
            if(colonyRoot.Length<Count) {
                int n=Math.Max(Count,PopulationLimit);bondParent=new int[n];colonyRoot=new int[n];colonySize=new int[n];sockets=new int[n];
                colonyMass=new float[n];colonyMotor=new float[n];colonyDefense=new float[n];colonyNear=new bool[n];colonyVelocity=new V3[n];colonyDirection=new V3[n];
            }
            colonyIndices.Clear();Array.Clear(colonySize,0,Count);Array.Clear(sockets,0,Count);
            Array.Clear(colonyMass,0,Count);Array.Clear(colonyMotor,0,Count);Array.Clear(colonyDefense,0,Count);Array.Clear(colonyNear,0,Count);
            for(int i=0;i<Count;i++) {
                var a=agents[i];int p=-1;
                if(a.Identity==null||colonyIndices.ContainsKey(a.Identity))throw new InvalidOperationException("Duplicate cell identity");
                if(a.Alive&&a.BondIdentity!=null&&colonyIndices.TryGetValue(a.BondIdentity,out int found)&&agents[found].Alive
                    &&CellEvolution.ParentIdentity(a)==a.BondIdentity&&a.Biology.ParentId==agents[found].Biology.Id
                    &&a.Biology.LineageId==agents[found].Biology.LineageId&&Multicellularity.Compatible(a.Biology.Genes,agents[found].Biology.Genes))p=found;
                if(p>=0&&(a.Position-agents[p].Position).Length>BondLength(a,agents[p])*2)p=-1;
                bondParent[i]=p;colonyRoot[i]=p<0?i:colonyRoot[p];
                if(p<0){a.BondIdentity=null;a.Biology.BondParentId=0;}else {sockets[p]++;a.Biology.BondParentId=agents[p].Biology.Id;}
                if(a.Alive)colonySize[colonyRoot[i]]++;
                agents[i]=a;colonyIndices.Add(a.Identity,i);
            }
            for(int i=0;i<Count;i++) {
                var a=agents[i];int r=colonyRoot[i];a.Biology.ColonyId=colonySize[r]>1?agents[r].Biology.Id:0;agents[i]=a;
                if(!a.Alive)continue;
                colonyMass[r]+=a.Biology.Genes.Size;
                colonyMotor[r]+=Ecology.Mobility(a.Biology.Genes)*Multicellularity.Motor(a.Biology)*a.Biology.Genes.Size;
                if(a.Biology.Role==CellRole.Defense)colonyDefense[r]+=a.Biology.Specialization;
                colonyNear[r]|=(a.Position-ActiveCenter).Sqr<36*36||i==Controlled;
            }
        }
        static float BondLength(VolumeAgent a,VolumeAgent b)=>.38f*(a.Biology.Genes.Size+b.Biology.Genes.Size);
        public string ConnectionBlock(int index)
        {
            if(index<0||index>=Count||!agents[index].Alive)return "Выбери живую клетку";
            var a=agents[index];var g=a.Biology.Genes;
            if(g.Adhesion<.35f)return "Сцепление ниже 0.35: потомок не удержится";
            if(sockets[index]>=(g.Branching>.45f?2:1))return "Нет свободного места ветвления: выбери другую клетку";
            int size=OrganismSize(index);
            for(int i=0;i<Count;i++)if(agents[i].Alive&&colonyRoot[i]==colonyRoot[index]&&size>=Multicellularity.SizeLimit(agents[i].Biology.Genes,MaxColonyCells))return "Достигнут наследуемый предел размера организма";
            return null;
        }
        public bool PlanConnection(BirthConnection choice)
        {
            if(Controlled<0||Controlled>=Count||!agents[Controlled].Alive||choice<BirthConnection.Natural||choice>BirthConnection.Retain)return false;
            if(choice==BirthConnection.Retain&&ConnectionBlock(Controlled)!=null)return false;
            var a=agents[Controlled];a.PlannedConnection=choice;agents[Controlled]=a;return true;
        }
        public bool Detach(int index)
        {
            if(index<0||index>=Count||bondParent[index]<0)return false;
            var a=agents[index];a.BondIdentity=null;a.Biology.BondParentId=0;agents[index]=a;RefreshColonies();return true;
        }
        bool RetainBirth(int index)=>agents[index].PlannedConnection==BirthConnection.Retain||
            (agents[index].PlannedConnection==BirthConnection.Natural&&index!=Controlled&&agents[index].Biology.Genes.Adhesion>.72f&&ConnectionBlock(index)==null);
        V3 BudPosition(int index,Cell child)
        {
            var a=agents[index];var axis=-a.Direction.Unit;
            if(sockets[index]>0)axis=new V3(axis.Z,.6f,-axis.X).Unit;
            return a.Position+axis*(.38f*(a.Biology.Genes.Size+child.Genes.Size));
        }
        bool BudFits(int index,Cell child,V3 position)
        {
            foreach(var o in Obstacles)if((position-o).Length<ObstacleRadius+.4f*child.Genes.Size)return false;
            for(int i=0;i<Count;i++)if(i!=index&&agents[i].Alive&&(agents[i].Position-position).Length<.3f*(child.Genes.Size+agents[i].Biology.Genes.Size))return false;
            return true;
        }
        void DevelopColony(ref Cell c,float dt)
        {
            if(c.ColonyId!=0) {
                c.Role=c.Genes.Differentiation>0?Multicellularity.Role(c.Genes,c.TissueSlot):CellRole.Generalist;
                c.Specialization=Math.Min(c.Genes.Differentiation,c.Specialization+dt*.08f*c.Genes.Differentiation);
            }else {c.Specialization=Math.Max(0,c.Specialization-dt*.2f);if(c.Specialization==0)c.Role=CellRole.Generalist;}
        }
        void ExchangeColonies()
        {
            for(int i=0;i<Count;i++) {
                int p=bondParent[i];if(p<0||!agents[i].Alive||!agents[p].Alive||!colonyNear[colonyRoot[i]]&&Tick%20!=0)continue;
                var a=agents[p];var b=agents[i];if(a.Biology.Energy<=0||b.Biology.Energy<=0)continue;
                float ea=a.Biology.Energy,eb=b.Biology.Energy;
                float received=Multicellularity.Transfer(ref a.Biology,ref b.Biology,colonyNear[colonyRoot[i]]?StepSeconds:1);
                a.Biology.Budget.Transport+=a.Biology.Energy-ea;b.Biology.Budget.Transport+=b.Biology.Energy-eb;
                b.Biology.TransportLastStep=b.Biology.Energy-eb;
                Diagnostics.TransportReceived+=received;Diagnostics.TransportLoss+=Math.Max(0,(double)ea+eb-a.Biology.Energy-b.Biology.Energy);
                agents[p]=a;agents[i]=b;
            }
        }
    }
}
