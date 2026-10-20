using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombieDetection : MonoBehaviour
    {
        public Transform eyes;
        public float detectionDistance = 20f;
        public float detectionAngle = 70f;
        public float hearingDistance = 15f;
        public float attackDistance = 1.6f;
        public float loseTargetDistance = 35f;
        public LayerMask viewMask = ~0;

        public bool CanSee(Transform target)
        {
            if (target == null)
                return false;
            var origin = eyes != null ? eyes.position : transform.position + Vector3.up * 1.5f;
            var to = target.position + Vector3.up * 1.2f - origin;
            var distance = to.magnitude;
            if (distance > detectionDistance)
                return false;
            if (Vector3.Angle(transform.forward, to) > detectionAngle)
                return false;
            var hits = Physics.RaycastAll(origin, to.normalized, distance, viewMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i].transform;
                if (hit == transform || hit.IsChildOf(transform))
                    continue;
                if (hit == target || hit.IsChildOf(target))
                    return true;
                return false;
            }

            return true;
        }

        public bool CanHear(Transform target, float noise)
        {
            if (target == null || noise <= 0.01f)
                return false;
            return Vector3.Distance(transform.position, target.position) <= hearingDistance * noise;
        }
    }
}
