using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Génère les N niveaux de détail QEM à partir d'un mesh source, et fournit
    /// le câblage vers un LODGroup Unity (le switch par taille écran ~ distance caméra).
    ///
    /// C'est le pont entre ta partie (QEM) et la gestion LOD de la caméra (A) :
    /// chaque méthode expose un BuildLevels ; A assemble les 3 dans la démo.
    /// </summary>
    public static class QemLodBuilder
    {
        // 4 niveaux par défaut : 100% (= original), 50%, 25%, 10% des faces.
        public static readonly float[] DefaultRatios = { 1f, 0.5f, 0.25f, 0.1f };

        /// <summary>
        /// Renvoie un Mesh Unity par ratio. ratios[i] = fraction des faces d'origine.
        /// La source n'est pas modifiée (Simplify travaille sur un Clone interne).
        /// </summary>
        public static Mesh[] BuildLevels(MyMesh source, float[] ratios = null)
        {
            ratios = ratios ?? DefaultRatios;
            source.BuildAdjacency();
            int baseFaces = source.AliveFaceCount;

            var simp = new QEMSimplifier();
            var meshes = new Mesh[ratios.Length];

            for (int i = 0; i < ratios.Length; i++)
            {
                int target = Mathf.Max(4, Mathf.RoundToInt(baseFaces * ratios[i]));
                MyMesh lvl = simp.Simplify(source, target);

                lvl.ToArrays(out Vector3[] verts, out int[] tris);
                var m = new Mesh { name = $"QEM_LOD{i}_{Mathf.RoundToInt(ratios[i] * 100)}pct" };
                if (verts.Length > 65535)
                    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.vertices = verts;
                m.triangles = tris;
                m.RecalculateNormals();
                m.RecalculateBounds();
                meshes[i] = m;

                Debug.Log($"[QEM] LOD{i} : cible {target} faces -> {tris.Length / 3} faces obtenues.");
            }
            return meshes;
        }

        /// <summary>
        /// Câble les niveaux dans un LODGroup : Unity bascule tout seul selon la
        /// taille de l'objet à l'écran (donc selon la distance caméra).
        /// 'screenHeights' = seuils de transition (fractions de hauteur écran),
        /// du plus détaillé au moins détaillé. Le dernier niveau sert de "culled".
        /// </summary>
        public static LODGroup SetupLODGroup(GameObject host, Mesh[] levels, Material material,
                                             float[] screenHeights = null)
        {
            screenHeights = screenHeights ?? new[] { 0.5f, 0.25f, 0.12f, 0.04f };

            // UnityEngine.LOD qualifié : sinon collision avec le namespace 'LOD'.
            var lods = new UnityEngine.LOD[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var child = new GameObject($"LOD{i}");
                child.transform.SetParent(host.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = levels[i];
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                lods[i] = new UnityEngine.LOD(screenHeights[Mathf.Min(i, screenHeights.Length - 1)],
                                              new Renderer[] { r });
            }

            // NB : ne PAS utiliser '??' avec GetComponent. L'opérateur ignore le null
            // surchargé d'Unity et peut renvoyer une référence "fantôme" -> SetLODs
            // lèverait alors un MissingComponentException. Test explicite obligatoire.
            var group = host.GetComponent<LODGroup>();
            if (group == null) group = host.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
            return group;
        }
    }
}
