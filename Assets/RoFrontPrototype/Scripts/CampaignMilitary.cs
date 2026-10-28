using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public enum OperationKind { Expansion, Land, Naval }
    public sealed class MilitaryOperation
    {
        public int id,faction,enemy=-1,target,frontCell,currentCell=-1,nextCell=-1,landing=-1;
        public OperationKind kind;public ArmyState state=ArmyState.Marching;public bool active=true;
        public int battleEnemy=-1;public float battleRate;public bool planning;public float troops,progress,clock;public Queue<int> route=new Queue<int>();
    }
    public sealed partial class TerritorySimulation
    {
        public readonly List<MilitaryOperation> Operations=new List<MilitaryOperation>();
        private int _operationId;
        private MilitaryOperation _resolvingOperation;
        private bool CanCampaignLand(int f,int cell)
        {
            if(!CampaignEnabled||_resolvingOperation==null||_resolvingOperation.faction!=f||_resolvingOperation.landing!=cell)return false;
            for(int d=0;d<4;d++)if(World.Neighbor(cell,d)==_resolvingOperation.currentCell)return true;return false;
        }
        private int OperationCount(int f){int count=0;foreach(var op in Operations)if(op.active&&op.faction==f)count++;return count;}
        private OrderResult CanOrder(int f)
        {if(Paused||Outcome!=CampaignOutcome.Playing)return OrderResult.Paused;if(TerritoryCounts[f]==0)return OrderResult.Eliminated;return OrderResult.Accepted;}
        private OrderResult StartCampaignExpansion(int f,float fraction)
        {
            var result=CanOrder(f);if(result!=OrderResult.Accepted)return result;
            float budget=PreviewTroops(f,fraction);if(budget<MinimumExpansionTroops)return OrderResult.NoTroops;
            EnsureFreeBoundary(f);int first=SelectFreeBoundary(f);if(first<0)return OrderResult.NoFreeLand;
            foreach(var existing in Operations)if(existing.active&&existing.faction==f&&existing.kind==OperationKind.Expansion){CommitTroops(f,budget);existing.troops+=budget;SyncOperations();return OrderResult.Accepted;}
            if(OperationCount(f)>=Rules.maximumOperations)return OrderResult.OperationLimit;
            CommitTroops(f,budget);Operations.Add(new MilitaryOperation{id=++_operationId,faction=f,target=first,frontCell=first,kind=OperationKind.Expansion,troops=budget});SyncOperations();return OrderResult.Accepted;
        }
        private OrderResult StartCampaignAttack(int f,int cell,float fraction,bool separateFront=false)
        {
            var result=CanOrder(f);if(result!=OrderResult.Accepted)return result;
            if(!IsBorderTarget(f,cell)||IsImpassable(cell))return OrderResult.NotAdjacent;
            int enemy=Owners[cell];if(enemy<0)return StartCampaignExpansion(f,fraction);
            if(AreAllied(f,enemy)||CampaignPeaceBlocks(f,enemy))return OrderResult.Allied;if(ProtectionRemaining(enemy)>0)return OrderResult.Protected;
            float budget=PreviewTroops(f,fraction);if(budget<OccupationSectorCost*TerrainResistance(cell))return OrderResult.NoTroops;
            foreach(var existing in Operations)if(!separateFront&&existing.active&&existing.faction==f&&existing.kind==OperationKind.Land&&existing.enemy==enemy){CommitTroops(f,budget);existing.troops+=budget;existing.target=cell;SyncOperations();return OrderResult.Accepted;}
            if(OperationCount(f)>=Rules.maximumOperations)return OrderResult.OperationLimit;
            RegisterAggression(f,enemy,WarObjective.Territory,cell);if(Rules.attackEndsProtection)ProtectionUntil[f]=0;
            CommitTroops(f,budget);Operations.Add(new MilitaryOperation{id=++_operationId,faction=f,enemy=enemy,target=cell,frontCell=cell,kind=OperationKind.Land,troops=budget});SyncOperations();return OrderResult.Accepted;
        }
        private int EnemyContact(int f,int enemy,int target)=>StrategicContact(f,enemy,target);
        private OrderResult StartCampaignInvasion(int f,int target,float fraction)
        {
            var result=CanOrder(f);if(result!=OrderResult.Accepted)return result;
            if(target<0||target>=Owners.Length)return OrderResult.NoLandRoute;if(Owners[target]==f)return OrderResult.OwnTerritory;
            if(Owners[target]>=0){if(AreAllied(f,Owners[target])||CampaignPeaceBlocks(f,Owners[target]))return OrderResult.Allied;if(ProtectionRemaining(Owners[target])>0)return OrderResult.Protected;}
            if(OperationCount(f)>=Rules.maximumOperations)return OrderResult.OperationLimit;
            if(!HasCoastalAccess(f))return OrderResult.NoCoastalAccess;
            float budget=PreviewTroops(f,fraction);if(budget<MinimumExpansionTroops)return OrderResult.NoTroops;
            int landing=-1,destination=target;
            if(World.Land[target])
            {
                float best=float.PositiveInfinity;int region=World.GeographicCountries[target];
                foreach(int coast in World.CoastCells)
                {
                    if(Owners[coast]==f||IsImpassable(coast))continue;
                    if(Owners[coast]>=0&&!CanAttackNation(f,Owners[coast]))continue;
                    float score=Distance(coast,target)+(World.GeographicCountries[coast]!=region?World.Width:0);
                    if(score<best){landing=coast;best=score;}
                }
                if(landing<0)return OrderResult.NoLandRoute;destination=AdjacentWater(landing);if(destination<0)return OrderResult.NoLandRoute;
            }
            int origin=-1;float distance=float.PositiveInfinity;
            foreach(int coast in _owned[f])if(AdjacentWater(coast)>=0)
                for(int d=0;d<4;d++){int n=World.Neighbor(coast,d);if(n<0||World.Land[n])continue;float value=Distance(n,destination);if(value<distance){distance=value;origin=n;}}
            if(origin<0)return OrderResult.NoCoastalAccess;
            var path=new Queue<int>();
            var op=new MilitaryOperation{id=++_operationId,faction=f,enemy=Owners[target],target=target,frontCell=origin,kind=OperationKind.Naval,troops=budget,currentCell=origin,nextCell=path.Count>0?path.Peek():origin,landing=landing,route=path,state=ArmyState.Sailing};
            op.planning=true;CommitTroops(f,budget);Operations.Add(op);PlanSeaRoute(origin,destination,route=>{op.planning=false;if(!op.active)return;if(route==null){EndCampaignOperation(op,ArmyState.Blocked);SyncOperations();return;}op.route=route;op.nextCell=route.Count>0?route.Peek():op.currentCell;},f==PlayerFaction);if(!op.active)return OrderResult.NoLandRoute;if(Owners[target]>=0)RegisterAggression(f,Owners[target],WarObjective.Naval,target);if(Owners[target]>=0&&Rules.attackEndsProtection)ProtectionUntil[f]=0;SyncOperations();FleetLaunched?.Invoke(f);return OrderResult.Accepted;
        }
        public int AdjacentWater(int land)
        {for(int d=0;d<4;d++){int n=World.Neighbor(land,d);if(n>=0&&!World.Land[n])return n;}return -1;}
        public CampaignAction ReinforceOperation(int id,float fraction)
        {
            var op=Operations.Find(x=>x.id==id&&x.active);if(op==null)return CampaignAction.Fail("La operación ya terminó.");
            float amount=PreviewTroops(op.faction,fraction);if(amount<OccupationSectorCost)return CampaignAction.Fail("No hay tropas disponibles para reforzar.");
            if(op.kind==OperationKind.Naval)return CampaignAction.Fail("Un transporte en el mar no puede recibir tropas desde tierra.");
            CommitTroops(op.faction,amount);op.troops+=amount;SyncOperations();return CampaignAction.Ok("Refuerzos enviados desde la reserva.");
        }
        public CampaignAction RetreatOperation(int id)
        {
            var op=Operations.Find(x=>x.id==id&&x.active);if(op==null)return CampaignAction.Fail("La operación ya terminó.");
            EndCampaignOperation(op,ArmyState.Idle);SyncOperations();return CampaignAction.Ok("Retirada completada. Supervivientes disponibles en reserva.");
        }
        public CampaignAction RetargetTransport(int id,int target)
        {
            var op=Operations.Find(x=>x.id==id&&x.active&&x.kind==OperationKind.Naval);if(op==null)return CampaignAction.Fail("Selecciona un transporte activo.");
            int landing=-1,destination=target;
            if(World.Land[target])
            {float best=float.PositiveInfinity;foreach(int coast in World.CoastCells){if(Owners[coast]==op.faction)continue;float d=Distance(coast,target);if(d<best){best=d;landing=coast;}}if(landing<0)return CampaignAction.Fail("No hay costa accesible.");destination=AdjacentWater(landing);}
            if(destination<0)return CampaignAction.Fail("No hay costa accesible.");int origin=OperationCell(op);int beach=landing;op.target=target;op.landing=beach;op.planning=true;op.currentCell=op.nextCell=origin;op.clock=op.progress=0;op.route.Clear();
            PlanSeaRoute(origin,destination,route=>{op.planning=false;if(!op.active)return;if(route==null){EndCampaignOperation(op,ArmyState.Blocked);SyncOperations();return;}op.route=route;op.target=target;op.landing=beach;op.nextCell=route.Count>0?route.Peek():origin;op.state=ArmyState.Sailing;},op.faction==PlayerFaction);
            SyncOperations();return CampaignAction.Ok("Nuevo destino recibido. Calculando la ruta por agua.");
        }
        private void EndCampaignOperation(MilitaryOperation op,ArmyState state)
        {if(!op.active)return;if(TacticalBalance){if(TerritoryCounts[op.faction]>0)Troops[op.faction]+=Mathf.Max(0,op.troops);else{Statistics[op.faction].troopsLost+=Mathf.Max(0,op.troops);op.troops=0;}}op.active=false;States[op.faction]=state;}
        private void CancelCampaignOperations(int f)
        {foreach(var op in Operations)if(op.faction==f&&op.active)EndCampaignOperation(op,ArmyState.Idle);States[f]=ArmyState.Idle;SyncOperations();}
        private void SyncOperations()
        {
            if(!CampaignEnabled)return;
            for(int f=0;f<FactionCount;f++){Targets[f]=AttackFactions[f]=-1;CommittedTroops[f]=0;FleetActive[f]=false;}
            foreach(var op in Operations)
            {
                if(!op.active)continue;int f=op.faction;CommittedTroops[f]+=Mathf.Max(0,op.troops);
                if(Targets[f]<0){Targets[f]=op.target;AttackFactions[f]=op.enemy;States[f]=op.state;LastAdvanceCells[f]=op.frontCell;}
                if(op.kind==OperationKind.Naval&&!FleetActive[f]){FleetActive[f]=true;FleetCells[f]=op.currentCell;FleetNext[f]=op.nextCell;FleetProgress[f]=op.progress;}
            }
        }
        private void SpendOperation(MilitaryOperation op,float losses)
        {losses=Mathf.Min(losses,op.troops);op.troops-=losses;CommittedTroops[op.faction]=Mathf.Max(0,CommittedTroops[op.faction]-losses);if(!TacticalBalance)Troops[op.faction]=Mathf.Max(0,Troops[op.faction]-losses);Statistics[op.faction].troopsLost+=losses;}
        private bool ResolveCampaignCell(MilitaryOperation op,int cell,float dt,bool landing)
        {
            int f=op.faction,defender=Owners[cell];op.frontCell=cell;if(defender>=0)RecordHostility(f,defender);
            if(IsImpassable(cell)||!CanAttackNation(f,defender)){EndCampaignOperation(op,ArmyState.Blocked);return false;}
            if(IsQuickMatch)return ResolveReferenceCell(op,cell,landing);if(TacticalBalance)return ResolveTacticalCell(op,cell,dt,landing);if(ReferenceBalance)return ResolveReferenceCell(op,cell,landing);
            if(defender>=0&&AvailableTroops(defender)>.001f)
            {
                float density=AvailableTroops(defender)/Mathf.Max(1,TerritoryCounts[defender]/World.CellAreaFactor);
                float fort=OutpostAt(defender,cell)?Rules.outpostLossMultiplier:1;
                float vulnerability=BetrayalUntil[defender]>ElapsedTime?Rules.betrayalVulnerability:1;
                float defenderLoss=Mathf.Min(BattleLossPerSecond*dt*vulnerability,AvailableTroops(defender));
                float required=defenderLoss*fort*Mathf.Clamp(Mathf.Sqrt(density+1)/12,.75f,1.5f);
                float attackerLoss=Mathf.Min(op.troops,required);
                if(required>op.troops&&required>0)defenderLoss*=op.troops/required;
                SpendOperation(op,attackerLoss);Troops[defender]=Mathf.Max(0,Troops[defender]-defenderLoss);Statistics[defender].troopsLost+=defenderLoss;
                LastCombat[f]=LastCombat[defender]=ElapsedTime;op.state=ArmyState.Fighting;BattleOccurred?.Invoke(f,defender,attackerLoss);
                if(op.troops<OccupationSectorCost)EndCampaignOperation(op,ArmyState.NoTroops);return false;
            }
            float cost=(defender<0?NeutralSectorCost:OccupationSectorCost)*TerrainResistance(cell);
            if(op.troops+.001f<cost){EndCampaignOperation(op,ArmyState.NoTroops);return false;}
            _resolvingOperation=landing?op:null;float before=Troops[f];bool captured=Capture(f,cell);_resolvingOperation=null;
            if(!captured){EndCampaignOperation(op,ArmyState.Blocked);return false;}
            op.troops=Mathf.Max(0,op.troops-(before-Troops[f]));op.state=ArmyState.Marching;return true;
        }
        private void AdvanceCampaignOperations(float dt)
        {
            SyncOperations();
            foreach(var op in Operations)
            {
                if(!op.active||Paused||op.planning)continue;
                if(TerritoryCounts[op.faction]==0){EndCampaignOperation(op,ArmyState.Eliminated);continue;}
                if(op.troops<(TacticalBalance?.001f:OccupationSectorCost)){EndCampaignOperation(op,ArmyState.NoTroops);continue;}
                if(op.kind==OperationKind.Naval)
                {
                    AdvanceNavalMotion(ref op.currentCell,ref op.nextCell,ref op.clock,ref op.progress,op.route,dt,(TacticalBalance?Rules.tacticalBoatSpeed:Rules.boatSpeed)*World.ResolutionScale);op.frontCell=OperationCell(op);
                    if(op.route.Count>0){op.state=ArmyState.Sailing;continue;}
                    if(op.landing<0){op.state=ArmyState.Anchored;op.clock=0;continue;}
                    if(Owners[op.landing]!=op.faction&&!ResolveCampaignCell(op,op.landing,dt,true))continue;
                    Landed?.Invoke(op.faction);op.kind=OperationKind.Land;op.enemy=Owners[op.target];op.clock=0;
                    if(op.enemy==op.faction||op.enemy<0||EnemyContact(op.faction,op.enemy,op.target)<0){op.kind=OperationKind.Expansion;op.enemy=-1;EnsureFreeBoundary(op.faction);}continue;
                }
                if(IsQuickMatch){AdvanceReferenceFront(op,dt);continue;}if(TacticalBalance){AdvanceTacticalFront(op,dt);continue;}if(ReferenceBalance){AdvanceReferenceFront(op,dt);continue;}
                op.clock+=dt;int cell=op.kind==OperationKind.Expansion?SelectFreeBoundary(op.faction):EnemyContact(op.faction,op.enemy,op.target);
                if(cell<0){EndCampaignOperation(op,ArmyState.Arrived);continue;}
                float intervalLand=MarchInterval/World.CellAreaFactor*TerrainResistance(cell)*(Owners[cell]>=0&&OutpostAt(Owners[cell],cell)?Rules.outpostTimeMultiplier:1);
                if(op.clock<intervalLand)continue;op.clock-=intervalLand;if(op.kind==OperationKind.Expansion)op.target=cell;
                if(ResolveCampaignCell(op,cell,intervalLand,false)&&op.kind==OperationKind.Expansion)RecordDirectedCapture(op.faction,cell);
            }
            Operations.RemoveAll(x=>!x.active);SyncOperations();
        }
        private void ThinkCampaign(int f)
        {
            if(TacticalBalance){if(!IsQuickMatch&&Options.difficulty==Difficulty.Brutal&&!IsTribe(f))ThinkGrandMilitary(f);else ThinkTactical(f);return;}
            if(AvailableTroops(f)<50||OperationCount(f)>=Rules.maximumOperations)return;
            if(IsTribe(f)){if(_random.NextDouble()<.25)StartCampaignExpansion(f,.15f);return;}
            EnsureFreeBoundary(f);bool free=SelectFreeBoundary(f)>=0;
            if(free)StartCampaignExpansion(f,.3f);
            int enemy=-1,point=-1,threat=-1;float favorable=0;
            foreach(int other in _neighborNations[f])
            {
                if(!CanAttackNation(f,other))continue;int cell=ContactCell(f,other);if(cell<0)continue;
                if(AvailableTroops(other)>AvailableTroops(f)*2.5f)threat=other;float advantage=AvailableTroops(f)-AvailableTroops(other)*1.25f;
                if(advantage>favorable){favorable=advantage;enemy=other;point=cell;}
            }
            if(point>=0&&AvailableTroops(f)>120)StartCampaignAttack(f,point,Options.difficulty==Difficulty.Relaxed? .25f:.5f);
            else if(!free&&HasCoastalAccess(f)&&AvailableTroops(f)>150&&_random.NextDouble()<.3)
            {
                int coast=-1;float best=float.PositiveInfinity;foreach(int c in World.CoastCells){if(Owners[c]==f||!CanAttackNation(f,Owners[c]))continue;float value=Distance(CapitalCells[f],c);if(value<best){best=value;coast=c;}}
                if(coast>=0)StartCampaignInvasion(f,coast,.35f);
            }
            if(threat>=0&&_random.NextDouble()<.2)ProposeAlliance(f,threat);
        }
    }
}
