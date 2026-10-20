using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityPack.Characters
{
    /// <summary>
    /// Timed operator introduction. ESC or Space skips to the menu.
    /// </summary>
    public class SWATIntroPlayer : MonoBehaviour
    {
        public Transform subject;
        public Camera shotCamera;
        public string nextScene = "MainMenu";
        public bool playOnStart = true;
        public KeyCode skipKey = KeyCode.Escape;
        public KeyCode skipKeyAlt = KeyCode.Space;

        static readonly Shot[] Shots =
        {
            new Shot(0f, 3f, new Vector3(0f, 0.15f, 1.4f), new Vector3(0.2f, 0.12f, 0f), 0f),
            new Shot(3f, 6f, new Vector3(0.4f, 0.2f, 1.1f), new Vector3(0f, 0.15f, 0f), 32f),
            new Shot(6f, 10f, new Vector3(0.8f, 1.1f, 1.6f), new Vector3(0f, 1.0f, 0f), 38f),
            new Shot(10f, 14f, new Vector3(0.55f, 1.15f, 0.9f), new Vector3(0.1f, 1.1f, 0f), 28f),
            new Shot(14f, 18f, new Vector3(0.35f, 1.65f, 0.8f), new Vector3(0f, 1.6f, 0f), 30f),
            new Shot(18f, 23f, new Vector3(1.8f, 1.2f, 2.4f), new Vector3(0f, 1.0f, 0f), 42f),
            new Shot(23f, 28f, new Vector3(1.1f, 1.35f, 1.5f), new Vector3(0.2f, 1.2f, 0f), 36f),
            new Shot(28f, 33f, new Vector3(0.2f, 1.55f, 2.2f), new Vector3(0f, 1.55f, 0f), 34f),
            new Shot(33f, 38f, new Vector3(-2.2f, 1.3f, 1.2f), new Vector3(0f, 1.1f, 0f), 40f),
            new Shot(38f, 44f, new Vector3(0f, 1.6f, -3.2f), new Vector3(0f, 1.2f, 2f), 48f)
        };

        float _time;
        bool _playing;
        CanvasGroup _fade;

        struct Shot
        {
            public float Start;
            public float End;
            public Vector3 LocalPos;
            public Vector3 Look;
            public float Fov;
            public Shot(float start, float end, Vector3 pos, Vector3 look, float fov)
            {
                Start = start;
                End = end;
                LocalPos = pos;
                Look = look;
                Fov = fov;
            }
        }

        void Start()
        {
            _fade = GetComponentInChildren<CanvasGroup>();
            if (playOnStart)
                _playing = true;
        }

        void Update()
        {
            if (!_playing)
                return;
            if (Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt))
            {
                LoadNext();
                return;
            }

            _time += Time.deltaTime;
            var black = _time < 3f ? 1f - _time / 3f : (_time > 42f ? Mathf.Clamp01((_time - 42f) / 2f) : 0f);
            if (_fade != null)
                _fade.alpha = black;

            if (subject != null && shotCamera != null)
            {
                var shot = Shots[Shots.Length - 1];
                for (var i = 0; i < Shots.Length; i++)
                {
                    if (_time >= Shots[i].Start && _time < Shots[i].End)
                    {
                        shot = Shots[i];
                        break;
                    }
                }

                var next = shot;
                for (var i = 0; i < Shots.Length - 1; i++)
                {
                    if (_time >= Shots[i].Start && _time < Shots[i + 1].Start)
                    {
                        var span = Mathf.Max(0.01f, Shots[i].End - Shots[i].Start);
                        var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_time - Shots[i].Start) / span));
                        shotCamera.transform.position = subject.TransformPoint(Vector3.Lerp(Shots[i].LocalPos, Shots[i + 1].LocalPos, t));
                        var look = Vector3.Lerp(Shots[i].Look, Shots[i + 1].Look, t);
                        shotCamera.transform.rotation = Quaternion.LookRotation(subject.TransformPoint(look) - shotCamera.transform.position, Vector3.up);
                        shotCamera.fieldOfView = Mathf.Lerp(Shots[i].Fov, Shots[i + 1].Fov, t);
                        next = shot;
                        break;
                    }
                }

                if (_time >= Shots[Shots.Length - 1].Start)
                {
                    shotCamera.transform.position = subject.TransformPoint(next.LocalPos);
                    shotCamera.transform.rotation = Quaternion.LookRotation(subject.TransformPoint(next.Look) - shotCamera.transform.position, Vector3.up);
                }
            }

            if (_time > 38f && subject != null)
                subject.position += subject.forward * 0.8f * Time.deltaTime;

            if (_time >= 44f)
                LoadNext();
        }

        void LoadNext()
        {
            _playing = false;
            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
        }
    }
}
