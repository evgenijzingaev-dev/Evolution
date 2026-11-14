using System;
using System.Collections.Generic;
using Evolution.Core;

namespace Evolution.Volume
{
    public struct FoodCloud { public long Order;public string Identity;public int Pattern;public V3 Position; public float Radius,Substrate,Organic; public float Concentration=>(Substrate*Biology.SubstrateEnergy+Organic)/(4.18879f*Radius*Radius*Radius); }
    public struct VolumeAgent
    {
        public long Order;
        public string BondIdentity;
        public BirthConnection PlannedConnection;
        public Cell Biology;
        public V3 Position,Previous,Direction,PreviousDirection,Velocity;
        public bool Alive;
        public double FedAt,BitAt,DiedAt;
        public int FoodCloud;
        public string Identity;
        public string FoodIdentity;
        public double RenderTime,RenderDuration;
        public double DivisionAt;
        public V3 DivisionOrigin,DivisionAxis;
        public bool DivisionChild;
        public DevelopmentDirection PlannedDevelopment,InheritedDevelopment;
        public float DevelopmentPaid;
        public int NaturalMutationMask;
    }
    // Spatial ecology shares Core physiology, inheritance and paid reproduction.
    public sealed partial class VolumeWorld
    {
        public const float StepSeconds=.05f;
        readonly List<VolumeAgent> agents;
        public const int PopulationLimit=2048;
        public const float DivisionSeconds=2;
        internal string DivisionRequest;
        public string LastOffspringIdentity {get;internal set;}
        public int Births {get;private set;}
        public VolumeDiagnostics Diagnostics {get;internal set;}=new VolumeDiagnostics();
        public bool NutrientSupply=true;
        readonly FoodCloud[] food;
        public readonly V3[] Obstacles;
        public V3 FlowOffset {get;private set;}
        public V3 ActiveCenter;
        public const float ObstacleRadius=1.3f;
        public int Count=>agents.Count;
        internal bool NeedsCompaction {
            get {if(Count<PopulationLimit)return false;foreach(var a in agents)if(!a.Alive&&Time-a.DiedAt>DivisionSeconds)return true;return false;}
        }
        internal void CompactArchived()
        {if(NeedsCompaction)agents.RemoveAll(a=>!a.Alive&&Time-a.DiedAt>DivisionSeconds);}
        public int FoodCount=>food.Length;
        public long Tick {get;private set;}
        public double Time=>Tick*(double)StepSeconds;
        public int Controlled=-1;
        public V3 Input;
        public bool Boost,Attack;
        public VolumeAgent Get(int i)=>agents[i];
        public FoodCloud GetFood(int i)=>food[i];
        public VolumeWorld(int count=64,uint seed=42,VolumeAgent[] founders=null)
        {
            if(count<1||count>96) throw new ArgumentOutOfRangeException(nameof(count));
            if(founders!=null) count=founders.Length;
            if(count<1||count>96) throw new ArgumentException("Invalid volume founders");
            Diagnostics.NextAgentOrder=count;Diagnostics.NextFoodOrder=12;
            agents=new List<VolumeAgent>(new VolumeAgent[count]);food=new FoodCloud[12];var rng=new RandomSource(seed);
            Obstacles=new[] {new V3(-4,0,2),new V3(5,-4,-3),new V3(1,7,4)};
            for(int i=0;i<food.Length;i++) {
                float angle=i*2.39996f;
                food[i]=new FoodCloud {Order=i+1,Identity="local-food-"+i,Pattern=i,Position=new V3((float)Math.Cos(angle)*8,(i%4-1.5f)*4,(float)Math.Sin(angle)*8),Radius=2.2f,
                    Substrate=i%3==0?0:35,Organic=i%3==0?55:0};
            }
            for(int i=0;i<count;i++) {
                var g=Genome.EcologicalFounder(Genome.Founder(ref rng),rng.State);
                var p=new V3(rng.Range(-11,11),rng.Range(-9,9),rng.Range(-11,11));
                var a=founders==null?new VolumeAgent {Position=p,Direction=new V3(rng.Range(-1,1),rng.Range(-1,1),rng.Range(-1,1)).Unit,
                    Biology=new Cell {Genes=g,Energy=Biology.Capacity(g)*.65f,Age=6,Growth=1,Integrity=1}}:founders[i];
                if(founders==null&&i==0) a.Position=food[1].Position+new V3(.9f,.6f,1.2f);
                if(a.Biology.Genes.Size<=0||float.IsNaN(a.Position.Sqr)||float.IsInfinity(a.Position.Sqr)) throw new ArgumentException("Invalid spatial founder");
                a.Biology.Id=a.Biology.LineageId=i+1;a.Alive=true;a.Previous=a.Position;a.Direction=a.Direction.Unit;a.PreviousDirection=a.Direction;
                a.Order=i+1;a.Identity="local-agent-"+i;a.RenderDuration=StepSeconds;
                a.FedAt=a.BitAt=a.DiedAt=double.NegativeInfinity;agents[i]=a;
            }
            RefreshColonies();
        }
        internal VolumeWorld(VolumeAgent[] agents,FoodCloud[] food,V3[] obstacles,long tick,V3 offset,VolumeDiagnostics diagnostics=null)
        {
            Diagnostics=diagnostics??new VolumeDiagnostics();
            this.agents=new List<VolumeAgent>(agents);this.food=food;Obstacles=obstacles;Tick=tick;FlowOffset=offset;
            for(int i=0;i<this.agents.Count;i++){var a=this.agents[i];if(a.Order==0)a.Order=++Diagnostics.NextAgentOrder;Diagnostics.NextAgentOrder=Math.Max(Diagnostics.NextAgentOrder,a.Order);this.agents[i]=a;}
            for(int k=0;k<food.Length;k++){if(food[k].Order==0)food[k].Order=++Diagnostics.NextFoodOrder;Diagnostics.NextFoodOrder=Math.Max(Diagnostics.NextFoodOrder,food[k].Order);}
            // Persist competition order: spatial rebucketing during Save must not change who feeds first.
            this.agents.Sort((a,b)=>a.Order.CompareTo(b.Order));Array.Sort(food,(a,b)=>a.Order.CompareTo(b.Order));
            for(int i=0;i<this.agents.Count;i++){var a=this.agents[i];a.FoodCloud=0;if(a.FoodIdentity!=null)for(int k=0;k<food.Length;k++)if(food[k].Identity==a.FoodIdentity){a.FoodCloud=k;break;}this.agents[i]=a;}
            RefreshColonies();
        }
        public V3 Current(V3 p)=>WaterField.Current(p+FlowOffset,Time);
        public float Temperature(V3 p)=>WaterField.Temperature(p+FlowOffset,Time);
        public float FoodPotential(int index,Cell organism)
        {
            var f=food[index];organism.Energy=0;organism.Budget=default;var sample=Sample(f.Position);
            Ecology.Photosynthesize(ref organism,ref f.Substrate,sample,1);Biology.Feed(ref organism,ref f.Substrate,ref f.Organic,sample.Temperature,1);
            return organism.Energy;
        }
        public void Step()
        {
            float dt=StepSeconds;string requested=DivisionRequest;DivisionRequest=null;
            int originalCount=Count;RefreshColonies();
            for(int k=0;k<food.Length;k++) {
                CloudEcology.Advance(ref food[k],dt,Diagnostics,NutrientSupply);
                food[k].Position+=Current(food[k].Position)*dt;
            }
            for(int i=0;i<Count;i++) {
                var a=agents[i];if(!a.Alive) continue;
                bool detailed=colonyNear[colonyRoot[i]];
                if(!detailed&&Tick%20!=0)continue;
                dt=detailed?StepSeconds:1;
                a.RenderTime=Time;a.RenderDuration=dt;
                a.Previous=a.Position;a.PreviousDirection=a.Direction;
                a.Biology.Budget=new EnergyBudget {Start=a.Biology.Energy};a.Biology.FoodLastStep=0;
                a.Biology.AttackLastStep=a.Biology.DamageLastStep=0;
                DevelopColony(ref a.Biology,dt);
                var target=Steer(i,out int cloud);a.FoodCloud=cloud;
                int root=colonyRoot[i],members=colonySize[root];
                bool driven=Controlled>=0&&colonyRoot[Controlled]==root;
                float throttle=i==Controlled?Math.Min(1,Input.Length):1;
                float speed;
                if(members>1) {
                    if(i==root) {
                        if(driven) {
                            float influence=ControlColony?1:agents[Controlled].Biology.Genes.Size/colonyMass[root];
                            if(Input.Sqr>.0001f)target=V3.Lerp(target.Unit,Input.Unit,influence).Unit;
                            else if(ControlColony)target=a.Direction;
                            throttle=1-influence+influence*Math.Min(1,Input.Length);
                        }
                        target=(target.Unit+Current(a.Position)*.15f).Unit;
                        a.Direction=V3.Turn(a.Direction,target,dt*1.6f/(float)Math.Sqrt(colonyMass[root])/(1+.1f*(members-1)));
                        speed=colonyMotor[root]/colonyMass[root]/(1+.12f*(members-1))*throttle*(driven&&Boost?1.65f:1);
                        a.Velocity=WaterField.Relax(a.Velocity,Current(a.Position)+a.Direction*speed,colonyMass[root],dt);
                        colonyVelocity[root]=a.Velocity;colonyDirection[root]=a.Direction;
                    } else {a.Velocity=colonyVelocity[root];a.Direction=colonyDirection[root];}
                    // Every cell pays its own propulsion effort; passive mass contributes drag.
                    speed=Ecology.Mobility(a.Biology.Genes)*Multicellularity.Motor(a.Biology);
                    speed*=driven?(ControlColony?Math.Min(1,Input.Length):1):1;
                    if(driven&&Boost)speed*=1.65f;
                } else {
                    if(i==Controlled && Input.Sqr>.0001f) target=Input.Unit;
                    if(i==Controlled && Input.Sqr<=.0001f) target=a.Direction;
                    target=(target.Unit+Current(a.Position)*.15f).Unit;
                    a.Direction=V3.Turn(a.Direction,target,dt*(1.6f/(float)Math.Sqrt(a.Biology.Genes.Size)));
                    speed=Ecology.Mobility(a.Biology.Genes)*Multicellularity.Motor(a.Biology)*throttle*(i==Controlled&&Boost?1.65f:1);
                    a.Velocity=WaterField.Relax(a.Velocity,Current(a.Position)+a.Direction*speed,a.Biology.Genes.Size,dt);
                }
                a.Position+=a.Velocity*dt;
                foreach(var obstacle in Obstacles) {
                    var delta=a.Position-obstacle;float radius=ObstacleRadius+.4f*a.Biology.Genes.Size;
                    if(delta.Sqr<radius*radius) { a.Position=obstacle+delta.Unit*radius;float inward=V3.Dot(a.Velocity,delta.Unit);if(inward<0) a.Velocity-=delta.Unit*inward; }
                }
                var c=a.Biology;var sample=Sample(a.Position);c.Age+=dt;c.SwimSpeed=speed;
                for(int k=0;k<food.Length;k++) if((food[k].Position-a.Position).Sqr<food[k].Radius*food[k].Radius) {
                    float before=c.Energy;
                    CloudEcology.Feed(ref c,ref food[k],sample,dt,Diagnostics);
                    if(c.Energy>before){a.FoodCloud=k;a.FoodIdentity=food[k].Identity;}
                }
                c.FoodLastStep=c.Budget.Intake;
                c.AssimilatedEnergy+=c.FoodLastStep;
                if(c.Budget.Substrate+c.Budget.Organic>0 && Time-a.FedAt>1.1) a.FedAt=Time;
                c.Budget.Upkeep=Multicellularity.Cost(c,sample.Temperature,Protection(i))*dt;
                c.AngularVelocity=(float)Math.Acos(Math.Max(-1,Math.Min(1,V3.Dot(a.PreviousDirection,a.Direction))))/dt;
                c.Boosting=driven&&Boost;
                c.Budget.Swimming=CellMotion.SwimmingCost(c)*dt;c.Budget.Turning=CellMotion.TurningCost(c)*dt;
                c.Budget.Movement=CellMotion.Cost(c)*dt;c.Budget.Boost=Math.Max(0,c.Budget.Movement-c.Budget.Swimming-c.Budget.Turning);
                c.Energy-=c.Budget.Upkeep+c.Budget.Movement;
                if(c.Growth<1&&c.Energy>Biology.Capacity(c.Genes)*.35f) {
                    float growth=Math.Min(1-c.Growth,.15f*dt);c.Growth+=growth;
                    c.Budget.Growth=growth*c.Genes.Size*.8f;c.Energy-=c.Budget.Growth;
                }
                Diagnostics.Metabolism(c);
                a.Biology=c;agents[i]=a;
            }
            for(int i=0;i<Count;i++) {
                if(!agents[i].Alive || (i==Controlled&&!Attack)) continue;
                if((agents[i].Position-ActiveCenter).Sqr>36*36)continue;
                dt=StepSeconds;
                for(int j=0;j<Count;j++) {
                    if(!agents[j].Alive||!Ecology.CanHunt(agents[i].Biology,agents[j].Biology)) continue;
                    float reach=.32f*(agents[i].Biology.Genes.Size+agents[j].Biology.Genes.Size);
                    if((agents[i].Position-agents[j].Position).Sqr>reach*reach || agents[i].Biology.Energy>=Biology.Capacity(agents[i].Biology.Genes)*.97f) continue;
                    var hunter=agents[i];var prey=agents[j];
                    float before=hunter.Biology.Energy,victim=prey.Biology.Energy;
                    float preyStructure=Biology.StructureEnergy(prey.Biology.Genes);
                    Ecology.Bite(ref hunter.Biology,ref prey.Biology,Protection(j),dt,out float remains);
                    float cost=(.18f+.3f*hunter.Biology.Genes.Predation)*dt;
                    Diagnostics.PredationDissipation+=Math.Max(0,before+victim+preyStructure-hunter.Biology.Energy-prey.Biology.Energy-(prey.Biology.Consumed?0:preyStructure)-remains-cost);
                    hunter.Biology.Budget.Attack+=cost;
                    hunter.Biology.Budget.Prey+=Math.Max(0,hunter.Biology.Energy-before+cost);
                    hunter.Biology.AssimilatedEnergy+=Math.Max(0,hunter.Biology.Energy-before+cost);
                    Diagnostics.Attack+=cost;Diagnostics.Intake+=Math.Max(0,hunter.Biology.Energy-before+cost);Diagnostics.PredationLoss+=Math.Max(0,victim-prey.Biology.Energy);
                    prey.Biology.Budget.PredationLoss+=Math.Max(0,victim-prey.Biology.Energy);
                    hunter.Biology.AttackLastStep=prey.Biology.DamageLastStep=1;
                    hunter.BitAt=Time;int remnant=NearestFood(prey.Position);if(remnant>=0)food[remnant].Organic+=remains;
                    agents[i]=hunter;agents[j]=prey;break;
                }
            }
            ResolveContacts();ExchangeColonies();
            for(int i=0;i<originalCount;i++) {
                var a=agents[i];if(!a.Alive)continue;
                if(a.Biology.Energy<=0) {
                    int remnant=NearestFood(a.Position);
                    if(!a.Biology.Consumed) {
                        float biomass=Math.Max(0,a.Biology.Energy)+Biology.StructureEnergy(a.Biology.Genes);
                        float recycled=remnant>=0?Biology.CarcassEnergy(a.Biology):0;
                        if(remnant>=0)food[remnant].Organic+=recycled;
                        Diagnostics.RecyclingLoss+=biomass-recycled;
                    }
                    Diagnostics.Unpaid+=Math.Max(0,-a.Biology.Energy);
                    Diagnostics.Death(a.Biology);a.Biology.Energy=0;a.Alive=false;a.DiedAt=Time;agents[i]=a;if(Controlled==i)Controlled=-1;continue;
                }
                if((i!=Controlled||requested==a.Identity)&&DivisionBlock(i)==null)Divide(i);
            }
            RefreshColonies();Tick++;
        }
        public float DivisionThreshold(int index)=>Biology.Capacity(agents[index].Biology.Genes)*Multicellularity.DivisionThreshold(agents[index].Biology)+CellEvolution.Cost(agents[index].Biology,agents[index].PlannedDevelopment)+(RetainBirth(index)?2*Multicellularity.BondCost:0);
        public bool PlanDevelopment(DevelopmentDirection direction)
        {
            if(Controlled<0||Controlled>=Count||!agents[Controlled].Alive||!CellEvolution.Available(agents[Controlled].Biology,direction))return false;
            var a=agents[Controlled];a.PlannedDevelopment=direction;agents[Controlled]=a;return true;
        }
        public string DivisionBlock(int index)
        {
            if(index<0||index>=Count||!agents[index].Alive)return "Выбери живую клетку";
            var a=agents[index];var c=a.Biology;
            if(a.PlannedConnection==BirthConnection.Retain) {
                string block=ConnectionBlock(index);if(block!=null)return block;
                var child=PreviewChild(index);
                if(!Multicellularity.Compatible(c.Genes,child.Genes)||OrganismSize(index)>=Multicellularity.SizeLimit(child.Genes,MaxColonyCells))return "Геном потомка не допускает эту связь";
                if(!BudFits(index,child,BudPosition(index,child)))return "Недостаточно пространства для связанного потомка";
            }
            if(Count>=PopulationLimit)return "Область заполнена организмами";
            if(a.DivisionAt>0&&Time-a.DivisionAt<DivisionSeconds)return "Деление ещё завершается";
            if(c.Generation>0&&c.Growth<1)return "Потомок ещё растёт: "+(c.Growth*100).ToString("F0")+"%. Нужен запас выше 35%";
            if(c.Age<Multicellularity.MaturityAge(c))return "До зрелости "+Math.Max(0,Multicellularity.MaturityAge(c)-c.Age).ToString("F1")+" с";
            if(!Biology.CanDivide(c)||c.Energy<DivisionThreshold(index))return "Накопи энергию: "+c.Energy.ToString("F1")+" / "+DivisionThreshold(index).ToString("F1");
            return null;
        }
        public bool RequestDivision()
        {
            if(DivisionBlock(Controlled)!=null)return false;
            DivisionRequest=agents[Controlled].Identity;return true;
        }
        void Divide(int index)
        {
            var a=agents[index];var c=a.Biology;
            // Derive a stable per-birth random stream; no unsaved global PRNG state.
            uint seed=2166136261;foreach(char ch in a.Identity)seed=unchecked((seed^ch)*16777619);
            var random=new RandomSource(seed^unchecked((uint)Tick)^0x6d2b79f5u);
            string identity=a.Identity+"/b"+(Tick+1);
            random.Next();
            if(Diagnostics.NextAgentOrder>=int.MaxValue-1)throw new InvalidOperationException("Cell identity space exhausted");
            int id=-(int)(Diagnostics.NextAgentOrder+1);
            bool collision;
            do {collision=false;for(int i=0;i<Count;i++)if(agents[i].Biology.Id==id){id--;collision=true;break;}}while(collision);
            var template=CellEvolution.Template(c.Genes,a.PlannedDevelopment);
            float developmentCost=CellEvolution.Cost(c,a.PlannedDevelopment);
            var child=new Cell {Id=id,ParentId=c.Id,ParentGenes=c.Genes,Generation=c.Generation+1,
                LineageId=c.LineageId,Integrity=1,Genes=template.Inherit(ref random,.15f,.06f,true)};
            bool retain=RetainBirth(index);
            V3 bud=BudPosition(index,child);
            if(retain&&(ConnectionBlock(index)!=null||!Multicellularity.Compatible(c.Genes,child.Genes)||OrganismSize(index)>=Multicellularity.SizeLimit(child.Genes,MaxColonyCells)||!BudFits(index,child,bud))) {if(a.PlannedConnection==BirthConnection.Retain)return;retain=false;}
            float attachment=retain?2*Multicellularity.BondCost:0;
            c.Energy-=developmentCost+attachment;
            if(!Biology.TryDivide(ref c,ref child))return;
            c.Budget.Contact+=attachment;Diagnostics.Contact+=attachment;
            if(retain){child.BondParentId=c.Id;child.TissueSlot=c.TissueSlot+1+sockets[index];}
            c.Budget.Division=Biology.StructureEnergy(child.Genes)+Biology.BirthOverhead(child.Genes)+child.Energy+developmentCost;
            child.Budget.Start=child.Energy;child.BirthMutationMask=Observation.MutationMask(c.Genes,child.Genes);
            // Division follows the existing body axis; independent steering starts next tick.
            V3 axis=a.Direction.Unit;
            V3 origin=a.Position;
            double at=(Tick+1)*(double)StepSeconds;
            var development=a.PlannedDevelopment;a.PlannedDevelopment=DevelopmentDirection.Natural;
            a.Biology=c;a.DivisionAt=at;a.DivisionOrigin=origin;a.DivisionAxis=axis;a.DivisionChild=false;
            a.Position=retain?origin:origin-axis*c.Genes.Size*.35f;agents[index]=a;
            var daughter=new VolumeAgent {Order=++Diagnostics.NextAgentOrder,Identity=identity,Biology=child,Alive=true,
                BondIdentity=retain?a.Identity:null,Position=retain?bud:origin+axis*c.Genes.Size*.35f,Previous=origin,Direction=axis,PreviousDirection=axis,
                Velocity=a.Velocity+axis*.15f,RenderTime=at,RenderDuration=StepSeconds,
                DivisionAt=at,DivisionOrigin=origin,DivisionAxis=axis,DivisionChild=true,
                InheritedDevelopment=development,DevelopmentPaid=developmentCost,NaturalMutationMask=Observation.MutationMask(template,child.Genes),
                FedAt=double.NegativeInfinity,BitAt=double.NegativeInfinity,DiedAt=double.NegativeInfinity};
            agents.Add(daughter);RefreshColonies();Births++;Diagnostics.Births++;Diagnostics.MaxGeneration=Math.Max(Diagnostics.MaxGeneration,child.Generation);Diagnostics.BirthOverhead+=Biology.BirthOverhead(child.Genes)+developmentCost;Diagnostics.Division+=Biology.StructureEnergy(child.Genes)+Biology.BirthOverhead(child.Genes)+developmentCost;if(index==Controlled)LastOffspringIdentity=identity;
        }
        V3 Steer(int i,out int cloud)
        {
            var a=agents[i];cloud=-1;float score=float.NegativeInfinity;var target=a.Direction;
            for(int k=0;k<food.Length;k++) {
                var copy=a.Biology;copy.Energy=0;copy.Budget=default;float substrate=food[k].Substrate,organic=food[k].Organic;
                var sample=Sample(food[k].Position);Ecology.Photosynthesize(ref copy,ref substrate,sample,1);
                Biology.Feed(ref copy,ref substrate,ref organic,sample.Temperature,1);
                float value=copy.Energy-Biology.Cost(copy.Genes,sample.Temperature)-.05f*(food[k].Position-a.Position).Length;
                if(value>score) {score=value;cloud=k;target=food[k].Position-a.Position;}
            }
            float sense=Ecology.SenseRadius(a.Biology.Genes);
            for(int j=0;j<Count;j++) if(j!=i&&agents[j].Alive) {
                var delta=agents[j].Position-a.Position;float d=delta.Length;
                if(d<sense&&Ecology.CanHunt(agents[j].Biology,a.Biology)) target=-delta;
                else if(d<sense&&Ecology.CanHunt(a.Biology,agents[j].Biology)&&a.Biology.Energy<Biology.Capacity(a.Biology.Genes)*.8f) target=delta;
                if(colonyRoot[i]!=colonyRoot[j]&&d<.8f) target-=delta.Unit*(.8f-d)*3;
            }
            foreach(var obstacle in Obstacles) {var delta=a.Position-obstacle;if(delta.Length<3.5f) target+=delta.Unit*(3.5f-delta.Length)*4;}
            return target.Unit;
        }
        int NearestFood(V3 p) {int best=-1;float d=float.MaxValue;for(int k=0;k<food.Length;k++) {float r=(p-food[k].Position).Sqr;if(r<d) {d=r;best=k;}}return best;}
        public OceanSample Sample(V3 p)=>new OceanSample {Temperature=Temperature(p),Light=WaterField.Light(p),Fertility=1};
    }
}
