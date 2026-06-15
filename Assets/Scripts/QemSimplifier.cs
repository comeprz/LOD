using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Quadric Error Metrics (Garland-Heckbert, t=0 : on ne contracte que les arêtes
    /// existantes). Couche fine au-dessus de MyMesh : toute la topologie (collapse,
    /// rebranchement, suppression des faces dégénérées) est déléguée à MyMesh.
    /// QEM n'apporte que : quadriques, coût d'arête, min-heap, boucle gloutonne.
    /// </summary>
    public class QemSimplifier : IMeshSimplifier
    {
        public string Name => "QEM (Garland-Heckbert)";

        /// <summary>Refuser un collapse qui retourne une normale voisine (via CheckCollapseFlips).</summary>
        public bool PreventNormalFlips = true;

        // =====================================================================
        //  QUADRIQUES  (étapes 1-3)
        // =====================================================================

        /// <summary> Plan d'une face -> quadrique Kp = p·pᵀ. </summary>
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

        /// <summary> Q d'un sommet = somme des Kp de ses faces incidentes (indexé comme Positions). </summary>
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

        // =====================================================================
        //  COÛT D'UNE ARÊTE  (étapes 4-6)
        // =====================================================================

        /// <summary> Coût d'une arête (v1,v2) ET position retenue v̄ (avec fallback). </summary>
        public static double EdgeCost(Quadric qBar, Vector3 v1, Vector3 v2, out Vector3 vBar)
        {
            if (qBar.TryOptimalPosition(out double x, out double y, out double z))
            {
                vBar = new Vector3((float)x, (float)y, (float)z);
                return qBar.Error(x, y, z);
            }
            Vector3 mid = (v1 + v2) * 0.5f;
            double e1 = qBar.Error(v1.x, v1.y, v1.z);
            double e2 = qBar.Error(v2.x, v2.y, v2.z);
            double em = qBar.Error(mid.x, mid.y, mid.z);
            if (e1 <= e2 && e1 <= em) { vBar = v1; return e1; }
            if (e2 <= em) { vBar = v2; return e2; }
            vBar = mid; return em;
        }

        // =====================================================================
        //  BOUCLE  (étapes 7-9)
        // =====================================================================

        public MyMesh Simplify(MyMesh input, int targetTriangleCount)
        {
            MyMesh mesh = input.Clone();   // ne touche pas la source (3 méthodes, même input)
            mesh.BuildAdjacency();

            Quadric[] Q = ComputeVertexQuadrics(mesh);
            int[] version = new int[mesh.Positions.Count];   // pour la lazy deletion
            var heap = new MinHeap();

            // Remplit le heap avec chaque arête unique (EnumerateEdges dédoublonne déjà).
            foreach (var (a, b) in mesh.EnumerateEdges())
                PushEdge(mesh, Q, version, heap, a, b);

            while (mesh.AliveFaceCount > targetTriangleCount && heap.Count > 0)
            {
                EdgeCollapse e = heap.Pop();

                // Lazy deletion : on ignore les entrées périmées.
                if (!mesh.VertexAlive[e.v1] || !mesh.VertexAlive[e.v2]) continue;
                if (version[e.v1] != e.ver1 || version[e.v2] != e.ver2) continue;

                // Anti-inversion (optionnel) : on saute le collapse s'il retourne une normale.
                if (PreventNormalFlips && mesh.CheckCollapseFlips(e.v1, e.v2, e.vBar)) continue;

                // Topologie déléguée à MyMesh. On garde v1, on supprime v2.
                mesh.CollapseEdge(e.v1, e.v2, e.vBar);

                Q[e.v1] = Q[e.v1] + Q[e.v2];   // quadrique du sommet fusionné
                version[e.v1]++;               // invalide les anciennes entrées touchant v1

                // Les arêtes autour de v1 ont changé -> on les recalcule.
                foreach (int w in mesh.NeighborVertices(e.v1))
                    PushEdge(mesh, Q, version, heap, e.v1, w);
            }

            return mesh;   // simplifié en place ; AliveFaceCount/ToArrays donnent le résultat
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
