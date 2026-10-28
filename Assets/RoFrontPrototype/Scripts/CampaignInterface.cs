using System;
using System.Globalization;
using UnityEngine;
namespace FrontierAtlas
{
    public enum CampaignPanel { None, Construction, Diplomacy, Nuclear, Arsenal, Navy, Information }
    public sealed partial class FrontierInterface
    {
        private Vector2 _campaignScroll;
        private string _donation="1000";
        private bool _donateTroops;
        private Rect CampaignBounds=>new Rect(W-430,108,408,H-282);
        private static string Amount(double value)=>value>=1000000?(value/1000000).ToString("0.##",CultureInfo.GetCultureInfo("es-MX"))+" M":value>=10000?(value/1000).ToString("0.#",CultureInfo.GetCultureInfo("es-MX"))+" mil":value.ToString("N0",CultureInfo.GetCultureInfo("es-MX"));
        private void DrawCampaignPanel()
        {
            if(!G.CampaignEnabled||_p.Panel==CampaignPanel.None)return;
            if(_p.Panel==CampaignPanel.Diplomacy&&!G.IsQuickMatch){OpenWorldBoard(0);_p.CloseCampaignPanel();return;}
            Rect r=CampaignBounds;FrontierUI.Panel(r);
            string[] titles={"","CONSTRUIR Y MEJORAR","DIPLOMACIA","ATAQUE ESTRATÉGICO","ARSENAL DE ESTRELLAS","OPERACIONES NAVALES","INFORMACIÓN"};
            Label(r.x+20,r.y+17,r.width-76,titles[(int)_p.Panel],_ui.Heading,34);
            if(_ui.Button(new Rect(r.xMax-48,r.y+17,30,30),"×"))_p.CloseCampaignPanel();
            Rect view=new Rect(r.x+18,r.y+66,r.width-36,r.height-80);
            Vector2 oldMouse=_ui.Mouse;_ui.Mouse=oldMouse-new Vector2(view.x,view.y)+_campaignScroll;
            float contentHeight=_p.Panel==CampaignPanel.Arsenal?900:_p.Panel==CampaignPanel.Navy?Mathf.Max(600,G.Operations.Count*86+G.Warships.Count*75+220):850;
            _campaignScroll=GUI.BeginScrollView(view,_campaignScroll,new Rect(0,0,view.width-20,contentHeight));
            float width=view.width-23;int cell=_p.ContextCell,player=G.PlayerFaction;
            switch(_p.Panel)
            {
                case CampaignPanel.Construction:DrawConstructionBody(width,cell,player);break;
                case CampaignPanel.Diplomacy:DrawDiplomacyBody(width,cell,player);break;
                case CampaignPanel.Nuclear:DrawNuclearBody(width,cell,player);break;
                case CampaignPanel.Arsenal:DrawArsenalBody(width,cell,player);break;
                case CampaignPanel.Navy:DrawNavyBody(width,cell,player);break;
                case CampaignPanel.Information:DrawInformationBody(width,cell);break;
            }
            GUI.EndScrollView();_ui.Mouse=oldMouse;
        }
        private void BodyLabel(float y,float width,string text,GUIStyle style=null,float height=28,Color? colour=null)=>_ui.Label(new Rect(0,y,width,height),text,style??_ui.Body,colour);
        private void Action(CampaignAction result)=>_p.Notify(result.message);
        private void DrawConstructionBody(float width,int cell,int player)
        {
            BodyLabel(0,width,"Oro: "+Amount(G.Gold[player]),_ui.Heading,32,FrontierUI.Gold);
            BodyLabel(42,width,"Haz clic en una posición de tu territorio para colocar un edificio o seleccionar uno existente.",_ui.Small,70);
            for(int i=0;i<G.Rules.buildings.Length;i++)
            {
                var definition=G.Rules.buildings[i];float y=124+i*49;
                DrawSymbol(new Rect(7,y+6,28,28),(int)definition.kind,FrontierUI.Muted);
                if(_ui.Button(new Rect(40,y,width-40,40),definition.name+" · "+Amount(G.ConstructionPrice(player,definition.kind,cell)),_p.SelectedBuilding==definition.kind))_p.SelectedBuilding=definition.kind;
            }
            var selected=G.Rules.Building(_p.SelectedBuilding);var existing=cell>=0?G.BuildingAt(cell):null;
            string detail=selected.kind==BuildingKind.City?"Cada nivel añade "+Amount(G.Rules.balanceProfile==BalanceProfile.OpenFront34Reference?G.Rules.openFrontCityCapacity:G.Rules.cityCapacity)+(G.IsQuickMatch?" de capacidad por nivel. Las ciudades se incorporan a la red industrial.":" de capacidad. Cada ciudad terminada suma +35% al reclutamiento base."):selected.kind==BuildingKind.Factory?"Alcance: "+G.FactoryConnectionRadius.ToString("0")+" celdas · "+(G.TacticalBalance?"fábricas, ciudades y puertos del círculo":"máximo "+G.Rules.factoryConnections+" conexiones")+". Los trenes entregan oro al llegar.":selected.kind==BuildingKind.Port?"Necesita costa. Sus barcos comerciales atracan, entregan oro y vuelven a salir.":selected.kind==BuildingKind.Outpost?"Radio "+selected.radius+" sectores. Desgaste atacante ×"+G.Rules.outpostLossMultiplier+"; ocupación ×"+G.Rules.outpostTimeMultiplier:selected.kind==BuildingKind.SAM?"Intercepción automática · radio inicial "+G.SamRadius(1).ToString("0")+" celdas · hasta cinco posiciones independientes.":"Permite lanzar misiles con munición y recarga.";
            BodyLabel(433,width,detail,_ui.Body,100);
            var validation=G.ValidateConstruction(player,cell,_p.SelectedBuilding);
            BodyLabel(550,width,validation.message,_ui.Small,60,validation.success?FrontierUI.Accent:FrontierUI.Muted);
            if(_ui.Button(new Rect(0,626,width,45),existing!=null&&existing.kind==_p.SelectedBuilding?"MEJORAR AQUÍ":"CONSTRUIR AQUÍ",true,validation.success))Action(G.Construct(player,cell,_p.SelectedBuilding));
            if(existing!=null)
            {
                BodyLabel(694,width,G.Rules.Building(existing.kind).name+" · nivel "+existing.level+" · "+(existing.Operational?"operativo":"en construcción"),_ui.Body,55);
                if(existing.kind==BuildingKind.Port&&existing.Operational&&_ui.Button(new Rect(0,761,width,42),"DESPLEGAR BUQUE · "+Amount(G.WarshipPrice(player))))Action(G.DeployWarship(player,cell));
            }
        }
        private void DrawDiplomacyBody(float width,int cell,int player)
        {
            int other=cell>=0?G.Owners[cell]:-1;
            if(other<0||other==player||G.IsTribe(other)){BodyLabel(0,width,"Selecciona una nación rival en el mapa para negociar.",_ui.Body,85);}
            else
            {
                BodyLabel(0,width,G.Names[other],_ui.Heading,55);var treaty=G.TreatyBetween(player,other);
                BodyLabel(68,width,G.AreAllied(player,other)?"Aliado · "+(treaty!=null?Mathf.CeilToInt(treaty.expiresAt-G.ElapsedTime)+" s restantes":"mismo equipo"):treaty!=null&&treaty.state==TreatyState.Pending?"Propuesta pendiente":"Sin alianza",_ui.Body,50);
                if(G.AreAllied(player,other))
                {
                    if(_ui.Button(new Rect(0,135,width,42),"RENOVAR ALIANZA",true))Action(G.RenewAlliance(player,other));
                    if(_ui.Button(new Rect(0,189,width,42),"ROMPER ALIANZA"))Action(G.Betray(player,other));
                }
                else if(treaty!=null&&treaty.state==TreatyState.Pending&&treaty.target==player)
                {
                    if(_ui.Button(new Rect(0,135,width*.48f,42),"ACEPTAR",true))Action(G.AnswerAlliance(player,other,true));
                    if(_ui.Button(new Rect(width*.52f,135,width*.48f,42),"RECHAZAR"))Action(G.AnswerAlliance(player,other,false));
                }
                else if(_ui.Button(new Rect(0,135,width,42),"PROPONER ALIANZA",true))Action(G.ProposeAlliance(player,other));
                BodyLabel(254,width,"DONACIÓN AL ALIADO",_ui.Caption);
                _donateTroops=_ui.Toggle(new Rect(0,294,width,35),"Donar tropas en vez de oro",_donateTroops);
                _donation=GUI.TextField(new Rect(0,343,width,38),_donation,14,new GUIStyle(GUI.skin.textField){font=_ui.Body.font,fontSize=20});
                if(_ui.Button(new Rect(0,395,width,42),"TRANSFERIR",false,G.AreAllied(player,other)))
                {if(double.TryParse(_donation,NumberStyles.Number,CultureInfo.GetCultureInfo("es-MX"),out double amount))Action(G.Donate(player,other,amount,_donateTroops));else _p.Notify("Escribe una cantidad válida.");}
                BodyLabel(453,width,"Romper un pacto marca públicamente al traidor y aumenta su vulnerabilidad durante "+G.Rules.betrayalSeconds+" segundos.",_ui.Small,85);
            }
            BodyLabel(555,width,"SOLICITUDES RECIBIDAS",_ui.Caption);float y=597;
            foreach(var treaty in G.Treaties)
            {
                if(treaty.target!=player||treaty.state!=TreatyState.Pending)continue;
                BodyLabel(y,width,G.Names[treaty.source],_ui.Body,30);
                if(_ui.Button(new Rect(0,y+33,width*.48f,35),"ACEPTAR"))Action(G.AnswerAlliance(player,treaty.source,true));
                if(_ui.Button(new Rect(width*.52f,y+33,width*.48f,35),"RECHAZAR"))Action(G.AnswerAlliance(player,treaty.source,false));y+=85;
            }
        }
        private void DrawNuclearBody(float width,int cell,int player)
        {
            BodyLabel(0,width,"Selecciona un punto y revisa el radio. Hace falta una segunda confirmación. También puedes destruir tu propia nación.",_ui.Body,90);
            for(int i=0;i<4;i++)
            {
                var definition=G.Rules.weapons[i];float y=105+i*145;BodyLabel(y,width,definition.name,_ui.Heading,35);
                BodyLabel(y+38,width,Amount(definition.cost)+" oro · radio "+definition.radius+" · alcance "+definition.range,_ui.Small,45);
                var valid=G.ValidateWeapon(player,cell,definition.kind);
                if(_ui.Button(new Rect(0,y+88,width,39),"VER RADIO Y PREPARAR",false,valid.success))_p.PreviewStrategicWeapon(definition.kind);
                if(!valid.success)BodyLabel(y+128,width,valid.message,new GUIStyle(_ui.Small){fontSize=12},24);
            }
            BodyLabel(720,width,"La intercepción del misil de ojivas múltiples es configurable. Configuración actual: "+(G.Rules.samInterceptsMIRV?"activada":"desactivada")+".",_ui.Small,75);
        }
        private void DrawArsenalBody(float width,int cell,int player)
        {
            BodyLabel(0,width,Amount(G.Stars[player])+" ESTRELLAS",_ui.Heading,37,FrontierUI.Gold);
            BodyLabel(47,width,"Las estrellas se ganan jugando y están separadas del oro. No hay compras con dinero real.",_ui.Small,65);
            for(int i=0;i<6;i++)
            {
                var kind=(ArsenalKind)i;float y=126+i*115;BodyLabel(y,width,TerritorySimulation.ArsenalName(kind),_ui.Heading,33);
                BodyLabel(y+35,width,Amount(G.Rules.arsenalCosts[i])+" estrellas · usos: "+G.ArsenalCharges(player,kind),_ui.Small,28);
                if(_ui.Button(new Rect(0,y+68,width*.48f,35),"ADQUIRIR",false,G.Stars[player]>=G.Rules.arsenalCosts[i]))Action(G.BuyArsenal(player,kind));
                if(_ui.Button(new Rect(width*.52f,y+68,width*.48f,35),"UTILIZAR",true,G.ArsenalCharges(player,kind)>0)){if(kind==ArsenalKind.Reinforcements)Action(G.UseArsenal(player,kind,cell));else _p.PreviewStrategicWeapon(kind==ArsenalKind.Mega?WeaponKind.Mega:kind==ArsenalKind.Scatter?WeaponKind.Scatter:kind==ArsenalKind.Artillery?WeaponKind.Artillery:kind==ArsenalKind.AirStrike?WeaponKind.AirStrike:WeaponKind.Railgun,true,kind);}
            }
            BodyLabel(829,width,"Estas habilidades tienen reglas originales del prototipo; no afirman reproducir efectos no documentados de otros juegos.",_ui.Small,70);
        }
        private void DrawNavyBody(float width,int cell,int player)
        {
            BodyLabel(0,width,"TRANSPORTES Y PUERTOS",_ui.Heading,34);
            BodyLabel(44,width,"La invasión usa tu porcentaje de envío. Los transportes muestran un barco pequeño con estela. El comercio navega entre puertos y atraca al llegar.",_ui.Small,85);
            if(_ui.Button(new Rect(0,141,width,40),"ENVIAR INVASIÓN",true,cell>=0&&G.HasCoastalAccess(player)))_p.SendBoat(cell);
            var port=cell>=0?G.BuildingAt(cell):null;
            if(_ui.Button(new Rect(0,193,width,40),"DESPLEGAR BUQUE · "+Amount(G.WarshipPrice(player)),false,port!=null&&port.owner==player&&port.kind==BuildingKind.Port&&port.Operational))Action(G.DeployWarship(player,cell));
            float y=257;
            foreach(var op in G.Operations)
            {
                if(!op.active||op.faction!=player||op.kind!=OperationKind.Naval)continue;
                BodyLabel(y,width,"Transporte "+op.id+" · "+TroopNumber(op.troops)+" tropas",_ui.Body,30);
                if(_ui.Button(new Rect(0,y+34,width*.48f,34),"LOCALIZAR"))_p.FocusCell(G.OperationCell(op));
                if(_ui.Button(new Rect(width*.52f,y+34,width*.48f,34),"NUEVO DESTINO")){_p.SelectedTransport=op.id;_p.Notify("Haz clic en agua o costa para cambiar el destino del transporte.");}y+=86;
            }
            foreach(var ship in G.Warships)
            {
                if(ship.owner!=player||ship.sunk)continue;BodyLabel(y,width,"Buque "+ship.id+" · integridad "+ship.health.ToString("0"),_ui.Body,30);
                if(_ui.Button(new Rect(0,y+34,width*.48f,34),"LOCALIZAR"))_p.FocusCell(G.WarshipCell(ship));
                if(_ui.Button(new Rect(width*.52f,y+34,width*.48f,34),"DAR DESTINO")){_p.SelectedWarship=ship.id;_p.Notify("Haz clic en agua para navegar o en costa enemiga para bombardear.");}y+=86;
            }
            BodyLabel(y+14,width,"Los barcos comerciales salen de puertos conectados, atracan durante "+G.Rules.portDwellSeconds+" segundos, entregan oro y vuelven a salir.",_ui.Small,90);
        }
        private void DrawInformationBody(float width,int cell)
        {
            int owner=cell>=0?G.Owners[cell]:-1;BodyLabel(0,width,owner>=0?G.Names[owner]:cell>=0&&_p.World.Land[cell]?"Tierra libre":"Mar",_ui.Heading,60);
            if(owner<0){BodyLabel(78,width,cell>=0&&G.IsIrradiated(cell)?"Terreno irradiado: mayor resistencia y excluido del control mundial hasta recuperarse.":"Selecciona una nación para consultar su información.",_ui.Body,110);return;}
            var selectedBuilding=G.BuildingAt(cell);
            if(selectedBuilding!=null&&selectedBuilding.kind==BuildingKind.Factory)
            {
                BodyLabel(83,width,"FÁBRICA · NIVEL "+selectedBuilding.level+" / 5",_ui.Heading,38);
                BodyLabel(128,width,G.FactoryStatus(selectedBuilding)+" · "+G.ConnectionsFor(selectedBuilding.id)+" conexiones",_ui.Body,36);
                BodyLabel(174,width,G.Trains.FindAll(t=>t.producer==selectedBuilding.id).Count+" trenes activos · "+Amount(G.Gold[owner])+" oro disponible",_ui.Small,38);
                BodyLabel(221,width,"Producción inicial prevista: "+Amount(G.FactoryEstimatedIncome(selectedBuilding))+" oro/s",_ui.Small,40);
                BodyLabel(266,width,"Salida cada ~"+G.FactoryDispatchInterval(selectedBuilding).ToString("0.0")+" s · alcance "+G.FactoryConnectionRadius.ToString("0")+" celdas",_ui.Small,42);
                BodyLabel(316,width,"Los pagos llegan al completar paradas válidas; la estimación cambia con la flota y sus destinos.",_ui.Small,65);
                BodyLabel(392,width,"NIVELES · PRECIO / TIEMPO / FRECUENCIA",_ui.Caption,36);
                for(int level=1;level<=5;level++)
                {double price=System.Math.Min(G.Rules.Building(BuildingKind.Factory).maxCost,G.Rules.Building(BuildingKind.Factory).cost*System.Math.Pow(2,level-1));float seconds=level==1?G.Rules.buildSeconds:G.Rules.buildSeconds*(1+(level-1)*.25f);BodyLabel(436+(level-1)*55,width,"N"+level+" · "+Amount(price)+" · "+seconds.ToString("0.0")+" s · salidas ~"+G.FactoryDispatchInterval(selectedBuilding,level).ToString("0.0")+" s · "+(G.TacticalBalance?"red completa":G.Rules.factoryConnections+" vías"),_ui.Small,50);}
                if(selectedBuilding.owner==G.PlayerFaction&&selectedBuilding.level<5&&_ui.Button(new Rect(0,735,width,42),"MEJORAR · "+Amount(G.ConstructionPrice(owner,BuildingKind.Factory,cell)),true,G.ValidateConstruction(owner,cell,BuildingKind.Factory).success))Action(G.Construct(owner,cell,BuildingKind.Factory));
                return;
            }
            if(selectedBuilding!=null&&selectedBuilding.kind==BuildingKind.SAM)
            {
                BodyLabel(83,width,"SAM · NIVEL "+selectedBuilding.level+" / 5",_ui.Heading,38);BodyLabel(134,width,"Cobertura: "+G.SamRadius(selectedBuilding.level).ToString("0.0")+" celdas",_ui.Body,40);
                BodyLabel(185,width,G.SamReadySlots(selectedBuilding)+" / "+selectedBuilding.level+" posiciones disponibles",_ui.Body,40);BodyLabel(236,width,"Próxima recarga: "+G.SamReloadRemaining(selectedBuilding).ToString("0.0")+" s",_ui.Small,42);
                BodyLabel(287,width,"Cada posición dispara y recarga por separado. Construir o mejorar deja temporalmente inactiva la instalación.",_ui.Small,85);
                int threats=0;foreach(var missile in G.Projectiles)if(G.SamCovers(selectedBuilding,missile))threats++;BodyLabel(388,width,threats+" amenazas compatibles dentro de cobertura",_ui.Small,55);
                BodyLabel(457,width,"El aro tenue muestra el alcance del siguiente nivel.",_ui.Small,55);
                if(selectedBuilding.owner==G.PlayerFaction&&selectedBuilding.level<5&&_ui.Button(new Rect(0,535,width,42),"MEJORAR · "+Amount(G.ConstructionPrice(owner,BuildingKind.SAM,cell)),true,G.ValidateConstruction(owner,cell,BuildingKind.SAM).success))Action(G.Construct(owner,cell,BuildingKind.SAM));return;
            }
            BodyLabel(83,width,"Tropas totales: "+TroopNumber(G.TotalTroops(owner)));BodyLabel(125,width,"En reserva: "+TroopNumber(G.AvailableTroops(owner)));
            BodyLabel(167,width,"Comprometidas: "+TroopNumber(G.CommittedTroops[owner]));BodyLabel(209,width,"Capacidad: "+TroopNumber(G.Capacity[owner]));
            BodyLabel(251,width,"Control mundial: "+(G.WorldControl(owner)*100).ToString("0.00")+"%");BodyLabel(293,width,"Oro: "+Amount(G.Gold[owner]));
            BodyLabel(335,width,"Ingresos previstos: "+Amount(G.IncomeRate(owner))+" oro/s");BodyLabel(377,width,"Protección: "+Mathf.CeilToInt(G.ProtectionRemaining(owner))+" s");
            BodyLabel(419,width,G.BetrayalUntil[owner]>G.ElapsedTime?"Marcada por traición":"Comercio: "+(owner==G.PlayerFaction?"red nacional":G.CanTrade(G.PlayerFaction,owner)?"disponible con este vecino":"interrumpido por hostilidades"));
            if(_ui.Button(new Rect(0,480,width,42),"ENFOCAR CAPITAL"))_p.FocusCell(G.CapitalCells[owner]);
            var ledger=G.Ledgers[owner];BodyLabel(546,width,"INGRESOS ACUMULADOS",_ui.Caption);BodyLabel(581,width,"Pasivos: "+Amount(ledger.passive));BodyLabel(618,width,"Comercio terrestre: "+Amount(ledger.landTrade));BodyLabel(655,width,"Comercio marítimo: "+Amount(ledger.seaTrade));BodyLabel(692,width,"Donaciones recibidas: "+Amount(ledger.donationsReceived));BodyLabel(729,width,"Construcción: "+Amount(ledger.constructionSpent));BodyLabel(766,width,"Gasto militar: "+Amount(ledger.militarySpent));
        }
        private void DrawResults()
        {
            int f=G.PlayerFaction;bool won=G.Outcome==CampaignOutcome.Victory;Rect r=new Rect(W*.5f-360,H*.5f-292,720,584);Color accent=won?FrontierUI.Accent:new Color(.93f,.44f,.36f);
            Color previous=GUI.color;GUI.color=new Color(previous.r,previous.g,previous.b,previous.a*_p.ResultsReveal);FrontierUI.Panel(r);FrontierUI.Fill(new Rect(r.x+26,r.y+1,r.width-52,3),accent);
            Label(r.x+34,r.y+26,r.width-68,"CAMPAÑA TERMINADA",_ui.Caption,27,accent);
            Label(r.x+30,r.y+60,r.width-60,won?"VICTORIA":"DERROTA",new GUIStyle(_ui.Hero){fontSize=62},82,accent);
            Label(r.x+34,r.y+151,r.width-68,G.EndReason,_ui.Body,55);
            string detail=won?"Has dejado tu huella en el mundo.":G.WinningFaction>=0?"Último adversario: "+G.Names[G.WinningFaction]:"Tu última frontera ha caído.";
            Label(r.x+34,r.y+207,r.width-68,detail,_ui.Small,29);FrontierUI.Line(r.x+34,r.y+249,r.width-68,new Color(.24f,.34f,.38f,.5f));
            var stats=G.Statistics[f];string[] captions={"TIEMPO DE PARTIDA","MÁXIMO TERRITORIO","MÁXIMO DE TROPAS","NACIONES ELIMINADAS","BAJAS Y DESGASTE","ORO GENERADO"};
            string[] values={TimeSpan.FromSeconds(G.ElapsedTime).ToString(@"hh\:mm\:ss"),stats.peakTerritory.ToString("N0")+" sectores",TroopNumber(stats.peakTroops),stats.nationsEliminated.ToString(),TroopNumber(stats.troopsLost),Amount(stats.goldEarned)};
            for(int i=0;i<6;i++){float x=r.x+34+i%2*332,y=r.y+266+i/2*60;Label(x,y,308,captions[i],_ui.Caption,24);Label(x,y+24,308,values[i],_ui.Heading,31);}
            Label(r.x+34,r.y+459,r.width-68,"Estrellas: "+Amount(G.Stars[f])+" · Reintentar conserva tu nombre y dificultad.\nElige una nueva capital.",_ui.Small,40);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&_p.ResultsInteractive;
            if(_ui.Button(new Rect(r.x+34,r.y+510,220,45),"REINTENTAR",true))_p.RetryCampaign();
            if(_ui.Button(new Rect(r.x+265,r.y+510,208,45),"MENÚ PRINCIPAL"))_p.ShowMainMenu();
            if(_ui.Button(new Rect(r.x+484,r.y+510,202,45),"CONFIGURACIÓN"))_p.ShowSettings();
            GUI.enabled=enabled;GUI.color=previous;
        }
        private void RangeLine(Vector2 first,Vector2 last,Color colour,float thickness=1.5f)
        {Vector2 d=last-first;if(d.magnitude>W)return;Matrix4x4 old=GUI.matrix;GUI.matrix=old*Matrix4x4.TRS(new Vector3(first.x,first.y,0),Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);FrontierUI.Fill(new Rect(0,-thickness*.5f,d.magnitude,thickness),colour);GUI.matrix=old;}
        private Vector2 RangePoint(float x,float z)
        {Vector3 p=_camera.WorldToScreenPoint(new Vector3(x,_p.World.RenderedSurfaceHeight(x,z)+1,z));return new Vector2(p.x,Screen.height-p.y)/Scale;}
        private void CoverageCircle(int cell,float radius,Color colour,float thickness=1.8f)
        {
            float cx=cell%_p.World.Width+.5f,cz=cell/_p.World.Width+.5f;CoverageLoop(cx,cz,radius,colour,thickness);
            if(_p.World.WrapLongitude){if(cx-radius<0)CoverageLoop(cx+_p.World.Width,cz,radius,colour,thickness);if(cx+radius>=_p.World.Width)CoverageLoop(cx-_p.World.Width,cz,radius,colour,thickness);}
        }
        private void CoverageLoop(float cx,float cz,float radius,Color colour,float thickness)
        {Vector2 last=Vector2.zero;for(int i=0;i<=96;i++){float a=i*Mathf.PI*2/96;var point=RangePoint(cx+Mathf.Cos(a)*radius,cz+Mathf.Sin(a)*radius);if(i>0)RangeLine(last,point,colour,thickness);last=point;}}
        private void DrawProtectionRange()
        {
            int cell=_p.QuickBuildActive?_p.HoverCell:_p.ContextCell;if(cell<0)return;bool placing=_p.Panel==CampaignPanel.Construction||_p.QuickBuildActive;var building=G.BuildingAt(cell);BuildingKind kind=placing?_p.SelectedBuilding:building!=null?building.kind:BuildingKind.City;
            if(!placing&&_p.Panel!=CampaignPanel.Information)return;int level=building!=null&&building.kind==kind?building.level:1;
            float radius=kind==BuildingKind.Factory?G.FactoryConnectionRadius:kind==BuildingKind.SAM?G.SamRadius(level):G.Rules.Building(kind).radius;if(radius<=0)return;
            CoverageCircle(cell,radius,kind==BuildingKind.Factory?new Color(.25f,.70f,1,.85f):new Color(.25f,.85f,.7f,.75f));
            if(kind==BuildingKind.SAM)
            {if(building!=null&&level<5)CoverageCircle(cell,G.SamRadius(level+1),new Color(.65f,.85f,.9f,.30f));if(building!=null)foreach(var missile in G.Projectiles)if(G.SamCovers(building,missile)){var point=Project(G.ProjectileCell(missile));FrontierUI.Fill(new Rect(point.x-3,point.y-3,6,6),FrontierUI.Gold);}}
            if(kind!=BuildingKind.Factory||!placing)return;G.PreviewFactoryConnections(G.PlayerFaction,cell);if(G.TradePreviewCell!=cell)return;
            foreach(var preview in G.TradePreview)if(preview.path!=null)
            {Vector2 last=RangePoint(preview.path[0]%_p.World.Width+.5f,preview.path[0]/_p.World.Width+.5f);for(int i=1;i<preview.path.Count;i++){int c=preview.path[i];var point=RangePoint(c%_p.World.Width+.5f,c/_p.World.Width+.5f);RangeLine(last,point,new Color(.85f,.95f,.9f,.8f),1);last=point;}}
        }
    }
}
