using UnityEngine;

namespace CityPack.Zombies
{
    public class PlayerProgressTracker : MonoBehaviour
    {
        public Transform player;
        public Transform pathStart;
        public Transform pathEnd;
        [Range(0f, 1f)]
        public float progress;

        void Update()
        {
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }

            if (player == null || pathStart == null || pathEnd == null)
                return;
            var axis = pathEnd.position - pathStart.position;
            var length = axis.magnitude;
            if (length < 0.1f)
                return;
            var along = Vector3.Dot(player.position - pathStart.position, axis / length);
            progress = Mathf.Clamp01(along / length);
        }
    }
}
