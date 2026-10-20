using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombieEncounterPoint : MonoBehaviour
    {
        public string encounterName = "Encounter";
        public int zombieCount = 1;
        public int zombieType;
        public float spawnRadius = 4f;
        public float activationDistance = 22f;
        public float triggerDistance = 12f;
        [Range(0f, 1f)] public float minimumPlayerProgress;
        [Range(0f, 1f)] public float maximumPlayerProgress = 1f;
        public float delay = 0.4f;
        public bool cinematicEnabled;
        public float cinematicDuration = 4f;
        public bool respawnEnabled;
        public float walkSpeed = 0.7f;
        public float chaseSpeed = 2.4f;
        public float health = 40f;
        public float damage = 8f;
        public Vector3[] localSlots = System.Array.Empty<Vector3>();

        [HideInInspector] public bool triggered;

        public Vector3 Slot(int index)
        {
            if (localSlots != null && index < localSlots.Length)
                return transform.TransformPoint(localSlots[index]);
            var angle = index * 47f;
            var offset = new Vector3(Mathf.Sin(angle) * spawnRadius, 0f, Mathf.Cos(angle) * spawnRadius);
            return transform.position + offset;
        }
    }
}
