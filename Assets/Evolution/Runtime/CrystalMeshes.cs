using System.Collections.Generic;
using Evolution.Presentation;
using UnityEngine;

namespace Evolution.Runtime
{
    internal static class CrystalMeshes
    {
        public static Vector3 Point(CrystalForm form,Vector3 p) {CrystalForms.Point(form,ref p.x,ref p.y,ref p.z);return p;}
        public static Mesh Create(CrystalForm form,int rings,int sides)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++) {
                float latitude=Mathf.PI*y/rings,longitude=Mathf.PI*2*x/sides;
                var n=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                var tangent=new Vector3(-Mathf.Sin(longitude),0,Mathf.Cos(longitude));var bitangent=Vector3.Cross(n,tangent);
                var p=Point(form,n*.5f);var pt=Point(form,(n+tangent*.002f).normalized*.5f);var pb=Point(form,(n+bitangent*.002f).normalized*.5f);
                Vector3 normal=Vector3.Cross(pt-p,pb-p).normalized;
                vertices.Add(p);normals.Add(normal.sqrMagnitude>.01f?normal:n);uv.Add(new Vector2(x/(float)sides,y/(float)rings));
                if(y<rings&&x<sides){int a=y*(sides+1)+x,b=a+sides+1;triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);}
            }
            var mesh=new Mesh {name="Crystal "+form};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
