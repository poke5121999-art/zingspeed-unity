using System.Collections.Generic;
using UnityEngine;

namespace ZS
{
    // Original Wwise events rendered by unity-decode-kit (udk/audiopick.py) into Resources/ZSAudio:
    // events.json maps an event name to its clips. The kart engine, drift and race music banks are
    // not in the APK (downloaded after install), so those sounds are absent rather than replaced.
    public static class ZsAudio
    {
        // Race moments -> the original event used for them (closest event the APK carries).
        public const string Countdown = "Play_Mode_Spray_UI_Countdown";
        public const string Nitro = "Play_Mode_RongLuTaoTai_SFX_Nitro";
        public const string MiniBoost = "Play_Mode_RongLuTaoTai_SFX_BoostSurge";
        public const string Win = "Play_Mode_GoldGrab_UI_Win";
        public const string Lose = "Play_Mode_Dajiangsai_UI_Loser";
        public const string Settle = "Play_UI_SaiJiHeYue_JieSuan";
        public const string LobbyEnter = "Play_Hall_Home_enter";
        public const string PickKart = "Play_Hall_PutCar";
        public const string StartRace = "Play_Hall_SEStart";

        static Dictionary<string, List<AudioClip>> clips;
        static AudioSource source;

        static void Load()
        {
            if (clips != null) return;
            clips = new Dictionary<string, List<AudioClip>>();
            var index = Resources.Load<TextAsset>("ZSAudio/events");
            if (index == null) return;
            foreach (var kv in Json.D(Json.Parse(index.text)))
            {
                var list = new List<AudioClip>();
                foreach (var id in Json.L(Json.D(kv.Value)["clips"]))
                {
                    var c = Resources.Load<AudioClip>("ZSAudio/" + (string)id);
                    if (c != null) list.Add(c);
                }
                clips[kv.Key] = list;
            }
        }

        public static void Play(string evt, float volume = 1f)
        {
            Load();
            if (!clips.TryGetValue(evt, out var list) || list.Count == 0) return;
            if (source == null)
            {
                var go = new GameObject("ZS Audio");
                if (Application.isPlaying) Object.DontDestroyOnLoad(go);  // throws outside play mode (editor smoke test)
                source = go.AddComponent<AudioSource>();
            }
            source.PlayOneShot(list[Random.Range(0, list.Count)], volume);
        }
    }
}
