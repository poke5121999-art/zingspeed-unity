using UnityEngine;

namespace ZS
{
    // Race readout: countdown, lap, position, speed, nitro slots, then the standings.
    // Text only for now; the original HUD sprites replace it once the UI prefabs are wired.
    [RequireComponent(typeof(Race))]
    public class RaceHud : MonoBehaviour
    {
        Race race;

        void Awake() => race = GetComponent<Race>();

        void Start()
        {
            var cam = FindAnyObjectByType<ChaseCamera>();
            if (cam != null) cam.Target = race.Player;
        }

        void OnGUI()
        {
            var p = race.Player;
            if (p == null) return;
            var big = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (race.State == Race.Phase.Countdown)
                GUI.Label(new Rect(Screen.width / 2 - 20, Screen.height / 3, 200, 60), Mathf.CeilToInt(race.CountdownSeconds - race.Clock).ToString(), big);
            var standings = race.Standings();
            GUI.Label(new Rect(20, 20, 400, 40), $"Vòng {Mathf.Clamp(p.Lap + 1, 1, race.Laps)}/{race.Laps}   Hạng {standings.IndexOf(p) + 1}/{race.Karts.Count}", big);
            GUI.Label(new Rect(20, Screen.height - 60, 400, 40), $"{p.Speed * 3.6f:F0} km/h   N2O {p.NitroSlots}  [{new string('|', (int)(p.NitroGauge / p.P.NitroFull * 10)),-10}]", big);
            if (race.State == Race.Phase.Finished)
                for (int i = 0; i < standings.Count; i++)
                {
                    var k = standings[i];
                    GUI.Label(new Rect(Screen.width / 2 - 150, 120 + 40 * i, 400, 40),
                        $"{i + 1}. {k.name}  {(k.FinishTime >= 0 ? k.FinishTime.ToString("F2") + " s" : "--")}", big);
                }
        }
    }
}
