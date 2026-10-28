using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FrontierAtlas
{
    // Explicit developer CLI only; captures the actual player framebuffer including UI.
    public sealed class PresentationAudit : MonoBehaviour
    {
        private string _directory;
        private int _errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-frontier-presentation-audit");
            if(flag<0||flag+1>=args.Length)return;
            var audit=new GameObject("Presentation audit").AddComponent<PresentationAudit>();
            audit._directory=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(audit._directory);
        }
        private void OnEnable()=>Application.logMessageReceived+=OnLog;
        private void OnDisable()=>Application.logMessageReceived-=OnLog;
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors++;}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(_directory,name+".png"));
        private IEnumerator Start()
        {
            var p=FindAnyObjectByType<FrontierPrototype>();while(p==null){yield return null;p=FindAnyObjectByType<FrontierPrototype>();}
            while(p.IntroProgress<.54f)yield return null;Capture("01_intro");yield return new WaitForSecondsRealtime(.2f);
            while(p.ScreenState==FrontierScreen.Intro)yield return null;
            yield return new WaitForSecondsRealtime(1);Capture("02_menu");yield return new WaitForSecondsRealtime(.2f);
            p.ShowCountries();yield return new WaitForSecondsRealtime(.5f);Capture("03_paises");yield return new WaitForSecondsRealtime(.2f);
            p.ShowSettings();yield return new WaitForSecondsRealtime(.5f);Capture("04_audio");yield return new WaitForSecondsRealtime(.2f);
            p.GetComponent<FrontierInterface>().SelectSettingsTab(1);yield return new WaitForSecondsRealtime(.5f);Capture("05_imagen");yield return new WaitForSecondsRealtime(.2f);
            p.GetComponent<FrontierInterface>().SelectSettingsTab(2);yield return new WaitForSecondsRealtime(.5f);Capture("06_controles");yield return new WaitForSecondsRealtime(.2f);
            p.CloseSettings();
            float priorMaster=p.Settings.Master;
            p.Settings.Master=priorMaster>.5f? .37f:.73f;p.Settings.Save();
            var loaded=new FrontierSettings();loaded.Load();
            bool persistence=Mathf.Abs(loaded.Master-p.Settings.Master)<.001f&&loaded.ReducedMotion==p.Settings.ReducedMotion&&loaded.Quality==p.Settings.Quality;
            p.Settings.Master=priorMaster;p.Settings.Save();p.Audio.ApplyVolumes();
            bool priorMotion=p.Settings.ReducedMotion;p.Settings.ReducedMotion=true;p.Visuals.ApplySettings();
            bool reducedMotionWorks=p.Visuals.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Motion")==0;
            p.Settings.ReducedMotion=priorMotion;p.Visuals.ApplySettings();
            p.StartMatch(Array.FindIndex(p.World.Countries,c=>c.code=="ZWE"));
            yield return new WaitForSecondsRealtime(.5f);Capture("07_partida");yield return new WaitForSecondsRealtime(.2f);
            int player=p.Simulation.PlayerFaction;
            int target=FreeContact(p.Simulation,player);float preview=p.PreviewTroops;
            int quietStart=p.Audio.PlayedEffects;
            p.HandleMapClick(target,1,new Vector2(860,470));
            bool radialWorks=p.RadialOpen&&!p.Simulation.HasOrder(player);
            yield return new WaitForSecondsRealtime(.3f);Capture("08_radial");yield return new WaitForSecondsRealtime(.2f);
            p.HandleMapClick(target,0,new Vector2(860,470));
            radialWorks&=!p.RadialOpen&&!p.Simulation.HasOrder(player);
            preview=p.PreviewTroops;
            p.HandleMapClick(target,0,new Vector2(860,470));
            bool singleClickWorks=p.Simulation.HasOrder(player)&&Mathf.Abs(p.Simulation.CommittedTroops[player]-preview)<.01f;
            yield return new WaitForSecondsRealtime(8);Capture("08_expansion");yield return new WaitForSecondsRealtime(.2f);
            p.Audio.Play("click");p.Audio.Play("order");p.Audio.Play("promotion");p.Audio.Expansion();
            bool quietExpansion=p.Audio.PlayedEffects==quietStart;
            for(int step=0;step<2400;step++){p.Simulation.Step(.05f);if(step%40==0)yield return null;}
            p.Visuals.Powers.Update(true);p.ViewContinent();yield return new WaitForSecondsRealtime(.5f);Capture("09_mundo");yield return new WaitForSecondsRealtime(.2f);
            p.GetComponent<FrontierInterface>().ShowPowers(true);yield return new WaitForSecondsRealtime(.3f);Capture("09_potencias");yield return new WaitForSecondsRealtime(.2f);
            p.GetComponent<FrontierInterface>().ShowPowers(false);
            p.ShowPause();yield return new WaitForSecondsRealtime(.4f);Capture("10_pausa");yield return new WaitForSecondsRealtime(.2f);p.Resume();
            int neighbor=Array.FindIndex(p.World.Countries,c=>c.code=="ZMB");
            var battleGame=p.Simulation;battleGame.AiEnabled=false;battleGame.CancelOrder(player);battleGame.CancelOrder(neighbor);
            int enemy=FreeContact(battleGame,player);SeedOwner(battleGame,enemy,neighbor);
            battleGame.Troops[player]=1000;battleGame.Troops[neighbor]=150;
            int beforeOutgoing=p.Audio.PlayedEffects;p.SendAttack(enemy);battleGame.Step(.1f);
            bool battleSeen=battleGame.States[player]==ArmyState.Fighting;
            bool quietOwnAttack=p.Audio.PlayedEffects==beforeOutgoing;
            int front=battleGame.LastAdvanceCells[player];
            FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(front%p.World.Width,front/p.World.Width),38*p.World.ResolutionScale);
            yield return new WaitForSecondsRealtime(.25f);Capture("11_combate");yield return new WaitForSecondsRealtime(.2f);
            battleGame.CancelOrder(player);battleGame.CancelOrder(neighbor);
            int victim=-1;for(int d=0;d<4;d++){int n=p.World.Neighbor(enemy,d);if(n>=0&&battleGame.Owners[n]==player){victim=n;break;}}
            battleGame.Troops[player]=400;battleGame.Troops[neighbor]=500;
            int attackWarnings=p.Audio.IncomingAttackAlerts;
            battleGame.AttackBorder(neighbor,victim,.75f);battleGame.Step(.1f);
            bool incomingAttackAlert=p.Audio.IncomingAttackAlerts==attackWarnings+1;
            for(int i=0;i<10;i++)battleGame.Step(.05f);
            bool alertCooldown=p.Audio.IncomingAttackAlerts==attackWarnings+1;
            p.GetComponent<FrontierInterface>().ShowAttacks(true);
            yield return new WaitForSecondsRealtime(.3f);Capture("11_attacks");yield return new WaitForSecondsRealtime(.2f);
            p.GetComponent<FrontierInterface>().ShowAttacks(false);
            int powerCountries=p.Visuals.Powers.Tiers.Count(t=>t>0);
            p.StartMatch(Array.FindIndex(p.World.Countries,c=>c.code=="MUS"));yield return new WaitForSecondsRealtime(.4f);
            var seaGame=p.Simulation;player=seaGame.PlayerFaction;seaGame.AiEnabled=false;seaGame.ReinforcementsEnabled=false;
            int madagascar=Array.FindIndex(p.World.Countries,c=>c.code=="MDG");
            int island=seaGame.CapitalCells[player];
            int shore=p.World.CoastCells.Where(id=>p.World.GeographicCountries[id]==madagascar).OrderBy(id=>Mathf.Abs(id%p.World.Width-island%p.World.Width)+Mathf.Abs(id/p.World.Width-island/p.World.Width)).First();
            seaGame.Troops[player]=1000;
            bool seaOrder=p.SendBoat(shore)==OrderResult.Accepted,seaMoved=false,landfall=false;
            seaGame.Landed+=f=>{if(f==player)landfall=true;};
            int departure=seaGame.FleetCells[player];
            for(int step=0;step<60;step++){seaGame.Step(.05f);if(step%20==0)yield return null;}
            seaMoved=seaGame.FleetCells[player]!=departure;
            FindAnyObjectByType<FrontierCamera>().Focus(new Vector2((island%p.World.Width+shore%p.World.Width)*.5f,(island/p.World.Width+shore/p.World.Width)*.5f),110);
            yield return new WaitForSecondsRealtime(.3f);Capture("12_navegacion");yield return new WaitForSecondsRealtime(.2f);
            for(int step=0;step<2400&&!landfall;step++){seaGame.Step(.05f);if(step%40==0)yield return null;}
            FindAnyObjectByType<FrontierCamera>().Focus(new Vector2(shore%p.World.Width,shore/p.World.Width),60);
            yield return new WaitForSecondsRealtime(.3f);Capture("13_desembarco");yield return new WaitForSecondsRealtime(.2f);
            int hostileCoast=p.World.CoastCells.Where(id=>p.World.GeographicCountries[id]==madagascar&&seaGame.Owners[id]==TerritorySimulation.Neutral).OrderBy(id=>Mathf.Abs(id%p.World.Width-island%p.World.Width)+Mathf.Abs(id/p.World.Width-island/p.World.Width)).First();
            SeedOwner(seaGame,hostileCoast,madagascar);seaGame.CancelOrder(madagascar);seaGame.Troops[madagascar]=800;
            int invasionWarnings=p.Audio.IncomingInvasionAlerts;
            bool invasionSent=seaGame.InvadeBySea(madagascar,island,.35f)==OrderResult.Accepted;
            bool incomingInvasionAlert=invasionSent&&p.Audio.IncomingInvasionAlerts==invasionWarnings+1;
            p.GetComponent<FrontierInterface>().ShowAttacks(true);p.FocusPlayer();seaGame.Paused=true;
            yield return new WaitForSecondsRealtime(.3f);Capture("14_invasion_entrante");yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForSecondsRealtime(1);
            var atlas=Resources.Load<Texture2D>("Cartography/EarthRelief8K");
            bool map8K=atlas!=null&&atlas.width==8192&&atlas.height==4096;
            var evidence=new Evidence{errors=_errors,settingsPersisted=persistence,musicPlaying=p.Audio.MusicPlaying,
                radialWorks=radialWorks,singleClickWorks=singleClickWorks,quietExpansion=quietExpansion,quietOwnAttack=quietOwnAttack,incomingAttackAlert=incomingAttackAlert,incomingInvasionAlert=incomingInvasionAlert,alertCooldown=alertCooldown,map8K=map8K,battleSeen=battleSeen,seaOrder=seaOrder,seaMoved=seaMoved,landfall=landfall,countries=p.World.Countries.Length,reducedMotionWorks=reducedMotionWorks,fontsLoaded=Resources.Load<Font>("Fonts/Lato-Regular")!=null&&Resources.Load<Font>("Fonts/Lato-Bold")!=null,
                playedEffects=p.Audio.PlayedEffects,powerCountries=powerCountries,expansionVariantsUsed=p.Audio.ExpansionVariantsUsed,introArtworkLoaded=Resources.Load<Texture2D>("Cinematic/EarthOrbitIntro")!=null,
                geographicBorders=p.Visuals.UsesGeographicBorders,screen=p.ScreenState.ToString(),
                screenshots=Directory.GetFiles(_directory,"*.png").Select(Path.GetFileName).OrderBy(x=>x).ToArray(),
                screenshotSimulationAccelerated=true};
            File.WriteAllText(Path.Combine(_directory,"presentation-audit.json"),JsonUtility.ToJson(evidence,true));
            Application.Quit(_errors==0&&persistence&&battleSeen&&seaOrder&&seaMoved&&landfall&&radialWorks&&singleClickWorks&&quietExpansion&&quietOwnAttack&&incomingAttackAlert&&incomingInvasionAlert&&alertCooldown&&map8K?0:1);
        }
        private static int FreeContact(TerritorySimulation game,int faction)
        {
            for(int id=0;id<game.Owners.Length;id++)if(game.Owners[id]==faction)
                for(int d=0;d<4;d++){int n=game.World.Neighbor(id,d);if(n>=0&&game.Owners[n]==TerritorySimulation.Neutral)return n;}
            throw new InvalidOperationException("Audit requires a neutral border");
        }
        // Controlled presentation fixture; canonical combat tests use independent small maps.
        private static void SeedOwner(TerritorySimulation game,int cell,int owner)
        {
            int old=game.Owners[cell];if(old>=0)game.TerritoryCounts[old]--;game.Owners[cell]=owner;game.TerritoryCounts[owner]++;
            var refresh=typeof(TerritorySimulation).GetMethod("RefreshFrontier",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            refresh.Invoke(game,new object[]{cell});for(int d=0;d<4;d++){int n=game.World.Neighbor(cell,d);if(n>=0)refresh.Invoke(game,new object[]{n});}
            FindAnyObjectByType<FrontierPrototype>().Visuals.Invalidate();
        }
        [Serializable]private class Evidence
        {
            public int errors,playedEffects,powerCountries,countries,expansionVariantsUsed;
            public bool radialWorks,singleClickWorks,quietExpansion,quietOwnAttack,incomingAttackAlert,incomingInvasionAlert,alertCooldown,map8K;
            public bool settingsPersisted,musicPlaying,geographicBorders,screenshotSimulationAccelerated,reducedMotionWorks,fontsLoaded,battleSeen,seaOrder,seaMoved,landfall,introArtworkLoaded;
            public string screen;public string[] screenshots;
        }
    }
}
