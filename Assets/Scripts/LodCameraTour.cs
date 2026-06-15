using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Promène automatiquement la caméra entre les distances où le LODGroup bascule
    /// d'un niveau à l'autre, pour démontrer le switch en live.
    ///
    /// Pose ce composant n'importe où dans la scène, lance Play.
    ///   - Auto : la caméra fait un aller-retour (proche -> loin) en passant par
    ///     chaque transition. Touche P pour mettre en pause/reprendre.
    ///   - Manuel (auto en pause) : flèches gauche/droite, ou touches 0-9, pour
    ///     sauter pile à la distance d'un niveau donné.
    /// Un affichage à l'écran indique le niveau actif et la distance courante.
    /// </summary>
    public class LodCameraTour : MonoBehaviour
    {
        [Tooltip("Le LODGroup à filmer. Si vide, cherche 'QEM_LOD_Demo' ou le premier de la scène.")]
        public LODGroup target;

        [Tooltip("Caméra déplacée. Si vide, utilise Camera.main.")]
        public Camera cam;

        [Tooltip("Seconds pour parcourir un niveau (vitesse du tour auto).")]
        public float secondsPerLevel = 2.0f;

        public bool autoPlay = true;
        [Tooltip("Aller-retour (sinon boucle proche->loin).")]
        public bool pingPong = true;

        private float[] levelDistances;   // distance "représentative" de chaque LOD
        private float nearDist, farDist;
        private float tourT;               // paramètre du tour auto [0..1]
        private float currentDist;
        private float desiredDist;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (target == null)
            {
                var go = GameObject.Find("QEM_LOD_Demo");
                target = go != null ? go.GetComponent<LODGroup>()
                                    : Object.FindFirstObjectByType<LODGroup>();
            }
            if (target == null || cam == null)
            {
                Debug.LogError("[LodTour] Il faut un LODGroup et une caméra.");
                enabled = false; return;
            }
            ComputeDistances();
            currentDist = desiredDist = nearDist;
        }

        /// <summary>Distance caméra->objet pour une hauteur écran relative donnée.</summary>
        private float DistanceForHeight(float h)
        {
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float worldSize = target.size * MaxScale(target.transform);
            // h = worldSize / (2 * d * tan(fov/2))  ->  d = worldSize / (2 * h * tan(fov/2))
            return worldSize * QualitySettings.lodBias / (2f * Mathf.Max(h, 1e-4f) * Mathf.Tan(fovRad * 0.5f));
        }

        private void ComputeDistances()
        {
            var lods = target.GetLODs();          // UnityEngine.LOD[] (var -> pas de collision)
            int n = lods.Length;
            levelDistances = new float[n];
            for (int i = 0; i < n; i++)
            {
                float upper = (i == 0) ? 1f : lods[i - 1].screenRelativeTransitionHeight;
                float lower = lods[i].screenRelativeTransitionHeight;
                float repH = Mathf.Sqrt(Mathf.Max(upper, 1e-4f) * Mathf.Max(lower, 1e-4f)); // milieu géométrique
                levelDistances[i] = DistanceForHeight(repH);
            }
            nearDist = levelDistances[0];
            farDist = levelDistances[n - 1] * 1.15f;   // un peu plus loin pour voir le dernier niveau / cull
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) autoPlay = !autoPlay;
            if (Input.GetKeyDown(KeyCode.R)) ComputeDistances();

            if (autoPlay)
            {
                float total = Mathf.Max(0.01f, secondsPerLevel * levelDistances.Length);
                tourT += Time.deltaTime / total;
                float p = pingPong ? Mathf.PingPong(tourT, 1f) : Mathf.Repeat(tourT, 1f);
                desiredDist = Mathf.Lerp(nearDist, farDist, p);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) Step(+1);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
                for (int i = 0; i < levelDistances.Length && i < 10; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i)) desiredDist = levelDistances[i];
            }

            // mouvement lissé vers la distance visée
            currentDist = Mathf.Lerp(currentDist, desiredDist, Time.deltaTime * 4f);
            PlaceCamera(currentDist);
        }

        private int stepIndex;
        private void Step(int dir)
        {
            stepIndex = Mathf.Clamp(stepIndex + dir, 0, levelDistances.Length - 1);
            desiredDist = levelDistances[stepIndex];
        }

        private void PlaceCamera(float d)
        {
            Vector3 center = target.transform.TransformPoint(target.localReferencePoint);
            Vector3 dir = cam.transform.position - center;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.back;
            dir.Normalize();
            cam.transform.position = center + dir * d;
            cam.transform.LookAt(center);
        }

        // niveau LOD qu'Unity afficherait à la distance courante (pour l'overlay)
        private int ActiveLod()
        {
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float worldSize = target.size * MaxScale(target.transform);
            float d = Vector3.Distance(cam.transform.position,
                        target.transform.TransformPoint(target.localReferencePoint));
            float h = worldSize * QualitySettings.lodBias / (2f * d * Mathf.Tan(fovRad * 0.5f));
            var lods = target.GetLODs();
            for (int i = 0; i < lods.Length; i++)
                if (h >= lods[i].screenRelativeTransitionHeight) return i;
            return -1; // culled
        }

        private static float MaxScale(Transform t)
        {
            Vector3 s = t.lossyScale;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        void OnGUI()
        {
            if (levelDistances == null) return;
            int lod = ActiveLod();
            string lodTxt = lod < 0 ? "Culled (trop loin)" : $"LOD{lod}";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.white } };
            GUI.Label(new Rect(15, 15, 600, 30), $"Niveau actif : {lodTxt}", style);
            GUI.Label(new Rect(15, 45, 600, 30), $"Distance : {currentDist:F1}", style);
            GUI.Label(new Rect(15, 75, 700, 30),
                      $"[P] {(autoPlay ? "pause" : "auto")}   [←/→] niveau   [0-9] niveau direct", style);
        }
    }
}
