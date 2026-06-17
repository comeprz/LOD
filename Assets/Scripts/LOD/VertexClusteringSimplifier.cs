using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    public class VertexClusteringSimplifier : IMeshSimplifier
    {
        public string Name => "Vertex Clustering";

        private int[] candidateGridResolutions;

        // Plusieurs résolutions
        public VertexClusteringSimplifier(int[] gridResolutions)
        {
            if (gridResolutions == null || gridResolutions.Length == 0)
                gridResolutions = new int[] { 40, 25, 15, 8 };

            candidateGridResolutions = gridResolutions;
        }

        public MyMesh Simplify(MyMesh input, int targetTriangleCount)
        {
            if (input == null || input.Positions.Count == 0)
                return input;

            // Plusieurs résolutions, on garde celle qui est +- égale a triangleCount
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

            // Cellules, sommets
            Dictionary<Vector3Int, List<int>> clusters = new Dictionary<Vector3Int, List<int>>();

            // Sommets, cellules
            Dictionary<int, Vector3Int> vertexToCell = new Dictionary<int, Vector3Int>();

            // Sommet vivant dans cellule
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

            // Cellule => sommet moyen
            MyMesh output = new MyMesh();
            Dictionary<Vector3Int, int> cellToNewVertex = new Dictionary<Vector3Int, int>();

            foreach (var pair in clusters)
            {
                Vector3 average = Vector3.zero;

                // + positions des sommets, / par le nombre de sommets => sommets moyens
                foreach (int oldVertexIndex in pair.Value)
                    average += input.Positions[oldVertexIndex];

                average /= pair.Value.Count;

                int newIndex = output.AddVertex(average);
                cellToNewVertex[pair.Key] = newIndex;
            }

            // Reconstruction des triangles
            HashSet<string> addedTriangles = new HashSet<string>();

            // Anciennes faces
            foreach (Face face in input.Faces)
            {   
                // Check mort ou cassé
                if (!face.alive || face.IsDegenerate())
                    continue;

                // Check sommets bien présents
                if (!vertexToCell.ContainsKey(face.v0) ||
                    !vertexToCell.ContainsKey(face.v1) ||
                    !vertexToCell.ContainsKey(face.v2))
                    continue;

                // Nouveaux sommets
                int a = cellToNewVertex[vertexToCell[face.v0]];
                int b = cellToNewVertex[vertexToCell[face.v1]];
                int c = cellToNewVertex[vertexToCell[face.v2]];

                // Triangle cassé, deux sommets identiques
                if (a == b || b == c || a == c)
                    continue;

                // Doublons
                string key = TriangleKey(a, b, c);
                if (addedTriangles.Contains(key))
                    continue;

                addedTriangles.Add(key);

                output.AddFace(a, b, c);
            }

            output.BuildAdjacency();
            return output;
        }

        // Box globale
        private Bounds ComputeBounds(MyMesh mesh)
        {
            bool foundFirst = false;
            Bounds bounds = new Bounds();

            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (!mesh.VertexAlive[i])
                    continue;

                // Créé une box centrée sur sommet
                if (!foundFirst)
                {
                    bounds = new Bounds(mesh.Positions[i], Vector3.zero);
                    foundFirst = true;
                }

                // Agrandir, contenir sommet
                else
                {
                    bounds.Encapsulate(mesh.Positions[i]);
                }
            }

            return bounds;
        }

        // Sommet dans quelle cellule ?
        private Vector3Int GetCell(Vector3 position, Bounds bounds, int gridResolution)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;

            // Normalisation des sommets
            float x = size.x == 0 ? 0 : (position.x - min.x) / size.x;
            float y = size.y == 0 ? 0 : (position.y - min.y) / size.y;
            float z = size.z == 0 ? 0 : (position.z - min.z) / size.z;

            // Conversion en cellules
            int ix = Mathf.Clamp(Mathf.FloorToInt(x * gridResolution), 0, gridResolution - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(y * gridResolution), 0, gridResolution - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(z * gridResolution), 0, gridResolution - 1);

            return new Vector3Int(ix, iy, iz);
        }

        // Tri + petit au + grand
        private string TriangleKey(int a, int b, int c)
        {
            int min = Mathf.Min(a, Mathf.Min(b, c));
            int max = Mathf.Max(a, Mathf.Max(b, c));
            int mid = a + b + c - min - max;

            return $"{min}_{mid}_{max}";
        }
    }
}