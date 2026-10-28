using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class QuickMatchAudit:MonoBehaviour
    {
        private string folder;private FrontierPrototype p;private int errors;private readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-quick-audit");if(i<0||i+1>=args.Length)return;var audit=new GameObject("Prueba de modos, red local e impacto nuclear").AddComponent<QuickMatchAudit>();audit.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(audit.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Assert||kind==LogType.Exception)errors++;}
        private void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("QUICK_AUDIT PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private static void Claim(TerritorySimulation g,int owner,int center,int radius)
        {
            var changed=(Action<int,int,int>)typeof(TerritorySimulation).GetField("TerritoryCaptured",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(g);
            for(int y=-radius;y<=radius;y++)for(int x=-radius;x<=radius;x++)
            {if(x*x+y*y>radius*radius)continue;int px=center%g.World.Width+x,py=center/g.World.Width+y;if(!g.World.InBounds(px,py))continue;int c=g.World.At(px,py);if(!g.World.Land[c]||g.Owners[c]==owner)continue;int previous=g.Owners[c];if(previous>=0)g.TerritoryCounts[previous]--;g.Owners[c]=owner;g.TerritoryCounts[owner]++;changed?.Invoke(owner,c,previous);}
        }
        private Building Build(TerritorySimulation g,int cell,BuildingKind kind)
        {Check(g.Construct(g.PlayerFaction,cell,kind).success,"Construcción de prueba: "+kind);return g.BuildingAt(cell);}
        private IEnumerator Tick(TerritorySimulation g,int ticks){for(int i=0;i<ticks;i++){g.Step(.1f);if(i%3==0)yield return null;}}
        private static bool Linked(TerritorySimulation g,Building a,Building b)=>g.TradeRoutes.Any(r=>!r.naval&&(r.fromBuilding==a.id&&r.toBuilding==b.id||r.fromBuilding==b.id&&r.toBuilding==a.id));
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception failure=null;try{next=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return run.Current;}}
        private IEnumerator Run()
        {
            yield return null;p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.Settings.ReducedMotion=false;p.PlayerName="Atlas";p.ShowMainMenu();yield return new WaitForSecondsRealtime(.5f);Capture("01_menu_dos_modos");yield return null;
            p.ChosenMode=MatchMode.Campaign;p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;int seed=p.World.StartCells[Array.FindIndex(p.World.Countries,c=>c.code=="MLI")],center=Enumerable.Range(12,100).Select(n=>seed+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(center);Check(p.ConfirmFoundation(),"Fundación de campaña");int f=g.PlayerFaction;Claim(g,f,center,110);g.RebuildCampaignIndexes();g.Gold[f]=100000000;Array.Clear(g.ProtectionUntil,0,g.FactionCount);
            var a=Build(g,center,BuildingKind.Factory);var b=Build(g,center+20,BuildingKind.Factory);var c=Build(g,center+40,BuildingKind.Factory);var city=Build(g,center+60,BuildingKind.City);Build(g,center+p.World.Width*24,BuildingKind.Silo);yield return Tick(g,240);
            Check(Linked(g,a,b)&&Linked(g,b,c)&&Linked(g,c,city)&&!Linked(g,a,city),"En la Tierra real las fábricas forman una cadena de alcance local");var camera=FindAnyObjectByType<FrontierCamera>();camera.Focus(new Vector2(center%p.World.Width+25,center/p.World.Width),75);p.OpenCampaignPanel(CampaignPanel.Information,b.cell);yield return new WaitForSecondsRealtime(.5f);Capture("02_radio_local_fabricas");yield return null;
            int target=center+70;p.OpenCampaignPanel(CampaignPanel.Nuclear,target);camera.Focus(new Vector2(target%p.World.Width,target/p.World.Width),75);double gold=g.Gold[f];Check(p.PreviewStrategicWeapon(WeaponKind.Atomic).success&&p.WeaponAim.FriendlyRisk,"Previsualización detecta edificios y tierra propios en el radio");Check(!p.ConfirmStrategicWeapon().success&&g.Projectiles.Count==0&&g.Gold[f]==gold,"Primer clic no dispara ni cobra");yield return new WaitForSecondsRealtime(.65f);Capture("03_radio_y_confirmacion");yield return null;Check(p.ConfirmStrategicWeapon().success&&g.Projectiles.Count==1,"Segunda confirmación crea exactamente un cohete");p.CloseCampaignPanel();var missile=g.Projectiles[0];missile.age=missile.duration-.05f;g.Step(.1f);yield return new WaitForSecondsRealtime(.7f);Capture("04_explosion_nuclear");yield return new WaitForSecondsRealtime(3);yield return Tick(g,10);Check(city.destroyed&&g.IsIrradiated(target)&&g.NuclearScars[target]==0,"Daño propio y centro rojo de contaminación real");Capture("05_rojo_amarillo_naranja_verde");yield return null;
            p.ShowMainMenu();p.ChosenMode=MatchMode.QuickMatch;p.BeginLocationSelection();g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;seed=p.World.StartCells[Array.FindIndex(p.World.Countries,q=>q.code=="SEN")];center=p.World.CoastCells.Where(q=>g.CanFoundAt(q)).OrderBy(q=>g.SeaDistance(q,seed)).First();p.SelectFoundation(center);Check(p.ConfirmFoundation(),"Partida rápida iniciada desde el menú");f=g.PlayerFaction;
            Check(g.IsQuickMatch&&g.OwnedCells(f).Any()&&g.Options.tribeCount==160&&Enumerable.Range(0,p.World.Countries.Length).Count(n=>g.TerritoryCounts[n]>0)==87,"88 naciones contando al jugador y 160 tribus en la Tierra");
            Check(p.Visuals.GetComponent<StructureModelVisuals>()==null&&p.Visuals.GetComponent<CommercialTrainVisuals>()==null&&p.Visuals.GetComponent<StrategicMissileVisuals>()==null&&p.Visuals.GetComponent<NavalVisuals>().UsesOriginalBoat,"Solo se utiliza el modelo original del barco en partida rápida");
            Claim(g,f,center,90);g.RebuildCampaignIndexes();g.Gold[f]=10000000;int factoryCell=center+10;var factory=Build(g,factoryCell,BuildingKind.Factory);Build(g,center+24,BuildingKind.City);var firstPort=Build(g,center,BuildingKind.Port);int secondPort=p.World.CoastCells.Where(q=>g.Owners[q]==f&&g.SeaDistance(q,center)>25&&g.SeaDistance(q,center)<85&&g.ValidateConstruction(f,q,BuildingKind.Port).success).OrderByDescending(q=>g.SeaDistance(q,center)).First();Build(g,secondPort,BuildingKind.Port);yield return Tick(g,300);
            Check(g.TradeRoutes.Any(r=>r.naval)&&g.TradeRoutes.Any(r=>!r.naval),"Puertos y fábrica del modo rápido generan rutas reales");camera.Focus(new Vector2(center%p.World.Width+15,center/p.World.Width),95);yield return new WaitForSecondsRealtime(.6f);Capture("06_hud_rapida");yield return null;
            p.OpenCampaignPanel(CampaignPanel.Information,factoryCell);yield return new WaitForSecondsRealtime(.3f);Capture("07_simbolos_y_conexiones");yield return null;p.CloseCampaignPanel();if(Application.isEditor)yield return Tick(g,300);g.ProtectionUntil[f]=1000000;g.AiEnabled=true;g.ReinforcementsEnabled=true;g.CampaignSpeed=5;p.AuditManualSimulation=false;
            float began=Time.realtimeSinceStartup,sim=g.ElapsedTime,duration=Application.isEditor?8:300,next=began+30;var frames=new List<float>();var late=new List<float>();int peakTrains=0,peakRoutes=0;
            while(Time.realtimeSinceStartup-began<duration)
            {
                float seconds=Time.realtimeSinceStartup-began;frames.Add(Time.unscaledDeltaTime*1000);if(seconds>180)late.Add(Time.unscaledDeltaTime*1000);peakTrains=Math.Max(peakTrains,g.Trains.Count);peakRoutes=Math.Max(peakRoutes,g.TradeRoutes.Count);
                if(Time.realtimeSinceStartup>=next){next+=30;File.WriteAllText(Path.Combine(folder,"progreso.json"),"{\"seconds\":"+Mathf.FloorToInt(seconds)+",\"errors\":"+errors+",\"routes\":"+g.TradeRoutes.Count+"}");}yield return null;
            }
            p.AuditManualSimulation=true;Check(g.Outcome==CampaignOutcome.Playing&&g.Ledgers[f].landTrade>0&&g.Ledgers[f].seaTrade>0,"Partida continua con entregas terrestres y marítimas después de la sesión");Check(errors==0,"Sin errores de ejecución ni shaders");Capture("08_rapida_tras_sesion");yield return new WaitForEndOfFrame();frames.Sort();late.Sort();float elapsed=Time.realtimeSinceStartup-began;
            File.WriteAllText(Path.Combine(folder,"quick-match-native.json"),JsonUtility.ToJson(new Report{seconds=elapsed,fps=frames.Count*1000/frames.Sum(),fpsAfterThreeMinutes=late.Count>0?late.Count*1000/late.Sum():0,p95Ms=frames[(int)(frames.Count*.95f)],effectiveSpeed=(g.ElapsedTime-sim)/elapsed,errors=errors,inEditor=Application.isEditor,peakRoutes=peakRoutes,peakTrains=peakTrains,checks=checks.ToArray()},true));Debug.Log("QUICK_NATIVE_COMPLETE");yield return null;if(!Application.isEditor)Application.Quit();
        }
        [Serializable]private sealed class Report{public float seconds,fps,fpsAfterThreeMinutes,p95Ms,effectiveSpeed;public int errors,peakRoutes,peakTrains;public bool inEditor;public string[] checks;}
    }
}
