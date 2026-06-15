using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Génère les niveaux QEM et les affiche TOUS EN MÊME TEMPS, côte à côte, pour
    /// comparer la décimation en direct. Pose ce composant sur ton GameObject Suzanne
    /// (MeshFilter + MeshRenderer). Clic-droit -> "Spawn LODs Side By Side" (ou Play).
    ///
    /// Pour la démo "switch caméra en live", utilise plutôt QemLodBuilder.SetupLODGroup
    /// + LodCameraTour.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class QemLodTest : MonoBehaviour
    {
        [Tooltip("Ratios de faces par niveau (1 = original).")]
        public float[] ratios = { 1f, 0.5f, 0.25f, 0.1f };

        [Tooltip("Espacement horizontal entre deux meshes.")]
        public float spacing = 2.5f;

        [Tooltip("Décalage de la rangée par rapport à l'original (monde).")]
        public Vector3 rowOffset = new Vector3(0f, 0f, 3f);

        [Tooltip("Afficher une étiquette LODi / nb de faces au-dessus de chaque mesh.")]
        public bool showLabels = true;
        public float labelHeight = 1.5f;

        private struct Spawned { public Transform t; public string label; }
        private readonly List<Spawned> spawned = new List<Spawned>();

        void Start() => Spawn();

        [ContextMenu("Spawn LODs Side By Side")]
        public void Spawn()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogError("[QEM] Pas de mesh."); return; }

            // Nettoie une rangée précédente.
            var old = GameObject.Find("QEM_LOD_Row");
            if (old != null) { if (Application.isPlaying) Destroy(old); else DestroyImmediate(old); }
            spawned.Clear();

            // Import + génération des niveaux.
            MyMesh source = FromUnity(mf.sharedMesh);
            Mesh[] levels = QemLodBuilder.BuildLevels(source, ratios);
            Material mat = GetComponent<MeshRenderer>().sharedMaterial;

            var root = new GameObject("QEM_LOD_Row");
            root.transform.SetParent(transform.parent, false);

            int n = levels.Length;
            float totalW = (n - 1) * spacing;
            Vector3 basePos = transform.position + rowOffset;

            for (int i = 0; i < n; i++)
            {
                int faces = levels[i].triangles.Length / 3;

                var go = new GameObject($"QEM_LOD{i}_{faces}faces");
                go.transform.SetParent(root.transform, false);
                go.transform.position = basePos + Vector3.right * (i * spacing - totalW * 0.5f);
                go.transform.rotation = transform.rotation;     // même orientation que l'original
                go.transform.localScale = transform.localScale;
                go.AddComponent<MeshFilter>().sharedMesh = levels[i];
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;

                spawned.Add(new Spawned { t = go.transform, label = $"LOD{i} — {faces} faces" });
            }

            Debug.Log($"[QEM] {n} niveaux affichés côte à côte sous 'QEM_LOD_Row'.");
        }

        private static MyMesh FromUnity(Mesh src)
        {
            var verts = src.vertices;
            var tris = src.triangles;

            // SOUDURE des sommets coïncidents. Un mesh importé duplique ses sommets le
            // long des coutures UV / arêtes dures : sans soudure le maillage est "découpé"
            // en îlots au niveau des indices -> la décimation ouvre des fentes (trous).
            // On fusionne tous les sommets de même position en un seul.
            var m = new MyMesh();
            var map = new Dictionary<(long, long, long), int>();
            var remap = new int[verts.Length];

            Bounds b = src.bounds;
            float tol = Mathf.Max(b.size.magnitude * 1e-5f, 1e-6f);
            float inv = 1f / tol;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = verts[i];
                var key = ((long)Mathf.Round(p.x * inv),
                           (long)Mathf.Round(p.y * inv),
                           (long)Mathf.Round(p.z * inv));
                if (!map.TryGetValue(key, out int idx))
                {
                    idx = m.AddVertex(p);
                    map[key] = idx;
                }
                remap[i] = idx;
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = remap[tris[i]], c = remap[tris[i + 1]], d = remap[tris[i + 2]];
                if (a == c || c == d || a == d) continue; // face dégénérée après soudure
                m.AddFace(a, c, d);
            }

            Debug.Log($"[QEM] Soudure import : {verts.Length} -> {m.Positions.Count} sommets " +
                      $"({verts.Length - m.Positions.Count} doublons fusionnés).");
            return m;
        }

        void OnGUI()
        {
            if (!showLabels) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            foreach (var s in spawned)
            {
                if (s.t == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(s.t.position + Vector3.up * labelHeight);
                if (sp.z < 0) continue; // derrière la caméra
                GUI.Label(new Rect(sp.x - 110, Screen.height - sp.y - 12, 220, 24), s.label, style);
            }
        }
    }
}
