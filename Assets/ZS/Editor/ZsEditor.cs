using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZS.EditorTools
{
    // Builds the playable scenes from the original ones (copies; originals stay untouched) and runs a
    // headless race to prove the loop works: batchmode -executeMethod ZS.EditorTools.Smoke.Run
    public static class Setup
    {
        public const string TrackArt = "Assets/ResForAssetBundles/Scenes/Preview/LevelScene/Level_11CityNew_Art.unity";
        public const string RaceScene = "Assets/ZS/Scenes/Race_11CityNew.unity";
        public const string LobbyScene = "Assets/ZS/Scenes/Lobby.unity", PodiumScene = "Assets/ZS/Scenes/Podium.unity";
        const string StateScenes = "Assets/ResForAssetBundles/Scenes/StateScene/";
        // The six karts whose full model (HNCar prefab) ships in the APK; the rest download after install.
        public static readonly string[] Karts = { "00004", "00006", "00297", "00016", "00175", "00284" };

        [MenuItem("ZS/Build scenes")]
        public static void BuildScenes()
        {
            Directory.CreateDirectory("Assets/ZS/Scenes");
            var scene = EditorSceneManager.OpenScene(TrackArt, OpenSceneMode.Single);
            int colliders = AddTrackColliders(scene);
            var race = new GameObject("Race").AddComponent<Race>();
            race.KartVisuals = KartPrefabs();
            var camGo = new GameObject("ZS Camera");
            camGo.AddComponent<Camera>().farClipPlane = 3000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ChaseCamera>();
            race.gameObject.AddComponent<RaceHud>();
            EditorSceneManager.SaveScene(scene, RaceScene, true);
            Debug.Log($"[zs] race scene {RaceScene}: {colliders} mesh colliders added");
            BuildLobby();
            BuildPodium();
            EditorBuildSettings.scenes = new[] { LobbyScene, RaceScene, PodiumScene }.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }

        static GameObject[] KartPrefabs() => Karts.Select(id => AssetDatabase.FindAssets($"HNCar_{id} t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith($"/HNCar_{id}.prefab")))
            .Where(p => p != null).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();

        static Transform Find(Scene scene, string path)
        {
            var parts = path.Split('/');
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == parts[0])
                {
                    var t = parts.Length == 1 ? root.transform : root.transform.Find(string.Join("/", parts.Skip(1)));
                    if (t != null) return t;
                }
            return null;
        }

        // Lobby_L_Art (the garage set) plus Lobby_L (camera and kart anchors), merged into one scene.
        public static void BuildLobby()
        {
            var art = EditorSceneManager.OpenScene(StateScenes + "Lobby_L_Art.unity", OpenSceneMode.Single);
            var anchors = EditorSceneManager.OpenScene(StateScenes + "Lobby_L.unity", OpenSceneMode.Additive);
            var menu = new GameObject("ZS Lobby").AddComponent<LobbyMenu>();
            menu.VehicleParent = Find(anchors, "Character/LobbyModelGroup/VehicleParent");
            menu.CameraPos = Find(anchors, "CameraPos/Pos_Lobby");
            EditorSceneManager.MergeScenes(anchors, art);
            var prefabs = KartPrefabs();
            menu.Karts = prefabs;
            menu.KartIds = Karts.Where(id => prefabs.Any(p => p.name == "HNCar_" + id)).ToArray();
            menu.Icons = menu.KartIds.Select(id => AssetDatabase.FindAssets($"NCar_{id}_Splited t:Texture2D")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault()).ToArray();
            EnsureCamera();
            EditorSceneManager.SaveScene(art, LobbyScene, true);
            Debug.Log($"[zs] lobby scene {LobbyScene}: {prefabs.Length} karts, {menu.Icons.Count(i => i != null)} icons, " +
                      $"vehicle anchor {(menu.VehicleParent != null)}, camera anchor {(menu.CameraPos != null)}");
        }

        public static void BuildPodium()
        {
            var scene = EditorSceneManager.OpenScene(StateScenes + "Podium.unity", OpenSceneMode.Single);
            var view = new GameObject("ZS Podium").AddComponent<PodiumView>();
            view.Karts = KartPrefabs();
            view.Ranks = Enumerable.Range(1, 3).Select(i => Find(scene, $"PodiumRoot/Pos/Single/Rank{i}")).ToArray();
            view.View = Find(scene, "PodiumRoot/CameraPathSingle_1");
            EnsureCamera();
            EditorSceneManager.SaveScene(scene, PodiumScene, true);
            Debug.Log($"[zs] podium scene {PodiumScene}: {view.Ranks.Count(r => r != null)}/3 rank anchors, camera path {(view.View != null)}");
        }

        static void EnsureCamera()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var main = cams.FirstOrDefault(c => c.CompareTag("MainCamera") && c.enabled) ?? cams.FirstOrDefault(c => c.enabled);
            if (main == null)
            {
                main = new GameObject("ZS Camera").AddComponent<Camera>();
                main.gameObject.AddComponent<AudioListener>();
            }
            main.tag = "MainCamera";
            main.farClipPlane = Mathf.Max(main.farClipPlane, 2000f);
        }

        // The track's own collision is Havok data the client cannot read back; the visual meshes
        // stand in for it.
        public static int AddTrackColliders(Scene scene)
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null || mf.GetComponent<MeshRenderer>() == null) continue;
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    n++;
                }
            return n;
        }
    }

    public static class Smoke
    {
        public static void Run()
        {
            int code = 1;
            try
            {
                if (!File.Exists(Setup.RaceScene) || !File.Exists(Setup.LobbyScene) || !File.Exists(Setup.PodiumScene)) Setup.BuildScenes();
                foreach (var check in new[] { Setup.LobbyScene, Setup.PodiumScene })
                {
                    EditorSceneManager.OpenScene(check, OpenSceneMode.Single);
                    var lobby = Object.FindAnyObjectByType<LobbyMenu>();
                    var podium = Object.FindAnyObjectByType<PodiumView>();
                    if (lobby != null && (lobby.VehicleParent == null || lobby.Karts.Length == 0)) throw new System.Exception("lobby: kart anchor or karts missing");
                    if (podium != null && podium.Ranks.Any(r => r == null)) throw new System.Exception("podium: rank anchor missing");
                    if (lobby == null && podium == null) throw new System.Exception(check + ": menu component missing");
                }
                EditorSceneManager.OpenScene(Setup.RaceScene, OpenSceneMode.Single);
                var race = Object.FindAnyObjectByType<Race>();
                race.Setup();
                race.Player.gameObject.AddComponent<Bot>().Leader = race.Player;  // autopilot for the player
                const float dt = 0.02f;
                var log = new List<string>();
                for (float t = 0; t < 900f && race.State != Race.Phase.Finished; t += dt)
                {
                    race.Tick(dt);
                    if (race.State == Race.Phase.Countdown) continue;
                    foreach (var k in race.Karts)
                    {
                        k.GetComponent<Bot>().Think();
                        k.Step(dt);
                    }
                    Physics.SyncTransforms();
                    if (Mathf.Repeat(t, 10f) < dt)
                        log.Add($"t={t:F0}s " + string.Join(" ", race.Karts.Select(k => $"{k.name}:lap{k.Lap} {k.Progress:F0}m {k.Speed * 3.6f:F0}kmh")));
                }
                var standings = race.Standings();
                foreach (var line in log) Debug.Log("[zs] " + line);
                Debug.Log("[zs] result " + string.Join(", ", standings.Select((k, i) => $"{i + 1}. {k.name} {(k.FinishTime >= 0 ? k.FinishTime.ToString("F1") + "s" : "DNF")}")));
                Debug.Log($"[zs] podium results {Session.Results.Count}: " + string.Join(", ", Session.Results.Select(r => $"{r.Name}/kart{r.Kart}")));
                code = !race.Karts.All(k => k.FinishTime >= 0) ? 2 : Session.Results.Count != race.Karts.Count ? 3 : 0;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }
    }
}
