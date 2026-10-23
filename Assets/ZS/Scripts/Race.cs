using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZS
{
    // One race: a grid behind the start line, a 3-2-1 countdown, laps counted along the race line,
    // positions by distance, results when the player finishes. The original runs this on the server
    // [reconstructed]; the data it uses (start line, laps, race line) comes from the client.
    public class Race : MonoBehaviour
    {
        public enum Phase { Countdown, Racing, Finished }

        public string Env = "e_11citynew";
        public int Laps = 2;                  // 2_StandardData ghost crosses the start line 3 times
        public GameObject[] KartVisuals;      // original kart prefabs, player first
        public int Bots = 5;
        public float CountdownSeconds = 3f;

        public Phase State { get; private set; }
        public float Clock { get; private set; }
        public readonly List<Kart> Karts = new List<Kart>();
        public Kart Player => Karts.Count > 0 ? Karts[0] : null;
        public System.Action<Phase> OnPhase;

        Track track;

        void Start() => Setup();

        public void Setup()
        {
            Env = Session.Track;
            track = Track.Load(Env, Laps);
            var right = Vector3.Cross(Vector3.up, track.StartDir).normalized;
            for (int i = 0; i <= Bots; i++)
            {
                int row = i / 2, col = i % 2;
                var pos = track.StartPos - track.StartDir * (6f + row * 7f) + right * (col == 0 ? -3f : 3f);
                var go = new GameObject(i == 0 ? "Player" : $"Bot{i}");
                go.transform.SetPositionAndRotation(pos + Vector3.up, Quaternion.LookRotation(track.StartDir));
                int visual = -1;
                if (KartVisuals != null && KartVisuals.Length > 0)
                {
                    // The player drives the lobby's pick; bots take the other karts in order.
                    int pick = Mathf.Clamp(Session.Kart, 0, KartVisuals.Length - 1);
                    visual = i == 0 ? pick : (pick + i) % KartVisuals.Length;
                    Instantiate(KartVisuals[visual], go.transform, false);
                }
                var k = go.AddComponent<Kart>();
                k.P = KartParams.Load();
                k.Track = track;
                k.IsPlayer = i == 0;
                k.Visual = visual;
                k.Gravity = track.Gravity;
                k.Progress = k.LastProgress = track.Progress(go.transform.position, ref k.TrackHint);
                k.Lap = k.Progress < track.Length * 0.5f ? 0 : -1;  // a grid slot past the line already is on lap 0
                Karts.Add(k);
                if (i > 0)
                {
                    var b = go.AddComponent<Bot>();
                    b.Lane = (i % 2 == 0 ? 1 : -1) * (0.2f + 0.15f * row);
                    b.Skill = 0.9f + 0.02f * i;
                }
            }
            foreach (var k in Karts)
                if (k.TryGetComponent<Bot>(out var b)) b.Leader = Player;
            SetPhase(Phase.Countdown);
        }

        void SetPhase(Phase p)
        {
            State = p;
            Clock = 0;
            foreach (var k in Karts) k.enabled = p != Phase.Countdown;
            if (p == Phase.Countdown) ZsAudio.Play(ZsAudio.Countdown);
            if (p == Phase.Finished)
            {
                Session.Results.Clear();
                foreach (var k in Standings())
                    Session.Results.Add(new Session.Result { Name = k.name, Kart = Mathf.Max(0, k.Visual), Time = k.FinishTime });
                if (Application.isPlaying) Invoke(nameof(ToPodium), 4f);
                ZsAudio.Play(Standings().IndexOf(Player) == 0 ? ZsAudio.Win : ZsAudio.Lose);
                ZsAudio.Play(ZsAudio.Settle, 0.8f);
            }
            OnPhase?.Invoke(p);
        }

        void Update() => Tick(Time.deltaTime);

        void ToPodium() => UnityEngine.SceneManagement.SceneManager.LoadScene(Scenes.Podium);

        public void Tick(float dt)
        {
            Clock += dt;
            if (State == Phase.Countdown && Clock >= CountdownSeconds) SetPhase(Phase.Racing);
            if (State != Phase.Racing) return;
            if (Player.GetComponent<Bot>() == null) Player.In = PlayerInput.Read();
            foreach (var k in Karts)
                if (k.FinishTime < 0 && k.Lap >= Laps) k.FinishTime = Clock;
            if (Player.FinishTime >= 0 && (Karts.All(k => k.FinishTime >= 0) || Clock > Player.FinishTime + 15f))
                SetPhase(Phase.Finished);
        }

        // Finished karts by time, then the rest by distance covered.
        public List<Kart> Standings() =>
            Karts.OrderBy(k => k.FinishTime < 0 ? 1 : 0).ThenBy(k => k.FinishTime).ThenByDescending(k => k.RaceDistance).ToList();
    }

    public static class PlayerInput
    {
        public static KartInput Read() => new KartInput
        {
            // The mobile game accelerates on its own; the only pedal is the brake [reconstructed from its HUD].
            Throttle = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? -1f : 1f,
            Steer = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1f : 0f),
            Drift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.Space),
            Nitro = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.E),
        };
    }
}
