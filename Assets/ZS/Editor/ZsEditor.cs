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

        [MenuItem("ZS/Build scenes")]
        public static void BuildScenes()
        {
            Directory.CreateDirectory("Assets/ZS/Scenes");
            var scene = EditorSceneManager.OpenScene(TrackArt, OpenSceneMode.Single);
            int colliders = AddTrackColliders(scene);
            var race = new GameObject("Race").AddComponent<Race>();
            var camGo = new GameObject("ZS Camera");
            camGo.AddComponent<Camera>().farClipPlane = 3000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ChaseCamera>();
            race.gameObject.AddComponent<RaceHud>();
            EditorSceneManager.SaveScene(scene, RaceScene, true);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RaceScene, true) };
            Debug.Log($"[zs] race scene {RaceScene}: {colliders} mesh colliders added");
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
                if (!File.Exists(Setup.RaceScene)) Setup.BuildScenes();
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
                code = race.Karts.All(k => k.FinishTime >= 0) ? 0 : 2;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }
    }
}
