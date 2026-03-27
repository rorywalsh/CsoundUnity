/*
Copyright (C) 2015 Rory Walsh.

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEngine;

namespace Csound.Unity
{
    /// <summary>
    /// Vector synthesis component: blends four <see cref="CsoundUnityPreset"/> placed at the corners
    /// of a unit square using bilinear interpolation, driven by a 2D <see cref="position"/> in [0,1]×[0,1].
    /// Inspired by the Sequential Prophet VS and Korg Wavestation vector synthesis paradigm.
    /// <para>
    /// Set <see cref="position"/> from any external script (XY pad, mouse, MIDI CC, etc.), or enable
    /// <see cref="automate"/> to let the component move the position automatically using one of the
    /// built-in <see cref="AutomationMode"/> patterns.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CsoundUnity))]
    [AddComponentMenu("CsoundUnity/Vector Morph")]
    public class CsoundUnityVectorMorph : MonoBehaviour
    {
        [Header("Presets (corners)")]
        [Tooltip("Bottom-left corner (0, 0)")]
        public CsoundUnityPreset bottomLeft;

        [Tooltip("Bottom-right corner (1, 0)")]
        public CsoundUnityPreset bottomRight;

        [Tooltip("Top-left corner (0, 1)")]
        public CsoundUnityPreset topLeft;

        [Tooltip("Top-right corner (1, 1)")]
        public CsoundUnityPreset topRight;

        [Header("Position")]
        [Tooltip("Current XY blend position in [0,1]×[0,1]. Can be driven externally.")]
        public Vector2 position;

        [Header("Blend")]
        [Tooltip("How discrete channels (button, checkbox, combobox) are handled during blend.")]
        public CsoundUnity.DiscreteBlendMode discreteBlendMode = CsoundUnity.DiscreteBlendMode.Ignore;

        [Header("Automation")]
        [Tooltip("When enabled, the component moves the position automatically.")]
        public bool automate;

        [Tooltip("Shape of the automated movement path.")]
        public AutomationMode automationMode = AutomationMode.Circle;

        [Tooltip("Speed multiplier for the automated movement.")]
        [Min(0f)]
        public float automationSpeed = 0.5f;

        private CsoundUnity _csound;
        private float _autoTime;
        private Vector2 _rwVelocity;

        void Awake()
        {
            _csound = GetComponent<CsoundUnity>();
        }

        void Update()
        {
            if (!_csound.IsInitialized) return;

            if (automate)
                position = ComputeAutomation();

            if (AllPresetsSet())
                _csound.BlendPresets(bottomLeft, bottomRight, topLeft, topRight, position, discreteBlendMode);
        }

        private bool AllPresetsSet() =>
            bottomLeft != null && bottomRight != null && topLeft != null && topRight != null;

        private Vector2 ComputeAutomation()
        {
            _autoTime += Time.deltaTime * automationSpeed;
            return automationMode switch
            {
                AutomationMode.Circle =>
                    new Vector2(Mathf.Sin(_autoTime), Mathf.Cos(_autoTime)) * 0.5f + Vector2.one * 0.5f,

                AutomationMode.Lissajous =>
                    new Vector2(Mathf.Sin(_autoTime), Mathf.Sin(_autoTime * 2f)) * 0.5f + Vector2.one * 0.5f,

                AutomationMode.RandomWalk =>
                    RandomWalkStep(),

                _ => position
            };
        }

        private Vector2 RandomWalkStep()
        {
            _rwVelocity += new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)) * Time.deltaTime;
            _rwVelocity = Vector2.ClampMagnitude(_rwVelocity, 0.3f);

            var next = position + _rwVelocity * Time.deltaTime;

            // Bounce off edges
            if (next.x < 0f || next.x > 1f) _rwVelocity.x = -_rwVelocity.x;
            if (next.y < 0f || next.y > 1f) _rwVelocity.y = -_rwVelocity.y;

            return new Vector2(Mathf.Clamp01(next.x), Mathf.Clamp01(next.y));
        }

        /// <summary>Available automation paths for vector position movement.</summary>
        public enum AutomationMode
        {
            /// <summary>Circular path around the centre.</summary>
            Circle,
            /// <summary>Lissajous figure (sine on X, double-frequency sine on Y).</summary>
            Lissajous,
            /// <summary>Random walk with bouncing boundaries.</summary>
            RandomWalk,
        }
    }
}
