using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class TacticalPresentationAudit:MonoBehaviour
    {
        private string _directory;private int _errors;private readonly List<string> _checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-frontier-tactical-audit");if(index<0||index+1>=args.Length)return;
            var audit=new GameObject("Prueba de combate y mar").AddComponent<TacticalPresentationAudit>();audit._directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(audit._directory);
        }
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)_errors++;}
        private void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);_checks.Add(name);Debug.Log("TACTICAL_AUDIT PASS "+name);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
        private IEnumerator Start()
        {
            var routine=Run();while(true){bool next=false;Exception failure=null;try{next=routine.MoveNext();}catch(Exception error){failure=error;}if(failure!=null){File.WriteAllText(Path.Combine(_directory,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return routine.Current;}
        }
        private IEnumerator Advance(TerritorySimulation game,float seconds)
        {for(int t=0;t<Mathf.CeilToInt(seconds/.1f);t++){game.Step(.1f);if(t%8==0)yield return null;}}
        private static void Claim(TerritorySimulation game,int cell,int owner)
        {
            if(!game.World.Land[cell]||game.Owners[cell]==owner)return;int old=game.Owners[cell];if(old>=0)game.TerritoryCounts[old]--;game.Owners[cell]=owner;game.TerritoryCounts[owner]++;
            var callback=(Action<int,int,int>)typeof(TerritorySimulation).GetField("TerritoryCaptured",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);callback?.Invoke(owner,cell,old);
        }
        private IEnumerator Run()
        {
            var p=FindAnyObjectByType<FrontierPrototype>();while(p==null){yield return null;p=FindAnyObjectByType<FrontierPrototype>();}p.FinishIntro();while(p.ScreenState==FrontierScreen.Intro)yield return null;
            p.AuditManualSimulation=true;p.PlayerName="Costa del Alba";p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;int f=g.PlayerFaction;
            int nation=Array.FindIndex(p.World.Countries,c=>c.code=="NAM");int capital=p.World.CoastCells.First(c=>p.World.GeographicCountries[c]==nation&&g.CanFoundAt(c)&&p.World.Latitude(c)>-25&&p.World.Latitude(c)<-21);
            Check(p.SelectFoundation(capital)&&p.ConfirmFoundation()&&g.AvailableTroops(f)==10000,"Nación nueva con 10000 tropas");var camera=FindAnyObjectByType<FrontierCamera>();camera.Focus(new Vector2(capital%p.World.Width+10,capital/p.World.Width),42);
            yield return new WaitForSecondsRealtime(1.5f);
            int target=p.World.At(capital%p.World.Width+40,capital/p.World.Width),sounds=p.Audio.PlayedEffects;p.SendFraction=.75f;
            p.HandleMapClick(target,0,new Vector2(900,420));Check(g.AvailableTroops(f)==10000,"Un clic no compromete tropas");yield return new WaitForSecondsRealtime(.1f);p.HandleMapClick(target,0,new Vector2(901,420));
            Check(g.AvailableTroops(f)==625&&g.CommittedTroops[f]==1875,"El doble clic separa de verdad 1875 soldados de la reserva");yield return new WaitForSecondsRealtime(.3f);Capture("01_reserva_y_envio");yield return null;
            var advance=Advance(g,22);while(advance.MoveNext())yield return advance.Current;
            Check(!g.Operations.Any(o=>o.active&&o.faction==f)&&g.AvailableTroops(f)==625,"La expansión se agota sin gastar la reserva restante");Check(p.Audio.PlayedEffects==sounds,"Las órdenes propias no añaden sonidos");p.Notify("El envío se agotó. Los 625 soldados de reserva siguen disponibles.");yield return new WaitForSecondsRealtime(.4f);Capture("02_mancha_organica");yield return null;
            int cx=capital%p.World.Width,cy=capital/p.World.Width;
            for(int y=-10;y<=10;y++)for(int x=0;x<=65;x++)Claim(g,p.World.At(cx+x,cy+y),x<=40?f:nation);g.RebuildCampaignIndexes();g.Troops[f]=100000;g.Troops[nation]=48000;g.ProtectionUntil[nation]=0;p.SendFraction=.5f;
            target=p.World.At(cx+41,cy);Check(p.SendAttack(target)==OrderResult.Accepted,"Ofensiva contra una nación vecina");var battle=g.Operations.First(o=>o.faction==f);advance=Advance(g,2.4f);while(advance.MoveNext())yield return advance.Current;
            Check(Mathf.Abs(battle.troops-2000)<1&&g.AvailableTroops(nation)<.1f&&g.AvailableTroops(f)==50000,"50 mil contra 48 mil: 2 mil en el frente y 50 mil en casa");camera.Focus(new Vector2(cx+35,cy),35);p.Notify("48.000 bajas por lado · quedan 2.000 atacantes y 50.000 de reserva.");yield return new WaitForSecondsRealtime(.4f);Capture("03_bajas_reales");yield return null;
            advance=Advance(g,1);while(advance.MoveNext())yield return advance.Current;Check(battle.troops<2000&&g.AvailableTroops(f)==50000,"La ocupación consume supervivientes sin devolverlos todavía");p.StopOrder();
            // Controlled coastal infrastructure to reach the naval presentation without playing a full campaign.
            int[] ports=p.World.CoastCells.Where(c=>p.World.GeographicCountries[c]==nation&&Mathf.Abs(c%p.World.Width-cx)<45&&Mathf.Abs(c/p.World.Width-cy)<90).OrderBy(c=>Mathf.Abs(c/p.World.Width-cy)).ToArray();
            int portA=ports.First(),portB=ports.First(c=>Mathf.Abs(c/p.World.Width-portA/p.World.Width)>30);
            foreach(int port in new[]{portA,portB})for(int dy=-5;dy<=5;dy++)for(int dx=-5;dx<=5;dx++)Claim(g,p.World.At(port%p.World.Width+dx,port/p.World.Width+dy),f);
            g.RebuildCampaignIndexes();g.Gold[f]=5000000;
            Check(g.Construct(f,portA,BuildingKind.Port).success&&g.Construct(f,portB,BuildingKind.Port).success,"Dos puertos en costas reales");advance=Advance(g,6);while(advance.MoveNext())yield return advance.Current;
            for(int t=0;t<200&&!g.TradeRoutes.Any(r=>r.naval);t++){g.Step(.1f);yield return null;}
            Check(g.TradeRoutes.Any(r=>r.naval),"Puertos terminados generan comercio automáticamente");var route=g.TradeRoutes.First(r=>r.naval);camera.Focus(new Vector2(route.path[0]%p.World.Width,route.path[0]/p.World.Width),24);
            yield return new WaitForSecondsRealtime(.5f);var commerce=p.Visuals.GetComponent<CommercialShipVisuals>();Check(commerce!=null&&commerce.ActiveModels>0,"Barcos comerciales visibles con el modelo original");Capture("04_puertos_y_comercio");yield return null;
            g.Troops[f]=10000;target=g.World.At(route.path[0]%g.World.Width-24,route.path[0]/g.World.Width);Check(!g.World.Land[target],"Destino de invasión frente a la costa");
            Check(p.SendBoat(target)==OrderResult.Accepted,"Transporte de invasión con tropas reales");advance=Advance(g,1.5f);while(advance.MoveNext())yield return advance.Current;
            var navalOp=g.Operations.First(o=>o.active&&o.faction==f&&o.kind==OperationKind.Naval);int cell=g.OperationCell(navalOp);camera.Focus(new Vector2(cell%p.World.Width,cell/p.World.Width),16);
            yield return new WaitForSecondsRealtime(.5f);var naval=p.Visuals.GetComponent<NavalVisuals>();Check(naval.UsesOriginalBoat&&naval.ActiveModels>0,"La invasión muestra un barco y no un triángulo");
            var mesh=(Mesh)typeof(NavalVisuals).GetField("_mesh",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(naval);Check(naval.TrailSegments>0&&mesh.vertexCount==naval.TrailSegments*6,"La malla militar sólo contiene estelas, sin marcadores triangulares");Capture("05_barco_militar_y_oleaje");yield return null;
            yield return new WaitForSecondsRealtime(.8f);Capture("06_mar_en_movimiento");yield return null;p.Settings.ReducedMotion=true;p.Visuals.ApplySettings();yield return new WaitForSecondsRealtime(.3f);Capture("07_movimiento_reducido");yield return null;
            Check(_errors==0,"Recorrido sin errores");File.WriteAllText(Path.Combine(_directory,"tactical-presentation.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=_errors,checks=_checks.ToArray(),objects=FindObjectsByType<GameObject>().Length,militaryModels=naval.ActiveModels,commercialModels=commerce.ActiveModels,controlledResourcesAndTerritory=true},true));
        }
        [Serializable]private sealed class Report{public bool inEditor,controlledResourcesAndTerritory;public int errors,objects,militaryModels,commercialModels;public string[] checks;}
    }
}
