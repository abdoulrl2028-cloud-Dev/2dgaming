using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityPack
{
    public static class GameAudio
    {
        public static AudioClip Clip(string name)
        {
            return Resources.Load<AudioClip>("GameAudio/" + name);
        }

        public static AudioSource Loop(string objectName, string clipName, float volume)
        {
            var clip = Clip(clipName);
            if (clip == null)
                return null;
            var source = new GameObject(objectName).AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.Play();
            return source;
        }

        public static void PlayOne(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            var source = new GameObject("OneShot").AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.PlayOneShot(clip, volume);
            Object.Destroy(source.gameObject, clip.length + 0.2f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnScene()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (scene == "Game")
            {
                Loop("CityMusic", "Music_Game", 0.34f);
                Loop("CityAmbience", "Ambience_City", 0.42f);
            }
            else if (scene == "SWAT_Intro" || scene == "MainMenu")
            {
                Loop("MenuMusic", "Music_Opening", scene == "MainMenu" ? 0.4f : 0.28f);
            }
        }
    }
}
