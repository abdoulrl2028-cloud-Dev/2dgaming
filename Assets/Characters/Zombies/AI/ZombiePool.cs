using System.Collections.Generic;
using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombiePool : MonoBehaviour
    {
        public ZombieController[] prefabs = System.Array.Empty<ZombieController>();
        public int prewarm = 12;
        public int maxAlive = 16;

        readonly List<ZombieController> _available = new List<ZombieController>();
        readonly List<ZombieController> _alive = new List<ZombieController>();

        void Awake()
        {
            for (var i = 0; i < prewarm; i++)
                _available.Add(Create(i % Mathf.Max(1, prefabs.Length)));
        }

        public int AliveCount => _alive.Count;

        public ZombieController Spawn(int variant, Vector3 position, Quaternion rotation)
        {
            if (_alive.Count >= maxAlive)
                return null;
            ZombieController zombie = null;
            for (var i = 0; i < _available.Count; i++)
            {
                if (_available[i] != null && (variant < 0 || _available[i].variantId.EndsWith((variant + 1).ToString("00"))))
                {
                    zombie = _available[i];
                    _available.RemoveAt(i);
                    break;
                }
            }

            if (zombie == null)
                zombie = Create(variant);
            if (zombie == null)
                return null;
            zombie.Present(position, rotation);
            _alive.Add(zombie);
            return zombie;
        }

        public void Reclaim(ZombieController zombie)
        {
            if (zombie == null)
                return;
            _alive.Remove(zombie);
            zombie.Hide();
            _available.Add(zombie);
        }

        ZombieController Create(int variant)
        {
            if (prefabs == null || prefabs.Length == 0 || prefabs[0] == null)
                return null;
            var index = Mathf.Clamp(variant, 0, prefabs.Length - 1);
            var instance = Instantiate(prefabs[index], transform);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
