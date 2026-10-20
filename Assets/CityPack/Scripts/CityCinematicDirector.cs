using UnityEngine;

namespace CityPack
{
    /// <summary>
    /// Continuous cinematic along placed markers. C restarts it. P hands control to the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityCinematicDirector : MonoBehaviour
    {
        public Camera cinematicCamera;
        public Transform[] waypoints = System.Array.Empty<Transform>();
        public Transform[] lookTargets = System.Array.Empty<Transform>();
        public float[] segmentDurations = System.Array.Empty<float>();
        public CityPlayerController player;
        public CitySidewalkNpc npc;
        public CityLightingController lighting;
        public bool playOnStart = true;
        public float defaultFov = 42f;

        public bool IsPlaying { get; private set; }

        float _elapsed;
        int _segment;
        Quaternion _look = Quaternion.identity;

        void Start()
        {
            if (playOnStart)
                Play();
            else
                GiveControlToPlayer();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (Input.GetKeyDown(KeyCode.C))
                Play();
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
                GiveControlToPlayer();
        }

        void LateUpdate()
        {
            if (!IsPlaying || cinematicCamera == null || waypoints == null || waypoints.Length < 2)
                return;

            var duration = SegmentDuration(_segment);
            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / duration);
            var eased = t * t * (3f - 2f * t);

            var position = Sample(eased);
            var ahead = Sample(Mathf.Clamp01(eased + 0.08f));
            var lookPoint = LookPoint(_segment, ahead);
            var desired = Quaternion.LookRotation((lookPoint - position).normalized, Vector3.up);
            _look = Quaternion.Slerp(_look, desired, 1f - Mathf.Exp(-2.4f * Time.deltaTime));

            cinematicCamera.transform.SetPositionAndRotation(position, _look);
            var fov = Mathf.Lerp(52f, defaultFov, Mathf.Clamp01((_segment + eased) / Mathf.Max(1, waypoints.Length - 1)));
            cinematicCamera.fieldOfView = fov;

            if (lighting != null)
                lighting.SetFocusDistance(Vector3.Distance(position, lookPoint));

            if (t >= 1f)
            {
                _elapsed = 0f;
                _segment++;
                if (_segment >= waypoints.Length - 1)
                    GiveControlToPlayer();
            }
        }

        public void Play()
        {
            if (waypoints == null || waypoints.Length < 2 || cinematicCamera == null)
                return;

            IsPlaying = true;
            _segment = 0;
            _elapsed = 0f;
            cinematicCamera.enabled = true;
            if (cinematicCamera.TryGetComponent<AudioListener>(out var listener))
                listener.enabled = true;

            if (player != null)
                player.SetControl(false);
            if (npc != null)
                npc.SetWalking(false);
            if (lighting != null)
                lighting.SetCinematicEffects(true);

            var start = waypoints[0].position;
            var look = LookPoint(0, waypoints[1].position);
            _look = Quaternion.LookRotation((look - start).normalized, Vector3.up);
            cinematicCamera.transform.SetPositionAndRotation(start, _look);
        }

        public void GiveControlToPlayer()
        {
            IsPlaying = false;
            if (cinematicCamera != null)
            {
                cinematicCamera.enabled = false;
                if (cinematicCamera.TryGetComponent<AudioListener>(out var listener))
                    listener.enabled = false;
            }

            if (lighting != null)
                lighting.SetCinematicEffects(false);
            if (player != null)
                player.SetControl(true);
            if (npc != null)
                npc.SetWalking(true);
        }

        float SegmentDuration(int segment)
        {
            if (segmentDurations != null && segment >= 0 && segment < segmentDurations.Length && segmentDurations[segment] > 0.1f)
                return segmentDurations[segment];
            return 8f;
        }

        Vector3 Sample(float t)
        {
            var count = waypoints.Length;
            var i0 = Mathf.Clamp(_segment - 1, 0, count - 1);
            var i1 = Mathf.Clamp(_segment, 0, count - 1);
            var i2 = Mathf.Clamp(_segment + 1, 0, count - 1);
            var i3 = Mathf.Clamp(_segment + 2, 0, count - 1);
            return Catmull(waypoints[i0].position, waypoints[i1].position, waypoints[i2].position, waypoints[i3].position, t);
        }

        Vector3 LookPoint(int segment, Vector3 fallback)
        {
            if (lookTargets != null && segment >= 0 && segment < lookTargets.Length && lookTargets[segment] != null)
                return lookTargets[segment].position;
            return fallback;
        }

        static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
