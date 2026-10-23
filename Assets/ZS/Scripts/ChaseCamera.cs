using UnityEngine;

namespace ZS
{
    // Third-person camera behind the player's kart; widens the field of view under boost.
    public class ChaseCamera : MonoBehaviour
    {
        public Kart Target;
        public float Distance = 6.5f, Height = 2.4f, BaseFov = 60f, BoostFov = 72f;
        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (Target == null) return;
            var t = Target.transform;
            Vector3 want = t.position - t.forward * Distance + t.up * Height;
            transform.position = Vector3.Lerp(transform.position, want, 8f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(t.position + t.up * 1.2f - transform.position, t.up), 10f * Time.deltaTime);
            if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Target.BoostTime > 0 ? BoostFov : BaseFov, 4f * Time.deltaTime);
        }
    }
}
