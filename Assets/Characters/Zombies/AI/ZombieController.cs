using UnityEngine;

namespace CityPack.Zombies
{
    /// <summary>
    /// Marker used by the pool and the encounter director.
    /// </summary>
    public class ZombieController : MonoBehaviour
    {
        public string variantId = "Zombie_01";
        public ZombieAI brain;
        public ZombieHealth health;
        public bool hiddenUntilTriggered = true;

        void Awake()
        {
            if (brain == null)
                brain = GetComponent<ZombieAI>();
            if (health == null)
                health = GetComponent<ZombieHealth>();
        }

        public void Present(Vector3 position, Quaternion rotation)
        {
            if (Grounding.Sample(position, out var ground))
                position = ground;
            transform.SetPositionAndRotation(position, rotation);
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
                agent.Warp(position);
            gameObject.SetActive(true);
            if (health != null)
            {
                health.health = Mathf.Max(health.health, 1f);
            }
            if (brain != null)
                brain.Activate(true);
        }

        public void Hide()
        {
            if (brain != null)
                brain.Activate(false);
            gameObject.SetActive(false);
        }
    }
}
