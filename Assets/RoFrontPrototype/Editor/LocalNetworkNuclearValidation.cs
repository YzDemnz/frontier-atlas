using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class LocalNetworkNuclearValidation
    {
        private static int checks;
        private static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("RED LOCAL / NUCLEAR: "+text);checks++;}
        private static TerritorySimulation Fixture()=> (TerritorySimulation)typeof(IndustrialDefenceValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
        private static void Tick(TerritorySimulation g,float seconds){g.LimitFrameWork=false;for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)g.Step(.1f);}
        private static Building Build(TerritorySimulation g,int x,int y,BuildingKind kind)
        {int cell=g.World.At(x,y);Check(g.Construct(g.PlayerFaction,cell,kind).success,"Construir "+kind+" "+x+","+y);return g.BuildingAt(cell);}
        private static bool Linked(TerritorySimulation g,Building a,Building b)=>g.TradeRoutes.Any(r=>!r.naval&&(r.fromBuilding==a.id&&r.toBuilding==b.id||r.fromBuilding==b.id&&r.toBuilding==a.id));
        private static byte[] LegacyV5(TerritorySimulation g)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write("ATLAS-CAMPAIGN");w.Write(5);w.Write(g.World.Width);w.Write(g.World.Height);w.Write(g.World.LandCount);w.Write(g.World.Countries.Length);foreach(var c in g.World.Countries)w.Write(c.code);w.Write(g.PlayerFaction);
                var codec=typeof(TerritorySimulation).Assembly.GetType("FrontierAtlas.CampaignCodec").GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic);
                w.Write(true);foreach(var f in typeof(MatchOptions).GetFields(BindingFlags.Instance|BindingFlags.Public).OrderBy(x=>x.Name,StringComparer.Ordinal))if(f.Name!="mode")codec.Invoke(null,new object[]{w,f.FieldType,f.GetValue(g.Options)});w.Write(((CampaignRandom)typeof(TerritorySimulation).GetField("_random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g)).State);
                var fields=(string[])typeof(TerritorySimulation).GetField("SavedStateFields",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);w.Write(fields.Length-1);
                foreach(string name in fields.Take(fields.Length-1)){var f=typeof(TerritorySimulation).GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);w.Write(name);codec.Invoke(null,new object[]{w,f.FieldType,f.GetValue(g)});}w.Flush();return stream.ToArray();
            }
        }
        public static void Run()
        {
            checks=0;Check(CampaignRules.Load().factoryConnectionRadius==18,"Radio local de 18 celdas normalizadas");var g=Fixture();int f=g.PlayerFaction;g.Rules.factoryConnectionRadius=18;
            var a=Build(g,40,34,BuildingKind.Factory);var b=Build(g,56,34,BuildingKind.Factory);var c=Build(g,72,34,BuildingKind.Factory);var city=Build(g,90,34,BuildingKind.City);var outside=Build(g,90,43,BuildingKind.City);var port=Build(g,56,16,BuildingKind.Port);Tick(g,30);
            Check(Linked(g,a,b)&&Linked(g,b,c)&&Linked(g,c,city)&&Linked(g,b,port),"Cadena de fábricas con ciudad y puerto dentro de cada círculo");
            Check(!Linked(g,a,c)&&!Linked(g,a,city)&&!Linked(g,c,outside),"No extiende directamente el radio al crecer la red ni acepta esquinas de un cuadrado");
            Check(g.TradeRoutes.All(r=>r.fromBuilding!=r.toBuilding)&&g.TradeRoutes.Where(r=>!r.naval).GroupBy(r=>Math.Min(r.fromBuilding,r.toBuilding)+":"+Math.Max(r.fromBuilding,r.toBuilding)).All(p=>p.Count()==1),"Sin autoenlaces ni enlaces dobles entre fábricas");
            var ids=g.TradeRoutes.Where(r=>!r.naval).Select(r=>r.id).ToArray();Tick(g,300);Check(ids.All(id=>g.TradeRoutes.Any(r=>r.id==id)),"Cinco minutos simulados conservan los enlaces locales");
            Check(g.TradeDeliveries.Any(d=>d.producer==a.id&&d.destination==city.id),"Un tren de la primera fábrica llega a la ciudad por las fábricas intermedias");
            g.PreviewFactoryConnections(f,b.cell);Tick(g,4);Check(g.TradePreview.Any(p=>p.station==a.id)&&g.TradePreview.Any(p=>p.station==c.id)&&g.TradePreview.All(p=>p.station!=b.id),"Previsualización incluye fábricas vecinas sin apuntarse a sí misma");
            g.Troops[f]=0;Check(Mathf.Abs(g.ReinforcementRate(f)-23.328f*1.7f)<.001f,"El +80% se combina con +35% por ciudad operativa");
            var old=Fixture();old.Rules.factoryConnectionRadius=100;var of=Build(old,40,40,BuildingKind.Factory);var oc=Build(old,90,40,BuildingKind.City);Tick(old,15);Check(Linked(old,of,oc),"Escenario antiguo reproduce alcance excesivo");
            var migrated=TerritorySimulation.RestoreCampaign(old.World,LegacyV5(old));Check(migrated.Rules.factoryConnectionRadius==18&&!migrated.TradeRoutes.Any(r=>!r.naval)&&migrated.Buildings.Count==old.Buildings.Count&&migrated.Gold.SequenceEqual(old.Gold),"v5 corrige radio y retira vías largas conservando edificios y recursos");
            var explicitSave=TerritorySimulation.RestoreCampaign(old.World,old.CaptureCampaign());Check(explicitSave.Rules.factoryConnectionRadius==100,"v6 conserva un radio personalizado explícito");
            var n=Fixture();f=n.PlayerFaction;var silo=Build(n,80,64,BuildingKind.Silo);var own=Build(n,120,64,BuildingKind.City);var safe=Build(n,40,64,BuildingKind.City);Tick(n,5);int target=n.World.At(140,64);n.Troops[f]=50000;
            var gate=new StrategicLaunchConfirmation();double gold=n.Gold[f];int ammo=silo.ammunition;
            Check(gate.Arm(n,target,WeaponKind.Atomic,10).success&&gate.FriendlyRisk,"Preparar detecta daño colateral propio");
            Check(n.Gold[f]==gold&&silo.ammunition==ammo&&n.Projectiles.Count==0,"Primera confirmación solo previsualiza: sin gasto ni misil");
            Check(!gate.Confirm(n,target,10.1f).success&&n.Projectiles.Count==0,"El mismo clic no puede confirmar de inmediato");gate.Cancel();Check(!gate.Active&&n.Gold[f]==gold,"Cancelar no consume recursos");
            gate.Arm(n,target,WeaponKind.Atomic,20);Check(!gate.Confirm(n,target+1,21).success&&!gate.Active,"Cambiar objetivo invalida autorización anterior");
            gate.Arm(n,target,WeaponKind.Atomic,30);Check(gate.Confirm(n,target,31).success&&n.Gold[f]==gold-n.Rules.Weapon(WeaponKind.Atomic).cost&&silo.ammunition==ammo-1,"Segunda confirmación paga exactamente un lanzamiento");
            Check(!gate.Confirm(n,target,32).success&&n.Projectiles.Count==1,"Doble confirmación posterior no duplica misiles");int territory=n.TerritoryCounts[f];Tick(n,9);
            Check(own.destroyed&&!safe.destroyed&&n.TerritoryCounts[f]<territory&&n.Troops[f]<50000&&n.IsIrradiated(own.cell),"Explosión destruye edificio, territorio y tropas del atacante dentro del radio; preserva lo exterior");
            Check(n.NuclearScars[target]==0&&n.RadiationPixel(target).g==0&&n.NuclearScars[n.World.At(170,64)]==1&&!n.IsIrradiated(n.World.At(171,64)),"Centro y borde de la huella usan la distancia exacta; fuera del radio no hay daño");
            var loaded=TerritorySimulation.RestoreCampaign(n.World,n.CaptureCampaign());Check(loaded.NuclearScars.Count==n.NuclearScars.Count&&loaded.RadiationPixel(target).Equals(n.RadiationPixel(target)),"v6 conserva colores y contaminación al cargar");Tick(loaded,181);Check(loaded.Radiation.Count==0&&loaded.NuclearScars.Count==0,"Al terminar la radiación se limpia también la huella");
            var self=(TerritorySimulation)typeof(DefeatValidation).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{35,true});f=self.PlayerFaction;self.Troops[f]=1000;self.ProtectionUntil[f]=999;int stars=self.Stars[f];Check(self.LaunchWeapon(f,self.CapitalCells[f],WeaponKind.Atomic,true).success,"Se permite un lanzamiento propio deliberado incluso con protección inicial");Tick(self,9);
            Check(self.Outcome==CampaignOutcome.Defeat&&self.WinningFaction==-1&&self.TerritoryCounts[f]==0&&self.Statistics[f].nationsEliminated==0&&self.Stars[f]==stars,"Autodestrucción derrota al jugador sin otorgarle victoria, estrellas o eliminación de sí mismo");
            string output=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");File.WriteAllText(Path.Combine(output,"local-network-nuclear-validation.json"),"{\"passed\":true,\"assertions\":"+checks+",\"recruitmentMultiplier\":1.8,\"factoryRadius\":18,\"saveVersion\":6}");Debug.Log("LOCAL_NETWORK_NUCLEAR_OK "+checks);
        }
    }
}
