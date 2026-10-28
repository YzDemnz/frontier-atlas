using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private const float WheelRadius=168,WheelLabelRadius=122,WheelDeadZone=58;
        private Texture2D _wheelTexture,_wheelFocusTexture;private int _wheelKey=int.MinValue,_wheelFocusCount;
        private float _wheelAmount,_wheelAngle,_wheelFocusAmount;private int _wheelFrame=-1,_wheelLogicalSelection=-1,_wheelDrawCell=-1;private bool _wheelWasOpen;
        private Vector2 _wheelDrawCenter;
        private readonly string[] _basicWheelLabels={"Atacar","Invadir","Retirar","Información","Enfocar"};
        private readonly string[] _campaignWheelLabels={"Atacar","Construir","Invadir","Alianza","Donar","Nuclear","Retirar","Información","Enfocar","Arsenal","Guerra"};
        private readonly string[] _quickWheelLabels={"Atacar","Construir","Invadir","Alianza","Donar","Nuclear","Retirar","Información","Enfocar","Arsenal"};
        private string[] _wheelLabels=>G.IsQuickMatch?_quickWheelLabels:G.CampaignEnabled?_campaignWheelLabels:_basicWheelLabels;
        public bool RadialAnimating=>_wheelAmount>.01f;
        [System.NonSerialized] public Vector2? AuditWheelPointer;
        public Vector2 WheelPointForAudit(int index)
        {float angle=index*(360f/_wheelLabels.Length)*Mathf.Deg2Rad;Vector2 point=(RadialCenter+new Vector2(Mathf.Sin(angle),-Mathf.Cos(angle))*WheelLabelRadius)*Scale;return new Vector2(point.x,Screen.height-point.y);}
        public int WheelSelection(Vector2 screenPosition)
        {
            Vector2 mouse=new Vector2(screenPosition.x,Screen.height-screenPosition.y)/Scale-RadialCenter;
            if(mouse.magnitude<WheelDeadZone||mouse.magnitude>WheelRadius+12)return -1;
            float angle=Mathf.Repeat(Mathf.Atan2(mouse.x,-mouse.y)*Mathf.Rad2Deg,360),step=360f/_wheelLabels.Length;
            int candidate=Mathf.FloorToInt((angle+step*.5f)/step)%_wheelLabels.Length;
            if(_p.RadialOpen&&_wheelLogicalSelection>=0&&Mathf.Abs(Mathf.DeltaAngle(angle,_wheelLogicalSelection*step))<step*.5f+2)return _wheelLogicalSelection;
            return candidate;
        }
        private bool WheelEnabled(int i)
        {
            int cell=_p.RadialOpen?_p.RadialCell:_wheelDrawCell;if(cell<0||cell>=G.Owners.Length)return false;int f=G.PlayerFaction,owner=G.Owners[cell];
            if(G.CampaignEnabled)
            {
                switch(i)
                {
                    case 0:return G.IsBorderTarget(f,cell)&&G.CanAttackNation(f,owner)&&_p.PreviewTroops>=(owner<0?G.NeutralSectorCost:G.OccupationSectorCost)*G.TerrainResistance(cell);
                    case 1:return owner==f;
                    case 2:return owner!=f&&CanEmbark&&_p.PreviewTroops>=G.NeutralSectorCost&&(owner<0||G.CanAttackNation(f,owner));
                    case 3:return owner>=0&&owner!=f&&!G.IsTribe(owner)&&!G.AllianceUnavailable(f,owner);
                    case 10:return owner>=0&&(G.IsDeclaredWar(f,owner)||G.CanDeclareWar(f,owner));
                    case 4:return owner>=0&&G.AreAllied(f,owner);
                    case 5:return _p.World.Land[cell]&&owner!=f;
                    case 6:return G.HasOrder(f);
                    default:return true;
                }
            }
            return i==0?G.IsBorderTarget(f,cell)&&_p.PreviewTroops>=(owner<0?G.NeutralSectorCost:G.OccupationSectorCost):i==1?owner!=f&&CanEmbark&&(G.FleetActive[f]||_p.PreviewTroops>=G.NeutralSectorCost):i==2?G.HasOrder(f):true;
        }
        public void ConfirmWheel(Vector2 screenPosition)
        {
            if(!_p.RadialOpen)return;int i=WheelSelection(screenPosition),cell=_p.RadialCell;
            if(i<0){_p.CloseRadial();return;}if(!WheelEnabled(i))return;_p.CloseRadial();
            if(G.CampaignEnabled)
            {
                switch(i)
                {
                    case 0:_p.SendAttack(cell);break;
                    case 1:_p.OpenCampaignPanel(CampaignPanel.Construction,cell);break;
                    case 2:_p.OpenCampaignPanel(CampaignPanel.Navy,cell);break;
                    case 3:ShowSimpleAlliance(cell);break;
                    case 4:_p.OpenCampaignPanel(CampaignPanel.Diplomacy,cell);break;
                    case 5:_p.OpenCampaignPanel(CampaignPanel.Nuclear,cell);break;
                    case 6:_p.StopOrder();break;
                    case 7:_p.OpenCampaignPanel(CampaignPanel.Information,cell);break;
                    case 8:_p.FocusCell(cell);break;
                    case 9:_p.OpenCampaignPanel(CampaignPanel.Arsenal,cell);break;
                    case 10:ShowWarActions(cell);break;
                }
                return;
            }
            switch(i){case 0:_p.SendAttack(cell);break;case 1:_p.SendBoat(cell);break;case 2:_p.StopOrder();break;case 3:_p.Inspect(cell);break;case 4:_p.FocusCell(cell);break;}
        }
        private void PrepareWheelTextures(int mask)
        {
            const int resolution=256;int count=_wheelLabels.Length,key=mask+count*65536;
            if(_wheelTexture==null)_wheelTexture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            if(_wheelFocusTexture==null)_wheelFocusTexture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            if(key==_wheelKey)return;_wheelKey=key;var pixels=new Color32[resolution*resolution];Color32[] focus=_wheelFocusCount==count?null:new Color32[pixels.Length];float step=360f/count;
            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)
            {
                Vector2 point=new Vector2(x+.5f-resolution*.5f,resolution*.5f-y-.5f)/(resolution*.5f);float r=point.magnitude;
                float angle=Mathf.Repeat(Mathf.Atan2(point.x,-point.y)*Mathf.Rad2Deg,360);int sector=Mathf.FloorToInt((angle+step*.5f)/step)%count;float edge=Mathf.Abs(Mathf.DeltaAngle(angle,sector*step));
                Color color=Color.clear;if(r<.33f)color=new Color(.016f,.029f,.038f,.98f);
                if(r>.365f&&r<.985f&&edge<step*.5f-1)
                {bool enabled=(mask&(1<<sector))!=0;color=enabled?new Color(.036f,.065f,.080f,.98f):new Color(.024f,.039f,.047f,.96f);if(r>.97f)color=enabled?new Color(.15f,.25f,.27f,.9f):new Color(.07f,.11f,.13f,.9f);color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.977f,.986f,r));}
                int index=y*resolution+x;pixels[index]=color;
                if(focus!=null){float a=Mathf.Abs(Mathf.DeltaAngle(angle,0));float alpha=r>.365f&&r<.985f?(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(step*.5f-2,step*.5f-1,a)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.97f,.985f,r))):0;focus[index]=new Color(1,1,1,alpha);}
            }
            _wheelTexture.SetPixels32(pixels);_wheelTexture.Apply(false,false);if(focus!=null){_wheelFocusTexture.SetPixels32(focus);_wheelFocusTexture.Apply(false,false);_wheelFocusCount=count;}
        }
        private void DrawRadial()
        {
            bool open=_p.RadialOpen;int selected=-1;
            if(open){_wheelDrawCell=_p.RadialCell;_wheelDrawCenter=RadialCenter;selected=WheelSelection(_p.IsPresentationAudit&&AuditWheelPointer.HasValue?AuditWheelPointer.Value:(Vector2)Input.mousePosition);}
            if(_wheelFrame!=Time.frameCount)
            {
                _wheelFrame=Time.frameCount;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);bool reduced=_p.Settings.ReducedMotion;
                if(open&&!_wheelWasOpen){_wheelLogicalSelection=-1;_wheelFocusAmount=0;}
                _wheelAmount=reduced?(open?1:0):Mathf.Lerp(_wheelAmount,open?1:0,1-Mathf.Exp(-(open?20:26)*dt));if(!open&&_wheelAmount<.01f)_wheelAmount=0;
                if(selected>=0){float target=selected*(360f/_wheelLabels.Length);_wheelAngle=_wheelLogicalSelection<0||reduced?target:Mathf.LerpAngle(_wheelAngle,target,1-Mathf.Exp(-22*dt));}
                _wheelFocusAmount=reduced?(selected>=0?1:0):Mathf.Lerp(_wheelFocusAmount,selected>=0?1:0,1-Mathf.Exp(-20*dt));_wheelLogicalSelection=selected;_wheelWasOpen=open;
            }
            if(_wheelAmount<=0||_wheelDrawCell<0)return;Vector2 center=_wheelDrawCenter;int mask=0;for(int i=0;i<_wheelLabels.Length;i++)if(WheelEnabled(i))mask|=1<<i;PrepareWheelTextures(mask);
            Matrix4x4 matrix=GUI.matrix;Color saved=GUI.color;GUI.color=new Color(saved.r,saved.g,saved.b,saved.a*_wheelAmount);float animationScale=_p.Settings.ReducedMotion?1:Mathf.Lerp(.92f,1,_wheelAmount);GUIUtility.ScaleAroundPivot(Vector2.one*animationScale,center);
            Rect bounds=new Rect(center.x-WheelRadius,center.y-WheelRadius,WheelRadius*2,WheelRadius*2);GUI.DrawTexture(bounds,_wheelTexture);
            if(_wheelFocusAmount>.005f){Matrix4x4 beforeRotation=GUI.matrix;GUIUtility.RotateAroundPivot(_wheelAngle,center);GUI.color=new Color(.17f,.49f,.46f,_wheelAmount*_wheelFocusAmount*.76f);GUI.DrawTexture(bounds,_wheelFocusTexture);GUI.matrix=beforeRotation;GUI.color=new Color(1,1,1,_wheelAmount);}
            for(int i=0;i<_wheelLabels.Length;i++)
            {
                float angle=i*(360f/_wheelLabels.Length)*Mathf.Deg2Rad;Vector2 point=center+new Vector2(Mathf.Sin(angle),-Mathf.Cos(angle))*WheelLabelRadius;
                bool enabled=WheelEnabled(i);Color colour=enabled?(i==selected?new Color(.98f,.99f,.99f):FrontierUI.White):new Color(.61f,.67f,.69f);
                _ui.Label(new Rect(point.x-44,point.y-18,88,36),_wheelLabels[i],new GUIStyle(i==selected?_ui.Caption:_ui.Body){alignment=TextAnchor.MiddleCenter,fontSize=12},colour);
            }
            string title=selected>=0?_wheelLabels[selected]:"ACCIONES";
            _ui.Label(new Rect(center.x-55,center.y-24,110,30),title,new GUIStyle(_ui.Heading){alignment=TextAnchor.MiddleCenter,fontSize=14},FrontierUI.White);
            _ui.Label(new Rect(center.x-51,center.y+9,102,34),selected<0?"Centro: cancelar":WheelEnabled(selected)?"Suelta para elegir":"No disponible",new GUIStyle(_ui.Small){alignment=TextAnchor.MiddleCenter,fontSize=11});
            GUI.matrix=matrix;GUI.color=saved;
        }
    }
}
