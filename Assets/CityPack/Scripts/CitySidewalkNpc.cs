using UnityEngine;
using UnityEngine.AI;

namespace CityPack
{
    /// <summary>
    /// Walks baked sidewalk points when a NavMesh is available.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CitySidewalkNpc : MonoBehaviour
    {
        public Transform[] points = System.Array.Empty<Transform>();
        NavMeshAgent _agent;
        int _index;
        int _tries;
        bool _walking;
        bool _ready;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        public void SetWalking(bool walking)
        {
            _walking = walking;
            if (_agent != null && _ready)
                _agent.isStopped = !walking;
        }

        void Update()
        {
            if (_agent == null)
                return;

            if (!_ready)
            {
                _tries++;
                if (NavMesh.SamplePosition(transform.position, out var hit, 6f, NavMesh.AllAreas) && _agent.Warp(hit.position))
                {
                    _ready = true;
                    if (points != null && points.Length > 0 && points[0] != null)
                        _agent.SetDestination(points[0].position);
                    _agent.isStopped = !_walking;
                }
                else if (_tries > 90)
                {
                    enabled = false;
                }

                return;
            }

            if (!_walking || points == null || points.Length == 0)
                return;
            if (_agent.pathPending || _agent.remainingDistance > 0.75f)
                return;

            _index = (_index + 1) % points.Length;
            if (points[_index] != null)
                _agent.SetDestination(points[_index].position);
        }
    }
}
