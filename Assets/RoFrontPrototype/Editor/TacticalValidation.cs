using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class TacticalValidation
    {
        private static int _checks;
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("TÁCTICA: "+message);_checks++;}
        private static GeographicPolygon Rect(float x0,float x1,float y0,float y1)=>new GeographicPolygon{rings=new[]{new GeographicRing{points=new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1),new Vector2(x0,y0)}}}};
        private static TerritorySimulation Fixture(bool neutral=false)
        {
            var geography=new AfricaGeography{west=0,east=128,south=0,north=64,wrapLongitude=false,neutralRegions=new[]{Rect(0,128,16,64)},countries=new[]{
                new CountryDefinition{code="A",name="A",longitude=8,latitude=25,polygons=Array.Empty<GeographicPolygon>()},new CountryDefinition{code="B",name="B",longitude=80,latitude=35,polygons=Array.Empty<GeographicPolygon>()},new CountryDefinition{code="C",name="C",longitude=112,latitude=56,polygons=Array.Empty<GeographicPolygon>()}}};
            var g=new TerritorySimulation(new WorldGrid(128,64,23,geography),23,0,new MatchOptions{customPlayer=true,advancedSystems=true,playerName="Prueba"}){AiEnabled=false,ReinforcementsEnabled=false};g.Rules.factoryConnectionRadius=99;g.Rules.tacticalPassiveIncome=0;int f=g.PlayerFaction;
            Check(g.TacticalBalance&&g.Troops[f]==10000&&g.Gold[f]==250000,"Perfil nuevo: 10000 tropas y capital inicial para ciudad más fábrica");Check(g.FoundPlayer(g.World.At(40,35),"Prueba"),"Fundación válida");
            if(!neutral)
            {
                Array.Clear(g.TerritoryCounts,0,g.FactionCount);
                for(int cell=0;cell<g.Owners.Length;cell++){int x=cell%128,owner=!g.World.Land[cell]?-2:x<24?0:x<64?f:x<94?1:2;g.Owners[cell]=owner;if(owner>=0)g.TerritoryCounts[owner]++;}
                g.RebuildCampaignIndexes();
            }
            for(int n=0;n<g.FactionCount;n++)g.ProtectionUntil[n]=0;g.Rules.victoryFraction=1;return g;
        }
        private static void Tick(TerritorySimulation g,float seconds){for(int t=0;t<Mathf.CeilToInt(seconds/.1f);t++)g.Step(.1f);}
        private static void Invariants(TerritorySimulation g)
        {
            var counts=new int[g.FactionCount];for(int c=0;c<g.Owners.Length;c++){Check(g.World.Land[c]?g.Owners[c]>=-1:g.Owners[c]==-2,"Agua y tierra conservadas");if(g.Owners[c]>=0)counts[g.Owners[c]]++;}
            Check(counts.SequenceEqual(g.TerritoryCounts),"Conteos consistentes");foreach(int f in Enumerable.Range(0,g.FactionCount)){Check(g.Troops[f]>=0&&g.CommittedTroops[f]>=0&&g.AvailableTroops(f)>=0,"Tropas no negativas");Check(Mathf.Abs(g.CommittedTroops[f]-g.Operations.Where(o=>o.active&&o.faction==f).Sum(o=>o.troops))<.1f,"Compromiso igual a ejércitos reales");Check(Mathf.Abs(g.TotalTroops(f)-g.AvailableTroops(f)-g.CommittedTroops[f])<.1f,"Reserva más ejércitos conserva el total");}
        }
        public static void Run()
        {
            _checks=0;var g=Fixture();int f=g.PlayerFaction,target=g.World.At(64,35),area=g.TerritoryCounts[1];g.Troops[f]=100000;g.Troops[1]=48000;
            Check(g.AttackBorder(f,target,.5f)==OrderResult.Accepted,"Enviar 50% a un rival vecino");var op=g.Operations.Single();
            Check(g.AvailableTroops(f)==50000&&op.troops==50000&&g.AvailableTroops(1)==48000,"Reserva separada y defensor intacto antes del contacto");Tick(g,2.4f);
            Check(g.AvailableTroops(1)<.1f&&Mathf.Abs(op.troops-2000)<1&&Mathf.Abs(g.Statistics[f].troopsLost-48000)<1,"50 mil contra 48 mil deja 2 mil atacantes antes de ocupar");
            Check(g.AvailableTroops(f)==50000&&op.active&&g.TerritoryCounts[1]==area,"El ejército no vuelve ni conquista mientras se resuelve la defensa");Tick(g,1);
            Check(g.TerritoryCounts[1]<area&&op.troops<2000&&g.AvailableTroops(f)==50000,"Los 2 mil supervivientes ocupan rápido pero siguen sufriendo costes");
            float survivors=op.troops;Check(g.RetreatOperation(op.id).success&&Mathf.Abs(g.AvailableTroops(f)-(50000+survivors))<.1f,"Sólo regresan los supervivientes reales");float reserve=g.AvailableTroops(f);Check(!g.RetreatOperation(op.id).success&&g.AvailableTroops(f)==reserve,"Una retirada no puede devolver tropas dos veces");Invariants(g);
            var equal=Fixture();f=equal.PlayerFaction;equal.Troops[f]=100000;equal.Troops[1]=50000;area=equal.TerritoryCounts[1];equal.AttackBorder(f,equal.World.At(64,35),.5f);Tick(equal,3);
            Check(equal.AvailableTroops(f)==50000&&equal.AvailableTroops(1)<.1f&&!equal.Operations.Any(o=>o.active)&&equal.TerritoryCounts[1]==area,"Fuerzas iguales se agotan sin usar la reserva para seguir conquistando");
            var away=Fixture();f=away.PlayerFaction;away.Troops[f]=100000;away.Troops[1]=48000;away.Troops[2]=100000;
            away.AttackBorder(1,away.World.At(94,35),.5f);var abroad=away.Operations.Single();abroad.planning=true;away.AttackBorder(f,away.World.At(64,35),.5f);Tick(away,2.4f);
            var invasion=away.Operations.Single(o=>o.faction==f);Check(Mathf.Abs(abroad.troops-24000)<.1f&&away.AvailableTroops(1)<.1f&&Mathf.Abs(invasion.troops-26000)<1,"Los 24 mil enviados fuera no defienden la patria");
            var third=Fixture();f=third.PlayerFaction;third.Troops[f]=10000;third.Troops[1]=10000;third.Troops[0]=10000;third.AttackBorder(f,third.World.At(64,35),.5f);third.AttackBorder(0,third.World.At(24,35),1);Tick(third,1);
            Check(third.AvailableTroops(f)<5000&&third.Operations.First(o=>o.faction==f).troops>0,"Un vecino aprovecha la reserva reducida sin dañar al ejército que está fuera");
            var quiet=Fixture();f=quiet.PlayerFaction;float enemy=quiet.Troops[1];Check(quiet.AttackBorder(f,quiet.World.At(64,35),0)==OrderResult.NoTroops,"Orden sin tropas rechazada");Tick(quiet,10);Check(quiet.Troops[1]==enemy,"El defensor no pierde tropas por seleccionar, esperar o una orden fallida");
            var tiny=Fixture();f=tiny.PlayerFaction;int last=tiny.World.At(64,35);
            for(int cell=0;cell<tiny.Owners.Length;cell++)if(tiny.Owners[cell]==1&&cell!=last){tiny.Owners[cell]=2;tiny.TerritoryCounts[1]--;tiny.TerritoryCounts[2]++;}tiny.RebuildCampaignIndexes();tiny.Troops[f]=100000;tiny.Troops[1]=48000;tiny.AttackBorder(f,last,.5f);Tick(tiny,4);
            Check(tiny.TerritoryCounts[1]==0&&!tiny.Operations.Any(o=>o.active&&o.faction==f)&&tiny.AvailableTroops(f)>51998&&tiny.AvailableTroops(f)<52000,"Al completar la anexión regresan cerca de 2 mil, descontada la ocupación");
            var early=Fixture(true);f=early.PlayerFaction;early.ReinforcementsEnabled=true;early.Rules.tacticalPassiveIncome=1800;double initialGold=early.Gold[f];float initialTroops=early.Troops[f];Tick(early,60);
            Check(Math.Abs(early.Gold[f]-initialGold-108000)<.01&&Math.Abs(early.Ledgers[f].passive-108000)<.01&&early.IncomeRate(f)==1800,"Desde el inicio produce 1800 de oro por segundo sin edificios");Check(early.Troops[f]-initialTroops<480*TerritorySimulation.RecruitmentBoost,"La regeneración inicial incorpora el aumento adicional del 35%");
            Check(early.BeginDirectionalExpansion(f,early.World.At(65,35),1)==OrderResult.Accepted,"Expansión inicial con todas las tropas");Tick(early,60);
            Check(!early.Operations.Any(o=>o.active)&&early.AvailableTroops(f)<480*TerritorySimulation.RecruitmentBoost,"El envío inicial se agota pese a regenerar lentamente");area=early.TerritoryCounts[f];Tick(early,5);Check(early.TerritoryCounts[f]==area,"La expansión no continúa sola con soldados recién regenerados");Invariants(early);
            var development=Fixture();f=development.PlayerFaction;development.Gold[f]=1000000;int city=development.World.At(38,35),factory=development.World.At(56,35);float before=development.Troops[f];
            development.Troops[f]=0;float baseRate=development.ReinforcementRate(f);
            development.Construct(f,city,BuildingKind.City);Tick(development,4.2f);float isolatedRate=development.ReinforcementRate(f);
            Check(development.Troops[f]==0&&Mathf.Abs(isolatedRate/baseRate-1.35f)<.0001f,"Una ciudad terminada suma 35% a la tasa base, incluso sin fábrica");
            development.Construct(f,factory,BuildingKind.Factory);Tick(development,24);Check(development.SupportedCityLevels(f)==1&&development.Ledgers[f].landTrade>0,"Ciudad y fábrica se conectan y entregan oro automáticamente");
            development.DestroyBuilding(development.BuildingAt(factory),f);Check(Mathf.Abs(development.ReinforcementRate(f)/baseRate-1.35f)<.0001f,"La ciudad conserva su reclutamiento al perder la fábrica");
            development.Construct(f,city,BuildingKind.City);Tick(development,6);Check(Mathf.Abs(development.ReinforcementRate(f)/baseRate-1.35f)<.0001f,"Mejorar una ciudad no cuenta como fundar otra");
            int city2=development.World.At(55,45);development.Construct(f,city2,BuildingKind.City);Tick(development,8);
            Check(Mathf.Abs(development.ReinforcementRate(f)/baseRate-1.70f)<.0001f,"Dos ciudades suman 70% sin multiplicación exponencial");
            Check(!development.TradeRoutes.Any(r=>!r.naval&&development.BuildingAt(r.path[0]).kind==BuildingKind.City),"Las conexiones nuevas parten de fábricas, sin enlaces artificiales entre ciudades aisladas");
            development.DestroyBuilding(development.BuildingAt(city2),f);Check(Mathf.Abs(development.ReinforcementRate(f)/baseRate-1.35f)<.0001f,"Una ciudad destruida deja de aportar el 35%");
            var network=Fixture();f=network.PlayerFaction;network.Gold[f]=5000000;
            foreach(var spec in new[]{(30,16,BuildingKind.Port),(55,16,BuildingKind.Port),(38,35,BuildingKind.City),(51,35,BuildingKind.Factory)})Check(network.Construct(f,network.World.At(spec.Item1,spec.Item2),spec.Item3).success,"Construcción de red mixta");
            Tick(network,40);foreach(var building in network.Buildings)Check(network.TradeRoutes.Any(r=>!r.naval&&(r.fromBuilding==building.id||r.toBuilding==building.id)),"Todos los edificios económicos entran en la red terrestre");
            Check(network.TradeRoutes.Any(r=>r.naval)&&network.Ledgers[f].landTrade>0&&network.Ledgers[f].seaTrade>0,"Puertos mantienen comercio marítimo y terrestre a la vez");
            Check(network.TradeRoutes.Where(r=>!r.naval).Select(r=>Math.Min(r.fromBuilding,r.toBuilding)+":"+Math.Max(r.fromBuilding,r.toBuilding)).Distinct().Count()==network.TradeRoutes.Count(r=>!r.naval),"No se duplican trenes ni ingresos por direcciones inversas");
            var restored=TerritorySimulation.RestoreCampaign(network.World,network.CaptureCampaign());Tick(restored,12);Check(restored.TradeRoutes.Count==network.TradeRoutes.Count&&Mathf.Abs(restored.ReinforcementRate(f)-network.ReinforcementRate(f))<.001f,"Guardar y cargar conserva red y reclutamiento sin duplicar");
            // Read the exact previous rule layout and migrate once, including armies in the field.
            var old=Fixture();f=old.PlayerFaction;old.Rules.initialTroops=2500;old.Rules.baseCapacity=5000;old.Rules.cityCapacity=25000;old.Rules.tribeTroops=180;old.Rules.tacticalBaseRegeneration=2;old.Rules.tacticalCityRecruitment=8;old.Rules.tacticalAiExpansionReserve=750;old.Troops[f]=2000;old.AttackBorder(f,old.World.At(64,35),.5f);
            var migrated=TerritorySimulation.RestoreCampaign(old.World,old.CaptureCampaign());Check(migrated.Troops[f]==4000&&migrated.Operations.Single().troops==4000&&migrated.CommittedTroops[f]==4000&&migrated.Rules.tacticalCityRecruitment==.35f,"Guardado anterior migra reserva y ofensiva por cuatro una sola vez");
            var twice=TerritorySimulation.RestoreCampaign(old.World,migrated.CaptureCampaign());Check(twice.Troops[f]==4000&&twice.Operations.Single().troops==4000,"Recargar un guardado migrado no vuelve a multiplicar");
            var sea=Fixture();f=sea.PlayerFaction;sea.Gold[f]=1000000;sea.Construct(f,sea.World.At(30,16),BuildingKind.Port);sea.Construct(f,sea.World.At(55,16),BuildingKind.Port);Tick(sea,4.2f);
            Check(sea.TradeRoutes.Any(r=>r.naval)&&sea.Ledgers[f].seaTrade==0,"Dos puertos empiezan el tráfico al terminar sin cobrar antes de llegar");var route=sea.TradeRoutes.First(r=>r.naval);Tick(sea,route.duration+1);Check(route.deliveries>0&&route.docking&&sea.Ledgers[f].seaTrade>0,"El barco llega, atraca y paga");int delivered=route.deliveries;Tick(sea,2);Check(route.deliveries==delivered,"El atraque no duplica ingresos");Tick(sea,6);Check(!route.docking,"El barco vuelve a salir");
            var ai=Fixture(true);f=ai.PlayerFaction;ai.Troops[f]=100000;ai.AiEnabled=true;Tick(ai,30);Check(ai.ExpansionDirection(0,out _,out var direction,out int primary,out int other)&&direction.x<0&&primary>other,"La IA elige rumbo alejándose del jugador cercano y conserva crecimiento secundario");Invariants(ai);
            var patient=Fixture(true);patient.Troops[0]=400;patient.AiEnabled=true;patient.ReinforcementsEnabled=true;area=patient.TerritoryCounts[0];Tick(patient,30);Check(patient.TerritoryCounts[0]==area&&patient.AvailableTroops(0)>=400&&!patient.Operations.Any(o=>o.active&&o.faction==0),"La IA espera a ahorrar tropas y no malgasta cada recluta en expansión");
            var shape=Fixture(true);f=shape.PlayerFaction;shape.Troops[f]=10000;shape.BeginDirectionalExpansion(f,shape.World.At(65,35),.8f);Tick(shape,6);Check(shape.ExpansionDirection(f,out var origin,out direction,out primary,out other)&&primary>other&&other>0,"La mancha táctica se extiende con rumbo y ocupa huecos laterales");
            float[] radii=shape.OwnedCells(f).Where(c=>Enumerable.Range(0,4).Any(d=>{int n=shape.World.Neighbor(c,d);return n>=0&&shape.Owners[n]!=f;})).Select(c=>new Vector2(c%128-origin.x,c/128-origin.y).magnitude).ToArray();
            Check(radii.Max()-radii.Min()>2&&shape.TerritoryCounts[f]>30,"Contorno irregular con variación espacial y avance ágil");
            int count=shape.World.LandCount;bool land=shape.World.Land[shape.World.At(40,16)];byte coast=CoastCoverage.Sample(shape.World,shape.World.At(40,16)),water=CoastCoverage.Sample(shape.World,shape.World.At(40,15));
            Check(coast>127&&coast<255&&water>0&&water<128,"La costa filtra el contorno conservando sus centros de tierra y agua");Check(shape.World.LandCount==count&&shape.World.Land[shape.World.At(40,16)]==land,"El suavizado no modifica navegación ni geografía");
            Check(shape.World.InlandCoastDistance[shape.World.At(40,16)]==0&&shape.World.SurfaceHeight(40,16)==0&&shape.World.SurfaceHeight(40,60)>0,"El relieve se une al agua en la costa y conserva altura tierra adentro");
            string output=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"tactical-validation.json"),"{\"result\":\"passed\",\"assertions\":"+_checks+",\"combat\":\"50000 vs 48000 leaves 2000 before occupation\",\"initialTroops\":10000,\"baseRegenerationMaximumPerSecond\":23.328,\"passiveGold\":1800}");Debug.Log("TACTICAL_VALIDATION_OK "+_checks);
        }
    }
}
