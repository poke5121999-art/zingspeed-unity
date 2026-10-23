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
            int colliders = AddHavokColliders("e_11citynew", "E_11CityNew");
            if (colliders == 0) colliders = AddTrackColliders(scene);
            int cleared = ClearRaceLine(Track.Load("e_11citynew", 1));
            var race = new GameObject("Race").AddComponent<Race>();
            race.KartVisuals = KartPrefabs();
            var camGo = new GameObject("ZS Camera");
            camGo.AddComponent<Camera>().farClipPlane = 3000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ChaseCamera>();
            race.gameObject.AddComponent<RaceHud>();
            EditorSceneManager.SaveScene(scene, RaceScene, true);
            Debug.Log($"[zs] race scene {RaceScene}: {colliders} mesh colliders added, {cleared} wall triangles across the race line removed from colliders");
            BuildLobby();
            BuildPodium();
            EditorBuildSettings.scenes = new[] { LobbyScene, RaceScene, PodiumScene }.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }

        // The visual meshes include barriers the original's Havok collision (Pick_Wall) does not have on
        // this route, some standing across the race line. The race line must be drivable, so wall-like
        // triangles hit by rays along it (three lanes, kart height) leave the collider mesh; the drawn
        // mesh is untouched.
        public static int ClearRaceLine(Track track)
        {
            Physics.SyncTransforms();
            var hits = new Dictionary<MeshCollider, List<Vector3>>();
            int n = track.Pos.Count;
            for (int i = 0; i < n; i++)
            {
                var a = track.Pos[i];
                var b = track.Pos[(i + 1) % n];
                var right = Vector3.Cross(Vector3.up, track.Dir[i]).normalized;
                foreach (float lane in new[] { -0.6f, 0f, 0.6f })
                {
                    var off = right * lane * (lane < 0 ? track.LeftWidth[i] : track.RightWidth[i]) + Vector3.up;
                    var d = b - a;
                    if (d.magnitude > 60f) continue;  // the closing jump of the loop
                    foreach (var h in Physics.RaycastAll(a + off, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider is MeshCollider mc && Mathf.Abs(Vector3.Dot(h.normal, Vector3.up)) < 0.5f)
                        {
                            if (!hits.TryGetValue(mc, out var l)) hits[mc] = l = new List<Vector3>();
                            l.Add(h.point);
                        }
                }
            }
            int removed = 0;
            foreach (var kv in hits)
            {
                var mc = kv.Key;
                var mesh = mc.sharedMesh;
                var v = mesh.vertices;
                var t = mesh.triangles;
                var w = mc.transform.localToWorldMatrix;
                var keep = new List<int>(t.Length);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 p0 = w.MultiplyPoint3x4(v[t[k]]), p1 = w.MultiplyPoint3x4(v[t[k + 1]]), p2 = w.MultiplyPoint3x4(v[t[k + 2]]);
                    var c = (p0 + p1 + p2) / 3f;
                    var nrm = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                    bool wall = Mathf.Abs(Vector3.Dot(nrm, Vector3.up)) < 0.5f;
                    bool near = kv.Value.Any(h => new Vector2(h.x - c.x, h.z - c.z).sqrMagnitude < 9f && Mathf.Abs(h.y - c.y) < 3f);
                    if (wall && near) { removed++; continue; }
                    keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]);
                }
                var copy = Object.Instantiate(mesh);
                copy.name = mesh.name + "_drivable";
                copy.triangles = keep.ToArray();
                Directory.CreateDirectory("Assets/ZS/Generated");
                AssetDatabase.CreateAsset(copy, $"Assets/ZS/Generated/{mc.name}_{mc.GetInstanceID() & 0xffff:x}_drivable.asset");
                mc.sharedMesh = copy;
                Debug.Log($"[zs] race line blocked by {mc.name} at {kv.Value.Count} rays; collider made drivable");
            }
            return removed;
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

        // The track's own collision: Havok meshes (Pick_Track ground, Pick_Wall walls) decoded by the kit
        // to OBJ text (tools/rebuild.sh). Shape space is Unity's world space, no axis flip.
        public static int AddHavokColliders(string env, string prefix)
        {
            int n = 0;
            Directory.CreateDirectory("Assets/ZS/Generated");
            foreach (var part in new[] { "Track", "Wall" })
            {
                var text = Resources.Load<TextAsset>($"ZSData/{env}/{prefix}_Pick_{part}.obj");
                if (text == null) continue;
                var v = new List<Vector3>();
                var t = new List<int>();
                foreach (var line in text.text.Split('\n'))
                {
                    var f = line.Split(' ');
                    if (f[0] == "v") v.Add(new Vector3(float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture)));
                    else if (f[0] == "f") { t.Add(int.Parse(f[1]) - 1); t.Add(int.Parse(f[2]) - 1); t.Add(int.Parse(f[3]) - 1); }
                }
                var mesh = new Mesh { name = $"{prefix}_Pick_{part}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh, $"Assets/ZS/Generated/{prefix}_Pick_{part}.asset");
                var go = new GameObject($"Havok {part}");
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                n++;
                Debug.Log($"[zs] havok {part}: {v.Count} vertices, {t.Count / 3} triangles");
            }
            return n;
        }

        // Fallback without decoded Havok data: the visual meshes stand in for the collision.
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
                var history = race.Karts.ToDictionary(k => k, k => new Queue<float>());
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
                    if (Mathf.Repeat(t, 5f) < dt)
                        foreach (var k in race.Karts)
                        {
                            history[k].Enqueue(k.RaceDistance);
                            if (history[k].Count > 5) history[k].Dequeue();  // oldest is ~20-25 s ago
                        }
                    if (Mathf.Repeat(t, 10f) < dt)
                        log.Add($"t={t:F0}s " + string.Join(" ", race.Karts.Select(k => $"{k.name}:lap{k.Lap} {k.Progress:F0}m {k.Speed * 3.6f:F0}kmh" +
                            (k.Speed < 3f ? $" stuck@{k.transform.position:F0} wall={k.LastWall} grounded={k.Grounded}" : ""))));
                }
                var standings = race.Standings();
                foreach (var line in log) Debug.Log("[zs] " + line);
                Debug.Log("[zs] result " + string.Join(", ", standings.Select((k, i) => $"{i + 1}. {k.name} {(k.FinishTime >= 0 ? k.FinishTime.ToString("F1") + "s" : "DNF")}")));
                Debug.Log($"[zs] podium results {Session.Results.Count}: " + string.Join(", ", Session.Results.Select(r => $"{r.Name}/kart{r.Kart}")));
                // A kart may miss the line only through the end-of-race timer, still driving: it must have
                // covered 200 m in its last 20 s.
                bool stuck = race.Karts.Any(k => k.FinishTime < 0 && k.RaceDistance - history[k].Peek() < 200f);
                code = race.State != Race.Phase.Finished || stuck ? 2 : Session.Results.Count != race.Karts.Count ? 3 : 0;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }
    }
}
