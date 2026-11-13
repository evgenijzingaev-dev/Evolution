using UnityEngine;
namespace Evolution.Runtime
{
    public struct WaterView {public Vector3 Focus;public float Distance,Yaw,Pitch;}
    public sealed class WaterCamera : MonoBehaviour
    {
        Camera lens;Vector3 focus,target;float yaw,targetYaw,pitch=15,targetPitch=15,distance=7,targetDistance=7;
        bool strategic;
        public Vector3 Focus=>focus;public Vector3 Target=>target;public float Distance=>distance;
        public WaterView View=>new WaterView {Focus=target,Distance=targetDistance,Yaw=targetYaw,Pitch=targetPitch};
        public bool Navigating=>Application.isFocused&&(Input.GetMouseButton(2)||Input.mouseScrollDelta.y!=0||Input.GetAxisRaw("Horizontal")!=0||Input.GetAxisRaw("Vertical")!=0||Input.GetKey(KeyCode.Q)||Input.GetKey(KeyCode.E));
        public void Initialize(Camera camera) {lens=camera;lens.fieldOfView=55;lens.nearClipPlane=.12f;lens.farClipPlane=1200;Apply(true);}
        public void SetStrategic(bool value){strategic=value;}
        public void FocusOn(Vector3 point,float range=7) {target=point;targetDistance=Mathf.Clamp(range,2.2f,strategic?240:45);}
        public void Follow(Vector3 point) {target=point;}
        public void Restore(WaterView view){target=view.Focus;targetDistance=view.Distance;targetYaw=view.Yaw;targetPitch=view.Pitch;}
        public void Orbit(float horizontal,float vertical) {targetYaw+=horizontal;targetPitch=Mathf.Clamp(targetPitch+vertical,-85,85);}
        public void Zoom(float wheel) {targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-wheel*.15f),2.2f,strategic?240:45);}
        public void Tick(bool overUi,bool follow)
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(Application.isFocused&&!overUi) {
                if(Input.GetMouseButton(1))Orbit(Input.GetAxisRaw("Mouse X")*3,-Input.GetAxisRaw("Mouse Y")*3);
                Zoom(Input.mouseScrollDelta.y);
                if(Input.GetMouseButton(2))target+=(-lens.transform.right*Input.GetAxisRaw("Mouse X")-lens.transform.up*Input.GetAxisRaw("Mouse Y"))*distance*.035f;
                if(!follow) {
                    Vector3 move=lens.transform.right*Input.GetAxisRaw("Horizontal")+lens.transform.forward*Input.GetAxisRaw("Vertical");
                    if(Input.GetKey(KeyCode.E))move+=Vector3.up;if(Input.GetKey(KeyCode.Q))move-=Vector3.up;
                    float speed=strategic?Mathf.Clamp(distance*.4f,5,70):5;if(strategic&&Input.GetKey(KeyCode.LeftShift))speed*=3;
                    target+=Vector3.ClampMagnitude(move,1)*dt*speed;
                }
            }
            float blend=1-Mathf.Exp(-5*dt);
            // Bound focus displacement as well as camera displacement: streamed regions follow a continuous route.
            focus=Vector3.MoveTowards(focus,Vector3.Lerp(focus,target,blend),Mathf.Min(2.5f,dt*100));
            distance=Mathf.Lerp(distance,targetDistance,blend);yaw=Mathf.LerpAngle(yaw,targetYaw,blend);pitch=Mathf.Lerp(pitch,targetPitch,blend);Apply();
        }
        public void ApplyImmediate() {focus=target;yaw=targetYaw;pitch=targetPitch;distance=targetDistance;Apply(true);}
        void Apply(bool immediate=false)
        {
            var rotation=Quaternion.Euler(pitch,yaw,0);Vector3 desired=focus-rotation*Vector3.forward*distance;
            Vector3 position=immediate?desired:Vector3.MoveTowards(lens.transform.position,desired,2.5f);
            Vector3 direction=focus-position;lens.transform.SetPositionAndRotation(position,direction.sqrMagnitude>.00001f?Quaternion.LookRotation(direction,Vector3.up):rotation);
        }
    }
}
