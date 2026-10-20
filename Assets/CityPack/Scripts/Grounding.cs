using UnityEngine;
using UnityEngine.AI;

namespace CityPack
{
    public static class Grounding
    {
        public static bool Sample(Vector3 near, out Vector3 point)
        {
            point = near;
            var origin = new Vector3(near.x, near.y + 40f, near.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
            var found = false;
            var lowest = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].normal.y < 0.55f)
                    continue;
                if (hits[i].point.y >= lowest)
                    continue;
                lowest = hits[i].point.y;
                point = hits[i].point;
                found = true;
            }

            if (found)
                return true;
            if (NavMesh.SamplePosition(near, out var nav, 12f, NavMesh.AllAreas))
            {
                point = nav.position;
                return true;
            }

            return false;
        }

        public static bool TryStreet(out Bounds road)
        {
            road = new Bounds(Vector3.zero, Vector3.zero);
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var name = renderers[i].name.ToLowerInvariant();
                if (!name.Contains("street") || name.Contains("light"))
                    continue;
                if (!any)
                {
                    road = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    road.Encapsulate(renderers[i].bounds);
                }
            }

            return any;
        }

        public static Vector3 OnStreet(Vector3 preferred, float along)
        {
            if (!TryStreet(out var road))
                return preferred;
            var x = preferred.x;
            if (x < road.min.x + 1.5f || x > road.max.x - 1.5f)
                x = road.center.x;
            else
                x = Mathf.Clamp(x, road.min.x + 1.5f, road.max.x - 1.5f);
            var z = Mathf.Lerp(road.min.z + 6f, road.max.z - 6f, Mathf.Clamp01(along));
            var probe = new Vector3(x, road.max.y + 8f, z);
            return Sample(probe, out var ground) ? ground : probe;
        }
    }
}
