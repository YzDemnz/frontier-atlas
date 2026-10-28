using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private void DrawSymbol(Rect rect,int symbol,Color colour)
        {Color old=GUI.color;GUI.color=colour;GUI.DrawTexture(rect,MapSymbols.Get(symbol));GUI.color=old;}
        private void DrawWorldSymbols()
        {
            if(!G.CampaignEnabled||Event.current.type!=EventType.Repaint)return;
            foreach(var building in G.Buildings)
            {
                if(building.destroyed)continue;bool modeled=!G.IsQuickMatch&&(building.kind==BuildingKind.City||building.kind==BuildingKind.Factory||building.kind==BuildingKind.Silo);if(modeled&&_camera.orthographicSize>=110)continue;Vector2 point=Project(building.cell);if(point.x<20||point.x>W-20||point.y<100||point.y>Dock.y-20)continue;
                float size=building.owner==G.PlayerFaction?25:20;if(_camera.orthographicSize>350)size=15;
                if(modeled){size=20;point.y-=18;}Rect rect=new Rect(point.x-size*.5f,point.y-size*.5f,size,size);FrontierUI.Rounded(new Rect(rect.x-3,rect.y-3,size+6,size+6),new Color(.02f,.045f,.06f,.80f),5);
                DrawSymbol(rect,(int)building.kind,building.Operational?FrontierUI.White:FrontierUI.Gold);
                if(!building.Operational)FrontierUI.Fill(new Rect(rect.x,rect.yMax+3,size*Mathf.Clamp01(1-(building.completeAt-G.ElapsedTime)/G.Rules.buildSeconds),2),FrontierUI.Gold);
            }
            for(int f=0;f<G.FactionCount;f++)if(G.TerritoryCounts[f]>0&&G.CapitalCells[f]>=0&&(f==G.PlayerFaction||_camera.orthographicSize<180))
            {Vector2 point=Project(G.CapitalCells[f]);if(point.y>100&&point.y<Dock.y-20&&point.x>20&&point.x<W-20){bool built=G.BuildingAt(G.CapitalCells[f])!=null;DrawSymbol(built?new Rect(point.x+7,point.y-18,11,11):new Rect(point.x-11,point.y-11,22,22),6,f==G.PlayerFaction?FrontierUI.Accent:FrontierUI.Muted);}}
            if(_p.Visuals.GetComponent<CommercialTrainVisuals>()==null)foreach(var train in G.RenderableTrains())
            {Vector3 screen=_camera.WorldToScreenPoint(train.position);Vector2 p=new Vector2(screen.x,Screen.height-screen.y)/Scale;if(p.x>20&&p.x<W-20&&p.y>100&&p.y<Dock.y-20)DrawSymbol(new Rect(p.x-4,p.y-4,8,8),7,FrontierUI.Gold);}
            foreach(var ship in G.Warships)if(!ship.sunk)
            {Vector3 position=G.NavalPosition(ship.currentCell,ship.nextCell,ship.progress);position.y=_p.World.SeaHeight(position.x,position.z,Time.time)+.4f;Vector3 pixel=_camera.WorldToScreenPoint(position);Vector2 p=new Vector2(pixel.x,Screen.height-pixel.y)/Scale;if(p.y>100&&p.y<Dock.y-20)DrawSymbol(new Rect(p.x-8,p.y-8,16,16),8,Powers.DisplayColors[ship.owner]);}
            if(_p.Visuals.GetComponent<StrategicMissileVisuals>()==null)foreach(var projectile in G.Projectiles)
            {int cell=G.ProjectileCell(projectile);Vector2 p=Project(cell);if(p.x>20&&p.x<W-20&&p.y>100&&p.y<Dock.y-20)DrawSymbol(new Rect(p.x-7,p.y-7,14,14),4,new Color(1,.55f,.28f));}
        }
        private void DrawBottomDashboard()
        {
            int f=G.PlayerFaction;Rect r=Dock;float leftWidth=286,middleWidth=304,gap=12,rightWidth=r.width-leftWidth-middleWidth-gap*2;
            Rect army=new Rect(r.x,r.y,leftWidth,r.height),send=new Rect(army.xMax+gap,r.y,middleWidth,r.height),actions=new Rect(send.xMax+gap,r.y,rightWidth,r.height);
            foreach(Rect card in new[]{army,send,actions})
            {FrontierUI.Rounded(new Rect(card.x,card.y+4,card.width,card.height),new Color(0,0,0,.22f),13);FrontierUI.Rounded(card,new Color(.025f,.052f,.069f,.97f),13);FrontierUI.Line(card.x+14,card.y,card.width-28,new Color(.24f,.40f,.43f,.46f));}
            Label(army.x+20,army.y+13,army.width-40,"TROPAS EN RESERVA",_ui.Caption,23);
            Label(army.x+19,army.y+34,army.width-38,TroopNumber(G.AvailableTroops(f))+(G.CampaignEnabled?" / "+TroopNumber(G.Capacity[f]):""),new GUIStyle(_ui.Heading){fontSize=28},37,FrontierUI.White);
            float fill=G.CampaignEnabled?Mathf.Clamp01(G.AvailableTroops(f)/Mathf.Max(1,G.Capacity[f])):Mathf.Clamp01(G.Troops[f]/2000);
            FrontierUI.Rounded(new Rect(army.x+20,army.y+76,army.width-40,4),new Color(.11f,.19f,.22f),2);FrontierUI.Rounded(new Rect(army.x+20,army.y+76,(army.width-40)*fill,4),FrontierUI.Accent,2);
            Label(army.x+20,army.y+89,army.width-40,"Reserva "+TroopNumber(G.AvailableTroops(f))+" · enviadas "+TroopNumber(G.CommittedTroops[f]),_ui.Small,24);
            Label(army.x+20,army.y+115,army.width-40,G.CampaignEnabled?Amount(G.Gold[f])+" oro · +"+Amount(G.IncomeRate(f))+"/s":"Refuerzos +"+G.ReinforcementRate(f).ToString("0.0")+"/s",_ui.Small,23,FrontierUI.Gold);
            _p.SendFraction=_ui.Slider(new Rect(send.x+22,send.y+14,send.width-44,65),"PORCENTAJE DE ENVÍO",_p.SendFraction,.1f,1);
            DrawAttackPresets(send.x+26,send.y+79);
            Label(send.x+22,send.y+112,send.width-44,"Enviarías "+TroopNumber(_p.PreviewTroops)+" tropas",_ui.Body,26,FrontierUI.Accent);
            if(G.CampaignEnabled)DrawQuickConstructionToolbar(actions);
            else
            {
                Label(actions.x+18,actions.y+18,actions.width-36,"Clic en tierra neutral vecina para expandir.",_ui.Body,50);
                if(_ui.Button(new Rect(actions.x+18,actions.y+90,150,35),"RETIRAR [X]",false,G.HasOrder(f)))_p.StopOrder();
            }
        }
    }
}
