using UnityEngine;
namespace Evolution.Runtime
{
    public sealed class WaterCamera : MonoBehaviour
    {
        Camera lens;Vector3 focus,target;float yaw,targetYaw,pitch=15,targetPitch=15,distance=7,targetDistance=7;
        public Vector3 Focus=>focus;
        public float Distance=>distance;
        public void Initialize(Camera camera) {lens=camera;lens.fieldOfView=55;lens.nearClipPlane=.12f;lens.farClipPlane=220;Apply(true);}
        public void FocusOn(Vector3 point,float range=7) {target=point;targetDistance=Mathf.Clamp(range,2.2f,45);}
        public void Follow(Vector3 point) {target=point;}
        public void Orbit(float horizontal,float vertical) {targetYaw+=horizontal;targetPitch=Mathf.Clamp(targetPitch+vertical,-85,85);}
        public void Zoom(float wheel) {targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-wheel*.15f),2.2f,45);}
        public void Tick(bool overUi,bool follow)
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(Application.isFocused&&!overUi) {
                if(Input.GetMouseButton(1)) Orbit(Input.GetAxisRaw("Mouse X")*3,-Input.GetAxisRaw("Mouse Y")*3);
                Zoom(Input.mouseScrollDelta.y);
                if(!follow) {
                    Vector3 move=lens.transform.right*Input.GetAxisRaw("Horizontal")+lens.transform.forward*Input.GetAxisRaw("Vertical");
                    if(Input.GetKey(KeyCode.E)) move+=Vector3.up;if(Input.GetKey(KeyCode.Q)) move-=Vector3.up;
                    target+=Vector3.ClampMagnitude(move,1)*dt*5;
                }
            }
            float blend=1-Mathf.Exp(-8*dt);focus=Vector3.Lerp(focus,target,blend);distance=Mathf.Lerp(distance,targetDistance,blend);
            yaw=Mathf.LerpAngle(yaw,targetYaw,blend);pitch=Mathf.Lerp(pitch,targetPitch,blend);Apply();
        }
        public void ApplyImmediate() {focus=target;yaw=targetYaw;pitch=targetPitch;distance=targetDistance;Apply(true);}
        void Apply(bool immediate=false)
        {
            var rotation=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-rotation*Vector3.forward*distance;
            // The visibility guard is 40 units; fog is opaque by 36. Keep each camera step inside that margin.
            Vector3 position=immediate?desired:Vector3.MoveTowards(lens.transform.position,desired,2.5f);
            Vector3 direction=focus-position;lens.transform.SetPositionAndRotation(position,direction.sqrMagnitude>.00001f?Quaternion.LookRotation(direction,Vector3.up):rotation);
        }
    }
}
