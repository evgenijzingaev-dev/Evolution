using System.Collections.Generic;
using UnityEngine;
namespace Evolution.Runtime
{
    internal static class MicroMeshes
    {
        // 117 vertices instead of a built-in sphere for every organelle and mote.
        public static Mesh Cell(int rings = 8, int sides = 12)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var uv = new List<Vector2>(); var tangents = new List<Vector4>();
            for (int y = 0; y <= rings; y++) for (int x = 0; x <= sides; x++) {
                float latitude = Mathf.PI * y / rings, longitude = 2 * Mathf.PI * x / sides;
                var n = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                vertices.Add(n * .5f); normals.Add(n); uv.Add(new Vector2(x / (float)sides, y / (float)rings));
                tangents.Add(new Vector4(-Mathf.Sin(longitude), 0, Mathf.Cos(longitude), 1));
                if (y < rings && x < sides) {
                    int a = y * (sides + 1) + x, b = a + sides + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            var mesh = new Mesh { name = "Micro cell (low polygon)" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTangents(tangents); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1.8f); return mesh;
        }
        public static Mesh Habitat()
        {
            var mesh = new Mesh { name = "Continuous habitat tile" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
