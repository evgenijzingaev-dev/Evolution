using Evolution.Core;
using UnityEngine;
namespace Evolution.Runtime
{
    public sealed class MicroscopeCamera : MonoBehaviour
    {
        Camera lens;
        float world, distance, targetDistance, yaw, pitch = 65, targetYaw, targetPitch = 65;
        Vector3 focus, targetFocus;
        bool dragging;
        Vector3 previousPointer;
        public bool Following { get; private set; }
        public Vector3 FocusPoint => targetFocus;
        public Vector3 RenderedFocus => focus;
        public float Distance => targetDistance;
        float MaximumDistance => Mathf.Max(2.5f, Mathf.Min(world * .32f, world * .23f / (Mathf.Tan(24 * Mathf.Deg2Rad) * Mathf.Max(1, lens.aspect))));
        public Vector3 NearestImage(Vector3 point) => new Vector3(focus.x + Observation.PeriodicDelta(point.x - focus.x, world),
            point.y, focus.z + Observation.PeriodicDelta(point.z - focus.z, world));
        public void Initialize(Camera camera, float worldSize)
        {
            lens = camera; world = worldSize;
            lens.nearClipPlane = .08f; lens.farClipPlane = Mathf.Max(200, world * 5); lens.fieldOfView = 48;
            Overview(); focus = targetFocus; distance = targetDistance; yaw = targetYaw; pitch = targetPitch;
            ApplyPose();
        }
        public void Overview() { Following = false; targetFocus = new Vector3(world * .5f, .5f, world * .5f); targetDistance = MaximumDistance; targetYaw = 0; targetPitch = 65; }
        public void Zoom(float wheel) { targetDistance = Mathf.Clamp(targetDistance * Mathf.Exp(-wheel * .15f), 1.8f, MaximumDistance); }
        public void FocusOn(Vector3 point, bool follow, float frameDistance = 5) { targetFocus = Bound(point); targetDistance = Mathf.Clamp(frameDistance, 2.5f, MaximumDistance); Following = follow; }
        public void StopFollowing() { Following = false; }
        public void FollowPoint(Vector3 point) {
            if (!Following) return;
            targetFocus = Bound(point);
        }
        Vector3 Bound(Vector3 point) => new Vector3(targetFocus.x + Observation.PeriodicDelta(point.x-targetFocus.x,world),
            .54f,targetFocus.z + Observation.PeriodicDelta(point.z-targetFocus.z,world));
        public Vector3 ControlDirection(Vector2 input) => Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
        public void Tick(bool pointerOverUI, bool controlling = false)
        {
            if (!lens || !Application.isFocused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (!pointerOverUI) Zoom(Input.mouseScrollDelta.y);
            if (!controlling && Input.GetKeyDown(KeyCode.Home)) Overview();
            if (Input.GetKeyDown(KeyCode.Escape)) Following = false;
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1)) { dragging = !pointerOverUI; previousPointer = Input.mousePosition; }
            Vector3 mouseDelta = (Input.mousePosition - previousPointer) * (1080f / Mathf.Max(1, Screen.height)); previousPointer = Input.mousePosition;
            if (!Input.GetMouseButton(2) && !Input.GetMouseButton(1)) dragging = false;
            var rotation = Quaternion.Euler(0, targetYaw, 0);
            var move = Vector3.zero;
            if ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))) move.z++;
            if ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))) move.z--;
            if ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))) move.x++;
            if ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))) move.x--;
            if (!controlling && move.sqrMagnitude > 0) { Following = false; targetFocus += rotation * move.normalized * targetDistance * .65f * dt; }
            if (!controlling && dragging && Input.GetMouseButton(2)) {
                Following = false;
                targetFocus -= rotation * new Vector3(mouseDelta.x * .1f, 0, mouseDelta.y * .1f) * targetDistance * .025f;
            }
            if (dragging && Input.GetMouseButton(1)) {
                targetYaw += mouseDelta.x * .1f * 3;
                targetPitch = Mathf.Clamp(targetPitch - mouseDelta.y * .1f * 2, 40, 85);
            }
            float blend = 1 - Mathf.Exp(-12 * dt);
            focus = Vector3.Lerp(focus, targetFocus, blend); distance = Mathf.Lerp(distance, targetDistance, blend);
            yaw = Mathf.LerpAngle(yaw, targetYaw, blend); pitch = Mathf.Lerp(pitch, targetPitch, blend);
            ApplyPose();
        }
        void ApplyPose()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            lens.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
