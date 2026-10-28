using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private int _warNation=-1;private Vector2 _warAnchor;
        public bool WarActionsOpen=>_warNation>=0;
        private Rect WarActionsBounds=>new Rect(Mathf.Clamp(_warAnchor.x-205,24,W-434),Mathf.Clamp(_warAnchor.y-100,130,Dock.y-358),410,344);
        public void CloseWarActions()=>_warNation=-1;
        public void ShowWarActions(int cell)
        {int other=cell>=0&&cell<G.Owners.Length?G.Owners[cell]:-1;if(other<0||other==G.PlayerFaction||G.IsTribe(other))return;_warNation=other;_warAnchor=RadialCenter;CloseSimpleAlliance();WorldBoardOpen=false;_p.CloseRadial();_p.CloseCampaignPanel();}
        private void DrawWarActions()
        {
            if(!WarActionsOpen)return;int f=G.PlayerFaction,other=_warNation;if(G.TerritoryCounts[other]==0){CloseWarActions();return;}
            Rect r=WarActionsBounds;FrontierUI.Rounded(r,new Color(.034f,.042f,.052f,.99f),10);Label(r.x+18,r.y+14,330,G.IsDeclaredWar(f,other)?"GUERRA Y TREGUA":"DECLARAR LA GUERRA",_ui.Heading,30);
            if(_ui.Button(new Rect(r.xMax-43,r.y+12,28,28),"×")){CloseWarActions();return;}
            Label(r.x+18,r.y+51,370,G.Names[other],_ui.Body,28,FrontierUI.Accent);
            string detail=G.IsDeclaredWar(f,other)?"En guerra · el comercio entre ambos está cerrado.":G.WarDeclarationBlock(f,other);
            if(detail.Length==0)detail=G.Neighbors(f,other)?"Frontera común. Declarar la guerra cierra el comercio.":"Invitación militar válida contra este enemigo común.";
            Label(r.x+18,r.y+88,374,detail,_ui.Small,48);
            if(!G.IsDeclaredWar(f,other))
            {
                if(_ui.Button(new Rect(r.x+18,r.y+147,374,42),"DECLARAR GUERRA",true,G.CanDeclareWar(f,other)))Action(G.DeclareWar(f,other));
                Label(r.x+18,r.y+207,374,"Solo entre vecinos o por una invitación de coalición. Los tratados y la protección inicial siguen vigentes.",_ui.Body,91);return;
            }
            bool pending=G.Affairs.proposals.Exists(p=>p.source==f&&p.target==other&&p.state==ProposalState.Pending&&p.kind==ProposalKind.Peace);
            string[] captions={"SOLICITAR CESE AL FUEGO","OFRECER 25% DEL TERRITORIO","OFRECER 50% DEL TERRITORIO"};float[] fractions={0,.25f,.5f};
            for(int i=0;i<3;i++)if(_ui.Button(new Rect(r.x+18,r.y+147+i*43,374,35),captions[i],i==0,!G.Paused&&!pending))Action(G.ProposeWarTruce(f,other,fractions[i]));
            Label(r.x+18,r.y+281,374,pending?"Oferta enviada. La IA decidirá según su situación.":"Duración: cinco meses de juego. La cesión ocurre solo si la otra nación acepta.",_ui.Small,50);
        }
        private void DrawSpeedControl()
        {
            float[] speeds={1,2,5,10,20};for(int i=0;i<speeds.Length;i++)
            if(_ui.Button(new Rect(244+i*49,90,45,35),"×"+speeds[i],G.CampaignSpeed==speeds[i]))G.CampaignSpeed=speeds[i];
        }
    }
}
