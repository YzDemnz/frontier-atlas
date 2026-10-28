using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class CampaignValidation
    {
        private static int _checks;
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("CAMPAÃ‘A: "+message);_checks++;}
        private static GeographicPolygon Rect(float x0,float x1,float y0,float y1)=>new GeographicPolygon{rings=new[]{new GeographicRing{points=new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1),new Vector2(x0,y0)}}}};
        private static TerritorySimulation Fixture(BalanceProfile profile=BalanceProfile.LegacyPrototype)
        {
            var geography=new AfricaGeography{west=0,east=256,south=0,north=128,wrapLongitude=false,neutralRegions=new[]{Rect(0,256,16,128)},countries=new[]{
                new CountryDefinition{code="A",name="NaciÃ³n A",longitude=8,latitude=60,polygons=Array.Empty<GeographicPolygon>()},
                new CountryDefinition{code="B",name="NaciÃ³n B",longitude=200,latitude=60,polygons=Array.Empty<GeographicPolygon>()}}};
            var game=new TerritorySimulation(new WorldGrid(256,128,7,geography),7,0,new MatchOptions{playerName="NaciÃ³n de prueba",customPlayer=true,advancedSystems=true}){AiEnabled=false,ReinforcementsEnabled=false};
            game.Rules.balanceProfile=profile;game.Rules.allowCapitalTrade=profile==BalanceProfile.LegacyPrototype;int player=game.PlayerFaction;Check(game.CanFoundAt(game.World.At(64,60))&&!game.CanFoundAt(game.World.At(40,4)),"FundaciÃ³n sÃ³lo en tierra libre");
            Check(game.FoundPlayer(game.World.At(64,60),"Mi naciÃ³n"),"Nombre y ubicaciÃ³n de capital libres");
            Array.Clear(game.TerritoryCounts,0,game.FactionCount);
            for(int cell=0;cell<game.Owners.Length;cell++)
            {
                int x=cell%256,owner=!game.World.Land[cell]?TerritorySimulation.Ocean:x<20?0:x<145?player:1;
                game.Owners[cell]=owner;if(owner>=0)game.TerritoryCounts[owner]++;
            }
            game.RebuildCampaignIndexes();for(int f=0;f<game.FactionCount;f++){game.Gold[f]=100000000;game.ProtectionUntil[f]=0;}
            return game;
        }
        private static void Tick(TerritorySimulation game,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)game.Step(.05f);}
        private static void Counts(TerritorySimulation game)
        {
            var counts=new int[game.FactionCount];for(int cell=0;cell<game.Owners.Length;cell++){int f=game.Owners[cell];Check(game.World.Land[cell]?f>=-1:f==-2,"Propiedad compatible con tierra y mar");if(f>=0)counts[f]++;}
            Check(counts.SequenceEqual(game.TerritoryCounts),"Recuentos territoriales conservados");
            foreach(int f in Enumerable.Range(0,game.FactionCount)){Check(game.Troops[f]>=0&&game.CommittedTroops[f]<=game.Troops[f]+.01f,"No hay tropas duplicadas ni negativas");Check(game.Gold[f]>=0,"Oro no negativo");}
        }
        private static void ReferenceNavigationChecks()
        {
            var data=new AfricaGeography{west=0,east=128,south=0,north=64,wrapLongitude=false,neutralRegions=new[]{Rect(0,128,32,64),Rect(60,64,2,32)},countries=new[]{new CountryDefinition{code="A",name="A",longitude=8,latitude=60,polygons=Array.Empty<GeographicPolygon>()},new CountryDefinition{code="B",name="B",longitude=100,latitude=60,polygons=Array.Empty<GeographicPolygon>()}}};
            var game=new TerritorySimulation(new WorldGrid(128,64,9,data),9,0,new MatchOptions{customPlayer=true,advancedSystems=true}){AiEnabled=false,ReinforcementsEnabled=false};
            game.Rules.navigationNodesPerTick=64;System.Collections.Generic.Queue<int> result=null;bool completed=false;int start=game.World.At(20,12),goal=game.World.At(100,12);
            var plan=typeof(TerritorySimulation).GetMethod("PlanSeaRoute",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            plan.Invoke(game,new object[]{start,goal,new Action<System.Collections.Generic.Queue<int>>(path=>{result=path;completed=true;}),false});
            Check(!completed&&game.PendingSeaPlans==1,"Un obstÃ¡culo inicia planificaciÃ³n incremental, sin bloquear la orden");
            for(int t=0;t<2000&&!completed;t++){game.Step(.1f);Check(game.NavigationNodesLastTick<=64,"Presupuesto de bÃºsqueda respetado por tick");}
            Check(completed&&result!=null&&result.Count>1,"Ruta marÃ­tima completa alrededor del obstÃ¡culo");
            int previous=start;foreach(int waypoint in result){Check(game.SeaLineClear(previous,waypoint),"Todos los tramos suavizados evitan tierra");previous=waypoint;}Check(previous==goal,"La ruta termina en el objetivo solicitado");
            completed=false;plan.Invoke(game,new object[]{start,goal,new Action<System.Collections.Generic.Queue<int>>(path=>{result=path;completed=true;}),false});Check(completed&&game.SeaCacheHits>0,"Una ruta repetida reutiliza el resultado");
            completed=false;plan.Invoke(game,new object[]{start,game.World.At(70,50),new Action<System.Collections.Generic.Queue<int>>(path=>{result=path;completed=true;}),false});Check(completed&&result==null,"Un destino terrestre invÃ¡lido se rechaza sin bÃºsqueda global");
        }
        public static void Run()
        {
            _checks=0;var economy=Fixture();int f=economy.PlayerFaction;double money=economy.Gold[f];float troops=economy.Troops[f],capacity=economy.Capacity[f];
            int city=economy.World.At(66,60),factory=economy.World.At(80,60);
            Check(economy.Construct(f,city,BuildingKind.City).success,"Construir ciudad");Check(Math.Abs(economy.Gold[f]-(money-economy.Rules.Building(BuildingKind.City).cost))<.01,"Precio descontado una sola vez");
            Check(!economy.Construct(f,city,BuildingKind.City).success,"No duplicar construcciÃ³n pendiente");Tick(economy,5);
            Check(economy.Capacity[f]==capacity+economy.Rules.cityCapacity&&economy.Troops[f]==troops,"La ciudad aumenta capacidad sin regalar tropas");
            Check(economy.Construct(f,city,BuildingKind.City).success,"Mejorar ciudad en su posiciÃ³n");Tick(economy,6);Check(economy.BuildingAt(city).level==2,"Mejora terminada");
            Check(economy.Construct(f,factory,BuildingKind.Factory).success,"Construir fÃ¡brica");Tick(economy,14);
            Check(economy.TradeRoutes.Any(r=>!r.naval&&r.owner==f),"La fÃ¡brica crea transporte terrestre real");money=economy.Gold[f];Tick(economy,20);
            Check(economy.Gold[f]>money+economy.Rules.baseIncome*20,"Los viajes comerciales entregan oro");
            int port1=economy.World.At(30,16),port2=economy.World.At(100,16);
            Check(!economy.Construct(f,economy.World.At(90,60),BuildingKind.Port).success,"Puerto interior rechazado");
            Check(economy.Construct(f,port1,BuildingKind.Port).success&&economy.Construct(f,port2,BuildingKind.Port).success,"Puertos costeros vÃ¡lidos");Tick(economy,15);
            Check(economy.TradeRoutes.Any(r=>r.naval),"Comercio marÃ­timo conectado");Check(economy.DeployWarship(f,port1).success,"Buque requiere puerto y oro");
            var commercial=economy.TradeRoutes.First(r=>r.naval);commercial.clock=commercial.duration-.05f;int deliveries=commercial.deliveries;Tick(economy,.1f);
            Check(commercial.docking&&commercial.deliveries==deliveries+1,"El barco atraca y entrega sus ingresos al llegar");Tick(economy,2);
            Check(commercial.docking&&commercial.deliveries==deliveries+1,"El atraque no entrega dinero repetido cada fotograma");Tick(economy,5);Check(!commercial.docking,"El barco vuelve a salir despuÃ©s de atracar");
            Check(commercial.path.Count==2&&economy.SeaLineClear(commercial.path[0],commercial.path[1]),"Ruta comercial directa y mÃ­nima en mar abierto");
            economy.Troops[1]=2500; // Fixed convoy strength isolates interception from the campaign starting balance.
            Check(economy.InvadeBySea(1,economy.CapitalCells[f],.25f)==OrderResult.Accepted,"InvasiÃ³n naval con tropas comprometidas");
            var convoy=economy.Operations.First(o=>o.kind==OperationKind.Naval);var warship=economy.Warships[0];warship.currentCell=convoy.currentCell;warship.nextCell=warship.currentCell;
            float enemyTroops=economy.Troops[1];Tick(economy,15);Check(economy.Troops[1]<enemyTroops&&!convoy.active,"El buque intercepta y hunde un transporte");Counts(economy);

            var ops=Fixture();f=ops.PlayerFaction;Check(ops.BeginExpansion(f,.25f)==OrderResult.NoFreeLand,"No inventar tierra neutral en un territorio ya ocupado");
            int free=ops.World.At(80,80);ops.Owners[free]=-1;ops.TerritoryCounts[f]--;ops.RebuildCampaignIndexes();
            Check(ops.BeginExpansion(f,.25f)==OrderResult.Accepted&&ops.AttackBorder(f,ops.World.At(145,60),.25f)==OrderResult.Accepted,"ExpansiÃ³n y ataque simultÃ¡neos");
            Check(ops.AttackBorder(f,ops.World.At(19,60),.2f)==OrderResult.Accepted&&ops.Operations.Count==3,"Tres frentes independientes sin duplicar la reserva");
            float before=ops.Troops[f];int id=ops.Operations[0].id;Check(ops.RetreatOperation(id).success&&ops.Troops[f]==before,"Retirar una operaciÃ³n conserva sus supervivientes");
            Check(ops.Operations.Count(o=>o.active)==2,"La retirada no cancela los otros frentes");Tick(ops,2);Counts(ops);
            var fort=Fixture();var plain=Fixture();f=fort.PlayerFaction;int enemy=fort.World.At(145,60),outpost=fort.World.At(150,60);
            Check(fort.Construct(1,outpost,BuildingKind.Outpost).success,"Puesto defensivo construido");Tick(fort,5);Tick(plain,5);
            fort.AttackBorder(f,enemy,.9f);plain.AttackBorder(f,enemy,.9f);float a=fort.Troops[f],b=plain.Troops[f];Tick(fort,1.1f);Tick(plain,1.1f);
            Check(a-fort.Troops[f]>(b-plain.Troops[f])*3,"La fortificaciÃ³n aumenta las pÃ©rdidas del atacante");

            var diplomacy=Fixture();f=diplomacy.PlayerFaction;Check(diplomacy.ProposeAlliance(f,1).success,"Propuesta pendiente");
            Check(diplomacy.AnswerAlliance(1,f,true).success&&diplomacy.AreAllied(f,1),"Aceptar alianza");
            Check(diplomacy.AttackBorder(f,diplomacy.World.At(145,60),.25f)==OrderResult.Allied,"La alianza bloquea ataques");
            double totalGold=diplomacy.Gold.Sum();Check(diplomacy.Donate(f,1,1000,false).success&&Math.Abs(diplomacy.Gold.Sum()-totalGold)<.01,"Donar oro conserva el total");
            float totalTroops=diplomacy.Troops.Sum();Check(diplomacy.Donate(f,1,100,true).success&&Mathf.Abs(diplomacy.Troops.Sum()-totalTroops)<.01,"Donar tropas conserva el total");
            Check(diplomacy.RenewAlliance(f,1).success&&diplomacy.Betray(f,1).success&&!diplomacy.AreAllied(f,1)&&diplomacy.BetrayalUntil[f]>diplomacy.ElapsedTime,"RenovaciÃ³n, ruptura y marca temporal");
            diplomacy.ProtectionUntil[1]=diplomacy.ElapsedTime+60;Check(diplomacy.AttackBorder(f,diplomacy.World.At(145,60),.25f)==OrderResult.Protected,"ProtecciÃ³n inicial bloquea ataques");

            var nuclear=Fixture();f=nuclear.PlayerFaction;int silo=nuclear.World.At(120,64),sam=nuclear.World.At(180,64),target=nuclear.World.At(190,64);
            Check(!nuclear.LaunchWeapon(f,target,WeaponKind.Atomic).success,"Lanzamiento sin silo rechazado");
            Check(nuclear.Construct(f,silo,BuildingKind.Silo).success&&nuclear.Construct(1,sam,BuildingKind.SAM).success,"Silo y defensa antiaÃ©rea");Tick(nuclear,6);
            bool intercepted=false;nuclear.StrategicEventRaised+=e=>{if(e.kind==StrategicEventKind.Interception)intercepted=true;};
            Check(nuclear.LaunchWeapon(f,target,WeaponKind.Atomic).success,"Lanzamiento con municiÃ³n y oro");Tick(nuclear,10);Check(intercepted&&nuclear.Radiation.Count==0,"IntercepciÃ³n antes del impacto");
            nuclear.DestroyBuilding(nuclear.BuildingAt(sam),f);Check(nuclear.LaunchWeapon(f,target,WeaponKind.Atomic).success,"Segundo disparo disponible");Tick(nuclear,10);
            Check(nuclear.Radiation.Count>0&&nuclear.Radiation.Keys.All(c=>nuclear.Owners[c]==-1),"Impacto deja radiaciÃ³n neutral sin regalar territorio");
            Check(nuclear.ValidLandCount<nuclear.World.LandCount,"RadiaciÃ³n excluida de superficie computable");Counts(nuclear);
            Tick(nuclear,40);Check(nuclear.LaunchWeapon(f,nuclear.World.At(225,80),WeaponKind.MIRV).success,"Lanzar MIRV");Tick(nuclear,8.1f);
            Check(nuclear.Projectiles.Any(p=>p.kind==WeaponKind.Warhead)&&nuclear.Projectiles.Count==350,"MIRV distribuye 350 ojivas independientes");
            nuclear.Stars[f]=50000;double gold=nuclear.Gold[f];Check(nuclear.BuyArsenal(f,ArsenalKind.Reinforcements).success&&nuclear.Gold[f]==gold,"Estrellas separadas del oro");
            nuclear.Troops[f]=100;Check(nuclear.UseArsenal(f,ArsenalKind.Reinforcements,-1).success&&nuclear.Troops[f]<=nuclear.Capacity[f]&&nuclear.ArsenalCharges(f,ArsenalKind.Reinforcements)==0,"Habilidad consumible y lÃ­mite de capacidad");
            var deterministicA=Fixture();var deterministicB=Fixture();f=deterministicA.PlayerFaction;
            deterministicA.AttackBorder(f,deterministicA.World.At(145,60),.35f);deterministicB.AttackBorder(f,deterministicB.World.At(145,60),.35f);
            for(int k=0;k<100;k++)deterministicA.Step(.1f);for(int k=0;k<200;k++)deterministicB.Step(.05f);
            Check(deterministicA.Owners.SequenceEqual(deterministicB.Owners)&&Mathf.Abs(deterministicA.Troops[f]-deterministicB.Troops[f])<.01,"SimulaciÃ³n fija independiente de la frecuencia de fotogramas");
            var abilities=Fixture();f=abilities.PlayerFaction;abilities.Stars[f]=1000000;gold=abilities.Gold[f];
            foreach(ArsenalKind kind in Enum.GetValues(typeof(ArsenalKind)))
            {Check(abilities.BuyArsenal(f,kind).success,"Compra de arsenal: "+kind);if(kind==ArsenalKind.Reinforcements)abilities.Troops[f]=100;Check(abilities.UseArsenal(f,kind,abilities.World.At(190,64)).success&&abilities.ArsenalCharges(f,kind)==0,"Uso funcional de arsenal: "+kind);}
            Check(abilities.Gold[f]==gold,"El arsenal no gasta oro ocultamente");Tick(abilities,9);Check(abilities.Radiation.Count>0,"Megabomba del arsenal resuelve un impacto");
            var heavy=Fixture();f=heavy.PlayerFaction;heavy.Construct(f,heavy.World.At(120,64),BuildingKind.Silo);Tick(heavy,5);gold=heavy.Gold[f];
            Check(heavy.LaunchWeapon(f,heavy.World.At(200,64),WeaponKind.Mega).success&&heavy.LaunchWeapon(f,heavy.World.At(225,80),WeaponKind.Hydrogen).success,"Megabomba y bomba de hidrÃ³geno disponibles");
            Check(Math.Abs(heavy.Gold[f]-(gold-heavy.Rules.Weapon(WeaponKind.Mega).cost-heavy.Rules.Weapon(WeaponKind.Hydrogen).cost))<.01,"Costes nucleares descontados una vez");Tick(heavy,9);Check(heavy.Radiation.Count>0,"Impactos de armas pesadas");
            var defeat=Fixture();f=defeat.PlayerFaction;int last=defeat.World.At(144,60);
            for(int cell=0;cell<defeat.Owners.Length;cell++)if(defeat.Owners[cell]==f&&cell!=last){defeat.Owners[cell]=-1;defeat.TerritoryCounts[f]--;}
            defeat.RebuildCampaignIndexes();defeat.Troops[f]=0;defeat.AttackBorder(1,last,1);Tick(defeat,1);Check(defeat.Outcome==CampaignOutcome.Defeat&&defeat.TerritoryCounts[f]==0,"Perder todo el territorio provoca derrota");
            var victory=Fixture();victory.Rules.victoryFraction=.47f;victory.EvaluateVictory();Check(victory.Outcome==CampaignOutcome.Victory&&victory.Paused,"Victoria termina la partida");
            Check(Mathf.Abs(CampaignBalance.GrowthPerTick(0,100000)-10)<.001f&&CampaignBalance.GrowthPerTick(100000,100000)==0,"RegeneraciÃ³n de referencia en reserva vacÃ­a y llena");
            Check(CampaignBalance.Capacity(100,0,25000)>CampaignBalance.Capacity(10,0,25000),"La tierra aumenta capacidad sin depender de ciudades");
            Check(Mathf.Abs(CampaignBalance.Capacity(100,2,25000)-CampaignBalance.Capacity(100,0,25000)-50000)<1,"Perfil de ciudad 25000");
            Check(Mathf.Abs(CampaignBalance.Capacity(100,2,250000)-CampaignBalance.Capacity(100,0,250000)-500000)<1,"Perfil separado de ciudad 250000");
            var modifiers=new CombatModifiers();float loss=CampaignBalance.AttackLoss(80,10000,5000,100,modifiers),timeCost=CampaignBalance.EnemyTime(16.5f,10000,5000,modifiers);
            modifiers.largeAttackerLoss=2;Check(Mathf.Abs(CampaignBalance.AttackLoss(80,10000,5000,100,modifiers)-(80*2*(.463f*2+.39f)))<.01f,"Modificador de atacante grande independiente");
            modifiers=new CombatModifiers{largeDefenderLoss=2};Check(CampaignBalance.AttackLoss(80,10000,5000,100,modifiers)>loss,"Modificador de defensor grande independiente");
            modifiers=new CombatModifiers{traitorLoss=.5f};Check(Mathf.Abs(CampaignBalance.AttackLoss(80,10000,5000,100,modifiers)-loss*.5f)<.001f,"Modificador de bajas por traiciÃ³n");
            modifiers=new CombatModifiers{largeAttackerSpeed=2};Check(Mathf.Abs(CampaignBalance.EnemyTime(16.5f,10000,5000,modifiers)-timeCost*2)<.001f,"Modificador temporal de atacante grande");
            modifiers=new CombatModifiers{largeDefenderSpeed=2,traitorSpeed=.5f};Check(Mathf.Abs(CampaignBalance.EnemyTime(16.5f,10000,5000,modifiers)-timeCost)<.001f,"Modificadores temporales de defensor y traiciÃ³n independientes");
            Check(CampaignBalance.NeutralTime(25,1000)>CampaignBalance.NeutralTime(16.5f,1000)&&CampaignBalance.NeutralTime(16.5f,100000)==5,"MontaÃ±a mÃ¡s lenta y lÃ­mites temporales neutros");
            Check(Math.Abs(CampaignBalance.MaritimeReward(100)-(10000+150*Math.Pow(100,1.1)))<.001,"Recompensa marÃ­tima de la referencia adjunta");
            var reference=Fixture(BalanceProfile.RoFrontPrototype);f=reference.PlayerFaction;reference.ReinforcementsEnabled=false;
            Check(reference.Rules.baseIncome==1000,"Ingreso pasivo configurado de 1000 por segundo");
            double ledgerBefore=reference.Ledgers[f].passive;Tick(reference,1);Check(Math.Abs(reference.Ledgers[f].passive-ledgerBefore-1000)<.01,"Ingreso pasivo registrado separado del transporte");
            int isolated=reference.World.At(60,60);reference.Construct(f,isolated,BuildingKind.Factory);Tick(reference,15);Check(!reference.TradeRoutes.Any(r=>r.owner==f),"Una fÃ¡brica aislada no inventa transporte hacia una capital no configurada");
            reference.Construct(f,reference.World.At(90,60),BuildingKind.City);float capacityBefore=reference.Capacity[f],troopsBefore=reference.Troops[f];Tick(reference,2);
            Check(reference.Capacity[f]==capacityBefore,"La ciudad incompleta no da capacidad");Tick(reference,3);Check(reference.Capacity[f]>capacityBefore&&reference.Troops[f]==troopsBefore,"La ciudad terminada da capacidad sin regalar soldados");
            Tick(reference,12);Check(reference.TradeRoutes.Any(r=>r.owner==f&&!r.naval),"La segunda estaciÃ³n conecta una carretera y camiÃ³n reales");
            var road=reference.TradeRoutes.First(r=>r.owner==f);reference.DestroyBuilding(reference.BuildingAt(road.path[road.path.Count-1]),f);Tick(reference,1);Check(!reference.TradeRoutes.Contains(road),"Destruir una estaciÃ³n invalida su conexiÃ³n");
            reference.Rules.balanceProfile=BalanceProfile.OpenFront34Reference;
            Check(reference.ConstructionPrice(f,BuildingKind.Port)==250000,"El contador econÃ³mico compartido incluye la fÃ¡brica completada");
            int neutral=reference.World.At(80,80);reference.Owners[neutral]=-1;reference.TerritoryCounts[f]--;reference.RebuildCampaignIndexes();float oldCapacity=reference.Capacity[f];reference.Troops[f]=1000;
            Check(reference.BeginExpansion(f,.25f)==OrderResult.Accepted,"Iniciar una ofensiva de referencia vÃ¡lida");float committed=reference.CommittedTroops[f];
            Check(committed==250&&reference.AvailableTroops(f)==750,"Compromiso entero separado inmediatamente de la reserva");Tick(reference,7);
            Check(reference.Owners[neutral]==f&&reference.Troops[f]<1000&&reference.Capacity[f]>oldCapacity,"Conquista contigua consume soldados y aumenta capacidad");
            reference.CancelOrder(f);reference.Troops[f]=.5f;neutral=reference.World.At(81,80);reference.Owners[neutral]=-1;reference.TerritoryCounts[f]--;reference.RebuildCampaignIndexes();
            Check(reference.BeginExpansion(f,1)==OrderResult.NoTroops&&reference.Owners[neutral]==-1,"Sin presupuesto no hay expansiÃ³n");
            var neighbors=Fixture(BalanceProfile.RoFrontPrototype);f=neighbors.PlayerFaction;int neighborCity=neighbors.World.At(155,60),homeFactory=neighbors.World.At(135,60);
            neighbors.Construct(1,neighborCity,BuildingKind.City);neighbors.Construct(f,homeFactory,BuildingKind.Factory);Tick(neighbors,20);
            Check(neighbors.CanTrade(f,1)&&neighbors.TradeRoutes.Any(r=>(r.fromBuilding==neighbors.BuildingAt(homeFactory).id&&r.toBuilding==neighbors.BuildingAt(neighborCity).id||r.toBuilding==neighbors.BuildingAt(homeFactory).id&&r.fromBuilding==neighbors.BuildingAt(neighborCity).id)),"Vecinos en paz conectan infraestructura a travÃ©s de su frontera");
            Check(neighbors.Ledgers[f].landTrade>0&&neighbors.Ledgers[1].landTrade>0,"Una entrega comercial beneficia a ambos propietarios");
            Check(neighbors.IncomeRate(1)>neighbors.Rules.baseIncome,"El HUD incluye el ingreso recibido desde conexiones de un vecino");
            Check(neighbors.AttackBorder(f,neighbors.World.At(145,60),.1f)==OrderResult.Accepted,"Una ofensiva vÃ¡lida inicia hostilidades");Tick(neighbors,1);
            Check(!neighbors.CanTrade(f,1)&&!neighbors.TradeRoutes.Any(r=>(r.fromBuilding==neighbors.BuildingAt(homeFactory).id&&r.toBuilding==neighbors.BuildingAt(neighborCity).id||r.toBuilding==neighbors.BuildingAt(homeFactory).id&&r.fromBuilding==neighbors.BuildingAt(neighborCity).id)),"La guerra interrumpe las rutas comerciales afectadas");
            neighbors.ProposeWarTruce(f,1);neighbors.AnswerCampaign(1,neighbors.Affairs.proposals.Last().id,true);Check(neighbors.CanTrade(f,1),"La tregua negociada permite recuperar el comercio");
            Check(FrontierPrototype.QuickBuildings.SequenceEqual(new[]{BuildingKind.City,BuildingKind.Port,BuildingKind.Factory,BuildingKind.Outpost,BuildingKind.SAM,BuildingKind.Silo}),"Teclas 1â€“6 coinciden con la barra de estructuras");
            var unfinished=Fixture(BalanceProfile.RoFrontPrototype);f=unfinished.PlayerFaction;int cityTarget=unfinished.World.At(145,60);unfinished.Construct(1,cityTarget,BuildingKind.City);unfinished.Troops[1]=0;unfinished.Troops[f]=100000;unfinished.AttackBorder(f,cityTarget,.5f);Tick(unfinished,.2f);
            Check(unfinished.BuildingAt(cityTarget).owner==f&&!unfinished.BuildingAt(cityTarget).Operational,"Capturar una ciudad incompleta no completa gratis su construcciÃ³n");
            ReferenceNavigationChecks();DirectionalExpansionValidation.Run();TacticalValidation.Run();
            string output=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"campaign-validation.json"),"{\"result\":\"passed\",\"assertions\":"+_checks+",\"systems\":[\"named spawn\",\"economy\",\"capacity\",\"upgrades\",\"land trade\",\"sea trade\",\"warships\",\"parallel attacks\",\"retreat\",\"fortifications\",\"alliances\",\"donations\",\"betrayal\",\"protection\",\"silos\",\"SAM\",\"radiation\",\"MIRV\",\"arsenal\",\"fixed timestep\",\"victory\"]}");
            Debug.Log("FRONTIER_CAMPAIGN_CHECKS_OK assertions="+_checks);
        }
    }
}
