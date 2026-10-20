using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombieEncounterDirector : MonoBehaviour
    {
        public PlayerProgressTracker progress;
        public ZombiePool pool;
        public Transform player;
        public CinemachineCamera revealCamera;
        public CinemachineCamera streetCamera;
        public CinemachineCamera hordeCamera;
        public float controlLockBlend = 0.35f;

        readonly List<ZombieEncounterPoint> _points = new List<ZombieEncounterPoint>();

        void Awake()
        {
            _points.AddRange(FindObjectsByType<ZombieEncounterPoint>(FindObjectsInactive.Exclude));
            _points.Sort((a, b) => a.minimumPlayerProgress.CompareTo(b.minimumPlayerProgress));
            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                var along = (point.minimumPlayerProgress + point.maximumPlayerProgress) * 0.5f;
                point.transform.position = Grounding.OnStreet(point.transform.position, along);
            }
        }

        void Update()
        {
            if (player == null && progress != null)
                player = progress.player;
            if (player == null)
                return;

            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                if (point == null || point.triggered)
                    continue;
                var traveled = progress != null ? progress.progress : 0f;
                if (traveled < point.minimumPlayerProgress || traveled > point.maximumPlayerProgress)
                    continue;
                if (Vector3.Distance(player.position, point.transform.position) > point.activationDistance)
                    continue;
                point.triggered = true;
                StartCoroutine(Run(point));
            }
        }

        IEnumerator Run(ZombieEncounterPoint point)
        {
            yield return new WaitForSeconds(point.delay);
            var cam = point.cinematicEnabled ? CameraFor(point) : null;
            var motor = player.GetComponent<CityPack.Characters.SWATCharacterController>();
            if (cam != null && motor != null)
            {
                var priority = cam.Priority;
                priority.Value = 40;
                cam.Priority = priority;
                cam.LookAt = point.transform;
                motor.SetControl(false);
            }

            var spawned = new List<ZombieController>();
            for (var i = 0; i < point.zombieCount; i++)
            {
                if (pool == null)
                    break;
                var spot = point.Slot(i);
                if (player != null && Vector3.Dot((spot - player.position).normalized, player.forward) > 0.65f && Vector3.Distance(spot, player.position) < 8f)
                    spot = point.transform.position - player.forward * (6f + i);
                if (Grounding.Sample(spot, out var grounded))
                    spot = grounded;
                else if (NavMeshSample(spot, out var onMesh))
                    spot = onMesh;
                var zombie = pool.Spawn(point.zombieType, spot, Quaternion.LookRotation(player.position - spot));
                if (zombie == null)
                    continue;
                if (zombie.brain != null)
                {
                    zombie.brain.walkSpeed = point.walkSpeed;
                    zombie.brain.chaseSpeed = point.chaseSpeed;
                    zombie.brain.damage = point.damage;
                }
                if (zombie.health != null)
                    zombie.health.health = point.health;
                spawned.Add(zombie);
                yield return new WaitForSeconds(0.35f);
            }

            if (cam != null)
            {
                yield return new WaitForSeconds(point.cinematicDuration);
                var priority = cam.Priority;
                priority.Value = 0;
                cam.Priority = priority;
                if (motor != null)
                    motor.SetControl(true);
            }

            if (!point.respawnEnabled)
                yield break;
            while (true)
            {
                yield return new WaitForSeconds(8f);
                for (var i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null && spawned[i].health != null && spawned[i].health.IsDead)
                        pool.Reclaim(spawned[i]);
                }
            }
        }

        CinemachineCamera CameraFor(ZombieEncounterPoint point)
        {
            if (point.zombieCount >= 8 && hordeCamera != null)
                return hordeCamera;
            if (point.encounterName.ToLowerInvariant().Contains("street") && streetCamera != null)
                return streetCamera;
            return revealCamera;
        }

        static bool NavMeshSample(Vector3 spot, out Vector3 sampled)
        {
            sampled = spot;
            if (!UnityEngine.AI.NavMesh.SamplePosition(spot, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                return false;
            sampled = hit.position;
            return true;
        }
    }
}
