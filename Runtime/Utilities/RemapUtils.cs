using UnityEngine;

namespace Csound.Unity.Utilities
{
    /// <summary>
    /// Utility class for remapping numeric values between ranges, with optional clamping and exponential skewing.
    /// </summary>
    public static class RemapUtils
    {
        /// <summary>
        /// Controls how the skew factor is interpreted when remapping values.
        /// </summary>
        public enum SkewMode
        {
            /// <summary>
            /// Skew factor has no upper bound. Values below 1 produce exponential mapping,
            /// 1 produces linear mapping, and values above 1 produce logarithmic mapping.
            /// </summary>
            Cabbage,
            /// <summary>
            /// Skew factor is normalised to the 0–1 range. Values below 0.5 produce exponential
            /// mapping, 0.5 produces linear mapping, and values above 0.5 produce logarithmic mapping.
            /// </summary>
            Normalized,
        };

        #region Public API

        /// <summary>
        /// Remaps a float value from one range to another with optional clamping and skewing.
        /// There are two possible modes for skewing: Cabbage and Normalized.
        /// <para>
        /// In Cabbage mode, the skew factor has no upper limit and the mapping behaves as follows:
        /// - Exponential mapping for values above 0 to 1
        /// - Linear mapping at 1
        /// - Logarithmic mapping for values above 1
        /// </para>
        /// <para>
        /// In Normalized mode, the skew factor is bound between 0 and 1, and the mapping behaves as follows:
        /// - Exponential mapping from values above 0 to 0.5
        /// - Linear mapping at 0.5
        /// - Logarithmic mapping for values above 0.5 to 1
        /// </para>
        /// <para>
        /// Both modes return the lower target bound (from2) when the skew is 0 or below.
        /// The Normalized mode returns the upper target bound (to2) when the skew is 1 or above.
        /// </para>
        /// </summary>
        /// <param name="value">The input value to be remapped.</param>
        /// <param name="from1">The lower bound of the source range.</param>
        /// <param name="to1">The upper bound of the source range.</param>
        /// <param name="from2">The lower bound of the target range.</param>
        /// <param name="to2">The upper bound of the target range.</param>
        /// <param name="clamp">Determines whether the mapped value should be clamped within the target range. Default is false.</param>
        /// <param name="skew">A skew factor that controls the mapping behavior. Default is 1</param>
        /// <param name="mode">The mode for skewing the mapping behavior. Default is Cabbage</param>
        /// <returns>The remapped value within the target range.</returns>
        public static float Remap(float value, float from1, float to1, float from2, float to2, bool clamp = false, float skew = 1f, SkewMode mode = SkewMode.Cabbage)
        {
            if (skew <= 0f) return from2;

            if (mode == SkewMode.Normalized)
            {
                if (skew >= 1f) return to2;
            }

            var normalizedValue = (value - from1) / (to1 - from1);
            var exponent = mode == SkewMode.Cabbage ? 1f / skew : Mathf.Log(skew) / Mathf.Log(0.5f);
            var factor = Mathf.Pow(normalizedValue, exponent);
            var retValue = Mathf.Lerp(from2, to2, factor);

            if (float.IsNaN(retValue)) return from2;
            if (float.IsPositiveInfinity(retValue)) return to2;
            if (float.IsNegativeInfinity(retValue)) return from2;

            return clamp ? Mathf.Clamp(retValue, from2, to2) : retValue;
        }

        /// <summary>
        /// Remaps a value from the given range to a normalised 0–1 value, with an optional power-curve skew.
        /// A skew of 1 gives a linear mapping; values below 1 compress the lower end (exponential feel).
        /// </summary>
        /// <param name="value">The input value to remap.</param>
        /// <param name="from">The lower bound of the input range.</param>
        /// <param name="to">The upper bound of the input range.</param>
        /// <param name="skew">Power-curve exponent applied after normalisation. Defaults to 1 (linear).</param>
        /// <returns>A 0–1 float representing the remapped position of <paramref name="value"/> within [<paramref name="from"/>, <paramref name="to"/>].</returns>
        public static float RemapTo0to1(float value, float from, float to, float skew = 1f)
        {
            if ((to - from) == 0) return 0;

            var proportion = Mathf.Clamp01((value - from) / (to - from));

            if (skew == 1)
                return proportion;

            return Mathf.Pow(proportion, skew);
        }

        /// <summary>
        /// Remaps a normalised 0–1 value to the given output range, with an optional power-curve skew.
        /// A skew of 1 gives a linear mapping; values below 1 compress the lower end (exponential feel).
        /// </summary>
        /// <param name="value">The normalised input value (0–1). Values outside this range are clamped.</param>
        /// <param name="from">The lower bound of the output range.</param>
        /// <param name="to">The upper bound of the output range.</param>
        /// <param name="skew">Power-curve exponent applied before scaling. Defaults to 1 (linear).</param>
        /// <returns>A float in the range [<paramref name="from"/>, <paramref name="to"/>] corresponding to the remapped input.</returns>
        public static float RemapFrom0to1(float value, float from, float to, float skew = 1f)
        {
            if (skew == 0) return to;

            var proportion = Mathf.Clamp01(value);

            if (skew != 1 && proportion > 0)
                proportion = Mathf.Exp(Mathf.Log(proportion) / skew);

            return from + (to - from) * proportion;
        }

        #endregion Public API
    }
}
