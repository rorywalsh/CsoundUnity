// CsoundUnityChild does not exist in a WebGL player: it is declared behind
// #if !UNITY_WEBGL || UNITY_EDITOR. This file needs the type at compile time, so it
// follows the same guard — without it no WebGL build of this project can compile.
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
using Csound.Unity.Utilities;

namespace Csound.Unity.Samples.AudioRouting.FMAudioChain
{
    /// <summary>
    /// Moves this object in a slow circle around the audio listener.
    ///
    /// <para>
    /// Purely a listening aid: in a scene where several CsoundUnity instances are playing at
    /// once, whichever one is orbiting is instantly identifiable, because it is the only sound
    /// that moves around you. Put it on a CsoundUnityChild and you can always tell the child's
    /// contribution apart from its parent's, which otherwise share the same timbre.
    /// </para>
    ///
    /// <para>
    /// Spatialisation is already set up by CsoundUnityChild, which forces
    /// <c>spatialBlend = 1</c> on its AudioSource, so no extra configuration is needed. If the
    /// orbiting sound comes out too quiet, raise the AudioSource's <b>Min Distance</b> rather
    /// than shrinking the radius — with Unity's default logarithmic rolloff the level falls off
    /// as soon as the distance exceeds it.
    /// </para>
    /// </summary>
    public class OrbitAroundListener : MonoBehaviour
    {
        [Tooltip("What to orbit around. Leave empty to use the scene's AudioListener, falling " +
                 "back to the main camera.")]
        public Transform target;

        [Header("Orbit")]
        [Tooltip("Distance from the target, in world units.")]
        public float radius = 4f;
        [Tooltip("Degrees per second. Slow is better: the point is to hear the movement, and a " +
                 "fast orbit turns into a wobble rather than a position.")]
        public float degreesPerSecond = 45f;
        [Tooltip("Height above the target. A little lift makes the circle easier to see in the " +
                 "Scene view without changing what you hear much.")]
        public float height = 0f;
        [Tooltip("Angle to start from, in degrees. Useful to spread several orbiting objects out.")]
        public float startAngle = 0f;

        float _angle;

        void Start()
        {
            _angle = startAngle;
            if (target == null) target = FindTarget();
            if (target == null)
                Debug.LogWarning("[OrbitAroundListener] No AudioListener or main camera found — " +
                                 "assign a target manually, otherwise this object will not move.");
        }

        void Update()
        {
            if (target == null) return;

            _angle += degreesPerSecond * Time.deltaTime;
            if (_angle >= 360f) _angle -= 360f;

            var rad = _angle * Mathf.Deg2Rad;
            transform.position = target.position
                                 + new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
        }

        static Transform FindTarget()
        {
            var listener = SceneObjectUtils.FindAny<AudioListener>();
            if (listener != null) return listener.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        void OnDrawGizmosSelected()
        {
            var centre = target != null ? target : FindTarget();
            if (centre == null) return;

            Gizmos.color = Color.cyan;
            var origin = centre.position + Vector3.up * height;
            var prev = origin + new Vector3(0f, 0f, radius);
            for (var i = 1; i <= 48; i++)
            {
                var rad = i / 48f * Mathf.PI * 2f;
                var next = origin + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
#endif
