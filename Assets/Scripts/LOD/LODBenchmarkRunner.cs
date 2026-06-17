using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LOD
{
    public class LODBenchmarkRunner : MonoBehaviour
    {
        [Serializable]
        public class MeshTestEntry
        {
            public string meshName;
            public MeshFilter sourceMeshFilter;
        }

        public enum LODMethod
        {
            VertexClustering,
            EdgeCollapse,
            QEM
        }

        [Header("Meshes à tester")]
        public MeshTestEntry[] meshesToTest;

        [Header("Affichage")]
        public Transform resultsParent;
        public Material resultMaterial;

        [Tooltip("Position de départ du premier résultat")]
        public Vector3 startPosition = Vector3.zero;

        [Tooltip("Espace horizontal entre Original, LOD1, LOD2, etc.")]
        public float spacingX = 3f;

        [Tooltip("Espace vertical/profondeur entre les méthodes")]
        public float spacingZBetweenMethods = 3f;

        [Tooltip("Espace vertical/profondeur entre les meshes")]
        public float spacingZBetweenMeshes = 12f;

        [Header("Niveaux de simplification")]
        [Range(0.01f, 1f)]
        public float[] targetRatios = new float[] { 0.75f, 0.5f, 0.25f, 0.1f };

        [Header("Vertex Clustering")]
        public int[] vertexClusteringGridResolutions = new int[] { 60, 40, 25, 15, 10, 6 };

        [Header("Métriques")]
        public bool computeError = true;
        public int errorSamples = 2000;

        // Fonctions UI

        public void ShowVertexClustering()
        {
            RunSelectedMethods(new LODMethod[] { LODMethod.VertexClustering });
        }

        public void ShowEdgeCollapse()
        {
            RunSelectedMethods(new LODMethod[] { LODMethod.EdgeCollapse });
        }

        public void ShowQEM()
        {
            RunSelectedMethods(new LODMethod[] { LODMethod.QEM });
        }

        public void ShowAllMethods()
        {
            RunSelectedMethods(new LODMethod[]
            {
                LODMethod.VertexClustering,
                LODMethod.EdgeCollapse,
                LODMethod.QEM
            });
        }

        public void ClearResults()
        {
            ClearPreviousResults();
        }

        // Lancement

        private void RunSelectedMethods(LODMethod[] methods)
        {
            ClearPreviousResults();

            if (meshesToTest == null || meshesToTest.Length == 0)
            {
                Debug.LogError("Aucun mesh à tester.");
                return;
            }

            List<IMeshSimplifier> simplifiers = BuildSimplifiers(methods);

            if (simplifiers.Count == 0)
            {
                Debug.LogError("Aucune méthode disponible.");
                return;
            }

            for (int meshIndex = 0; meshIndex < meshesToTest.Length; meshIndex++)
            {
                MeshTestEntry meshEntry = meshesToTest[meshIndex];

                if (meshEntry.sourceMeshFilter == null || meshEntry.sourceMeshFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"Mesh ignoré : {meshEntry.meshName} n'a pas de MeshFilter valide.");
                    continue;
                }

                string currentMeshName = string.IsNullOrWhiteSpace(meshEntry.meshName)
                    ? meshEntry.sourceMeshFilter.name
                    : meshEntry.meshName;

                MyMesh original = MeshConverter.FromUnity(meshEntry.sourceMeshFilter.sharedMesh);

                Debug.Log("");
                Debug.Log($"==============================");
                Debug.Log($"MESH : {currentMeshName}");
                Debug.Log($"Original : {original.AliveVertexCount} sommets / {original.AliveFaceCount} triangles");
                Debug.Log($"==============================");

                for (int methodIndex = 0; methodIndex < simplifiers.Count; methodIndex++)
                {
                    IMeshSimplifier simplifier = simplifiers[methodIndex];

                    Debug.Log("");
                    Debug.Log($"--- Algo : {simplifier.Name} ---");

                    // Ligne de placement pour ce mesh + cette méthode
                    float z = startPosition.z
                              - meshIndex * spacingZBetweenMeshes
                              - methodIndex * spacingZBetweenMethods;

                    for (int level = 0; level < targetRatios.Length; level++)
                    {
                        float ratio = targetRatios[level];
                        int targetTriangleCount = Mathf.Max(1, Mathf.RoundToInt(original.AliveFaceCount * ratio));

                        Stopwatch stopwatch = Stopwatch.StartNew();
                        MyMesh simplified = simplifier.Simplify(original, targetTriangleCount);
                        stopwatch.Stop();

                        Mesh unityMesh = MeshConverter.ToUnity(
                            simplified,
                            $"{currentMeshName}_{simplifier.Name}_LOD{level + 1}"
                        );

                        float error = -1f;

                        if (computeError)
                            error = SimplificationMetrics.SymmetricError(original, simplified, errorSamples);

                        string errorText = computeError ? $" | erreur={error:E3}" : "";

                        Debug.Log(
                            $"LOD{level + 1} ratio={ratio:P0} target={targetTriangleCount} tris" +
                            $" => {simplified.AliveVertexCount} sommets / {simplified.AliveFaceCount} triangles" +
                            $" | temps={stopwatch.Elapsed.TotalMilliseconds:F2} ms" +
                            errorText
                        );

                        float x = startPosition.x + (level + 1) * spacingX;

                        CreateDisplayObject(
                            currentMeshName,
                            $"{simplifier.Name}_LOD{level + 1}",
                            unityMesh,
                            meshEntry.sourceMeshFilter.transform,
                            level + 1,
                            methodIndex
                        );
                    }
                }
            }
        }

        // Création des méthodes
        private List<IMeshSimplifier> BuildSimplifiers(LODMethod[] methods)
        {
            List<IMeshSimplifier> simplifiers = new List<IMeshSimplifier>();

            foreach (LODMethod method in methods)
            {
                switch (method)
                {
                    case LODMethod.VertexClustering:
                        simplifiers.Add(new VertexClusteringSimplifier(vertexClusteringGridResolutions));
                        break;

                    case LODMethod.EdgeCollapse:
                    {
                        IMeshSimplifier edgeCollapse = TryCreateSimplifier("LOD.EdgeCollapseSimplifier");


                        if (edgeCollapse != null)
                            simplifiers.Add(edgeCollapse);
                        else
                            Debug.LogWarning("EdgeCollapseSimplifier introuvable. Le bouton ne fera rien tant que la classe n'existe pas.");

                        break;
                    }

                    case LODMethod.QEM:
                    {
                        IMeshSimplifier qem = TryCreateSimplifier("LOD.QEMSimplifier");

                        if (qem != null)
                            simplifiers.Add(qem);
                        else
                            Debug.LogWarning("QEMSimplifier introuvable. Le bouton ne fera rien tant que la classe n'existe pas.");

                        break;
                    }
                }
            }

            return simplifiers;
        }

        private IMeshSimplifier TryCreateSimplifier(string fullTypeName)
        {
            Type type = FindType(fullTypeName);

            if (type == null)
                return null;

            object instance = Activator.CreateInstance(type);

            return instance as IMeshSimplifier;
        }

        private Type FindType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        // Affichage
        private void CreateDisplayObject(
            string meshName,
            string lodName,
            Mesh mesh,
            Transform sourceTransform,
            int columnIndex,
            int rowIndex
        )
        {
            GameObject obj = new GameObject($"{meshName}_{lodName}");

            Transform parent = resultsParent != null ? resultsParent : transform;
            obj.transform.SetParent(parent, true);

            Vector3 rightOffset = Vector3.right * (columnIndex * spacingX);
            Vector3 rowOffset = Vector3.back * (rowIndex * spacingZBetweenMethods);

            Vector3 finalPosition = sourceTransform.position + rightOffset + rowOffset;
            Quaternion finalRotation = sourceTransform.rotation;

            obj.transform.SetPositionAndRotation(finalPosition, finalRotation);
            obj.transform.localScale = sourceTransform.lossyScale;

            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

            if (resultMaterial != null)
                meshRenderer.sharedMaterial = resultMaterial;
        }

        private void ClearPreviousResults()
        {
            Transform parent = resultsParent != null ? resultsParent : transform;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}