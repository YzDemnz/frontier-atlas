using UnityEngine;

namespace FrontierAtlas
{
    public sealed class FrontierCamera : MonoBehaviour
    {
        public bool InputEnabled=true;public float Tilt=78;
        public float ZoomSpeed=1;
        private Camera _camera;
        private float _width,_height,_zoom;
        private Vector2 _position;
        private Vector3 _lastMouse;
        private bool _dragging;
        public bool ReducedMotion;
        public int NuclearShakes {get;private set;}
        public float ShakeOffset {get;private set;}
        private float _shakeEnd,_shakeStarted;
        private Vector3 _basePosition;
        public void NuclearShake(bool reducedMotion)
        {if(reducedMotion)return;NuclearShakes++;if(_shakeEnd<=Time.unscaledTime)_shakeStarted=Time.unscaledTime;_shakeEnd=Mathf.Min(_shakeStarted+2.4f,Time.unscaledTime+1.65f);}
        private void LateUpdate()
        {
            if(_camera==null)return;
            if(ReducedMotion)_shakeEnd=0;
            float envelope=Mathf.Clamp01((_shakeEnd-Time.unscaledTime)/1.65f);envelope*=envelope;
            float t=Time.unscaledTime*39;float dx=Mathf.Sin(t*1.17f)+.35f*Mathf.Sin(t*2.73f),dy=Mathf.Cos(t*.93f)+.35f*Mathf.Sin(t*3.13f);
            Vector3 offset=new Vector3(dx,0,dy)*(_camera.orthographicSize*.018f*envelope);ShakeOffset=offset.magnitude;
            transform.SetPositionAndRotation(_basePosition+offset,Quaternion.Euler(Tilt,0,Mathf.Sin(t*1.31f)*2.1f*envelope));
        }
        public void Initialize(Camera camera,float width,float height)
        {
            _camera=camera;_width=width;_height=height;
            transform.rotation=Quaternion.Euler(Tilt,0,0);ResetView();
        }
        public void ResetView()
        {
            _position=new Vector2(_width*.5f,_height*.5f);_zoom=_height*.58f;
            Apply(true);
        }
        public void Focus(Vector2 position,float zoom)
        {_position=position;_zoom=zoom;Apply(true);}
        public void Backdrop(float time,bool reducedMotion)
        {
            _position=new Vector2(_width*(.37f+(reducedMotion?0:Mathf.Sin(time*.04f)*.018f)),_height*.51f);
            _zoom=_height*.57f;Apply(true);
        }
        private void Apply(bool immediate=false)
        {
            _position.x=Mathf.Clamp(_position.x,-_width*.15f,_width*1.1f);
            _position.y=Mathf.Clamp(_position.y,0,_height);
            _camera.orthographicSize=immediate?_zoom:Mathf.Lerp(_camera.orthographicSize,_zoom,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
            _basePosition=new Vector3(_position.x,350,_position.y-350/Mathf.Tan(Tilt*Mathf.Deg2Rad));transform.SetPositionAndRotation(_basePosition,Quaternion.Euler(Tilt,0,0));
        }
        private void Update()
        {
            if(_camera==null)return;
            if(InputEnabled)
            {
                float wheel=Input.mouseScrollDelta.y;
                if(Mathf.Abs(wheel)>.01f)_zoom=Mathf.Clamp(_zoom*Mathf.Pow(.85f,wheel*ZoomSpeed),24,_height*.66f);
                float speed=_camera.orthographicSize*1.1f*Time.unscaledDeltaTime;
                if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))_position.x-=speed;
                if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))_position.x+=speed;
                if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))_position.y+=speed;
                if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))_position.y-=speed;
                if(Input.GetMouseButtonDown(2)){_dragging=true;_lastMouse=Input.mousePosition;}
                if(Input.GetMouseButtonUp(2))_dragging=false;
                if(_dragging)
                {
                    Vector3 delta=Input.mousePosition-_lastMouse;
                    _position-=new Vector2(delta.x,delta.y)*(_camera.orthographicSize*2/Mathf.Max(1,Screen.height));
                    _lastMouse=Input.mousePosition;
                }
            }
            else _dragging=false;
            Apply();
        }
    }
}
