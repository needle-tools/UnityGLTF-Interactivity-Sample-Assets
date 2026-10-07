using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityGLTF.Interactivity;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Formats test values for labels and readme files with the shortest representation that
    /// round-trips ("R"), always with '.' as decimal separator. Rounding (e.g. "F2") can make
    /// correct results look wrong, like ceil(757.003235) shown as "757.00 = 758.00".
    /// </summary>
    public static class TestValueFormat
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>Matrix components in compact mode keep at most this many decimals.</summary>
        public const int CompactMaxDecimals = 3;

        /// <summary>Appended to a compact value that was shortened, so it can't be mistaken for the exact value.</summary>
        public const string TruncatedMarker = "...";

        public static string ToStr(float f) => f.ToString("R", Invariant);

        /// <summary>
        /// Exact value if it has at most <see cref="CompactMaxDecimals"/> decimals (or 3 significant
        /// digits in exponent form), otherwise rounded and marked with <see cref="TruncatedMarker"/>.
        /// E.g. 0.5 -> "0.5", 0.33333334 -> "0.333...", 62.999996 -> "63...", -0.0001 -> "-1E-04",
        /// 1.2345E-07 -> "1.23E-07...".
        /// </summary>
        public static string ToCompactStr(float f)
        {
            var full = ToStr(f);
            if (float.IsNaN(f) || float.IsInfinity(f))
                return full;

            if (full.IndexOf('E') >= 0)
                return CompactExponent(f);

            var dot = full.IndexOf('.');
            if (dot < 0 || full.Length - dot - 1 <= CompactMaxDecimals)
                return full;

            var rounded = f.ToString("0." + new string('#', CompactMaxDecimals), Invariant);
            // Tiny values would collapse to "0" / "-0" and lose sign and magnitude, e.g. -0.0001
            if (rounded == "0" || rounded == "-0")
                return CompactExponent(f);
            return rounded + TruncatedMarker;
        }

        private static string CompactExponent(float f)
        {
            var shortExp = f.ToString("0.##E+00", Invariant);
            return float.Parse(shortExp, Invariant) == f ? shortExp : shortExp + TruncatedMarker;
        }

        /// <param name="compactMatrices">Shorten matrix components with <see cref="ToCompactStr"/> (for space-limited labels).</param>
        public static string ToStr(object v, bool compactMatrices = false)
        {
            System.Func<float, string> component = compactMatrices ? ToCompactStr : ToStr;
            switch (v)
            {
                case null:
                    return "null";
                case float f:
                    return ToStr(f);
                case double d:
                    return d.ToString("R", Invariant);
                case bool b:
                    return b.ToString(Invariant);
                case int i:
                    return i.ToString(Invariant);
                case Vector2 v2:
                    return Tuple(v2.x, v2.y);
                case Vector3 v3:
                    return Tuple(v3.x, v3.y, v3.z);
                case Vector4 v4:
                    return Tuple(v4.x, v4.y, v4.z, v4.w);
                case Quaternion q:
                    return Tuple(q.x, q.y, q.z, q.w);
                case GltfFloat2x2 m2:
                    return List(component, m2.m0, m2.m1, m2.m2, m2.m3);
                case GltfFloat3x3 m3:
                    return List(component, m3.m0, m3.m1, m3.m2, m3.m3, m3.m4, m3.m5, m3.m6, m3.m7, m3.m8);
                case Matrix4x4 m:
                    return List(component,
                        m.m00, m.m01, m.m02, m.m03,
                        m.m10, m.m11, m.m12, m.m13,
                        m.m20, m.m21, m.m22, m.m23,
                        m.m30, m.m31, m.m32, m.m33);
                case System.IFormattable formattable:
                    return formattable.ToString(null, Invariant);
                default:
                    return v.ToString();
            }
        }

        private static string Tuple(params float[] values) => "(" + string.Join(", ", values.Select(ToStr)) + ")";

        private static string List(System.Func<float, string> component, params float[] values) => "[" + string.Join(",", values.Select(component)) + "]";
    }
}
