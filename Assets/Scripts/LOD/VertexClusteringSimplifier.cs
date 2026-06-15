using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    public class VertexClusteringSimplifier : IMeshSimplifier
    {
        public string Name => "Vertex Clustering";

        private int fixedGridResolution;
        private int[] candidateGridResolutions;

        // Mode simple : une seule résolution de grille
        public VertexClusteringSimplifier(int gridResolution)
        {
            fixedGridResolution = Mathf.Max(1, gridResolution);
            candidateGridResolutions = null;
        }

        // Mode adaptatif : plusieurs résolutions, on garde celle qui approche le mieux targetTriangleCount
        public VertexClusteringSimplifier(int[] gridResolutions)
        {
            if (gridResolutions == null || gridResolutions.Length == 0)
                gridResolutions = new int[] { 40, 25, 15, 8 };

            candidateGridResolutions = gridResolutions;
            fixedGridResolution = -1;
        }

        // Constructeur vide utile pour ton benchmark global
        public VertexClusteringSimplifier()
        {
            candidateGridResolutions = new int[] { 40, 25, 15, 8 };
            fixedGridResolution = -1;
        }

        public MyMesh Simplify(MyMesh input, int targetTriangleCount)
        {
            if (input == null || input.Positions.Count == 0)
                return input;

            // Si on est en mode résolution fixe
            if (candidateGridResolutions == null)
                return SimplifyWithGrid(input, fixedGridResolution);

            // Sinon, mode adaptatif :
            // on teste plusieurs résolutions et on garde celle qui se rapproche le plus du nombre de triangles demandé
            MyMesh bestMesh = null;
            int bestDifference = int.MaxValue;

            foreach (int resolution in candidateGridResolutions)
            {
                int safeResolution = Mathf.Max(1, resolution);

                MyMesh candidate = SimplifyWithGrid(input, safeResolution);

                int difference = Mathf.Abs(candidate.AliveFaceCount - targetTriangleCount);

                if (bestMesh == null || difference < bestDifference)
                {
                    bestMesh = candidate;
                    bestDifference = difference;
                }
            }

            return bestMesh;
        }

        private MyMesh SimplifyWithGrid(MyMesh input, int gridResolution)
        {
            Bounds bounds = ComputeBounds(input);

            Dictionary<Vector3Int, List<int>> clusters = new Dictionary<Vector3Int, List<int>>();
            Dictionary<int, Vector3Int> vertexToCell = new Dictionary<int, Vector3Int>();

            // 1) Chaque sommet vivant est placé dans une cellule de la grille
            for (int i = 0; i < input.Positions.Count; i++)
            {
                if (!input.VertexAlive[i])
                    continue;

                Vector3Int cell = GetCell(input.Positions[i], bounds, gridResolution);

                if (!clusters.ContainsKey(cell))
                    clusters[cell] = new List<int>();

                clusters[cell].Add(i);
                vertexToCell[i] = cell;
            }

            // 2) Chaque cellule devient un nouveau sommet moyen
            MyMesh output = new MyMesh();
            Dictionary<Vector3Int, int> cellToNewVertex = new Dictionary<Vector3Int, int>();

            foreach (var pair in clusters)
            {
                Vector3 average = Vector3.zero;

                foreach (int oldVertexIndex in pair.Value)
                    average += input.Positions[oldVertexIndex];

                average /= pair.Value.Count;

                int newIndex = output.AddVertex(average);
                cellToNewVertex[pair.Key] = newIndex;
            }

            // 3) Reconstruction des triangles
            HashSet<string> addedTriangles = new HashSet<string>();

            foreach (Face face in input.Faces)
            {
                if (!face.alive || face.IsDegenerate())
                    continue;

                if (!vertexToCell.ContainsKey(face.v0) ||
                    !vertexToCell.ContainsKey(face.v1) ||
                    !vertexToCell.ContainsKey(face.v2))
                    continue;

                int a = cellToNewVertex[vertexToCell[face.v0]];
                int b = cellToNewVertex[vertexToCell[face.v1]];
                int c = cellToNewVertex[vertexToCell[face.v2]];

                // Triangle dégénéré : deux sommets sont devenus identiques
                if (a == b || b == c || a == c)
                    continue;

                // Évite les triangles doublons
                string key = TriangleKey(a, b, c);
                if (addedTriangles.Contains(key))
                    continue;

                addedTriangles.Add(key);

                // On garde l'ordre original pour préserver les normales
                output.AddFace(a, b, c);
            }

            output.BuildAdjacency();
            return output;
        }

        private Bounds ComputeBounds(MyMesh mesh)
        {
            bool foundFirst = false;
            Bounds bounds = new Bounds();

            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (!mesh.VertexAlive[i])
                    continue;

                if (!foundFirst)
                {
                    bounds = new Bounds(mesh.Positions[i], Vector3.zero);
                    foundFirst = true;
                }
                else
                {
                    bounds.Encapsulate(mesh.Positions[i]);
                }
            }

            return bounds;
        }

        private Vector3Int GetCell(Vector3 position, Bounds bounds, int gridResolution)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;

            float x = size.x == 0 ? 0 : (position.x - min.x) / size.x;
            float y = size.y == 0 ? 0 : (position.y - min.y) / size.y;
            float z = size.z == 0 ? 0 : (position.z - min.z) / size.z;

            int ix = Mathf.Clamp(Mathf.FloorToInt(x * gridResolution), 0, gridResolution - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(y * gridResolution), 0, gridResolution - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(z * gridResolution), 0, gridResolution - 1);

            return new Vector3Int(ix, iy, iz);
        }

        private string TriangleKey(int a, int b, int c)
        {
            int min = Mathf.Min(a, Mathf.Min(b, c));
            int max = Mathf.Max(a, Mathf.Max(b, c));
            int mid = a + b + c - min - max;

            return $"{min}_{mid}_{max}";
        }
    }
}