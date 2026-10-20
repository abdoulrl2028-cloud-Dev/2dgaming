using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombieHealth : MonoBehaviour
    {
        public float health = 40f;
        public Animator animator;
        public ZombieAI brain;

        public bool IsDead { get; private set; }

        public void Damage(float amount)
        {
            if (IsDead)
                return;
            health -= amount;
            if (health <= 0f)
            {
                IsDead = true;
                if (animator != null)
                    animator.SetTrigger("Death");
                if (brain != null)
                    brain.Kill();
            }
            else if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }
    }
}
