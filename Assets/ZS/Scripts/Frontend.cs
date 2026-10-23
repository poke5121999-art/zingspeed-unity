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
}
