using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Une face triangulaire. On garde un flag "alive" pour pouvoir supprimer
    /// sans réindexer tout le tableau à chaque opération (réindexation -> Compact()).
    /// </summary>
    public struct Face
    {
        public int v0, v1, v2;
        public bool alive;

        public Face(int a, int b, int c)
        {
            v0 = a; v1 = b; v2 = c; alive = true;
        }

        public bool Contains(int v) => v0 == v || v1 == v || v2 == v;

        public bool IsDegenerate() => v0 == v1 || v1 == v2 || v0 == v2;

        /// <summary>Remplace l'indice "from" par "to" (utilisé lors d'un collapse).</summary>
        public Face Replaced(int from, int to)
        {
            var f = this;
            if (f.v0 == from) f.v0 = to;
            if (f.v1 == from) f.v1 = to;
            if (f.v2 == from) f.v2 = to;
            return f;
        }
    }

    /// <summary>
    /// Structure de maillage interne, partagée par les 3 méthodes (clustering,
    /// edge collapse, QEM). Sommets + faces indexées + adjacence sommet->faces.
    ///
    /// - Vertex clustering : reconstruit un mesh neuf, lit juste Positions/Faces.
    /// - Edge collapse / QEM : utilisent CollapseEdge() + VertexFaces pour l'adjacence.
    ///
    /// L'opération CollapseEdge est commune (édition pure de topologie, sans
    /// notion de coût). Le COÛT (longueur d'arête vs quadrique) et la boucle
    /// (heap) restent propres à chaque dev.
    /// </summary>
    public class MyMesh
    {
        public List<Vector3> Positions = new List<Vector3>();
        public List<bool> VertexAlive = new List<bool>();
        public List<Face> Faces = new List<Face>();

        /// <summary>Pour chaque sommet : l'ensemble des indices de faces incidentes.</summary>
        public List<HashSet<int>> VertexFaces = new List<HashSet<int>>();

        public int AliveVertexCount { get; private set; }
        public int AliveFaceCount { get; private set; }

        // ---------------------------------------------------------------------
        // Construction
        // ---------------------------------------------------------------------

        public int AddVertex(Vector3 p)
        {
            Positions.Add(p);
            VertexAlive.Add(true);
            VertexFaces.Add(new HashSet<int>());
            return Positions.Count - 1;
        }

        public int AddFace(int a, int b, int c)
        {
            Faces.Add(new Face(a, b, c));
            return Faces.Count - 1;
        }

        /// <summary>
        /// (Re)construit l'adjacence sommet->faces et recompte les éléments vivants.
        /// À appeler une fois le mesh chargé, avant toute simplification par collapse.
        /// </summary>
        public void BuildAdjacency()
        {
            for (int i = 0; i < VertexFaces.Count; i++)
                VertexFaces[i].Clear();

            // au cas où le nombre de sommets aurait changé
            while (VertexFaces.Count < Positions.Count)
                VertexFaces.Add(new HashSet<int>());

            AliveFaceCount = 0;
            for (int fi = 0; fi < Faces.Count; fi++)
            {
                var f = Faces[fi];
                if (!f.alive) continue;
                if (f.IsDegenerate()) { f.alive = false; Faces[fi] = f; continue; }
                VertexFaces[f.v0].Add(fi);
                VertexFaces[f.v1].Add(fi);
                VertexFaces[f.v2].Add(fi);
                AliveFaceCount++;
            }

            AliveVertexCount = 0;
            for (int i = 0; i < VertexAlive.Count; i++)
                if (VertexAlive[i]) AliveVertexCount++;
        }

        // ---------------------------------------------------------------------
        // Adjacence / requêtes
        // ---------------------------------------------------------------------

        /// <summary>Sommets voisins (reliés par une arête) du sommet v.</summary>
        public HashSet<int> NeighborVertices(int v)
        {
            var result = new HashSet<int>();
            foreach (int fi in VertexFaces[v])
            {
                var f = Faces[fi];
                if (!f.alive) continue;
                if (f.v0 != v) result.Add(f.v0);
                if (f.v1 != v) result.Add(f.v1);
                if (f.v2 != v) result.Add(f.v2);
            }
            return result;
        }

        /// <summary>
        /// Énumère chaque arête (a,b) une seule fois (a &lt; b) à partir des faces vivantes.
        /// Pratique pour initialiser le tas des candidats au collapse.
        /// </summary>
        public IEnumerable<(int a, int b)> EnumerateEdges()
        {
            var seen = new HashSet<long>();
            foreach (var f in Faces)
            {
                if (!f.alive) continue;
                foreach (var (x, y) in new[] { (f.v0, f.v1), (f.v1, f.v2), (f.v2, f.v0) })
                {
                    int a = Mathf.Min(x, y), b = Mathf.Max(x, y);
                    long key = ((long)a << 32) | (uint)b;
                    if (seen.Add(key)) yield return (a, b);
                }
            }
        }

        // ---------------------------------------------------------------------
        // Édition de topologie
        // ---------------------------------------------------------------------

        private void KillFace(int fi)
        {
            var f = Faces[fi];
            if (!f.alive) return;
            VertexFaces[f.v0].Remove(fi);
            VertexFaces[f.v1].Remove(fi);
            VertexFaces[f.v2].Remove(fi);
            f.alive = false;
            Faces[fi] = f;
            AliveFaceCount--;
        }

        /// <summary>
        /// Contracte l'arête/paire (vKeep, vRemove) -> vKeep, déplacé en newPos.
        /// - les faces partagées par les deux deviennent dégénérées et sont supprimées
        /// - les autres faces de vRemove sont reconnectées sur vKeep
        /// - vRemove est marqué mort, l'adjacence est mise à jour
        ///
        /// Topologie pure : aucun coût ici. Appeler CheckCollapseFlips() AVANT
        /// si tu veux refuser les inversions de normales.
        /// </summary>
        public void CollapseEdge(int vKeep, int vRemove, Vector3 newPos)
        {
            Positions[vKeep] = newPos;

            // copie : on va modifier VertexFaces[vRemove] pendant l'itération
            var facesOfRemove = new List<int>(VertexFaces[vRemove]);

            foreach (int fi in facesOfRemove)
            {
                var f = Faces[fi];
                if (!f.alive) continue;

                if (f.Contains(vKeep))
                {
                    // face le long de l'arête contractée -> dégénère
                    KillFace(fi);
                }
                else
                {
                    // reconnecte vRemove -> vKeep
                    f = f.Replaced(vRemove, vKeep);
                    Faces[fi] = f;
                    VertexFaces[vKeep].Add(fi);
                    if (f.IsDegenerate()) KillFace(fi);
                }
            }

            VertexFaces[vRemove].Clear();
            if (VertexAlive[vRemove])
            {
                VertexAlive[vRemove] = false;
                AliveVertexCount--;
            }
        }

        /// <summary>
        /// Vérifie si contracter (vKeep, vRemove) vers newPos retourne une face
        /// voisine (inversion de normale). Renvoie true s'il y a au moins un flip.
        /// dotThreshold : on considère qu'il y a flip si dot(n_avant, n_apres) &lt; seuil.
        /// </summary>
        public bool CheckCollapseFlips(int vKeep, int vRemove, Vector3 newPos, float dotThreshold = 0.0f)
        {
            var candidates = new HashSet<int>(VertexFaces[vKeep]);
            candidates.UnionWith(VertexFaces[vRemove]);

            foreach (int fi in candidates)
            {
                var f = Faces[fi];
                if (!f.alive) continue;
                // les faces contenant les deux sommets vont disparaître : on les ignore
                if (f.Contains(vKeep) && f.Contains(vRemove)) continue;

                Vector3 a0 = Positions[f.v0], b0 = Positions[f.v1], c0 = Positions[f.v2];

                // positions après collapse : vRemove et vKeep -> newPos
                Vector3 a1 = (f.v0 == vRemove || f.v0 == vKeep) ? newPos : a0;
                Vector3 b1 = (f.v1 == vRemove || f.v1 == vKeep) ? newPos : b0;
                Vector3 c1 = (f.v2 == vRemove || f.v2 == vKeep) ? newPos : c0;

                Vector3 nBefore = Vector3.Cross(b0 - a0, c0 - a0);
                Vector3 nAfter = Vector3.Cross(b1 - a1, c1 - a1);

                // face écrasée après coup -> sera retirée, pas un vrai flip
                if (nAfter.sqrMagnitude < 1e-16f) continue;

                if (Vector3.Dot(nBefore.normalized, nAfter.normalized) < dotThreshold)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Géométrie utilitaire
        // ---------------------------------------------------------------------

        public Vector3 FaceNormal(Face f)
        {
            Vector3 a = Positions[f.v0], b = Positions[f.v1], c = Positions[f.v2];
            return Vector3.Cross(b - a, c - a).normalized;
        }

        // ---------------------------------------------------------------------
        // Compaction / export
        // ---------------------------------------------------------------------

        /// <summary>
        /// Produit des tableaux propres et réindexés (sommets/triangles vivants
        /// uniquement), prêts pour Unity. N'altère pas le mesh courant.
        /// </summary>
        public void ToArrays(out Vector3[] outVerts, out int[] outTris)
        {
            int[] remap = new int[Positions.Count];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            var verts = new List<Vector3>();
            for (int i = 0; i < Positions.Count; i++)
            {
                if (!VertexAlive[i]) continue;
                remap[i] = verts.Count;
                verts.Add(Positions[i]);
            }

            var tris = new List<int>();
            foreach (var f in Faces)
            {
                if (!f.alive || f.IsDegenerate()) continue;
                int a = remap[f.v0], b = remap[f.v1], c = remap[f.v2];
                if (a < 0 || b < 0 || c < 0) continue; // sécurité
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            outVerts = verts.ToArray();
            outTris = tris.ToArray();
        }

        /// <summary>Copie profonde (utile pour lancer les 3 méthodes sur la même source).</summary>
        public MyMesh Clone()
        {
            var m = new MyMesh
            {
                Positions = new List<Vector3>(Positions),
                VertexAlive = new List<bool>(VertexAlive),
                Faces = new List<Face>(Faces),
                AliveVertexCount = AliveVertexCount,
                AliveFaceCount = AliveFaceCount
            };
            m.VertexFaces = new List<HashSet<int>>(VertexFaces.Count);
            foreach (var set in VertexFaces)
                m.VertexFaces.Add(new HashSet<int>(set));
            return m;
        }
    }
}
