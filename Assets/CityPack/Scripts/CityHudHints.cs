using UnityEngine;

namespace CityPack
{
    [DisallowMultipleComponent]
    public class CityHudHints : MonoBehaviour
    {
        public CityLightingController lighting;
        public CityCinematicDirector director;

        void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            var mode = director != null && director.IsPlaying ? "Cinematic" : "Jogo";
            var time = lighting != null ? lighting.preset.ToString() : "Day";
            var text = mode + "  |  " + time + "\nF1 Dia    F2 Pôr do sol    F3 Noite\nP jogar    C repetir a cinematic    Shift correr";
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(16f, 16f, 460f, 78f), text, style);
        }
    }
}
