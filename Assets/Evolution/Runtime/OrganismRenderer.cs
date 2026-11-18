using System.Collections.Generic;
using Evolution.Core;
using Evolution.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    // Persistent state per real ID, shared meshes/materials, fixed instancing buffers.
    internal sealed class OrganismRenderer
    {
        readonly Mesh mesh, membraneMesh;
        readonly Material body, membrane;
        Material current;
        readonly Matrix4x4[] matrices = new Matrix4x4[256];
        readonly Vector4[] colors = new Vector4[256], shapes = new Vector4[256], motors = new Vector4[256], responses = new Vector4[256], contacts = new Vector4[256], vitals = new Vector4[256];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly Dictionary<int, Cell> previousTick, byId;
        readonly Dictionary<int, CellAnimation> animations;
        readonly Dictionary<int, Vector3> positions;
        readonly Dictionary<int, float> headings;
        readonly List<int> expired;
        readonly HashSet<int> detailedIds;
        struct Remnant { public Vector3 Position; public Quaternion Rotation; public Color Color; public double Time; public float Size; public MembranePose Pose; }
        readonly Remnant[] remnants = new Remnant[128];
        int remnantCursor, used, detailedCount;
        float world, visualTime;
        double lastTime;
        bool closeView, highMembrane;
        Vector3 cameraFocus;
        float visibleRadius;
        MembranePose pose;
        public OrganismRenderer(Mesh mesh, Material body, Material membrane, int capacity, Mesh membraneMesh)
        {
            this.mesh = mesh; this.membraneMesh = membraneMesh; this.body = body; this.membrane = membrane;
            previousTick = new Dictionary<int, Cell>(capacity); byId = new Dictionary<int, Cell>(capacity);
            animations = new Dictionary<int, CellAnimation>(capacity); positions = new Dictionary<int, Vector3>(capacity);
            headings = new Dictionary<int, float>(capacity); expired = new List<int>(capacity); detailedIds = new HashSet<int>(128);
        }
        internal int AnimatedCount => animations.Count;
        internal bool TryAnimation(int id, out CellAnimation animation) => animations.TryGetValue(id,out animation);
        public static Vector3 Center(Cell c) => new Vector3(c.X, .54f, c.Z);
        public Vector3 Position(Cell c) => positions.TryGetValue(c.Id, out var p) ? p : Center(c);
        Quaternion Rotation(Cell c) => Quaternion.Euler(0, -(headings.TryGetValue(c.Id, out float h) ? h : c.Heading) * Mathf.Rad2Deg, 0);
        CellAnimation Ensure(Cell c, Simulation simulation, float previousSpeed)
        {
            var target = AnimationTargets.From(c, simulation.EnvironmentAt(c.X, c.Z), previousSpeed, simulation.StepSeconds);
            if (!animations.TryGetValue(c.Id, out var a)) { a = new CellAnimation(c.Id, target); animations.Add(c.Id, a); }
            a.Target = target; return a;
        }
        public void Capture(Simulation simulation)
        {
            previousTick.Clear();
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); previousTick.Add(c.Id, c);
                if (!animations.ContainsKey(c.Id)) Ensure(c, simulation, c.SwimSpeed);
            }
        }
        // Called after EVERY simulation tick, including ticks hidden by fast-forward or a slow frame.
        public void ObserveTick(Simulation simulation)
        {
            double time = simulation.Tick * (double)simulation.StepSeconds;
            byId.Clear(); for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); byId.Add(c.Id, c); }
            foreach (var pair in previousTick) if (!byId.ContainsKey(pair.Key)) {
                var c = pair.Value;
                remnants[remnantCursor++ % remnants.Length] = new Remnant { Position = Position(c), Rotation = Rotation(c),
                    Color = StrategyColor(c), Time = time, Size = c.Genes.Size * Mathf.Lerp(.55f, 1, c.Growth),
                    Pose = animations.TryGetValue(c.Id,out var dying) ? MembranePose.From(dying,time,(float)dying.Phase) : default };
            }
            foreach (var pair in byId) {
                var c = pair.Value; bool existed = previousTick.TryGetValue(c.Id, out var previous);
                var a = Ensure(c, simulation, existed ? previous.SwimSpeed : c.SwimSpeed);
                float angle = 0;
                int contact = c.AttackTargetId > 0 ? c.AttackTargetId : c.DamageSourceId > 0 ? c.DamageSourceId : c.BondParentId;
                if (contact > 0 && (byId.TryGetValue(contact, out var other) || previousTick.TryGetValue(contact, out other))) {
                    float dx = Observation.PeriodicDelta(other.X - c.X, simulation.WorldSize), dz = Observation.PeriodicDelta(other.Z - c.Z, simulation.WorldSize);
                    angle = CellMotion.AngleDelta(Mathf.Atan2(dz, dx) - c.Heading);
                }
                a.Observe(c, time, angle);
                if (!existed && c.ParentId > 0) {
                    a.Divide(time);
                    if (animations.TryGetValue(c.ParentId, out var parent)) parent.Divide(time);
                }
            }
            expired.Clear(); foreach (int id in animations.Keys) if (!byId.ContainsKey(id)) expired.Add(id);
            foreach (int id in expired) { animations.Remove(id); positions.Remove(id); headings.Remove(id); }
        }
        public static Color RoleColor(CellRole role)
        {
            switch (role) {
                case CellRole.Feeding: return new Color(.3f, .95f, .65f);
                case CellRole.Movement: return new Color(.22f, .75f, 1);
                case CellRole.Defense: return new Color(1, .72f, .3f);
                case CellRole.Reproduction: return new Color(.95f, .35f, .7f);
                default: return new Color(.4f, .85f, .85f);
            }
        }
        static Color StrategyColor(Cell c)
        {
            var g = c.Genes;
            Color color = Color.Lerp(new Color(.2f, .65f, .85f), new Color(.2f, .95f, .35f), g.Photosynthesis);
            color = Color.Lerp(color, new Color(.95f, .35f, .16f), g.Predation);
            return Color.Lerp(color, new Color(.9f, .72f, .4f), g.Membrane * .5f);
        }
        Color Tint(Cell c, int mode) => mode == 3 ? Color.Lerp(StrategyColor(c), RoleColor(c.Role), c.Specialization * .55f) : mode == 1 ? Color.Lerp(new Color(.18f, .9f, .55f), new Color(1, .52f, .24f), c.Genes.OrganicSpecialization)
            : mode == 2 ? Color.Lerp(RoleColor(CellRole.Generalist), RoleColor(c.Role), c.Specialization)
            : Color.Lerp(new Color(.2f, .5f, 1), new Color(1, .3f, .22f), c.Genes.PreferredTemperature / 50);
        bool Visible(Cell c) => new Vector2(Observation.PeriodicDelta(c.X-cameraFocus.x,world), Observation.PeriodicDelta(c.Z-cameraFocus.z,world)).sqrMagnitude < visibleRadius * visibleRadius;
        float Phase(Cell c, CellAnimation a) => (float)(a.Phase % (40 * System.Math.PI));
        Vector3 Interior(MembranePose p, Vector3 point, float size)
        {
            p.Interior(point.x, point.y, point.z, out float x, out float y, out float z);
            return new Vector3(x * .82f, y * .65f, z * .76f) * size;
        }
        Vector3 Surface(MembranePose p, float x, float y, float z, float size)
        {
            p.Deform(ref x, ref y, ref z); return new Vector3(x * .82f, y * .65f, z * .76f) * size;
        }
        public void Draw(Simulation simulation, int mode, float worldSize, int selectedId, double time, float interpolation, float cameraDistance, Vector3 focus, bool detailed)
        {
            cameraFocus = focus; visibleRadius = cameraDistance * 2.5f + 5; closeView = detailed && cameraDistance < 18;
            world = worldSize; visualTime = (float)time; used = 0;
            double dt = System.Math.Max(0, time - lastTime); lastTime = time;
            byId.Clear();
            for (int i = 0; i < simulation.Count; i++) { var c = simulation.GetCell(i); byId.Add(c.Id, c); }
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i);
                if (!animations.TryGetValue(c.Id, out var a)) a = Ensure(c, simulation, c.SwimSpeed);
                a.Advance(dt);
            }
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); var a = animations[c.Id];
                if (c.ColonyId > 0 && c.ColonyId != c.Id && animations.TryGetValue(c.ColonyId, out var root))
                    a.Synchronize(root.Phase,c.TissueSlot,dt);
                Vector3 point = Center(c); float heading = c.Heading;
                if (previousTick.TryGetValue(c.Id, out var previous)) {
                    Vector3 origin = Center(previous), delta = point - origin;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    point = origin + delta * interpolation;
                    heading = previous.Heading + CellMotion.AngleDelta(c.Heading - previous.Heading) * interpolation;
                }
                // Only the real newborn gets a bounded birth offset. No phantom daughters or forces.
                double birthAge = System.Math.Max(0,time-a.DivisionAt);
                if (c.ParentId > 0 && birthAge < .8 && byId.TryGetValue(c.ParentId, out var parent)) {
                    Vector3 delta = Center(parent) - point;
                    delta.x = Observation.PeriodicDelta(delta.x, world); delta.z = Observation.PeriodicDelta(delta.z, world);
                    point += Vector3.ClampMagnitude(delta, .3f * c.Genes.Size) * (1 - CellAnimation.SmoothBirth(birthAge));
                }
                if (c.BondParentId > 0) {
                    // Bounded tissue compliance (<= 1.5% of body size), entirely in presentation.
                    float sway = .015f * c.Genes.Size * a.Turn * Mathf.Sin((float)a.Phase - c.TissueSlot*.3f);
                    point += Quaternion.Euler(0,-heading*Mathf.Rad2Deg,0) * new Vector3(0,0,sway);
                }
                headings[c.Id] = heading; positions[c.Id] = point;
            }
            byId.TryGetValue(selectedId, out var selected);
            current = body; pose = default; detailedCount = 0; detailedIds.Clear();
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); if (!Visible(c)) continue;
                var a = animations[c.Id]; var p = Position(c); var r = Rotation(c);
                float phase = Phase(c, a), s = c.Genes.Size * Mathf.Lerp(.55f, 1, a.Growth);
                // Per-cell LOD plus a hard budget. Selection always receives full detail.
                Vector3 cameraDelta = p - cameraFocus; cameraDelta.x = Observation.PeriodicDelta(cameraDelta.x, world); cameraDelta.z = Observation.PeriodicDelta(cameraDelta.z, world);
                bool detail = closeView && (c.Id == selectedId || (cameraDelta.sqrMagnitude < 120 && detailedCount < 64));
                if (detail) { detailedCount++; detailedIds.Add(c.Id); }
                var cellPose = MembranePose.From(a, time, phase); Color color = Tint(c, mode);
                float streaming = .008f * a.Activity * (1 - a.Stress * .6f);
                var drift = new Vector3(Mathf.Sin(phase * .3f), .3f * Mathf.Sin(phase * .2f), Mathf.Cos(phase * .3f)) * streaming;
                Add(p + r * Interior(cellPose, drift, s), r, new Vector3(.24f, .25f, .24f) * s, Color.Lerp(color, new Color(.35f, .2f, .55f), .65f));
                if (!detail) continue;
                Add(p + r * Interior(cellPose, drift + new Vector3(.03f,.08f,.04f), s), r, Vector3.one * .07f * s, new Color(.8f,.8f,1));
                for (int j = 0; j < 3 + (int)(c.Genes.Uptake * 1.4f); j++) {
                    float angle = j * 2.39996f + c.Id;
                    Vector3 point = new Vector3(Mathf.Cos(angle)*.19f, .045f*Mathf.Sin(angle), Mathf.Sin(angle)*.18f) + drift * Mathf.Sin(j + phase * .15f);
                    Add(p + r * Interior(cellPose, point, s), r * Quaternion.Euler(0, j * 45, 20), new Vector3(.10f,.065f,.06f) * s, Color.Lerp(color,Color.white,.35f));
                }
                if (c.Genes.Photosynthesis > .25f) for (int j = 0; j < 4; j++) {
                    float angle = j * Mathf.PI * .5f + c.Id;
                    var point = new Vector3(Mathf.Cos(angle)*.16f,.11f,Mathf.Sin(angle)*.16f) + drift;
                    Add(p + r * Interior(cellPose,point,s),r * Quaternion.Euler(0,j*45,0),new Vector3(.13f,.035f,.08f)*s,
                        Color.Lerp(new Color(.08f,.3f,.16f),new Color(.4f,1,.3f),a.Light));
                }
                if (c.Genes.Predation > .2f) {
                    float bite = a.Attack(time), opening = .1f + .08f * bite;
                    var aim = Quaternion.Euler(0,-a.AttackAngle*Mathf.Rad2Deg*bite,0);
                    for (int side = -1; side <= 1; side += 2)
                        Add(p + r * (aim * Surface(cellPose,.48f+.07f*bite,.015f,side*opening,s)),r * aim * Quaternion.Euler(0,side*(28-35*bite),0),
                            new Vector3(.24f,.055f,.045f)*s*c.Genes.Predation,new Color(.95f,.4f,.18f));
                }
                if (c.Genes.Membrane > .35f) for (int j = 0; j < 6; j++) {
                    float angle = j * Mathf.PI / 3;
                    Add(p + r * Surface(cellPose,Mathf.Cos(angle)*.47f,.09f,Mathf.Sin(angle)*.47f,s),r * Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0),
                        new Vector3(.065f,.10f,.17f)*s*c.Genes.Membrane*(1+.15f*a.Defense),Color.Lerp(new Color(.8f,.67f,.38f),new Color(1,.85f,.4f),a.Defense));
                }
                DrawMotors(c,a,cellPose,p,r,s,phase,color);
                // One finite capture -> internal vacuole -> digestion cycle per confirmed intake window.
                double foodAge = time - a.FeedAt;
                if (foodAge >= 0 && foodAge < 1.2) {
                    float t = (float)(foodAge/1.2), capture = Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.5f));
                    float radius = Mathf.Lerp(.56f,.14f,capture);
                    var point = new Vector3(Mathf.Cos(a.ContactAngle)*radius,.04f,Mathf.Sin(a.ContactAngle)*radius);
                    Vector3 local = capture < .8f ? Surface(cellPose,point.x,point.y,point.z,s) : Interior(cellPose,point,s);
                    float digest = 1 - Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.65f)/.35f));
                    Add(p+r*local,r,Vector3.one*.065f*s*digest,new Color(.45f,1,.65f));
                    if (t > .4f) Add(p+r*Interior(cellPose,point*.65f,s),r,new Vector3(.12f,.09f,.11f)*s*Mathf.Sin(t*Mathf.PI),new Color(.3f,.65f,.48f));
                }
                if (Mathf.Abs(c.TransportLastStep) > .00001f && byId.TryGetValue(c.BondParentId,out var donor)) {
                    Vector3 delta = Periodic(Position(donor)-p); float t = Mathf.Repeat(visualTime*.8f+c.Id*.31f,1);
                    if (c.TransportLastStep > 0) t = 1-t;
                    Add(p+delta*t+Vector3.up*.09f,r,Vector3.one*.035f*s,new Color(.75f,1,.7f));
                }
            }
            // Selection is independent of LOD and never animates biology.
            if (selected.Id > 0) for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i); if (!Visible(c) || !Observation.SameOrganism(selected,c)) continue;
                float s = c.Genes.Size*Mathf.Lerp(.55f,1,animations[c.Id].Growth);
                for (int j = 0; j < 12; j++) {
                    float angle = j*Mathf.PI/6;
                    Add(Position(c)+new Vector3(Mathf.Cos(angle)*.5f,.02f,Mathf.Sin(angle)*.47f)*s,Quaternion.identity,Vector3.one*.035f*s,new Color(.55f,1,1));
                }
            }
            Flush(); current = membrane; highMembrane = false;
            for (int i = 0; i < remnants.Length; i++) {
                var remnant = remnants[i]; float age = (float)(time-remnant.Time);
                if (remnant.Size <= 0 || age < 0 || age > 1.4f) continue;
                if (Periodic(remnant.Position-cameraFocus).sqrMagnitude > visibleRadius*visibleRadius) continue;
                var color = remnant.Color; color.a = .25f*(1-age/1.4f);
                pose = remnant.Pose; pose.Amplitude += age*.025f;
                // Collapse and shader erosion; no locomotor activity, decorative resources, or energy writes.
                Add(remnant.Position,remnant.Rotation,new Vector3(.82f,.65f*(1-age*.3f),.76f)*remnant.Size,color,age/1.4f);
            }
            Flush();
            for (int tier = 0; tier < 2; tier++) {
                highMembrane = tier == 1;
                for (int i = 0; i < simulation.Count; i++) {
                    var c = simulation.GetCell(i); if (!Visible(c) || detailedIds.Contains(c.Id) != highMembrane) continue;
                    var a = animations[c.Id]; var p = Position(c); var r = Rotation(c);
                    float s = c.Genes.Size*Mathf.Lerp(.55f,1,a.Growth);
                    pose = MembranePose.From(a,time,Phase(c,a));
                    Color color = Tint(c,mode);
                    color = Color.Lerp(color,new Color(.65f,.25f,.8f),a.Stress*.2f);
                    color = Color.Lerp(color,new Color(1,.85f,.4f),a.Defense*.4f);
                    color = Color.Lerp(color,new Color(.4f,1,.8f),a.Repair*.2f);
                    color = Color.Lerp(color,new Color(1,.18f,.1f),a.Damage(time)*.4f);
                    color *= .7f+.3f*a.Activity; color.a = .22f+.16f*c.Genes.Membrane;
                    Add(p,r,new Vector3(.82f,.65f,.76f)*s,color);
                    pose = default;
                    if (c.BondParentId > 0 && byId.TryGetValue(c.BondParentId,out var parent)) {
                        Vector3 delta = Periodic(Position(parent)-p);
                        // Curved, anchored connector: endpoints stay on the true interpolated centers.
                        Vector3 last = p; float thickness = .13f*Mathf.Min(s,parent.Genes.Size);
                        int segments = closeView ? 4 : 1;
                        for (int j = 1; j <= segments; j++) {
                            float t = j/(float)segments;
                            Vector3 bend = new Vector3(-delta.z,0,delta.x).normalized * Mathf.Sin(t*Mathf.PI) * .035f * s * a.Turn;
                            Vector3 next = p+delta*t+bend;
                            Segment(last,next,thickness,color); last = next;
                        }
                    }
                }
                Flush();
            }
        }
        void DrawMotors(Cell c, CellAnimation a, MembranePose p, Vector3 center, Quaternion rotation, float size, float phase, Color color)
        {
            if (a.Target.Flagellum) {
                Vector3 last = center+rotation*Surface(p,-.5f,0,0,size);
                for (int j = 1; j <= 12; j++) {
                    float t = j/12f;
                    MotorCurves.Flagellum(t,phase,a.Drive,a.Activity,a.Turn,out float x,out float z);
                    Vector3 rootOffset = Surface(p,-.5f,0,0,size)-new Vector3(-.41f,0,0)*size;
                    Vector3 next = center+rotation*(new Vector3(x*.82f,0,z*.76f)*size+rootOffset);
                    Segment(last,next,.025f*size*(1-.65f*t),color); last = next;
                }
            }
            if (a.Target.Cilia) for (int j = 0; j < 12; j++) {
                float angle = j*Mathf.PI/6;
                Vector3 origin = Surface(p,Mathf.Cos(angle)*.5f,0,Mathf.Sin(angle)*.5f,size);
                Vector3 raw = new Vector3(Mathf.Cos(angle)*.41f,0,Mathf.Sin(angle)*.38f)*size;
                Vector3 last = center+rotation*origin;
                for (int k = 1; k <= 3; k++) {
                    MotorCurves.Cilium(k/3f,angle,phase,a.Drive,a.Activity,a.Turn,out float x,out float z);
                    Vector3 next = center+rotation*(new Vector3(x*.82f,0,z*.76f)*size+origin-raw);
                    Segment(last,next,.014f*size,color); last = next;
                }
            }
        }
        Vector3 Periodic(Vector3 delta)
        {
            delta.x = Observation.PeriodicDelta(delta.x,world); delta.z = Observation.PeriodicDelta(delta.z,world); return delta;
        }
        void Segment(Vector3 a, Vector3 b, float thickness, Color color)
        {
            Vector3 d = b-a;
            if (d.sqrMagnitude > .00000001f) Add((a+b)*.5f,Quaternion.FromToRotation(Vector3.right,d),new Vector3(d.magnitude+thickness,thickness,thickness),color);
        }
        void Add(Vector3 p, Quaternion rotation, Vector3 scale, Color color, float decay = 0)
        {
            p.x = cameraFocus.x+Observation.PeriodicDelta(p.x-cameraFocus.x,world); p.z = cameraFocus.z+Observation.PeriodicDelta(p.z-cameraFocus.z,world);
            matrices[used] = Matrix4x4.TRS(p,rotation,scale); colors[used] = color;
            shapes[used] = new Vector4(pose.Amplitude,pose.Phase,pose.Stretch,pose.Pinch);
            motors[used] = new Vector4(pose.Drive,pose.Turn,pose.Acceleration,pose.Amoeba);
            responses[used] = new Vector4(pose.Feed,pose.Damage,pose.Defense,pose.Stress);
            contacts[used] = new Vector4(pose.ContactX,pose.ContactZ,pose.InjuryX,pose.InjuryZ);
            vitals[used] = new Vector4(pose.Repair,0,decay,0);
            if (++used == matrices.Length) Flush();
        }
        void Flush()
        {
            if (used == 0) return;
            properties.SetVectorArray("_Shape",shapes); properties.SetVectorArray("_Motor",motors);
            properties.SetVectorArray("_Response",responses); properties.SetVectorArray("_Contact",contacts); properties.SetVectorArray("_Vital",vitals);
            properties.SetVectorArray("_InstanceColor",colors); properties.SetFloat("_WorldSize",world);
            Graphics.DrawMeshInstanced(current == membrane && highMembrane ? membraneMesh : mesh,0,current,matrices,used,properties,ShadowCastingMode.Off,false);
            used = 0;
        }
    }
}
