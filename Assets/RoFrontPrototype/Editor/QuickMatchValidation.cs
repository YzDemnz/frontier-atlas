using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class QuickMatchValidation
    {
        private static int checks;
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("PARTIDA RÁPIDA: "+text);checks++;}
        private static TerritorySimulation Fixture()
        {
            var source=(TerritorySimulation)typeof(IndustrialDefenceValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
            var g=new TerritorySimulation(source.World,27,0,new MatchOptions{customPlayer=true,advancedSystems=true,mode=MatchMode.QuickMatch,tribeCount=4,playerName="Rápida"}){AiEnabled=false,ReinforcementsEnabled=false,LimitFrameWork=false};
            g.FoundPlayer(g.World.At(60,60),"Rápida");Array.Clear(g.TerritoryCounts,0,g.FactionCount);
            for(int c=0;c<g.Owners.Length;c++){int x=c%256,f=!g.World.Land[c]?-2:x<128?g.PlayerFaction:x<200?0:1;g.Owners[c]=f;if(f>=0)g.TerritoryCounts[f]++;}g.RebuildCampaignIndexes();return g;
        }
        private static void Tick(TerritorySimulation g,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)g.Step(.1f);}
        public static void Run()
        {
            checks=0;var g=Fixture();int f=g.PlayerFaction;
            Check(g.IsQuickMatch&&g.TacticalBalance&&g.Rules.initialTroops==2500&&g.Troops[f]==2500&&g.Gold[f]==0,"Modo y tropas iniciales separados de la campaña");
            Check(g.Rules.cityCapacity==25000&&g.Rules.victoryFraction==.8f&&g.Rules.protectionSeconds==60&&g.Rules.brutalProtectionSeconds==120,"Capacidad de ciudad, meta 80% y protección de un minuto");
            Check(g.Rules.Weapon(WeaponKind.Atomic).cost==750000&&g.Rules.Weapon(WeaponKind.Hydrogen).cost==5000000&&g.Rules.Weapon(WeaponKind.MIRV).cost==25000000,"Precios nucleares de la barra observada");
            Check(g.ConstructionPrice(f,BuildingKind.City)==125000&&g.ConstructionPrice(f,BuildingKind.Factory)==125000&&g.ConstructionPrice(f,BuildingKind.Port)==125000&&g.ConstructionPrice(f,BuildingKind.Outpost)==50000&&g.ConstructionPrice(f,BuildingKind.SAM)==1500000,"Precios iniciales de infraestructura");
            double gold=g.Gold[f];Tick(g,10);Check(Math.Abs(g.Gold[f]-gold-10000)<.01,"Mil de oro por segundo en el perfil rápido");
            g.Troops[f]=0;Check(Mathf.Abs(g.ReinforcementRate(f)-40)<.001f,"Curva de crecimiento de referencia arranca en 40 tropas/s; cadencia calibrada, fórmula privada no verificada");g.Troops[f]=CapacityFraction(g,.42f);float middle=g.ReinforcementRate(f);g.Troops[f]=CapacityFraction(g,.95f);Check(middle>g.ReinforcementRate(f),"Regeneración máxima en zona media, menor al acercarse al límite");g.Troops[f]=g.Capacity[f];Check(g.ReinforcementRate(f)==0,"No regenera sobre el límite");
            g.Gold[f]=10000000;g.Troops[f]=25000;float capacity=g.Capacity[f];Check(g.Construct(f,g.World.At(55,45),BuildingKind.City).success,"Ciudad en territorio propio");Tick(g,5);Check(Mathf.Abs(g.Capacity[f]-capacity-25000)<.1f,"Ciudad completada añade 25000 de capacidad");Check(g.ConstructionPrice(f,BuildingKind.City)==250000,"Siguiente ciudad sube al segundo escalón");
            Array.Clear(g.ProtectionUntil,0,g.FactionCount);g.Troops[f]=25000;g.Troops[0]=20000;int original=g.TerritoryCounts[0];Check(g.AttackBorder(f,g.World.At(128,60),.5f)==OrderResult.Accepted,"Ataque con 50% sobre frontera vecina");Check(Mathf.Abs(g.AvailableTroops(f)-12500)<.01f&&Mathf.Abs(g.CommittedTroops[f]-12500)<.01f,"Reserva separada de las tropas desplegadas desde el primer instante");Tick(g,2);Check(g.TerritoryCounts[0]<original&&g.Troops[0]>0&&g.CommittedTroops[f]<12500,"Combate del modo rápido avanza celda a celda y desgasta ambos ejércitos");
            Check(g.ProposeAlliance(f,0).success&&g.AnswerAlliance(0,f,true).success&&g.AreAllied(f,0),"Alianza sencilla termina hostilidades y bloquea ataques ordinarios");Check(g.AttackBorder(f,Enumerable.Range(0,g.Owners.Length).First(c=>g.Owners[c]==0&&g.IsBorderTarget(f,c)),.5f)==OrderResult.Allied,"No ataca a un aliado");Check(g.Betray(f,0).success&&!g.AreAllied(f,0)&&Mathf.Abs(g.BetrayalUntil[f]-g.ElapsedTime-30)<.01f&&g.Rules.betrayalVulnerability==2,"Traición dura 30 segundos y reduce a la mitad el coste enemigo");
            g.AiEnabled=true;Tick(g,40);Check(g.Affairs.proposals.Count==0&&g.Affairs.loans.Count==0&&g.Affairs.coalitions.Count==0,"IA rápida usa combate y alianzas, sin deudas ni diplomacia de campaña");
            var copy=TerritorySimulation.RestoreCampaign(g.World,g.CaptureCampaign());Check(copy.IsQuickMatch&&copy.Options.tribeCount==4&&copy.TerritoryCounts.SequenceEqual(g.TerritoryCounts)&&copy.Rules.cityCapacity==25000,"Guardar/cargar conserva modo, reglas y propiedad");
            var campaign=new TerritorySimulation(g.World,27,0,new MatchOptions{customPlayer=true,advancedSystems=true});Check(!campaign.IsQuickMatch&&campaign.Rules.initialTroops==10000&&campaign.Rules.tacticalPassiveIncome==1800&&campaign.Rules.cityCapacity==100000,"Iniciar rápida no altera el perfil original de campaña");
            string folder=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");File.WriteAllText(Path.Combine(folder,"quick-match-validation.json"),"{\"passed\":true,\"assertions\":"+checks+",\"reference\":\"RoFront recorded gameplay; OpenFront 0.34.24 where private values unavailable\",\"onlineMultiplayer\":false}");Debug.Log("QUICK_MATCH_OK "+checks);
        }
        private static float CapacityFraction(TerritorySimulation g,float part)=>g.Capacity[g.PlayerFaction]*part;
    }
}
