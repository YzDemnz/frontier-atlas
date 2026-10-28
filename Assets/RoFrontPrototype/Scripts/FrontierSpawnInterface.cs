using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private void DrawSpawnSelection()
        {
            if(_p.Settings.Labels)DrawLabels();
            FrontierUI.Panel(new Rect(20,18,W-40,83));
            Label(42,30,W-84,"ELIGE DÓNDE FUNDAR "+_p.PlayerName.ToUpperInvariant(),_ui.Heading,35);
            Label(42,68,W-84,"Haz clic en tierra libre. Acerca el mapa con la rueda y muévelo con WASD o el botón central.",_ui.Body,28);
            int cell=_p.SelectedSpawnCell;
            if(cell>=0)
            {
                Vector2 pos=Project(cell);_ui.Dot(pos,15,FrontierUI.Accent);
                _ui.Label(new Rect(pos.x-120,pos.y+15,240,30),"TU CAPITAL",new GUIStyle(_ui.Caption){alignment=TextAnchor.MiddleCenter},FrontierUI.White);
            }
            Rect bar=new Rect((W-850)*.5f,H-112,850,88);FrontierUI.Panel(bar);
            Label(bar.x+22,bar.y+15,390,cell<0?"Selecciona tu posición en el mapa":"Posición elegida. Tu frontera nace aquí.",_ui.Body,50);
            if(_ui.Button(new Rect(bar.x+440,bar.y+20,235,45),G.IsQuickMatch&&_p.QuickDeploymentRemaining>0?"DESPLIEGUE · "+Mathf.CeilToInt(_p.QuickDeploymentRemaining)+" s":"FUNDAR NACIÓN",true,cell>=0))_p.ConfirmFoundation();
            if(_ui.Button(new Rect(bar.x+689,bar.y+20,139,45),"VOLVER"))_p.ShowMainMenu();
            if(_p.NoticeExpiry>Time.unscaledTime){FrontierUI.Panel(new Rect((W-760)*.5f,H-172,760,43));Label((W-760)*.5f+16,H-162,728,_p.Notice,_ui.Body,30);}
        }
    }
}
