using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Banc de test à coller sur un GameObject qui a un MeshFilter (+ MeshRenderer).
    /// - Garde le mesh d'origine, reconvertit une fois.
    /// - Slider live (OnGUI) en play mode : tu glisses, le mesh se re-décime.
    /// - Boutons clic-droit (ContextMenu) "Simplify" / "Reset" hors play mode.
    ///
    /// Pour tester une autre méthode (quand A/C ont fini), change juste la ligne
    /// MakeSimplifier() plus bas.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class SimplifierTester : MonoBehaviour
    {
        [Tooltip("Proportion de triangles conservés (1 = original, 0.1 = 10%).")]
        [Range(0.01f, 1f)]
        public float keepRatio = 0.5f;

        [Tooltip("Affiche le slider live en play mode.")]
        public bool showGui = true;

        private MeshFilter _filter;
        private Mesh _originalUnityMesh;
        private MyMesh _source;          // mesh d'origine converti (immuable)
        private int _originalTriCount;

        private float _lastAppliedRatio = -1f;
        private float _lastErr;
        private double _lastMs;
        private int _lastTris;

        private IMeshSimplifier MakeSimplifier()
        {
            // <-- échange ici pour comparer les méthodes
            return new EdgeCollapseSimplifier();
            // return new VertexClusteringSimplifier();   // dev A
            // return new QuadricSimplifier();            // dev C
        }

        private void Awake() => CacheSource();

        private void CacheSource()
        {
            _filter = GetComponent<MeshFilter>();
            if (_filter.sharedMesh == null)
            {
                Debug.LogWarning("[SimplifierTester] Pas de mesh sur le MeshFilter.");
                return;
            }

            // ne pas écraser l'original si déjà caché
            if (_originalUnityMesh == null)
                _originalUnityMesh = _filter.sharedMesh;

            _source = MeshConverter.FromUnity(_originalUnityMesh);
            _originalTriCount = _source.AliveFaceCount;
            Debug.Log($"[SimplifierTester] Source : {_source.AliveVertexCount} sommets, {_originalTriCount} triangles.");
        }

        [ContextMenu("Simplify")]
        public void Apply()
        {
            if (_source == null) CacheSource();
            if (_source == null) return;

            int target = Mathf.Max(4, Mathf.RoundToInt(_originalTriCount * keepRatio));

            var simplifier = MakeSimplifier();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            MyMesh result = simplifier.Simplify(_source, target);
            sw.Stop();

            _filter.sharedMesh = MeshConverter.ToUnity(result, $"{simplifier.Name}_{target}");

            _lastMs = sw.Elapsed.TotalMilliseconds;
            _lastTris = result.AliveFaceCount;
            _lastErr = SimplificationMetrics.SymmetricError(_source, result);
            _lastAppliedRatio = keepRatio;

            Debug.Log($"[{simplifier.Name}] cible={target}  obtenu={_lastTris} tris  " +
                      $"E={_lastErr:E3}  t={_lastMs:F1}ms");
        }

        [ContextMenu("Reset (mesh original)")]
        public void ResetMesh()
        {
            if (_originalUnityMesh != null)
            {
                _filter.sharedMesh = _originalUnityMesh;
                _lastAppliedRatio = -1f;
            }
        }

        private void OnGUI()
        {
            if (!showGui || !Application.isPlaying) return;

            const float w = 360f;
            GUILayout.BeginArea(new Rect(12, 12, w, 130), GUI.skin.box);
            GUILayout.Label($"Triangles conservés : {(keepRatio * 100f):F0}%   " +
                            $"(cible ≈ {Mathf.RoundToInt(_originalTriCount * keepRatio)})");
            keepRatio = GUILayout.HorizontalSlider(keepRatio, 0.01f, 1f);

            if (GUILayout.Button("Reset")) { keepRatio = 1f; ResetMesh(); }

            if (_lastAppliedRatio >= 0f)
                GUILayout.Label($"-> {_lastTris} tris | E={_lastErr:E2} | {_lastMs:F1} ms");
            GUILayout.EndArea();

            // re-décime quand le slider a bougé (une fois par changement de valeur)
            if (Application.isPlaying && !Mathf.Approximately(keepRatio, _lastAppliedRatio))
            {
                if (keepRatio >= 0.999f) ResetMesh();
                else Apply();
            }
        }
    }
}