using System;

namespace LOD
{
    /// <summary>
    /// Quadrique d'erreur de Garland-Heckbert : matrice symétrique 4x4 Q.
    /// On ne stocke que les 10 coefficients uniques. error(v) = vᵀ Q v.
    /// Tout est en double (le solve 4x4 est sensible numériquement).
    /// </summary>
    public struct Quadric
    {
        public double q11, q12, q13, q14, q22, q23, q24, q33, q34, q44;

        public Quadric(double a11, double a12, double a13, double a14,
                       double a22, double a23, double a24,
                       double a33, double a34, double a44)
        {
            q11 = a11; q12 = a12; q13 = a13; q14 = a14;
            q22 = a22; q23 = a23; q24 = a24;
            q33 = a33; q34 = a34; q44 = a44;
        }

        /// <summary> Kp = p·pᵀ pour un plan p = (a,b,c,d) avec a²+b²+c²=1. </summary>
        public static Quadric FromPlane(double a, double b, double c, double d)
        {
            return new Quadric(
                a * a, a * b, a * c, a * d,
                       b * b, b * c, b * d,
                              c * c, c * d,
                                     d * d);
        }

        public static Quadric operator +(Quadric x, Quadric y)
        {
            return new Quadric(
                x.q11 + y.q11, x.q12 + y.q12, x.q13 + y.q13, x.q14 + y.q14,
                               x.q22 + y.q22, x.q23 + y.q23, x.q24 + y.q24,
                                              x.q33 + y.q33, x.q34 + y.q34,
                                                             x.q44 + y.q44);
        }

        /// <summary> error(v) = vᵀ Q v avec v homogène (x,y,z,1). </summary>
        public double Error(double x, double y, double z)
        {
            return q11 * x * x + 2 * q12 * x * y + 2 * q13 * x * z + 2 * q14 * x
                 + q22 * y * y + 2 * q23 * y * z + 2 * q24 * y
                 + q33 * z * z + 2 * q34 * z
                 + q44;
        }

        /// <summary>
        /// Position v̄ qui minimise vᵀ Q v : résout A·v = -b (A = bloc 3x3, b = (q14,q24,q34)).
        /// Retourne false si non inversible (zone plate) -> l'appelant prend le fallback.
        /// </summary>
        public bool TryOptimalPosition(out double x, out double y, out double z)
        {
            double a11 = q11, a12 = q12, a13 = q13;
            double a21 = q12, a22 = q22, a23 = q23;
            double a31 = q13, a32 = q23, a33 = q33;

            double det = a11 * (a22 * a33 - a23 * a32)
                       - a12 * (a21 * a33 - a23 * a31)
                       + a13 * (a21 * a32 - a22 * a31);

            if (Math.Abs(det) < 1e-10)
            {
                x = y = z = 0;
                return false;
            }

            double invDet = 1.0 / det;
            double c11 = (a22 * a33 - a23 * a32) * invDet;
            double c12 = (a13 * a32 - a12 * a33) * invDet;
            double c13 = (a12 * a23 - a13 * a22) * invDet;
            double c21 = (a23 * a31 - a21 * a33) * invDet;
            double c22 = (a11 * a33 - a13 * a31) * invDet;
            double c23 = (a13 * a21 - a11 * a23) * invDet;
            double c31 = (a21 * a32 - a22 * a31) * invDet;
            double c32 = (a12 * a31 - a11 * a32) * invDet;
            double c33 = (a11 * a22 - a12 * a21) * invDet;

            double bx = -q14, by = -q24, bz = -q34;
            x = c11 * bx + c12 * by + c13 * bz;
            y = c21 * bx + c22 * by + c23 * bz;
            z = c31 * bx + c32 * by + c33 * bz;
            return true;
        }
    }
}
