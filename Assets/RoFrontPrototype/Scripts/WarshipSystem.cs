using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class Warship
    {
        public int id,owner,currentCell,nextCell,target=-1,enemyOperation=-1;public float health,progress,clock,nextPlan;public bool sunk;
        public bool planning;public Queue<int> route=new Queue<int>();
    }
    public sealed partial class TerritorySimulation
    {
        public readonly List<Warship> Warships=new List<Warship>();
        private int _warshipId;private float _navalCombatClock;
        public double WarshipPrice(int f)
        {int count=0;foreach(var ship in Warships)if(!ship.sunk&&ship.owner==f)count++;return System.Math.Min(750000,Rules.warshipCost*(1+count*.25));}
        public CampaignAction DeployWarship(int f,int portCell)
        {
            if(!CampaignEnabled||Paused||Outcome!=CampaignOutcome.Playing)return CampaignAction.Fail("Reanuda la partida para desplegar un buque.");
            var port=BuildingAt(portCell);if(port==null||port.owner!=f||port.kind!=BuildingKind.Port||!port.Operational)return CampaignAction.Fail("Necesitas un puerto propio operativo.");
            int water=AdjacentWater(portCell);if(water<0)return CampaignAction.Fail("El puerto no tiene acceso al mar.");
            double price=WarshipPrice(f);if(Gold[f]<price)return CampaignAction.Fail("No hay suficiente oro para el buque.");
            Gold[f]-=price;Ledgers[f].militarySpent+=price;Warships.Add(new Warship{id=++_warshipId,owner=f,currentCell=water,nextCell=water,health=Rules.warshipHealth});
            return CampaignAction.Ok("Buque desplegado. Interceptará amenazas cercanas automáticamente.");
        }
        public CampaignAction MoveWarship(int id,int target)
        {
            var ship=Warships.Find(s=>s.id==id&&!s.sunk);if(ship==null)return CampaignAction.Fail("El buque no está disponible.");
            if(target<0||target>=Owners.Length)return CampaignAction.Fail("Objetivo fuera del mapa.");
            if(World.Land[target]&&(Owners[target]<0||!CanAttackNation(ship.owner,Owners[target])))return CampaignAction.Fail("El bombardeo requiere una costa enemiga sin protección.");
            int goal=World.Land[target]?AdjacentWater(target):target;if(goal<0)return CampaignAction.Fail("Elige agua o una posición costera.");
            if(World.Land[target])RegisterAggression(ship.owner,Owners[target],WarObjective.Bombardment,target);int origin=WarshipCell(ship);ship.planning=true;ship.currentCell=ship.nextCell=origin;ship.route.Clear();ship.clock=ship.progress=0;ship.enemyOperation=-1;ship.target=target;
            PlanSeaRoute(origin,goal,route=>{ship.planning=false;if(ship.sunk||route==null)return;ship.route=route;ship.target=target;ship.nextCell=route.Count>0?route.Peek():origin;},ship.owner==PlayerFaction);
            return CampaignAction.Ok(World.Land[target]?"Buque enviado para bombardear la costa.":"Destino del buque actualizado.");
        }
        public int TradeCell(TradeRoute route)
        {
            if(route.path==null||route.path.Count==0)return -1;Vector3 position=TradePosition(route,out _);
            return World.At(Mathf.Clamp(Mathf.FloorToInt(position.x),0,World.Width-1),Mathf.Clamp(Mathf.FloorToInt(position.z),0,World.Height-1));
        }
        private void AdvanceWarships(float dt)
        {
            foreach(var ship in Warships)
            {
                if(ship.sunk)continue;if(TerritoryCounts[ship.owner]==0){ship.sunk=true;continue;}
                AdvanceNavalMotion(ref ship.currentCell,ref ship.nextCell,ref ship.clock,ref ship.progress,ship.route,dt,Rules.warshipSpeed*World.ResolutionScale);
            }
            _navalCombatClock+=dt;if(_navalCombatClock<.5f)return;float elapsed=_navalCombatClock;_navalCombatClock=0;
            foreach(var ship in Warships)
            {
                if(ship.sunk)continue;MilitaryOperation target=null;float best=Rules.warshipDetection;
                foreach(var op in Operations)
                {
                    if(!op.active||op.kind!=OperationKind.Naval||!CanAttackNation(ship.owner,op.faction)||!IsHostile(ship.owner,op.faction))continue;
                    float d=Distance(WarshipCell(ship),OperationCell(op));if(d<best){best=d;target=op;}
                }
                if(target!=null)
                {
                    if(best<=Rules.warshipRange)
                    {
                        SpendOperation(target,Rules.warshipDamage*elapsed);LastCombat[target.faction]=LastCombat[ship.owner]=ElapsedTime;
                        LastAdvanceCells[ship.owner]=OperationCell(target);BattleOccurred?.Invoke(ship.owner,target.faction,Rules.warshipDamage*elapsed);
                        if(target.troops<OccupationSectorCost){EndCampaignOperation(target,ArmyState.NoTroops);Emit(StrategicEventKind.ShipSunk,ship.owner,target.faction,OperationCell(target),"Un transporte de "+Names[target.faction]+" ha sido hundido.");}
                    }
                    else if(!ship.planning&&ship.nextPlan<=ElapsedTime)
                    {ship.nextPlan=ElapsedTime+4;int origin=WarshipCell(ship);ship.planning=true;ship.currentCell=ship.nextCell=origin;ship.route.Clear();ship.clock=ship.progress=0;PlanSeaRoute(origin,OperationCell(target),route=>{ship.planning=false;if(!ship.sunk&&route!=null){ship.route=route;ship.nextCell=route.Count>0?route.Peek():origin;}});}
                }
                foreach(var other in Warships)
                {
                    if(other.id==ship.id||other.sunk||!CanAttackNation(ship.owner,other.owner)||!IsHostile(ship.owner,other.owner)||Distance(WarshipCell(ship),WarshipCell(other))>Rules.warshipRange)continue;
                    other.health-=Rules.warshipDamage*elapsed;if(other.health<=0){other.sunk=true;Emit(StrategicEventKind.ShipSunk,ship.owner,other.owner,other.currentCell,"Buque enemigo hundido.");}break;
                }
                foreach(var route in TradeRoutes)
                {
                    if(!route.naval||route.disabledUntil>ElapsedTime||!CanAttackNation(ship.owner,route.owner)||!IsHostile(ship.owner,route.owner))continue;int cell=TradeCell(route);
                    if(cell<0||Distance(WarshipCell(ship),cell)>4)continue;route.disabledUntil=ElapsedTime+30;route.clock=0;
                    double bounty=route.reward*.5;CreditGold(ship.owner,bounty,GoldFlow.Capture);Emit(StrategicEventKind.ShipSunk,ship.owner,route.owner,cell,"Un barco comercial ha sido interceptado.");
                }
                if(ship.target>=0&&World.Land[ship.target]&&Owners[ship.target]>=0&&CanAttackNation(ship.owner,Owners[ship.target])&&Distance(WarshipCell(ship),ship.target)<=Rules.warshipRange)
                {
                    int defender=Owners[ship.target];float damage=Mathf.Min(AvailableTroops(defender),Rules.warshipDamage*elapsed*.5f);
                    Troops[defender]-=damage;Statistics[defender].troopsLost+=damage;LastCombat[defender]=ElapsedTime;LastAdvanceCells[ship.owner]=ship.target;
                    BattleOccurred?.Invoke(ship.owner,defender,damage);var building=BuildingAt(ship.target);if(building!=null){building.health-=Rules.warshipDamage*elapsed;if(building.health<=0)DestroyBuilding(building,ship.owner);}
                }
                bool nearHome=false;for(int d=0;d<4;d++){int n=World.Neighbor(WarshipCell(ship),d);if(n>=0&&Owners[n]==ship.owner)nearHome=true;}
                if(nearHome)ship.health=Mathf.Min(Rules.warshipHealth,ship.health+12*elapsed);
            }
            SyncOperations();
        }
    }
}
