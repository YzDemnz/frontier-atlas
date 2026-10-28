using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        public bool WorldBoardOpen {get;private set;}
        private int _worldTab,_worldNation=-1,_compactChoice,_aidChoice,_goalChoice,_pickedCell=-1,_cessionRadius=2;private bool _pickBoardCell,_offerLand,_askLand,_pledgeLand,_militaryCondition;
        private Vector2 _nationScroll,_boardScroll;private string _nationSearch="",_offerGold="0",_askGold="0",_offerTroops="0",_askTroops="0",_loanAmount="50000",_interest="1",_days="90",_periodic="0",_helpAmount="500",_collateralGold="0",_coalitionName="Liga de naciones";
        private string[] _saveFiles=Array.Empty<string>();private float _boardContentHeight=1600;
        private static readonly string[] WorldTabs={"Diplomacia","Coaliciones","Ayuda militar","Finanzas","Deudas","Paz","Historial","Guardados","Ofertas"};
        private static readonly string[] CompactNames={"Alianza","Defensa mutua","No agresión","Libre comercio","Protección militar","Alto el fuego","Paz","Subsidio","Tributo"};
        private static readonly string[] AidNames={"Tropas","Oro","Préstamo urgente","Diversión terrestre","Intervención","Escolta naval","Bombardeo costero","Defensa conjunta","Mediación"};
        private static readonly string[] GoalNames={"Territorio","Capital","Infraestructura","Recuperación","Defensa","Diversión","Naval","Bombardeo"};
        public void OpenWorldBoard(int tab)
        {CloseWarActions();CloseSimpleAlliance();WorldBoardOpen=true;_pickBoardCell=false;_worldTab=tab;_boardScroll=Vector2.zero;_p.CloseRadial();_p.CancelQuickBuilding();if(_worldNation<0||_worldNation>=G.FactionCount)_worldNation=G.PlayerFaction==0?1:0;if(_p.ContextCell>=0)_pickedCell=_p.ContextCell;_saveFiles=CampaignFiles.List();}
        public void CloseWorldBoard(){WorldBoardOpen=false;_pickBoardCell=false;}
        public bool CaptureBoardCell(int cell){if(!_pickBoardCell)return false;_pickedCell=cell;_pickBoardCell=false;WorldBoardOpen=true;return true;}
        private double Number(string text,double fallback=0){return double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double v)&&!double.IsNaN(v)&&!double.IsInfinity(v)?Math.Max(0,Math.Min(1e12,v)):fallback;}
        private void Field(ref string value,string name,ref float y,float width=310)
        {BodyLabel(y,width,name,_ui.Small);value=GUI.TextField(new Rect(0,y+25,width,32),value,16,new GUIStyle(GUI.skin.textField){font=_ui.Body.font,fontSize=18,padding=new RectOffset(9,9,4,4)});y+=68;}
        private void Text(string text,ref float y,float width,float height=42,Color? color=null){BodyLabel(y,width,text,_ui.Body,height,color);y+=height+8;}
        private bool BoardButton(string text,ref float y,float width,bool enabled=true){bool clicked=_ui.Button(new Rect(0,y,width,35),text,false,enabled);y+=44;return clicked;}
        private int Choice(string[] names,int choice,ref float y,float width)
        {int columns=3;float column=(width-16)/columns;for(int i=0;i<names.Length;i++)if(_ui.Button(new Rect(i%columns*(column+8),y+i/columns*38,column,32),names[i],i==choice))choice=i;y+=Mathf.CeilToInt(names.Length/(float)columns)*38+8;return choice;}
        private int[] NegotiatedCells(int owner)
        {var cells=new List<int>();if(_pickedCell<0)return cells.ToArray();int x=_pickedCell%G.World.Width,y=_pickedCell/G.World.Width;for(int dy=-_cessionRadius;dy<=_cessionRadius;dy++)for(int dx=-_cessionRadius;dx<=_cessionRadius;dx++){if(dx*dx+dy*dy>_cessionRadius*_cessionRadius||!G.World.InBounds(x+dx,y+dy))continue;int c=G.World.At(x+dx,y+dy);if(G.Owners[c]==owner)cells.Add(c);}return cells.ToArray();}
        private NegotiationTerms BoardTerms()=>new NegotiationTerms{compact=(CompactKind)_compactChoice,durationDays=_days.Trim()=="-1"?-1:Number(_days,90),sourceGold=Number(_offerGold),targetGold=Number(_askGold),sourceTroops=Number(_offerTroops),targetTroops=Number(_askTroops),periodicGold=Number(_periodic),militaryEnemy=_militaryCondition&&_pickedCell>=0?G.Owners[_pickedCell]:-1,sourceTerritory=_offerLand?NegotiatedCells(G.PlayerFaction):Array.Empty<int>(),targetTerritory=_askLand?NegotiatedCells(_worldNation):Array.Empty<int>()};
        private void DrawWorldBoard()
        {
            if(!WorldBoardOpen)return;float width=Mathf.Min(1110,W-40),height=Mathf.Min(750,H-112);Rect box=new Rect((W-width)/2,(H-height)/2,width,height);FrontierUI.Fill(new Rect(0,0,W,H),new Color(.006f,.014f,.022f,.7f));FrontierUI.Panel(box);
            Label(box.x+22,box.y+16,width-90,"CAMPAÑA  ·  "+G.CampaignDate.ToString("dd 'de' MMMM 'de' yyyy",CultureInfo.GetCultureInfo("es-MX")),_ui.Heading,33);
            if(_ui.Button(new Rect(box.xMax-54,box.y+15,32,32),"×"))CloseWorldBoard();float tx=box.x+20;
            for(int i=0;i<WorldTabs.Length;i++){float tw=(width-40)/WorldTabs.Length;if(_ui.Button(new Rect(tx+i*tw,box.y+60,tw-5,37),WorldTabs[i],_worldTab==i)){_worldTab=i;_boardScroll=Vector2.zero;if(i==7)_saveFiles=CampaignFiles.List();}}
            float ny=box.y+112,nw=235;Label(tx,ny,nw,"NACIONES",_ui.Caption);_nationSearch=GUI.TextField(new Rect(tx,ny+29,nw,32),_nationSearch,32);
            var nations=new List<int>();for(int f=0;f<G.FactionCount;f++)if(f!=G.PlayerFaction&&G.TerritoryCounts[f]>0&&!G.IsTribe(f)&&G.Names[f].IndexOf(_nationSearch,StringComparison.CurrentCultureIgnoreCase)>=0)nations.Add(f);
            _nationScroll=GUI.BeginScrollView(new Rect(tx,ny+73,nw,box.yMax-ny-95),_nationScroll,new Rect(0,0,nw-20,nations.Count*39));
            for(int i=0;i<nations.Count;i++){int f=nations[i];if(_ui.Button(new Rect(0,i*39,nw-22,34),G.Names[f],_worldNation==f)){_worldNation=f;_boardScroll=Vector2.zero;}}GUI.EndScrollView();
            Rect view=new Rect(tx+nw+22,ny,width-nw-62,box.yMax-ny-20);Vector2 mouse=_ui.Mouse;_ui.Mouse=mouse-new Vector2(view.x,view.y)+_boardScroll;_boardScroll=GUI.BeginScrollView(view,_boardScroll,new Rect(0,0,view.width-22,_boardContentHeight));float w=view.width-30,y=0;int player=G.PlayerFaction,other=_worldNation;
            if(_worldTab!=7&&_worldTab!=6&&(!G.CampaignEnabled||other<0||G.TerritoryCounts[other]==0)){Text("Elige una nación viva de la lista.",ref y,w);}
            else switch(_worldTab)
            {
                case 0:DrawDiplomaticBoard(player,other,w,ref y);break;
                case 1:DrawCoalitionBoard(player,other,w,ref y);break;
                case 2:DrawSupportBoard(player,other,w,ref y);break;
                case 3:DrawFinanceBoard(player,other,w,ref y);break;
                case 4:DrawDebtBoard(player,w,ref y);break;
                case 5:DrawPeaceBoard(player,other,w,ref y);break;
                case 6:DrawHistoryBoard(w,ref y);break;
                case 7:DrawSaveBoard(w,ref y);break;
                case 8:DrawPendingOffers(player,w,ref y);break;
            }
            _boardContentHeight=Mathf.Max(view.height-5,y+16);GUI.EndScrollView();_ui.Mouse=mouse;
        }
        private void DrawDiplomaticBoard(int player,int other,float w,ref float y)
        {
            string[] relations={"Hostilidad","Desconfianza","Neutralidad","Amistad","Alianza"};string[] personalities={"Pacífica","Expansionista","Oportunista","Defensiva","Comercial"};
            if(BoardButton("OFERTAS PENDIENTES ("+G.Affairs.proposals.FindAll(p=>p.target==player&&p.state==ProposalState.Pending).Count+")",ref y,w)){_worldTab=8;_boardScroll=Vector2.zero;}
            Text(G.Names[other]+"  ·  "+relations[(int)G.Relation(player,other)],ref y,w,32,FrontierUI.Accent);
            Text("Reputación: "+G.Affairs.reputation[other].ToString("0")+" / 100  ·  Personalidad "+personalities[(int)G.Personality(other)]+"\nConfianza hacia ti: "+G.Memory(other,player).trust.ToString("0")+"  ·  Tropas totales: "+TroopNumber(G.TotalTroops(other)),ref y,w,58);
            Text("Rivalidad: "+G.RivalryPressure(player,other).ToString("0")+" / 100 · actividad comercial: "+(G.TradeActivity(player,other)*100).ToString("0")+"%"+(G.AllianceUnavailable(player,other)?" · Alianza bloqueada":""),ref y,w,48);
            if(G.Paused){Text("Partida en pausa. Reanuda para enviar o aceptar acuerdos.",ref y,w,38,FrontierUI.Gold);if(BoardButton("REANUDAR CAMPAÑA",ref y,w))G.Paused=false;}
            _compactChoice=Choice(CompactNames,_compactChoice,ref y,w);Field(ref _days,"Duración en días de juego (0: vence hoy; −1: indefinida)",ref y,w);
            Field(ref _offerGold,"Oro que ofreces ahora",ref y,w);Field(ref _askGold,"Oro que solicitas ahora",ref y,w);Field(ref _offerTroops,"Tropas que ofreces",ref y,w);Field(ref _askTroops,"Tropas que solicitas",ref y,w);Field(ref _periodic,"Pago periódico cada 15 días: subsidio tuyo / tributo del receptor",ref y,w);
            DrawTerritoryTerms(w,ref y);
            _militaryCondition=_ui.Toggle(new Rect(0,y,w,32),"Exigir intervención contra el propietario del punto marcado",_militaryCondition);y+=42;
            if(BoardButton("ENVIAR OFERTA",ref y,w,_compactChoice!=(int)CompactKind.Alliance||!G.AllianceUnavailable(player,other)))Action(G.ProposeCampaign(player,other,ProposalKind.Treaty,BoardTerms(),"Oferta diplomática con concesiones negociables."));
            DrawPendingOffers(player,w,ref y);
            foreach(var pact in G.Affairs.compacts)if(pact.active&&(pact.source==player||pact.target==player))
            {Text(CompactNames[(int)pact.kind]+" · "+G.Names[pact.source==player?pact.target:pact.source]+" · "+(pact.endsDay<0?"indefinido":"vence día "+pact.endsDay.ToString("0.0")),ref y,w);if(BoardButton("ROMPER ACUERDO #"+pact.id,ref y,w))Action(G.BreakCompact(player,pact.id));}
        }
        private void DrawTerritoryTerms(float w,ref float y)
        {
            if(BoardButton(_pickedCell<0?"ELEGIR ÁREA EN EL MAPA":"CAMBIAR ÁREA · celda "+_pickedCell,ref y,w)){WorldBoardOpen=false;_pickBoardCell=true;_p.CloseCampaignPanel();_p.Notify("Haz clic en el área que quieres negociar. No se emitirá una orden militar.");}
            _cessionRadius=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(0,y,w,22),_cessionRadius,1,20));y+=28;Text("Radio del área: "+_cessionRadius+" celdas. Solo incluye tierra del propietario indicado.",ref y,w,36);
            _offerLand=_ui.Toggle(new Rect(0,y,w,30),"Ofrecer mi territorio marcado ("+NegotiatedCells(G.PlayerFaction).Length+" celdas)",_offerLand);y+=37;
            _askLand=_ui.Toggle(new Rect(0,y,w,30),"Solicitar su territorio marcado ("+NegotiatedCells(_worldNation).Length+" celdas)",_askLand);y+=40;
        }
        private void DrawPendingOffers(int player,float w,ref float y)
        {
            Text("NEGOCIACIONES PENDIENTES",ref y,w,30,FrontierUI.Accent);
            for(int i=0,count=G.Affairs.proposals.Count;i<count;i++){var p=G.Affairs.proposals[i];if(p.state!=ProposalState.Pending||(p.source!=player&&p.target!=player))continue;var t=p.terms;
                string detail=p.kind==ProposalKind.Loan?"Préstamo: "+Amount(t.principal)+" oro · interés "+(t.interestPerPeriod*100).ToString("0.##")+"% / 15 días · plazo "+t.loanDays.ToString("0")+" días\nPresta "+G.Names[t.loanOfferedByLender?p.source:p.target]+" · recibe "+G.Names[t.loanOfferedByLender?p.target:p.source]+"\nGarantía: "+Amount(t.collateralGold)+" oro retenido y "+t.collateral.Length+" celdas":p.kind==ProposalKind.RenegotiateDebt?"Renegociar deuda #"+t.loanId+" · ampliar "+t.loanDays.ToString("0")+" días · interés "+(t.interestPerPeriod*100).ToString("0.##")+"% / 15 días":p.kind==ProposalKind.CoalitionInvite?"Invitación a coalición #"+t.coalitionId:CompactNames[(int)t.compact]+" · "+t.durationDays.ToString("0")+" días\nOfrece "+Amount(t.sourceGold)+" / exige "+Amount(t.targetGold)+" oro · tropas "+Amount(t.sourceTroops)+" / "+Amount(t.targetTroops)+"\nTierra "+(t.sourceTerritoryFraction>0?(t.sourceTerritoryFraction*100).ToString("0")+"%":t.sourceTerritory.Length.ToString())+" / "+(t.targetTerritoryFraction>0?(t.targetTerritoryFraction*100).ToString("0")+"%":t.targetTerritory.Length.ToString())+" · cuota "+Amount(t.periodicGold)+" / 15 días"+(t.militaryEnemy>=0?" · intervención contra "+G.Names[t.militaryEnemy]:"");
                Text("#"+p.id+" · "+G.Names[p.source]+" → "+G.Names[p.target]+"\n"+p.note+"\n"+detail,ref y,w,154);
                if(p.target==player){if(BoardButton("ACEPTAR #"+p.id,ref y,w))Action(G.AnswerCampaign(player,p.id,true));if(BoardButton("RECHAZAR #"+p.id,ref y,w))Action(G.AnswerCampaign(player,p.id,false));if(p.kind!=ProposalKind.CoalitionInvite&&p.kind!=ProposalKind.Mediation&&BoardButton("CONTRAOFERTA #"+p.id+" CON LOS IMPORTES DEL FORMULARIO",ref y,w)){_worldNation=p.source;var revised=BoardTerms();if(p.kind==ProposalKind.Loan){revised=new NegotiationTerms{principal=Number(_loanAmount),interestPerPeriod=Number(_interest)/100,loanDays=Number(_days,90),collateral=t.collateral,collateralGold=t.collateralGold,collateralValue=t.collateralValue};}if(p.kind==ProposalKind.RenegotiateDebt){revised=new NegotiationTerms{loanId=t.loanId,loanDays=Number(_days,90),interestPerPeriod=Number(_interest)/100};}Action(G.CounterCampaign(player,p.id,revised));}}
            }
        }
    }
}
