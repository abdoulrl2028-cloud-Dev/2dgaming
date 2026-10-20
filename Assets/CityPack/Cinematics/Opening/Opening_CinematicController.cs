using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityPack.Cinematics
{
    public class OpeningCinematicController : MonoBehaviour
    {
        public PlayableDirector timeline;
        public Camera outputCamera;
        public Transform[] shotAnchors = System.Array.Empty<Transform>();
        public Transform[] shotLooks = System.Array.Empty<Transform>();
        public float duration = 43f;
        public bool allowSkip = true;
        public KeyCode skipKey = KeyCode.Escape;
        public KeyCode skipKeyAlt = KeyCode.Space;
        public string nextScene = "SWAT_Intro";
        public string title = "[NOME DO MEU JOGO]";
        public CanvasGroup fade;
        public Text titleLabel;
        public AudioSource music;
        public AudioSource ambience;
        public AudioSource wind;
        public AudioSource traffic;
        public bool useTimeline = true;
        public bool playOnStart = true;

        float _time;
        bool _running;

        void Start()
        {
            if (titleLabel != null)
                titleLabel.text = title;
            if (playOnStart)
                Begin();
            if (!useTimeline)
                timeline = null;
        }

        static void Assign(AudioSource source, string clipName, float volume)
        {
            if (source == null)
                return;
            if (source.clip == null)
                source.clip = GameAudio.Clip(clipName);
            source.volume = volume;
            source.loop = true;
            if (source.clip != null && !source.isPlaying)
                source.Play();
        }

        public void Begin()
        {
            _running = true;
            _time = 0f;
            if (timeline != null)
                timeline.Play();
            Assign(music, "Music_Opening", 0.55f);
            Assign(ambience, "Ambience_City", 0.4f);
            Assign(wind, "Wind", 0.14f);
        }

        void Update()
        {
            if (!_running)
                return;
            if (allowSkip && (Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt)))
            {
                Finish();
                return;
            }

            _time += Time.deltaTime;
            if (fade != null)
            {
                if (_time < 3f)
                    fade.alpha = 1f - _time / 3f;
                else if (_time > duration - 3f)
                    fade.alpha = Mathf.Clamp01((_time - (duration - 3f)) / 3f);
                else
                    fade.alpha = 0f;
            }

            if (titleLabel != null)
            {
                var show = _time > 36f && _time < 40f;
                var group = titleLabel.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = show ? Mathf.Clamp01((_time - 36f) / 1.2f) * (1f - Mathf.Clamp01((_time - 39f) / 1f)) : 0f;
                titleLabel.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(24f, 0f, Mathf.Clamp01((_time - 36f) / 1.5f)));
            }

            if (timeline == null)
                DriveCamera();

            if (_time >= duration)
                Finish();
        }

        void DriveCamera()
        {
            if (outputCamera == null || shotAnchors == null || shotAnchors.Length == 0)
                return;
            var marks = new[] { 3f, 8f, 15f, 23f, 30f, 36f };
            var index = 0;
            for (var i = 0; i < marks.Length && i < shotAnchors.Length; i++)
            {
                if (_time >= marks[i])
                    index = i;
            }

            var next = Mathf.Min(index + 1, shotAnchors.Length - 1);
            var spanStart = index == 0 ? 3f : marks[Mathf.Min(index, marks.Length - 1)];
            var spanEnd = next == index ? duration : marks[Mathf.Min(next, marks.Length - 1)];
            var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(spanStart, spanEnd, _time));
            outputCamera.transform.position = Vector3.Lerp(shotAnchors[index].position, shotAnchors[next].position, t);
            var lookA = shotLooks != null && index < shotLooks.Length && shotLooks[index] != null ? shotLooks[index].position : shotAnchors[index].position + shotAnchors[index].forward;
            var lookB = shotLooks != null && next < shotLooks.Length && shotLooks[next] != null ? shotLooks[next].position : lookA;
            var look = Vector3.Lerp(lookA, lookB, t);
            if ((look - outputCamera.transform.position).sqrMagnitude > 0.01f)
                outputCamera.transform.rotation = Quaternion.Slerp(outputCamera.transform.rotation, Quaternion.LookRotation(look - outputCamera.transform.position), 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        void Finish()
        {
            if (!_running)
                return;
            _running = false;
            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
        }
    }
}
