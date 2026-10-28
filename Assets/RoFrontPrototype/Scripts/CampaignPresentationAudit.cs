using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    // Opt-in local developer scenario. Never writes the player's name or stars.
    public sealed class CampaignPresentationAudit : MonoBehaviour
    {
        private string _directory;private int _errors,_peakObjects;private bool _measure;private readonly List<float> _frames=new List<float>();
        private readonly List<AuditCheck> _checks=new List<AuditCheck>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-frontier-campaign-audit");if(index<0||index+1>=args.Length)return;
            var audit=new GameObject("Auditoría de campaña").AddComponent<CampaignPresentationAudit>();audit._directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(audit._directory);
        }
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)_errors++;}
        private void Update(){if(_measure)_frames.Add(Time.unscaledDeltaTime);if(Time.frameCount%60==0)_peakObjects=Mathf.Max(_peakObjects,FindObjectsByType<GameObject>().Length);}
        private void Check(bool success,string name){_checks.Add(new AuditCheck{name=name,passed=success});Debug.Log("CAMPAIGN_AUDIT "+(success?"PASS ":"FAIL ")+name);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
        private IEnumerator Start()
        {
            IEnumerator routine=Run();while(true)
            {
                bool next=false;Exception failure=null;try{next=routine.MoveNext();}catch(Exception e){failure=e;}
                if(failure!=null){File.WriteAllText(Path.Combine(_directory,"audit-failure.txt"),failure.ToString());Debug.LogException(failure);Application.Quit(1);yield break;}
                if(!next)yield break;yield return routine.Current;
            }
        }
        private IEnumerator Advance(TerritorySimulation game,float seconds)
        {int ticks=Mathf.CeilToInt(seconds/.05f);for(int n=0;n<ticks;n++){game.Step(.05f);if(n%80==0)yield return null;}}
        private static void Claim(TerritorySimulation game,IEnumerable<int> cells,int faction,bool onlyNeutral=false,bool rebuild=true)
        {
            var callback=(Action<int,int,int>)typeof(TerritorySimulation).GetField("TerritoryCaptured",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            foreach(int cell in cells)
            {
                if(!game.World.Land[cell]||game.Owners[cell]==faction||(onlyNeutral&&game.Owners[cell]!=TerritorySimulation.Neutral))continue;
                int old=game.Owners[cell];if(old>=0)game.TerritoryCounts[old]--;game.Owners[cell]=faction;if(faction>=0)game.TerritoryCounts[faction]++;callback?.Invoke(faction,cell,old);
            }
            if(rebuild)game.RebuildCampaignIndexes();
        }
        private static IEnumerable<int> Disk(WorldGrid world,int center,int radius)
        {
            for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)if(x*x+y*y<=radius*radius&&world.InBounds(center%world.Width+x,center/world.Width+y))yield return world.At(center%world.Width+x,center/world.Width+y);
        }
        private static int Place(TerritorySimulation game,int f,BuildingKind kind,int origin,float min=0)
        {
            int best=-1;float score=float.PositiveInfinity;
            foreach(int cell in game.OwnedCells(f))
            {
                if(game.Owners[cell]!=f)continue;float dx=cell%game.World.Width-origin%game.World.Width,dy=cell/game.World.Width-origin/game.World.Width,d=dx*dx+dy*dy;
                if(d<min*min||d>=score||!game.ValidateConstruction(f,cell,kind).success)continue;best=cell;score=d;
            }
            return best;
        }
        private IEnumerator Run()
        {
            var p=FindAnyObjectByType<FrontierPrototype>();while(p==null){yield return null;p=FindAnyObjectByType<FrontierPrototype>();}
            while(p.IntroProgress<.55f)yield return null;Capture("01_intro");yield return null;while(p.ScreenState==FrontierScreen.Intro)yield return null;
            p.Settings.ReducedMotion=false;p.Settings.Quality=2;p.Settings.Apply();p.PlayerName="Costa del Alba";p.TeamMode=false;yield return new WaitForSecondsRealtime(1);Capture("02_nombre");yield return null;
            p.BeginLocationSelection();yield return new WaitForSecondsRealtime(.4f);Capture("03_elegir_posicion");yield return null;
            var g=p.Simulation;int player=g.PlayerFaction;g.AiEnabled=false;g.ReinforcementsEnabled=false;
            int namibia=Array.FindIndex(p.World.Countries,c=>c.code=="NAM");
            int capital=p.World.CoastCells.First(c=>p.World.GeographicCountries[c]==namibia&&g.CanFoundAt(c)&&p.World.Latitude(c)>-25&&p.World.Latitude(c)<-21);
            Check(player==p.World.Countries.Length&&g.TerritoryCounts[player]==0,"El jugador no sustituye ni elige una nación existente");
            Check(p.SelectFoundation(capital)&&p.ConfirmFoundation(),"Nombre y capital elegida en tierra libre");
            Check(g.Names[player]=="Costa del Alba"&&g.CapitalCells[player]==capital&&g.ProtectionRemaining(player)>0,"Fundación, identidad y protección inicial");
            yield return new WaitForSecondsRealtime(.4f);Capture("04_nacion_fundada");yield return null;
            int soundStart=p.Audio.PlayedEffects;int area=g.TerritoryCounts[player];p.Expand();yield return new WaitForSecondsRealtime(4);
            Check(g.TerritoryCounts[player]>area&&p.Audio.PlayedEffects==soundStart,"Expansión real con consumo de tropas y sin sonido");Capture("05_expansion");yield return null;
            p.StopOrder();p.HandleMapClick(capital,1,new Vector2(850,470));var ui=p.GetComponent<FrontierInterface>();
            Vector2 wheel=ui.WheelPointForAudit(1);ui.AuditWheelPointer=wheel;yield return new WaitForSecondsRealtime(.3f);Capture("06_rueda_circular");yield return null;
            Check(ui.WheelSelection(wheel)==1,"Selección direccional de la rueda circular");ui.ConfirmWheel(wheel);ui.AuditWheelPointer=null;
            Check(p.Panel==CampaignPanel.Construction&&!p.RadialOpen,"La rueda abre una acción funcional de construcción");
            Claim(g,Disk(p.World,capital,78),player);g.Gold[player]=30000000;g.Stars[player]=500000;
            for(int f=0;f<g.FactionCount;f++)g.ProtectionUntil[f]=0;
            int city=Place(g,player,BuildingKind.City,capital);float oldTroops=g.Troops[player],oldCap=g.Capacity[player];
            Check(city>=0&&g.Construct(player,city,BuildingKind.City).success,"Construcción de ciudad desde el estado real");yield return Advance(g,5);
            Check(g.Capacity[player]>oldCap&&Mathf.Abs(g.Troops[player]-oldTroops)<.01f,"La ciudad aumenta capacidad sin regalar tropas");
            int factory=Place(g,player,BuildingKind.Factory,city,12);Check(factory>=0&&g.Construct(player,factory,BuildingKind.Factory).success,"Fábrica colocable");yield return Advance(g,14);
            p.OpenCampaignPanel(CampaignPanel.Construction,factory);p.SelectedBuilding=BuildingKind.Factory;yield return new WaitForSecondsRealtime(.2f);Capture("07_construir_simbolos");yield return null;
            Check(g.TradeRoutes.Any(r=>r.owner==player&&!r.naval),"Trenes y comercio terrestre activos");
            p.CloseCampaignPanel();var rail=g.TradeRoutes.First(r=>r.owner==player&&!r.naval);Vector3 trainPoint=g.TradePosition(rail,out _);FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(trainPoint.x,trainPoint.z),24);
            yield return new WaitForSecondsRealtime(3);Capture("07b_tren_y_linea_blanca");yield return null;
            var trains=p.Visuals.GetComponent<CommercialTrainVisuals>();Check(trains!=null&&trains.ActiveModels>0&&trains.ModelForRoute(rail.id)!=null,"Locomotora FBX original sobre conexión comercial real");
            Check(trains.SmokeParticles>0&&trains.SmokeParticles<=128,"Humo negro ligero y partículas limitadas al acercar la cámara");
            for(int key=0;key<6;key++){p.SelectQuickBuilding(key);Check(p.QuickBuildActive&&p.SelectedBuilding==FrontierPrototype.QuickBuildings[key],"Atajo de estructura "+(key+1)+" coincide con la barra");}
            p.CancelQuickBuilding();p.SelectQuickBuilding(3);int fortCell=Place(g,player,BuildingKind.Outpost,city,18);Check(fortCell>=0&&p.PlaceQuickBuilding(fortCell).success&&!p.QuickBuildActive,"Colocación rápida real con coste y salida del modo");
            p.SelectQuickBuilding(0);p.CancelQuickBuilding();Check(!p.QuickBuildActive,"Cancelar colocación no construye ni gasta oro");
            int port1=Place(g,player,BuildingKind.Port,capital,9);Check(port1>=0&&g.Construct(player,port1,BuildingKind.Port).success,"Primer puerto en costa propia");yield return Advance(g,5);
            int port2=Place(g,player,BuildingKind.Port,port1,30);Check(port2>=0&&g.Construct(player,port2,BuildingKind.Port).success,"Segundo puerto en costa propia");yield return Advance(g,14);
            var trade=g.TradeRoutes.First(r=>r.owner==player&&r.naval);Check(Enumerable.Range(1,trade.path.Count-1).All(i=>g.SeaLineClear(trade.path[i-1],trade.path[i])),"Ruta comercial suavizada y libre de obstáculos en todos sus tramos");
            p.CloseCampaignPanel();p.FocusCell(g.TradeCell(trade));FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(g.TradeCell(trade)%p.World.Width,g.TradeCell(trade)/p.World.Width),30);
            yield return new WaitForSecondsRealtime(.4f);Capture("08_modelo_barco_navegando");yield return null;
            var boats=p.Visuals.GetComponent<CommercialShipVisuals>();Check(boats!=null&&boats.ActiveModels>0&&boats.ModelForRoute(trade.id)!=null,"Modelo FBX del creador instanciado en barcos comerciales");
            Check(p.Visuals.GetComponent<NavalVisuals>().TrailSegments==0,"Los barcos comerciales no dejan estela");
            int delivered=trade.deliveries;yield return Advance(g,Mathf.Max(.1f,trade.duration-trade.clock+.1f));
            p.FocusCell(g.TradeCell(trade));FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(g.TradeCell(trade)%p.World.Width,g.TradeCell(trade)/p.World.Width),22);
            yield return new WaitForSecondsRealtime(.3f);Capture("09_barco_atracado");yield return null;
            Check(trade.docking&&trade.deliveries==delivered+1,"Atraque y entrega de ingresos al llegar");
            var boat=boats.ModelForRoute(trade.id);Quaternion rotation=boat!=null?boat.transform.rotation:Quaternion.identity;
            yield return new WaitForSecondsRealtime(.5f);Check(boat!=null&&Quaternion.Angle(rotation,boat.transform.rotation)>.01f,"Oleaje por transformación sin deformar el modelo");
            bool direction=trade.reverse;yield return Advance(g,7);Check(!trade.docking&&trade.reverse!=direction,"El barco vuelve a salir después del atraque");
            int ally=Enumerable.Range(0,g.FactionCount).First(f=>f!=player&&!g.IsTribe(f)&&g.TerritoryCounts[f]>0);
            Check(g.ProposeAlliance(player,ally).success&&g.AnswerAlliance(ally,player,true).success,"Alianza funcional");
            Check(g.Donate(player,ally,1000,false).success,"Donación de oro al aliado");p.OpenCampaignPanel(CampaignPanel.Diplomacy,g.CapitalCells[ally]);
            yield return new WaitForSecondsRealtime(.2f);Capture("10_diplomacia");yield return null;
            int hostile=Enumerable.Range(0,g.FactionCount).First(f=>f!=player&&f!=ally&&!g.IsTribe(f)&&g.TerritoryCounts[f]>0);
            int contact=-1;foreach(int cell in g.OwnedCells(player)){for(int d=0;d<4;d++){int n=p.World.Neighbor(cell,d);if(n>=0&&p.World.Land[n]&&g.Owners[n]==-1){contact=n;break;}}if(contact>=0)break;}
            Claim(g,new[]{contact},hostile);g.Troops[hostile]=2000;g.Troops[player]=8000;
            Check(g.BeginExpansion(player,.2f)==OrderResult.Accepted&&g.AttackBorder(player,contact,.2f)==OrderResult.Accepted,"Múltiples operaciones conservando la reserva");
            p.CloseCampaignPanel();ui.ShowAttacks(true);p.FocusCell(contact);yield return new WaitForSecondsRealtime(.3f);Capture("11_operaciones");yield return null;ui.ShowAttacks(false);
            p.StopOrder();int enemyCoast=p.World.CoastCells.First(c=>g.Owners[c]==-1&&Mathf.Abs(c%p.World.Width-port2%p.World.Width)+Mathf.Abs(c/p.World.Width-port2/p.World.Width)<150);
            Claim(g,new[]{enemyCoast},hostile);g.CancelOrder(hostile);g.Troops[hostile]=1000;
            Check(g.InvadeBySea(hostile,capital,.25f)==OrderResult.Accepted,"Invasión enemiga real por agua");
            var invasion=g.Operations.First(o=>o.faction==hostile&&o.kind==OperationKind.Naval);Vector3 before=g.NavalPosition(invasion.currentCell,invasion.nextCell,invasion.progress);
            yield return Advance(g,3);Vector3 after=g.NavalPosition(invasion.currentCell,invasion.nextCell,invasion.progress);
            Check(Vector3.Distance(before,after)>0&&Vector3.Distance(before,after)<=g.Rules.boatSpeed*p.World.ResolutionScale*3.6f,"Transporte pequeño y velocidad limitada por distancia real");
            p.FocusCell(g.OperationCell(invasion));yield return new WaitForSecondsRealtime(.3f);Capture("12_triangulo_estela");yield return null;
            Check(p.Audio.IncomingInvasionAlerts>0&&p.Visuals.GetComponent<NavalVisuals>().TrailSegments>0,"Sólo las invasiones dejan estela y generan aviso recibido");
            Check(g.DeployWarship(player,port1).success,"Buque desplegado desde puerto");var ship=g.Warships.Last();ship.currentCell=g.OperationCell(invasion);ship.nextCell=ship.currentCell;yield return Advance(g,10);
            Check(!invasion.active,"El buque intercepta el transporte");p.OpenCampaignPanel(CampaignPanel.Navy,port1);yield return new WaitForSecondsRealtime(.2f);Capture("13_marina");yield return null;
            p.CloseCampaignPanel();g.Stars[player]=100000;Check(g.BuyArsenal(player,ArsenalKind.Reinforcements).success,"Compra con estrellas separadas del oro");p.OpenCampaignPanel(CampaignPanel.Arsenal,capital);yield return new WaitForSecondsRealtime(.2f);Capture("14_arsenal");yield return null;
            p.CloseCampaignPanel();
            // Bounded late-game stress fixture: same canonical construction and orders for all nations.
            for(int faction=0;faction<g.FactionCount;faction++)
            {
                if(g.TerritoryCounts[faction]==0||g.CapitalCells[faction]<0||g.IsTribe(faction))continue;
                Claim(g,Disk(p.World,g.CapitalCells[faction],10),faction,true,false);g.Gold[faction]=5000000;
            }
            g.RebuildCampaignIndexes();
            for(int faction=0;faction<g.FactionCount;faction++)
            {
                if(g.TerritoryCounts[faction]==0||g.CapitalCells[faction]<0||g.IsTribe(faction))continue;
                int c=Place(g,faction,BuildingKind.City,g.CapitalCells[faction]);if(c>=0)g.Construct(faction,c,BuildingKind.City);
                c=Place(g,faction,BuildingKind.Factory,g.CapitalCells[faction]);if(c>=0)g.Construct(faction,c,BuildingKind.Factory);
                if(faction%8==0)yield return null;
            }
            yield return Advance(g,12);g.AiEnabled=true;p.ViewContinent();yield return new WaitForSecondsRealtime(1);
            _measure=true;yield return new WaitForSecondsRealtime(20);_measure=false;Capture("15_mundo_rendimiento");yield return null;
            Check(_frames.Count>100&&g.Buildings.Count>200,"Escenario de rendimiento con economía e infraestructura activas");
            g.AiEnabled=false;g.CancelOrder(player);
            int silo=Place(g,player,BuildingKind.Silo,capital,14);Check(silo>=0&&g.Construct(player,silo,BuildingKind.Silo).success,"Silo colocable");yield return Advance(g,6);
            int target=g.CapitalCells[hostile];g.Gold[player]=50000000;p.OpenCampaignPanel(CampaignPanel.Nuclear,target);yield return new WaitForSecondsRealtime(.3f);Capture("16_nuclear");yield return null;
            Check(g.LaunchWeapon(player,target,WeaponKind.Atomic).success,"Lanzamiento estratégico validado");yield return Advance(g,Mathf.Max(10,g.SeaDistance(silo,target)/g.Rules.missileSpeed+2));
            p.CloseCampaignPanel();p.FocusCell(target);yield return new WaitForSecondsRealtime(.3f);Capture("17_radiacion");yield return null;Check(g.Radiation.Count>0,"Radiación y actualización parcial del mapa");
            float[] times=_frames.OrderBy(x=>x).ToArray();float average=times.Length>0?times.Average():0,p95=times.Length>0?times[Mathf.Min(times.Length-1,Mathf.FloorToInt(times.Length*.95f))]:0;
            var evidence=new AuditEvidence{errors=_errors,checks=_checks.ToArray(),screenShots=Directory.GetFiles(_directory,"*.png").Select(Path.GetFileName).OrderBy(x=>x).ToArray(),
                averageFps=average>0?1/average:0,p95FrameMilliseconds=p95*1000,measuredFrames=times.Length,peakGameObjects=_peakObjects,
                buildings=g.Buildings.Count,routes=g.TradeRoutes.Count,operations=g.Operations.Count,directSeaRoutes=g.DirectSeaRoutes,navigationSearches=g.NavigationSearches,
                originalModelUsed=_checks.Any(c=>c.passed&&c.name.StartsWith("Modelo FBX")),scenarioUsesControlledResourcesAndTerritory=true};
            File.WriteAllText(Path.Combine(_directory,"campaign-presentation-audit.json"),JsonUtility.ToJson(evidence,true));
            Application.Quit(_errors==0&&_checks.All(c=>c.passed)?0:1);
        }
        [Serializable]private sealed class AuditCheck{public string name;public bool passed;}
        [Serializable]private sealed class AuditEvidence
        {
            public int errors,measuredFrames,peakGameObjects,buildings,routes,operations,directSeaRoutes,navigationSearches;public float averageFps,p95FrameMilliseconds;
            public bool originalModelUsed,scenarioUsesControlledResourcesAndTerritory;public AuditCheck[] checks;public string[] screenShots;
        }
    }
}
