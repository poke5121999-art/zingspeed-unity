using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZS
{
    // What the lobby picked and what the race produced; survives scene loads.
    public static class Session
    {
        public static int Kart;
        public static string Track = "e_11citynew";
        public static readonly List<Result> Results = new List<Result>();

        public struct Result
        {
            public string Name;
            public int Kart;
            public float Time;  // -1: did not finish
        }
    }

    // The original lobby and podium screens are NGUI prefabs driven by game code the client ships
    // only as IL2CPP (their scripts export as stubs), so the menus here are drawn with IMGUI
    // [reconstructed]. The 3D scenes, anchors, kart models and icons are the original ones.
    public static class Scenes
    {
        public const string Lobby = "Lobby", Race = "Race_11CityNew", Podium = "Podium";
        public static readonly (string Env, string Name, string Scene)[] Tracks = { ("e_11citynew", "11 City New", Race) };
    }

    public class LobbyMenu : MonoBehaviour
    {
        public GameObject[] Karts;      // original HNCar prefabs, Setup.Karts order
        public Texture2D[] Icons;       // NCar_<id>_Splited, null where the APK has none
        public string[] KartIds;
        public Transform VehicleParent; // Character/LobbyModelGroup/VehicleParent in Lobby_L
        public Transform CameraPos;     // CameraPos/Pos_Lobby

        enum Screen { Lobby, Karts, Tracks }
        Screen screen;
        GameObject shown;

        void Start()
        {
            var cam = Camera.main;
            if (cam != null && CameraPos != null && CameraPos.position != Vector3.zero)
                cam.transform.SetPositionAndRotation(CameraPos.position, CameraPos.rotation);
            Show(Session.Kart);
        }

        void Show(int i)
        {
            Session.Kart = Mathf.Clamp(i, 0, Karts.Length - 1);
            if (shown != null) Destroy(shown);
            if (Karts.Length == 0) return;
            shown = Instantiate(Karts[Session.Kart], VehicleParent != null ? VehicleParent : transform, false);
        }

        void Update()
        {
            if (shown != null && screen == Screen.Karts) shown.transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
        }

        void OnGUI()
        {
            GUI.skin.button.fontSize = GUI.skin.label.fontSize = 22;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            switch (screen)
            {
                case Screen.Lobby:
                    GUI.Label(new Rect(30, 20, 600, 40), $"Xe: {KartIds[Session.Kart]}    Đường đua: {TrackName()}");
                    if (GUI.Button(new Rect(w - 260, h - 220, 230, 60), "Chọn xe")) screen = Screen.Karts;
                    if (GUI.Button(new Rect(w - 260, h - 150, 230, 60), "Chọn đường đua")) screen = Screen.Tracks;
                    if (GUI.Button(new Rect(w - 260, h - 80, 230, 60), "Đua")) SceneManager.LoadScene(Scenes.Race);
                    break;
                case Screen.Karts:
                    for (int i = 0; i < Karts.Length; i++)
                    {
                        var r = new Rect(30 + (i % 3) * 170, 30 + (i / 3) * 150, 160, 140);
                        var tex = Icons != null && i < Icons.Length ? Icons[i] : null;
                        if (tex != null ? GUI.Button(r, tex) : GUI.Button(r, KartIds[i])) Show(i);
                        if (i == Session.Kart) GUI.Box(new Rect(r.x, r.yMax - 6, r.width, 6), GUIContent.none);
                    }
                    if (GUI.Button(new Rect(w - 260, h - 80, 230, 60), "Xong")) screen = Screen.Lobby;
                    break;
                case Screen.Tracks:
                    for (int i = 0; i < Scenes.Tracks.Length; i++)
                        if (GUI.Button(new Rect(30, 30 + i * 70, 320, 60), Scenes.Tracks[i].Name))
                        {
                            Session.Track = Scenes.Tracks[i].Env;
                            screen = Screen.Lobby;
                        }
                    GUI.Label(new Rect(30, h - 70, 900, 40), "Các đường khác tải về sau khi cài, không có trong APK.");
                    break;
            }
        }

        static string TrackName()
        {
            foreach (var t in Scenes.Tracks)
                if (t.Env == Session.Track) return t.Name;
            return Session.Track;
        }
    }

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
