using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZS
{
    public class PodiumView : MonoBehaviour
    {
        public GameObject[] Karts;
        public Transform[] Ranks;   // PodiumRoot/Pos/Single/Rank1..3
        public Transform View;      // first point of the original single-race camera path

        void Start()
        {
            var cam = Camera.main;
            if (cam != null && View != null)
                cam.transform.SetPositionAndRotation(View.position, View.rotation);
            for (int i = 0; i < Ranks.Length && i < Session.Results.Count; i++)
            {
                var r = Session.Results[i];
                if (Ranks[i] != null && r.Kart < Karts.Length)
                    Instantiate(Karts[r.Kart], Ranks[i].position, Ranks[i].rotation);
            }
        }

        void OnGUI()
        {
            GUI.skin.button.fontSize = GUI.skin.label.fontSize = 22;
            for (int i = 0; i < Session.Results.Count; i++)
            {
                var r = Session.Results[i];
                GUI.Label(new Rect(30, 20 + i * 34, 600, 34), $"{i + 1}. {r.Name}   {(r.Time >= 0 ? r.Time.ToString("F2") + " s" : "chưa về đích")}");
            }
            if (GUI.Button(new Rect(UnityEngine.Screen.width - 260, UnityEngine.Screen.height - 80, 230, 60), "Về sảnh"))
                SceneManager.LoadScene(Scenes.Lobby);
        }
    }
}
