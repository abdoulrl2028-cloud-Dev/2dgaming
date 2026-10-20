using UnityEngine;
using UnityEngine.UI;

namespace SahelBrasil
{
    /// <summary>
    /// Two-step visit mission. Completion is distance on the ground plane,
    /// so a trigger volume is not required.
    /// </summary>
    public class SahelExplorationMission : MonoBehaviour
    {
        public Transform player;
        public SahelMissionStep[] steps;
        public Text title;
        public Text body;

        int _index = -1;

        void Start()
        {
            _index = 0;
            Show();
        }

        void Update()
        {
            if (!Application.isPlaying || player == null || steps == null || _index >= steps.Length)
                return;

            var step = steps[_index];
            if (step.point == null)
                return;

            var flat = player.position - step.point.position;
            flat.y = 0f;
            if (flat.magnitude <= step.radius)
            {
                _index++;
                Show();
            }
        }

        void Show()
        {
            if (title == null || body == null)
                return;

            if (steps == null || _index >= steps.Length)
            {
                title.text = "Missão concluída";
                body.text = "Você atravessou a aldeia do Sahel. A pista, o mercado e o baobá ficam para trás.";
                return;
            }

            title.text = "Exploração do Sahel  " + (_index + 1) + "/" + steps.Length;
            body.text = steps[_index].prompt;
        }
    }

    [System.Serializable]
    public class SahelMissionStep
    {
        public string prompt;
        public Transform point;
        public float radius = 3.4f;
    }
}
