using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Pont entre le Mesh Unity et notre MyMesh.
    ///
    /// IMPORTANT : les meshes importés (OBJ/FBX, Suzanne exportée de Blender)
    /// dupliquent souvent les sommets sur les coutures (normales/UV différents).
    /// Sans soudure, l'adjacence est cassée -> les collapses ne propagent pas et
    /// le modèle "se fragmente" comme l'aggregation ratée du papier Garland.
    /// On weld par défaut (positions quasi identiques -> un seul sommet).
    /// </summary>
    public static class MeshConverter
    {
        public static MyMesh FromUnity(Mesh mesh, bool weld = true, float weldEpsilon = 1e-5f)
        {
            Vector3[] verts = mesh.vertices;

            var rawTris = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
                rawTris.AddRange(mesh.GetTriangles(s));

            var m = new MyMesh();

            if (!weld)
            {
                foreach (var v in verts) m.AddVertex(v);
                for (int i = 0; i + 2 < rawTris.Count; i += 3)
                    m.AddFace(rawTris[i], rawTris[i + 1], rawTris[i + 2]);
                m.BuildAdjacency();
                return m;
            }
            
            var map = new Dictionary<long, int>();
            int[] remap = new int[verts.Length];
            float inv = 1f / weldEpsilon;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                long kx = (long)Mathf.Round(p.x * inv);
                long ky = (long)Mathf.Round(p.y * inv);
                long kz = (long)Mathf.Round(p.z * inv);
                long key = kx * 73856093L ^ ky * 19349663L ^ kz * 83492791L;

                if (!map.TryGetValue(key, out int idx))
                {
                    idx = m.AddVertex(p);
                    map[key] = idx;
                }
                remap[i] = idx;
            }

            for (int i = 0; i + 2 < rawTris.Count; i += 3)
            {
                int a = remap[rawTris[i]];
                int b = remap[rawTris[i + 1]];
                int c = remap[rawTris[i + 2]];
                if (a == b || b == c || a == c) continue;
                m.AddFace(a, b, c);
            }

            m.BuildAdjacency();
            return m;
        }

        public static Mesh ToUnity(MyMesh m, string name = "Simplified")
        {
            m.ToArrays(out Vector3[] verts, out int[] tris);

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
