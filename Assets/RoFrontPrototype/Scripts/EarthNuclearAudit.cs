using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class EarthNuclearAudit:MonoBehaviour
    {
        private string folder;private int errors;private readonly List<string> checks=new List<string>();
        private FrontierPrototype p;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void Bootstrap()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-frontier-earth-nuclear-audit");if(i<0||i+1>=args.Length)return;var a=new GameObject("Prueba de Tierra y alerta nuclear").AddComponent<EarthNuclearAudit>();a.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(a.folder);}
        private void OnEnable()=>Application.logMessageReceived+=Log;
        private void OnDisable()=>Application.logMessageReceived-=Log;
        private void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);checks.Add(text);Debug.Log("EARTH_NUCLEAR PASS "+text);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        private IEnumerator Start()
        {var run=Run();while(true){bool next=false;Exception failure=null;try{next=run.MoveNext();}catch(Exception e){failure=e;}if(failure!=null){File.WriteAllText(Path.Combine(folder,"failure.txt"),failure.ToString());Debug.LogException(failure);yield break;}if(!next)yield break;yield return run.Current;}}
        private IEnumerator Run()
        {
            yield return null;p=FindAnyObjectByType<FrontierPrototype>();p.AuditManualSimulation=true;p.Settings.ReducedMotion=false;p.PlayerName="Atlas";p.ShowMainMenu();yield return new WaitForSecondsRealtime(1);
            var ui=FindAnyObjectByType<FrontierInterface>();var cinema=(CinematicPresentation)typeof(FrontierInterface).GetField("_cinematic",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);var earth=cinema.Earth;
            Check(earth.Ready&&earth.RenderedFrames>0,"Shader de globo real importado y renderizado");float angle=earth.RotationDegrees;Capture("01_tierra_menu");yield return new WaitForSecondsRealtime(8);Capture("02_tierra_girando");
            Check(Mathf.Abs(Mathf.DeltaAngle(angle,earth.RotationDegrees))>8,"Continentes rotan con tiempo real en el menú");
            p.Settings.ReducedMotion=true;yield return null;yield return null;angle=earth.RotationDegrees;yield return new WaitForSecondsRealtime(.3f);Check(earth.RotationDegrees==angle,"Movimiento reducido detiene Tierra y nubes");p.Settings.ReducedMotion=false;
            var samples=new List<float>();float began=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-began<15){samples.Add(Time.unscaledDeltaTime*1000);yield return null;}
            p.BeginLocationSelection();var g=p.Simulation;g.AiEnabled=false;g.ReinforcementsEnabled=false;int center=p.World.StartCells[Array.FindIndex(p.World.Countries,c=>c.code=="ZWE")];int start=Enumerable.Range(12,70).Select(n=>center+n).First(c=>g.CanFoundAt(c));p.SelectFoundation(start);Check(p.ConfirmFoundation(),"Partida fundada para probar amenazas");Array.Clear(g.ProtectionUntil,0,g.FactionCount);int f=g.PlayerFaction;int enemy=Enumerable.Range(0,p.World.Countries.Length).First(n=>n!=f&&g.CapitalCells[n]>=0);
            var nav=FindAnyObjectByType<FrontierCamera>();nav.Focus(new Vector2(start%p.World.Width,start/p.World.Width),75);int rendered=earth.RenderedFrames;yield return new WaitForSecondsRealtime(.3f);Check(earth.RenderedFrames==rendered,"Fondo no se renderiza durante la partida");
            Check(g.LaunchWeapon(enemy,start,WeaponKind.Hydrogen,true).success,"Lanzamiento real enemigo contra jugador");p.RefreshNuclearWarning();Check(p.IncomingNuclearCount==1&&p.IncomingNuclearOwner==enemy,"Solo el objetivo humano recibe WARNING y ETA");yield return new WaitForSecondsRealtime(.3f);Capture("03_warning_rojo");yield return new WaitForSecondsRealtime(.25f);Capture("04_warning_parpadeo");
            g.Projectiles[0].active=false;p.RefreshNuclearWarning();Check(p.IncomingNuclearCount==0,"Cancelar o interceptar elimina el aviso sin esperar al impacto");g.Projectiles.Clear();
            int other=Enumerable.Range(0,p.World.Countries.Length).First(n=>n!=enemy&&n!=f&&g.CapitalCells[n]>=0);int target=g.CapitalCells[other];Check(g.LaunchWeapon(enemy,target,WeaponKind.Hydrogen,true).success,"Lanzamiento real contra tercer país");p.RefreshNuclearWarning();Check(p.IncomingNuclearCount==0,"Misil hacia otro país no muestra WARNING al jugador");
            nav.Focus(new Vector2(target%p.World.Width,target/p.World.Width),115);yield return new WaitForSecondsRealtime(.3f);Vector3 original=nav.transform.position;int shakes=nav.NuclearShakes;var missile=g.Projectiles.Last();missile.age=missile.duration-.05f;g.Step(.1f);yield return new WaitForSecondsRealtime(.08f);Capture("05_explosion_y_sacudida");
            Check(nav.NuclearShakes==shakes+1&&nav.ShakeOffset>0,"Impacto nuclear ajeno sacude la cámara");var visuals=FindAnyObjectByType<StrategicMissileVisuals>();Check(visuals.NuclearBursts>0&&g.Radiation.Count>0,"Impacto produce nube nuclear y conserva radiación verde");yield return new WaitForSecondsRealtime(.7f);Capture("06_nube_nuclear");yield return new WaitForSecondsRealtime(2);Check(nav.ShakeOffset==0&&Vector3.Distance(original,nav.transform.position)<.001f,"Sacudida termina sin deriva de cámara ni cambio de zoom");
            p.Settings.ReducedMotion=true;nav.ReducedMotion=true;shakes=nav.NuclearShakes;int bursts=visuals.NuclearBursts;Check(g.LaunchWeapon(enemy,target,WeaponKind.Hydrogen,true).success,"Segundo impacto de prueba válido");missile=g.Projectiles.Last();missile.age=missile.duration-.05f;g.Step(.1f);yield return null;Check(nav.NuclearShakes==shakes&&visuals.NuclearBursts==bursts,"Movimiento reducido evita sacudida y destello");
            p.Settings.ReducedMotion=false;p.ShowMainMenu();yield return new WaitForSecondsRealtime(.3f);samples.Sort();Check(errors==0,"Recorrido gráfico sin errores de Unity");File.WriteAllText(Path.Combine(folder,"earth-nuclear.json"),JsonUtility.ToJson(new Report{inEditor=Application.isEditor,errors=errors,checks=checks.ToArray(),fps=samples.Count*1000/samples.Sum(),p95Ms=samples[(int)(samples.Count*.95f)],width=Screen.width,height=Screen.height},true));Debug.Log("EARTH_NUCLEAR_COMPLETE");
        }
        [Serializable]private sealed class Report{public bool inEditor;public int errors,width,height;public float fps,p95Ms;public string[] checks;}
    }
}
