using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary> Une entrée du tas : un collapse candidat (v1,v2) avec son coût. </summary>
    public struct EdgeCollapse
    {
        public double cost;     // v̄ᵀ Q̄ v̄
        public int v1, v2;      // l'arête
        public Vector3 vBar;    // position retenue après collapse
        public int ver1, ver2;  // versions au moment du calcul (pour la lazy deletion)
    }

    /// <summary>
    /// Min-heap binaire sur EdgeCollapse.cost. Unity < .NET 6 n'a pas de
    /// System.Collections.Generic.PriorityQueue, donc on l'écrit nous-mêmes.
    /// Push et Pop sont en O(log n).
    /// </summary>
    public class MinHeap
    {
        private readonly List<EdgeCollapse> h = new List<EdgeCollapse>();

        public int Count => h.Count;

        public void Push(EdgeCollapse e)
        {
            h.Add(e);
            int i = h.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (h[parent].cost <= h[i].cost) break;
                Swap(parent, i);
                i = parent;
            }
        }

        public EdgeCollapse Pop()
        {
            EdgeCollapse top = h[0];
            int last = h.Count - 1;
            h[0] = h[last];
            h.RemoveAt(last);

            int n = h.Count, i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                if (l < n && h[l].cost < h[smallest].cost) smallest = l;
                if (r < n && h[r].cost < h[smallest].cost) smallest = r;
                if (smallest == i) break;
                Swap(smallest, i);
                i = smallest;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            EdgeCollapse tmp = h[a];
            h[a] = h[b];
            h[b] = tmp;
        }
    }
}
