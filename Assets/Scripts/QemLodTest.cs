using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Test du LOD QEM. Pose ce composant sur le GameObject qui affiche ton Suzanne
    /// dense (MeshFilter + MeshRenderer). Clic-droit -> "Build QEM LOD" (ou Play).
    ///
    /// Il génère les 4 niveaux QEM et les câble dans un LODGroup sur un nouvel objet,
    /// posé à côté de l'original. Unity choisit ensuite le niveau selon la taille à
    /// l'écran (= distance caméra).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class QemLodTest : MonoBehaviour
    {
        [Tooltip("Ratios de faces par niveau (LOD0 = 1 = original).")]
        public float[] ratios = { 1f, 0.5f, 0.25f, 0.1f };

        [Tooltip("Décalage de l'objet LOD, pour le voir à côté de l'original.")]
        public Vector3 offset = new Vector3(2.5f, 0f, 0f);

        void Start() => Build();

        [ContextMenu("Build QEM LOD")]
        public void Build()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogError("[QEM] Pas de mesh."); return; }

            MyMesh source = FromUnity(mf.sharedMesh);

            // 1) génère les niveaux (logge le nb de faces de chacun)
            Mesh[] levels = QemLodBuilder.BuildLevels(source, ratios);

            // 2) câble le LODGroup sur un objet dédié, à côté de l'original
            var host = new GameObject("QEM_LOD_Demo");
            host.transform.SetParent(transform.parent, false);
            host.transform.position = transform.position + offset;
            host.transform.rotation = transform.rotation;
            host.transform.localScale = transform.localScale;

            var mat = GetComponent<MeshRenderer>().sharedMaterial;
            QemLodBuilder.SetupLODGroup(host, levels, mat);

            Debug.Log($"[QEM] LODGroup prêt sur '{host.name}' avec {levels.Length} niveaux. " +
                      "Bouge la caméra (ou l'objet) pour voir le switch.");
        }

        private static MyMesh FromUnity(Mesh src)
        {
            var m = new MyMesh();
            var v = src.vertices;
            var t = src.triangles;
            for (int i = 0; i < v.Length; i++) m.AddVertex(v[i]);
            for (int i = 0; i < t.Length; i += 3) m.AddFace(t[i], t[i + 1], t[i + 2]);
            return m;
        }
    }
}
