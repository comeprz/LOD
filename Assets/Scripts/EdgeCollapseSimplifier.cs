using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    public class EdgeCollapseSimplifier : IMeshSimplifier
    {
        public string Name => "Edge Collapse";

        /// <summary>Refuse un collapse si une face voisine s'inverse (dot &lt; ce seuil).</summary>
        public float NormalFlipThreshold = 0.0f;   // 0 = bloque si l'angle dépasse 90°

        /// <summary>Repousse les arêtes de bord en fin de file pour préserver la silhouette.</summary>
        public bool PreserveBoundaries = true;
        public float BoundaryPenalty = 1000f;

        // entrée du tas : un collapse candidat, avec les versions des 2 sommets
        // au moment du push (sert à détecter les entrées périmées).
        private struct Collapse
        {
            public int vKeep, vRemove;
            public int verKeep, verRemove;
            public Vector3 newPos;
        }

        public MyMesh Simplify(MyMesh input, int targetTriangleCount)
        {
            MyMesh m = input.Clone();           // on ne mute jamais la source

            int[] version = new int[m.Positions.Count];

            var heap = new MinHeap<Collapse>();

            int SharedFaceCount(int a, int b)
            {
                int n = 0;
                foreach (int fi in m.VertexFaces[a])
                {
                    var f = m.Faces[fi];
                    if (f.alive && f.Contains(b)) n++;
                }
                return n;
            }

            float Cost(int a, int b)
            {
                float c = Vector3.Distance(m.Positions[a], m.Positions[b]);
                if (PreserveBoundaries && SharedFaceCount(a, b) == 1)
                    c *= BoundaryPenalty;
                return c;
            }

            Vector3 NewPos(int a, int b) => (m.Positions[a] + m.Positions[b]) * 0.5f;

            void PushEdge(int a, int b)
            {
                heap.Push(Cost(a, b), new Collapse
                {
                    vKeep = a,
                    vRemove = b,
                    verKeep = version[a],
                    verRemove = version[b],
                    newPos = NewPos(a, b)
                });
            }

            // init : une entrée par arête unique
            foreach (var (a, b) in m.EnumerateEdges())
                PushEdge(a, b);

            // boucle principale
            while (m.AliveFaceCount > targetTriangleCount && heap.Count > 0)
            {
                var (_, c) = heap.Pop();

                // 1) validation lazy : sommets vivants + versions à jour
                if (!m.VertexAlive[c.vKeep] || !m.VertexAlive[c.vRemove]) continue;
                if (version[c.vKeep] != c.verKeep || version[c.vRemove] != c.verRemove) continue;

                // 2) sécurité : l'arête existe-t-elle encore ? (sinon collapse non défini)
                if (SharedFaceCount(c.vKeep, c.vRemove) == 0) continue;

                // 3) anti-inversion : on saute si ça replie une face voisine
                if (m.CheckCollapseFlips(c.vKeep, c.vRemove, c.newPos, NormalFlipThreshold))
                    continue;

                // 4) on contracte
                m.CollapseEdge(c.vKeep, c.vRemove, c.newPos);

                // 5) vKeep a bougé : toutes ses arêtes changent de coût.
                //    On invalide les anciennes entrées (bump version) et on re-pushe.
                version[c.vKeep]++;
                foreach (int n in m.NeighborVertices(c.vKeep))
                    PushEdge(c.vKeep, n);
            }

            return m;
        }

        // =====================================================================
        // Min-heap binaire (clé = float). Pas de PriorityQueue garanti côté Unity,
        // donc implémentation maison. ~30 lignes.
        // =====================================================================
        private class MinHeap<T>
        {
            private struct Node { public float key; public T value; }
            private readonly List<Node> _data = new List<Node>();

            public int Count => _data.Count;

            public void Push(float key, T value)
            {
                _data.Add(new Node { key = key, value = value });
                int i = _data.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_data[parent].key <= _data[i].key) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public (float key, T value) Pop()
            {
                Node root = _data[0];
                int last = _data.Count - 1;
                _data[0] = _data[last];
                _data.RemoveAt(last);

                int n = _data.Count;
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                    if (l < n && _data[l].key < _data[smallest].key) smallest = l;
                    if (r < n && _data[r].key < _data[smallest].key) smallest = r;
                    if (smallest == i) break;
                    Swap(i, smallest);
                    i = smallest;
                }
                return (root.key, root.value);
            }

            private void Swap(int i, int j)
            {
                Node tmp = _data[i];
                _data[i] = _data[j];
                _data[j] = tmp;
            }
        }
    }
}