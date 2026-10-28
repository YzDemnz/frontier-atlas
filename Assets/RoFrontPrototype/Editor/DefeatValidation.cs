using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class DefeatValidation
    {
        private static int checks;
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException("DERROTA: "+text);checks++;}
        private static TerritorySimulation Fixture(int row=35,bool found=true)
        {
            var polygon=new GeographicPolygon{rings=new[]{new GeographicRing{points=new[]{new Vector2(0,16),new Vector2(128,16),new Vector2(128,64),new Vector2(0,64),new Vector2(0,16)}}}};
            var map=new AfricaGeography{west=0,east=128,south=0,north=64,wrapLongitude=false,neutralRegions=new[]{polygon},countries=new[]{new CountryDefinition{code="A",name="Alfa",longitude=8,latitude=25,polygons=Array.Empty<GeographicPolygon>()},new CountryDefinition{code="B",name="Beta",longitude=110,latitude=40,polygons=Array.Empty<GeographicPolygon>()}}};
            var g=new TerritorySimulation(new WorldGrid(128,64,23,map),23,0,new MatchOptions{customPlayer=true,advancedSystems=true,playerName="Reintento"}){AiEnabled=false,ReinforcementsEnabled=false};if(!found)return g;int f=g.PlayerFaction,last=g.World.At(40,row);Check(g.FoundPlayer(last,"Reintento"),"Fundación válida");Array.Clear(g.TerritoryCounts,0,g.FactionCount);
            for(int c=0;c<g.Owners.Length;c++){int owner=!g.World.Land[c]?-2:c==last?f:c%128<90?0:1;g.Owners[c]=owner;if(owner>=0)g.TerritoryCounts[owner]++;}g.RebuildCampaignIndexes();for(int n=0;n<g.FactionCount;n++){g.ProtectionUntil[n]=0;g.Gold[n]=10000000;g.Troops[n]=20000;}g.Troops[f]=0;g.Rules.victoryFraction=1;return g;
        }
        private static void Tick(TerritorySimulation g,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)g.Step(.1f);}
        public static void Run()
        {
            checks=0;var unstarted=Fixture(found:false);unstarted.EvaluateVictory();Tick(unstarted,2);Check(unstarted.Outcome==CampaignOutcome.Playing,"No perder antes de elegir capital");
            var g=Fixture();int f=g.PlayerFaction;g.EvaluateVictory();Check(g.Troops[f]==0&&g.Outcome==CampaignOutcome.Playing,"Quedarse sin soldados no equivale a perder territorio");int last=g.CapitalCells[f];g.Troops[f]=1500;Check(g.AttackBorder(f,g.World.At(39,35),2f/3).Equals(OrderResult.Accepted),"Ejército propio comprometido fuera de la reserva");g.Operations.Single().planning=true;Check(g.AttackBorder(0,last,.5f)==OrderResult.Accepted,"Ataque real contra la última celda");Tick(g,4);
            Check(g.TerritoryCounts[f]==0&&g.Outcome==CampaignOutcome.Defeat&&g.Paused&&g.WinningFaction==0,"Anexión terrestre termina en derrota y pausa");Check(g.Troops[f]==0&&g.CommittedTroops[f]==0&&!g.Operations.Any(o=>o.active&&o.faction==f),"Derrotado sin reservas ni ejércitos fantasma");Check(Mathf.Abs(g.Statistics[f].troopsLost-1500)<1,"La defensa y el ejército desplegado se pierden al eliminar la nación");float elapsed=g.ElapsedTime;int eliminations=g.Statistics[0].nationsEliminated;Tick(g,10);g.EvaluateVictory();Check(g.ElapsedTime==elapsed&&g.Statistics[0].nationsEliminated==eliminations,"El resultado congela el mundo y no premia la derrota dos veces");
            var saved=TerritorySimulation.RestoreCampaign(g.World,g.CaptureCampaign());Check(saved.Outcome==CampaignOutcome.Defeat&&saved.TerritoryCounts[f]==0&&saved.Paused,"Una derrota guardada sigue terminada al cargar");
            var naval=Fixture(16);Check(naval.InvadeBySea(0,naval.CapitalCells[naval.PlayerFaction],.5f)==OrderResult.Accepted,"Invasión real a la última costa");Tick(naval,15);Check(naval.Outcome==CampaignOutcome.Defeat&&naval.TerritoryCounts[naval.PlayerFaction]==0,"Invasión naval también provoca derrota");
            var nuclear=Fixture();Check(nuclear.Construct(0,nuclear.World.At(20,30),BuildingKind.Silo).success,"Silo enemigo válido");Tick(nuclear,5);Check(nuclear.LaunchWeapon(0,nuclear.CapitalCells[nuclear.PlayerFaction],WeaponKind.Atomic).success,"Lanzamiento contra la última celda");Tick(nuclear,10);Check(nuclear.Outcome==CampaignOutcome.Defeat&&nuclear.Radiation.Count>0,"Pérdida por impacto nuclear termina la partida");
            var fallback=Fixture();f=fallback.PlayerFaction;last=fallback.CapitalCells[f];fallback.Owners[last]=0;fallback.TerritoryCounts[f]=0;fallback.TerritoryCounts[0]++;fallback.RebuildCampaignIndexes();fallback.EvaluateVictory();Check(fallback.Outcome==CampaignOutcome.Defeat&&fallback.States[f]==ArmyState.Eliminated,"Revisión de seguridad detecta jugador fundado sin territorio");
            var capital=Fixture();f=capital.PlayerFaction;int extra=capital.World.At(42,35);capital.Owners[extra]=f;capital.TerritoryCounts[0]--;capital.TerritoryCounts[f]++;capital.RebuildCampaignIndexes();typeof(TerritorySimulation).GetMethod("UnclaimCell",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(capital,new object[]{capital.CapitalCells[f],0});capital.EvaluateVictory();Check(capital.TerritoryCounts[f]==1&&capital.Outcome==CampaignOutcome.Playing,"Perder solo la capital no elimina territorio restante");
            foreach(string threat in new[]{"incoming_attack","incoming_invasion"})for(int variant=0;variant<3;variant++)
            {var clip=Resources.Load<AudioClip>("Audio/"+threat+"_"+variant);Check(clip!=null&&clip.channels==2&&clip.length>.5f&&clip.length<1.5f,"Alerta estéreo original importada");var samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0)&&samples.Max(x=>Mathf.Abs(x))<1,"Audio decodificado sin saturación digital");}
            string folder=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");File.WriteAllText(Path.Combine(folder,"defeat-validation.json"),"{\"passed\":true,\"assertions\":"+checks+",\"paths\":[\"land\",\"naval\",\"nuclear\",\"fallback\",\"saved defeat\",\"no premature defeat\",\"audio decode\"]}");Debug.Log("DEFEAT_VALIDATION_OK "+checks);
        }
    }
}
