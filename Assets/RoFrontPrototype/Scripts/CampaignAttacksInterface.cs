using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private Vector2 _operationsScroll;
        private readonly List<MilitaryOperation> _panelOperations=new List<MilitaryOperation>();
        private readonly List<StrategicProjectile> _panelProjectiles=new List<StrategicProjectile>();
        private void DrawCampaignAttacks()
        {
            Rect r=AttacksBounds;FrontierUI.Panel(r);Label(r.x+18,r.y+14,r.width-36,"ATAQUES Y AMENAZAS",_ui.Heading,32);
            _panelOperations.Clear();_panelProjectiles.Clear();int player=G.PlayerFaction;
            foreach(var op in G.Operations)if(op.active&&(op.faction==player||op.enemy==player||(op.target>=0&&G.Owners[op.target]==player)))_panelOperations.Add(op);
            foreach(var p in G.Projectiles)if(p.active&&(p.owner==player||p.targetNation==player))_panelProjectiles.Add(p);
            Label(r.x+18,r.y+51,r.width-36,_panelOperations.Count+" operaciones · "+_panelProjectiles.Count+" proyectiles",_ui.Small,25);
            Rect view=new Rect(r.x+14,r.y+90,r.width-28,210);float height=Mathf.Max(view.height,_panelOperations.Count*95+_panelProjectiles.Count*75);
            Vector2 old=_ui.Mouse;_ui.Mouse=old-new Vector2(view.x,view.y)+_operationsScroll;
            _operationsScroll=GUI.BeginScrollView(view,_operationsScroll,new Rect(0,0,view.width-20,height));
            if(_panelOperations.Count+_panelProjectiles.Count==0)_ui.Label(new Rect(4,5,view.width-24,50),"No hay operaciones activas.",_ui.Body);
            for(int i=0;i<_panelOperations.Count;i++)
            {
                float y=i*95;if(y+95<_operationsScroll.y||y>_operationsScroll.y+view.height)continue;var op=_panelOperations[i];bool own=op.faction==player;
                string title=own?(op.kind==OperationKind.Expansion?"Expansión dirigida":op.kind==OperationKind.Naval?"Invasión naval":"Contra "+G.Names[op.enemy]):G.Names[op.faction]+(op.kind==OperationKind.Naval?" invade":" ataca");
                _ui.Label(new Rect(4,y,view.width-28,26),title,_ui.Body,own?FrontierUI.White:FrontierUI.Gold);
                _ui.Label(new Rect(4,y+29,view.width-28,26),TroopNumber(op.troops)+" tropas · "+(op.planning?"calculando ruta":StateTitle(op.state).ToLowerInvariant()),_ui.Small);
                if(_ui.Button(new Rect(4,y+58,79,29),"VER"))_p.FocusCell(op.kind==OperationKind.Naval?G.OperationCell(op):op.frontCell);
                if(own)
                {
                    if(_ui.Button(new Rect(91,y+58,93,29),"REFORZAR",false,op.kind!=OperationKind.Naval&&_p.PreviewTroops>=G.OccupationSectorCost))Action(G.ReinforceOperation(op.id,_p.SendFraction));
                    if(_ui.Button(new Rect(192,y+58,89,29),"RETIRAR"))Action(G.RetreatOperation(op.id));
                }
            }
            for(int i=0;i<_panelProjectiles.Count;i++)
            {
                float y=_panelOperations.Count*95+i*75;if(y+75<_operationsScroll.y||y>_operationsScroll.y+view.height)continue;var projectile=_panelProjectiles[i];
                _ui.Label(new Rect(4,y,view.width-28,26),G.Rules.Weapon(projectile.kind).name,_ui.Body,projectile.owner==player?FrontierUI.White:FrontierUI.Gold);
                _ui.Label(new Rect(4,y+31,190,26),"Impacto en "+Mathf.CeilToInt(projectile.duration-projectile.age)+" s",_ui.Small);
                if(_ui.Button(new Rect(211,y+28,70,29),"VER"))_p.FocusCell(G.ProjectileCell(projectile));
            }
            GUI.EndScrollView();_ui.Mouse=old;
            if(_ui.Button(new Rect(r.x+18,r.yMax-47,r.width-36,32),"RETIRAR TODAS TUS OPERACIONES",false,G.HasOrder(player)))_p.StopOrder();
        }
    }
}
