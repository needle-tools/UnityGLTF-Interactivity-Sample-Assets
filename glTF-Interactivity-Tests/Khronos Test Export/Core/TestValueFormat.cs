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

        /// <summary>Prefixed to a compact value that was rounded, so it can't be mistaken for the exact value.</summary>
        public const string ApproxMarker = "~";

        /// <summary>Character width of the monospaced matrix grid on labels (TextMeshPro &lt;mspace&gt;).</summary>
        private const string GridCharWidth = "0.6em";

        public static string ToStr(float f) => f.ToString("R", Invariant);

        /// <summary>
        /// Exact value if it has at most <see cref="CompactMaxDecimals"/> decimals, otherwise rounded and
        /// marked with <see cref="ApproxMarker"/>. Values that round to 0 (float noise like 2.8E-08) become "~0".
        /// E.g. 0.5 -> "0.5", 0.33333334 -> "~0.333", 62.999996 -> "~63", 2.8E-08 -> "~0", 1.2345E+20 -> "~1.23E+20".
        /// </summary>
        public static string ToCompactStr(float f)
        {
            var full = ToStr(f);
            if (float.IsNaN(f) || float.IsInfinity(f) || f == 0f)
                return full;

            var rounded = f.ToString("0." + new string('#', CompactMaxDecimals), Invariant);
            if (rounded == "0" || rounded == "-0")
                return ApproxMarker + "0";

            if (full.IndexOf('E') >= 0)
            {
                // Large values in exponent form
                var shortExp = f.ToString("0.##E+00", Invariant);
                return float.Parse(shortExp, Invariant) == f ? shortExp : ApproxMarker + shortExp;
            }

            var dot = full.IndexOf('.');
            if (dot < 0 || full.Length - dot - 1 <= CompactMaxDecimals)
                return full;

            return ApproxMarker + rounded;
        }

        /// <param name="compactMatrices">Show matrices as a compact, aligned grid with <see cref="ToCompactStr"/>
        /// components (TextMeshPro rich text, for space-limited labels only).</param>
        public static string ToStr(object v, bool compactMatrices = false)
        {
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
                // GltfFloat2x2/3x3 are column-major: index = column * size + row
                case GltfFloat2x2 m2:
                    return compactMatrices
                        ? Grid(2, (r, c) => m2[c * 2 + r])
                        : List(m2.m0, m2.m1, m2.m2, m2.m3);
                case GltfFloat3x3 m3:
                {
                    var values = new[] { m3.m0, m3.m1, m3.m2, m3.m3, m3.m4, m3.m5, m3.m6, m3.m7, m3.m8 };
                    return compactMatrices
                        ? Grid(3, (r, c) => values[c * 3 + r])
                        : List(values);
                }
                case Matrix4x4 m:
                    return compactMatrices
                        ? Grid(4, (r, c) => m[r, c])
                        : List(
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

        private static string List(params float[] values) => "[" + string.Join(",", values.Select(ToStr)) + "]";

        /// <summary>
        /// One matrix row per line, values right-aligned in monospaced columns. All rows have the same
        /// width, so the columns stay aligned on the centered label, and &lt;nobr&gt; keeps a row from
        /// wrapping (the label auto-sizes its font instead). Starts and ends with a line break.
        /// </summary>
        private static string Grid(int size, System.Func<int, int, float> valueAt)
        {
            var cells = new string[size, size];
            var columnWidths = new int[size];
            for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
            {
                cells[r, c] = ToCompactStr(valueAt(r, c));
                columnWidths[c] = Mathf.Max(columnWidths[c], cells[r, c].Length);
            }

            var rows = Enumerable.Range(0, size).Select(r =>
                $"<nobr><mspace={GridCharWidth}>"
                + string.Join("  ", Enumerable.Range(0, size).Select(c => cells[r, c].PadLeft(columnWidths[c])))
                + "</mspace></nobr>");
            return "\n" + string.Join("\n", rows) + "\n";
        }
    }
}
