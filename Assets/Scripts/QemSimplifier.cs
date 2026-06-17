using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    public class QEMSimplifier : IMeshSimplifier
    {
        public string Name => "QEM";

        public bool PreventNormalFlips = true;

        public static Quadric FaceQuadric(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            float len = n.magnitude;
            if (len < 1e-12f) return new Quadric();
            n /= len;
            double a = n.x, b = n.y, c = n.z;
            double d = -(a * p0.x + b * p0.y + c * p0.z);
            return Quadric.FromPlane(a, b, c, d);
        }

        public static Quadric[] ComputeVertexQuadrics(MyMesh mesh)
        {
            var Q = new Quadric[mesh.Positions.Count];
            foreach (var f in mesh.Faces)
            {
                if (!f.alive) continue;
                Quadric kp = FaceQuadric(mesh.Positions[f.v0], mesh.Positions[f.v1], mesh.Positions[f.v2]);
                Q[f.v0] = Q[f.v0] + kp;
                Q[f.v1] = Q[f.v1] + kp;
                Q[f.v2] = Q[f.v2] + kp;
            }
            return Q;
        }

        public static double EdgeCost(Quadric qBar, Vector3 v1, Vector3 v2, out Vector3 vBar)
        {
            if (qBar.TryOptimalPosition(out double x, out double y, out double z))
            {
                vBar = new Vector3((float)x, (float)y, (float)z);
                return qBar.Error(x, y, z);
            }

            double e0 = qBar.Error(v1.x, v1.y, v1.z);                                  
            double e1 = qBar.Error(v2.x, v2.y, v2.z);                                 
            Vector3 mid = (v1 + v2) * 0.5f;
            double eh = qBar.Error(mid.x, mid.y, mid.z);                              
            double C = e0;
            double A = 2.0 * (e0 + e1 - 2.0 * eh);
            double B = e1 - e0 - A;

            if (Math.Abs(A) > 1e-14)
            {
                double tStar = -B / (2.0 * A);
                if (tStar > 0.0 && tStar < 1.0)
                {
                    Vector3 vt = v1 + (float)tStar * (v2 - v1);
                    double et = qBar.Error(vt.x, vt.y, vt.z);
                    if (et <= e0 && et <= e1 && et <= eh)
                    {
                        vBar = vt;
                        return et;
                    }
                }
            }

            if (e0 <= e1 && e0 <= eh) { vBar = v1; return e0; }
            if (e1 <= eh) { vBar = v2; return e1; }
            vBar = mid; return eh;
        }

        public MyMesh Simplify(MyMesh input, int targetTriangleCount)
        {
            MyMesh mesh = input.Clone();   
            mesh.BuildAdjacency();

            Quadric[] Q = ComputeVertexQuadrics(mesh);
            int[] version = new int[mesh.Positions.Count];   
            var heap = new MinHeap();

            foreach (var (a, b) in mesh.EnumerateEdges())
                PushEdge(mesh, Q, version, heap, a, b);

            while (mesh.AliveFaceCount > targetTriangleCount && heap.Count > 0)
            {
                EdgeCollapse e = heap.Pop();

                if (!mesh.VertexAlive[e.v1] || !mesh.VertexAlive[e.v2]) continue;
                if (version[e.v1] != e.ver1 || version[e.v2] != e.ver2) continue;

                
                if (PreventNormalFlips && mesh.CheckCollapseFlips(e.v1, e.v2, e.vBar)) continue;

               
                mesh.CollapseEdge(e.v1, e.v2, e.vBar);

                Q[e.v1] = Q[e.v1] + Q[e.v2];  
                version[e.v1]++;               

                foreach (int w in mesh.NeighborVertices(e.v1))
                    PushEdge(mesh, Q, version, heap, e.v1, w);
            }

            return mesh;   
        }

        private static void PushEdge(MyMesh mesh, Quadric[] Q, int[] version,
                                     MinHeap heap, int v1, int v2)
        {
            Quadric qBar = Q[v1] + Q[v2];
            double cost = EdgeCost(qBar, mesh.Positions[v1], mesh.Positions[v2], out Vector3 vBar);
            heap.Push(new EdgeCollapse
            {
                cost = cost, v1 = v1, v2 = v2, vBar = vBar,
                ver1 = version[v1], ver2 = version[v2]
            });
        }
    }
}
