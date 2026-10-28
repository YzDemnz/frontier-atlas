using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private int _allianceNation=-1;private Vector2 _allianceAnchor;
        public bool SimpleAllianceOpen=>_allianceNation>=0;
        private Rect SimpleAllianceBounds=>new Rect(Mathf.Clamp(_allianceAnchor.x-180,24,W-384),Mathf.Clamp(_allianceAnchor.y-100,106,Dock.y-222),360,208);
        public void CloseSimpleAlliance()=>_allianceNation=-1;
        public void ShowSimpleAlliance(int cell)
        {
            int other=cell>=0&&cell<G.Owners.Length?G.Owners[cell]:-1;if(other<0||other==G.PlayerFaction||G.IsTribe(other))return;
            CloseWarActions();_allianceNation=other;_allianceAnchor=RadialCenter;_p.CloseRadial();_p.CloseCampaignPanel();WorldBoardOpen=false;
        }
        private void DrawSimpleAlliance()
        {
            if(!SimpleAllianceOpen)return;int other=_allianceNation,player=G.PlayerFaction;if(other>=G.FactionCount||G.TerritoryCounts[other]==0){CloseSimpleAlliance();return;}
            Rect r=SimpleAllianceBounds;FrontierUI.Rounded(r,new Color(.025f,.046f,.059f,1),9);Label(r.x+18,r.y+14,288,"ALIANZA",_ui.Heading,30);
            if(_ui.Button(new Rect(r.xMax-43,r.y+12,28,28),"×")){CloseSimpleAlliance();return;}
            Label(r.x+18,r.y+49,r.width-36,G.Names[other],_ui.Body,30,FrontierUI.Accent);
            if(G.IsQuickMatch){DrawQuickAlliance(r.width-36,other);return;}
            bool allied=G.AreAllied(player,other);var pending=G.Affairs.proposals.Find(p=>p.state==ProposalState.Pending&&p.kind==ProposalKind.Treaty&&p.terms.compact==CompactKind.Alliance&&p.terms.sourceGold==0&&p.terms.targetGold==0&&p.terms.sourceTroops==0&&p.terms.targetTroops==0&&p.terms.sourceTerritory.Length==0&&p.terms.targetTerritory.Length==0&&p.terms.periodicGold==0&&p.terms.militaryEnemy<0&&((p.source==player&&p.target==other)||(p.source==other&&p.target==player)));
            string detail=allied?"Ya sois aliados. No podéis atacaros.":pending!=null?(pending.target==player?(pending.terms.durationDays<0?"Este país propone una alianza indefinida.":"Este país propone una alianza de "+pending.terms.durationDays.ToString("0")+" días."):"Propuesta enviada. Esperando su respuesta."):"Alianza bilateral de 30 días, sin pagos ni concesiones.";
            Label(r.x+18,r.y+85,r.width-36,G.Paused?"Reanuda la partida para negociar.":detail,_ui.Small,53);
            if(pending!=null&&pending.target==player)
            {
                if(_ui.Button(new Rect(r.x+18,r.y+153,156,36),"ACEPTAR",true,!G.Paused&&!G.AllianceUnavailable(player,other)))Action(G.AnswerCampaign(player,pending.id,true));
                if(_ui.Button(new Rect(r.x+185,r.y+153,156,36),"RECHAZAR",false,!G.Paused))Action(G.AnswerCampaign(player,pending.id,false));
            }
            else if(_ui.Button(new Rect(r.x+18,r.y+153,r.width-36,36),G.AllianceUnavailable(player,other)?"RIVALIDAD · ALIANZA BLOQUEADA":allied?"ALIADOS":pending!=null?"ESPERANDO RESPUESTA":"PROPONER ALIANZA",true,!G.Paused&&!allied&&pending==null&&!G.AllianceUnavailable(player,other)))Action(G.ProposeCampaign(player,other,ProposalKind.Treaty,new NegotiationTerms{compact=CompactKind.Alliance,durationDays=30},"Alianza bilateral sin pagos ni concesiones."));
        }
    }
}
