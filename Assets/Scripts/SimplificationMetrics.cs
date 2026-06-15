using System.Collections.Generic;
using UnityEngine;

namespace LOD
{
    /// <summary>
    /// Métrique d'erreur géométrique commune, utilisée pour comparer les 3 méthodes
    /// (et NON par les algos eux-mêmes — c'est une mesure d'évaluation, comme le E_i
    /// de Garland-Heckbert).
    ///
    /// E = moyenne des distances² entre des points échantillonnés sur un mesh et la
    /// surface de l'autre, dans les deux sens (symétrique). Plus c'est petit, mieux
    /// l'approximation colle à l'original.
    /// </summary>
    public static class SimplificationMetrics
    {
        public struct Result
        {
            public int triangles;
            public int vertices;
            public float meanSquaredError;
            public double simplifyMs;

            public override string ToString()
                => $"tris={triangles,7}  verts={vertices,7}  E={meanSquaredError:E3}  t={simplifyMs:F1}ms";
        }

        /// <summary>
        /// Erreur symétrique entre deux maillages. maxSamples plafonne le nombre de
        /// points testés par sens (sous-échantillonnage aléatoire) pour rester rapide :
        /// c'est du brute force O(samples * faces).
        /// </summary>
        public static float SymmetricError(MyMesh original, MyMesh simplified, int maxSamples = 4000, int seed = 1)
        {
            float a = OneWayError(original, simplified, maxSamples, seed);
            float b = OneWayError(simplified, original, maxSamples, seed + 1);
            return 0.5f * (a + b);
        }

        /// <summary>Moyenne des distances² des sommets de "from" vers la surface de "to".</summary>
        public static float OneWayError(MyMesh from, MyMesh to, int maxSamples, int seed)
        {
            var toFaces = new List<(Vector3 a, Vector3 b, Vector3 c)>();
            foreach (var f in to.Faces)
            {
                if (!f.alive || f.IsDegenerate()) continue;
                toFaces.Add((to.Positions[f.v0], to.Positions[f.v1], to.Positions[f.v2]));
            }
            if (toFaces.Count == 0) return float.PositiveInfinity;

            var samples = new List<Vector3>();
            for (int i = 0; i < from.Positions.Count; i++)
                if (from.VertexAlive[i]) samples.Add(from.Positions[i]);
            if (samples.Count == 0) return 0f;

            if (samples.Count > maxSamples)
            {
                var rng = new System.Random(seed);
                for (int i = samples.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (samples[i], samples[j]) = (samples[j], samples[i]);
                }
                samples.RemoveRange(maxSamples, samples.Count - maxSamples);
            }

            double sum = 0.0;
            foreach (var p in samples)
            {
                float best = float.PositiveInfinity;
                foreach (var (a, b, c) in toFaces)
                {
                    Vector3 cp = ClosestPointOnTriangle(p, a, b, c);
                    float d2 = (p - cp).sqrMagnitude;
                    if (d2 < best) best = d2;
                }
                sum += best;
            }
            return (float)(sum / samples.Count);
        }

        /// <summary>
        /// Point le plus proche d'un triangle (Ericson, Real-Time Collision Detection).
        /// </summary>
        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return a + v * ab;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return a + w * ac;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + w * (c - b);
            }

            float denom = 1f / (va + vb + vc);
            float vv = vb * denom;
            float ww = vc * denom;
            return a + ab * vv + ac * ww;
        }
    }
}
