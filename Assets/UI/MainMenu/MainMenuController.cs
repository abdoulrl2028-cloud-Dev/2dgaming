using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityPack.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public string gameScene = "Game";
        public string title = "[NOME DO MEU JOGO]";
        public Text titleLabel;
        public Button playButton;

        void Awake()
        {
            if (titleLabel != null)
                titleLabel.text = title;
            if (playButton != null)
                playButton.onClick.AddListener(Play);
        }

        public void Play()
        {
            GameAudio.PlayOne(GameAudio.Clip("Ui_Click"), 0.8f);
            if (Application.CanStreamedLevelBeLoaded(gameScene))
                SceneManager.LoadScene(gameScene);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
