using UnityEngine;

namespace CityPack.Zombies
{
    public class ZombieAudio : MonoBehaviour
    {
        public AudioSource voice;
        public AudioSource foley;
        public AudioClip idleClip;
        public AudioClip alertClip;
        public AudioClip attackClip;
        public AudioClip deathClip;
        public float zombieAudioDistance = 18f;

        void Awake()
        {
            if (voice != null)
                voice.maxDistance = zombieAudioDistance;
            if (idleClip == null)
                idleClip = GameAudio.Clip("Step_Zombie");
        }

        public void PlayAlert()
        {
            Play(voice, alertClip);
        }

        public void PlayAttack()
        {
            Play(voice, attackClip);
        }

        public void PlayDeath()
        {
            Play(voice, deathClip);
        }

        public void PlayStep()
        {
            Play(foley, idleClip);
        }

        static void Play(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip);
        }
    }
}
