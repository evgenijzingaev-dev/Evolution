using System.Collections.Generic;
using Evolution.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Evolution.Runtime
{
    // Shared skin, intercellular bridges and role organelles use the same instanced 3D mesh.
    // Rendering reads biology; it never changes topology, energy or random state.
    internal sealed class OrganismRenderer
    {
        readonly Mesh mesh;
        readonly Material material;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] colors = new Vector4[1023];
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly Dictionary<int, Cell> byId;
        int used;
        float world;
        public OrganismRenderer(Mesh mesh, Material material, int capacity)
        { this.mesh = mesh; this.material = material; byId = new Dictionary<int, Cell>(capacity); }
        public static Color RoleColor(CellRole role)
        {
            switch (role) {
                case CellRole.Feeding: return new Color(.4f, .95f, .5f);
                case CellRole.Movement: return new Color(.25f, .8f, 1);
                case CellRole.Defense: return new Color(1, .7f, .3f);
                case CellRole.Reproduction: return new Color(.95f, .4f, .75f);
                default: return new Color(.6f, .8f, .85f);
            }
        }
        static Color Skin(Cell c) => Color.HSVToRGB((c.ColonyId * .618034f) % 1, .4f, .62f);
        public void Draw(Simulation simulation, int mode, float worldSize)
        {
            world = worldSize; used = 0; byId.Clear();
            for (int i = 0; i < simulation.Count; i++) byId.Add(simulation.GetCell(i).Id, simulation.GetCell(i));
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i);
                var position = new Vector3(c.X, .48f, c.Z);
                var rotation = Quaternion.Euler(0, -c.Heading * Mathf.Rad2Deg, 0);
                float s = c.Genes.Size;
                if (c.ColonyId != 0) {
                    Add(position, rotation, new Vector3(.78f, .48f, .72f) * s, Skin(c));
                    if (c.BondParentId > 0 && byId.TryGetValue(c.BondParentId, out var parent)) {
                        Vector3 d = new Vector3(Delta(parent.X - c.X), 0, Delta(parent.Z - c.Z));
                        if (d.sqrMagnitude > .0001f) {
                            float thickness = .38f * Mathf.Min(s, parent.Genes.Size);
                            Add(position + d * .5f, Quaternion.FromToRotation(Vector3.right, d.normalized),
                                new Vector3(d.magnitude + thickness, thickness, thickness), Skin(c));
                        }
                    }
                }
            }
            Flush();
            for (int i = 0; i < simulation.Count; i++) {
                var c = simulation.GetCell(i);
                var position = new Vector3(c.X, .54f, c.Z);
                var rotation = Quaternion.Euler(0, -c.Heading * Mathf.Rad2Deg, 0);
                float s = c.Genes.Size, d = c.Specialization;
                Color color = mode == 1 ? Color.Lerp(new Color(.2f, .8f, .45f), new Color(1, .55f, .2f), c.Genes.OrganicSpecialization)
                    : mode == 2 ? Color.Lerp(RoleColor(CellRole.Generalist), RoleColor(c.Role), d)
                    : Color.Lerp(new Color(.15f, .5f, 1), new Color(1, .3f, .2f), c.Genes.PreferredTemperature / 50);
                if (c.ColonyId != 0 && mode != 2) color = Color.Lerp(Skin(c), color, .35f);
                float stretch = c.Role == CellRole.Movement ? 1 + .3f * d : 1;
                Add(position, rotation, new Vector3(.65f * stretch, .45f, .6f) * s, color);
                Color nucleus = Color.Lerp(new Color(.65f, .85f, .9f), RoleColor(c.Role), d);
                Add(position + Vector3.up * .21f * s, rotation, new Vector3(.2f, .13f, .2f) * s, nucleus);
                if (d < .15f) continue;
                if (c.Role == CellRole.Movement) {
                    for (int tail = -1; tail <= 1; tail++)
                        Add(position + rotation * new Vector3(-.4f, .04f, tail * .15f) * s,
                            rotation * Quaternion.Euler(0, tail * 20, 0), new Vector3(.45f * d, .055f, .065f) * s, nucleus);
                } else if (c.Role == CellRole.Defense) {
                    Add(position + Vector3.up * .2f * s, rotation, new Vector3(.68f, .12f, .65f) * s, nucleus);
                } else if (c.Role == CellRole.Feeding) {
                    Add(position + rotation * new Vector3(.25f, .12f, 0) * s, rotation, new Vector3(.22f, .18f, .3f) * s, nucleus * .65f);
                } else if (c.Role == CellRole.Reproduction) {
                    Add(position + rotation * new Vector3(-.16f, .23f, .12f) * s, rotation, Vector3.one * .16f * s * d, nucleus);
                }
            }
            Flush();
        }
        float Delta(float d) => d - Mathf.Floor(d / world + .5f) * world;
        void Add(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            // Periodic copies meet at the dish boundary; shader clips everything outside the world.
            float radius = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * .5f;
            int minX = position.x + radius > world ? -1 : 0;
            int maxX = position.x - radius < 0 ? 1 : 0;
            int minZ = position.z + radius > world ? -1 : 0;
            int maxZ = position.z - radius < 0 ? 1 : 0;
            for (int x = minX; x <= maxX; x++) for (int z = minZ; z <= maxZ; z++) {
                matrices[used] = Matrix4x4.TRS(position + new Vector3(x * world, 0, z * world), rotation, scale);
                colors[used++] = color;
                if (used == matrices.Length) Flush();
            }
        }
        void Flush()
        {
            if (used == 0) return;
            properties.SetVectorArray("_InstanceColor", colors); properties.SetFloat("_WorldSize", world);
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, used, properties, ShadowCastingMode.Off, false);
            used = 0;
        }
    }
}
