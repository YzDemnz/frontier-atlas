using System;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public bool TacticalBalance=>CampaignEnabled&&Rules.balanceProfile==BalanceProfile.TacticalRealtime;
        public float TotalTroops(int faction)=>TacticalBalance?Troops[faction]+CommittedTroops[faction]:Troops[faction];
        private void CommitTroops(int faction,float amount){if(TacticalBalance)Troops[faction]=Mathf.Max(0,Troops[faction]-amount);}
        public float StoredTroops(int faction)=>TacticalBalance?AvailableTroops(faction):Troops[faction];
        public double PassiveIncome(int faction)=>TacticalBalance?Rules.tacticalPassiveIncome:Rules.baseIncome*AIMultiplier(faction);
        public float MinimumExpansionTroops=>TacticalBalance?Rules.tacticalNeutralCost/World.CellAreaFactor:ReferenceBalance?16/(Rules.normalizeTerritoryArea?World.CellAreaFactor:1):NeutralSectorCost;
        private float TacticalCapacity(int f)=>IsQuickMatch?QuickCapacity(f):Rules.baseCapacity+Mathf.Pow(Mathf.Max(0,NormalizedArea(f)),.45f)*320+_cityLevels[f]*Rules.cityCapacity;
        public float SupportedCityLevels(int f)
        {
            float levels=0;var seen=new System.Collections.Generic.HashSet<int>();
            foreach(var route in TradeRoutes)
            {
                if(route.naval||route.disabledUntil>ElapsedTime)continue;var from=BuildingById(route.fromBuilding);var to=BuildingById(route.toBuilding);
                if(from==null||to==null||!from.Operational||!to.Operational||from.owner!=f||to.owner!=f)continue;
                Building city=from.kind==BuildingKind.Factory&&to.kind==BuildingKind.City?to:to.kind==BuildingKind.Factory&&from.kind==BuildingKind.City?from:null;
                if(city!=null&&seen.Add(city.id))levels+=city.level;
            }
            return levels;
        }
        private float[] _recruitmentCache;private float _recruitmentUntil;private int _recruitmentVersion=-1;
        private float TacticalRecruitment(int f)
        {
            if(_recruitmentCache==null||ElapsedTime>=_recruitmentUntil||_recruitmentVersion!=TradeNetworkVersion)
            {
                if(_recruitmentCache==null)_recruitmentCache=new float[FactionCount];Array.Clear(_recruitmentCache,0,_recruitmentCache.Length);
                // Each completed city contributes once, regardless of route count or upgrade level.
                foreach(var city in Buildings)if(city.Operational&&city.kind==BuildingKind.City)
                    _recruitmentCache[city.owner]+=Rules.tacticalCityRecruitment;
                _recruitmentUntil=ElapsedTime+.5f;_recruitmentVersion=TradeNetworkVersion;
            }
            float rate=Rules.tacticalBaseRegeneration*(1+_recruitmentCache[f]);return rate*Mathf.Clamp01(1-AvailableTroops(f)/Mathf.Max(1,Capacity[f]))*AIMultiplier(f);
        }
        private bool ResolveTacticalCell(MilitaryOperation op,int cell,float dt,bool landing)
        {
            int defender=Owners[cell];
            if(defender>=0&&AvailableTroops(defender)>.001f)
            {
                if(op.battleEnemy!=defender){op.battleEnemy=defender;op.battleRate=Mathf.Max(Rules.tacticalMinimumBattleRate,Mathf.Min(op.troops,AvailableTroops(defender))/Rules.tacticalBattleSeconds);}
                float fort=OutpostAt(defender,cell)?Rules.outpostLossMultiplier:1;
                float vulnerability=BetrayalUntil[defender]>ElapsedTime?Rules.betrayalVulnerability:1;
                float defended=Mathf.Min(AvailableTroops(defender),Mathf.Min(op.troops/fort*vulnerability,op.battleRate*dt*vulnerability));
                float loss=defended/vulnerability*fort;SpendOperation(op,loss);Troops[defender]=Mathf.Max(0,Troops[defender]-defended);Statistics[defender].troopsLost+=defended;
                LastCombat[op.faction]=LastCombat[defender]=ElapsedTime;op.state=ArmyState.Fighting;BattleOccurred?.Invoke(op.faction,defender,loss);
                if(op.troops<.001f)EndCampaignOperation(op,ArmyState.NoTroops);
                // Engagement time does not also buy occupation of a tile.
                return false;
            }
            op.battleEnemy=-1;
            float cost=(defender<0?Rules.tacticalNeutralCost:Rules.tacticalOccupationCost)/World.CellAreaFactor*TerrainResistance(cell);
            if(op.troops+.0001f<cost){SpendOperation(op,op.troops);EndCampaignOperation(op,ArmyState.NoTroops);return false;}
            _resolvingOperation=landing?op:null;_captureCostOverride=0;bool captured;
            try{captured=Capture(op.faction,cell);}finally{_captureCostOverride=-1;_resolvingOperation=null;}
            if(!captured){EndCampaignOperation(op,ArmyState.Blocked);return false;}
            SpendOperation(op,cost);op.state=ArmyState.Marching;return true;
        }
        private void AdvanceTacticalFront(MilitaryOperation op,float dt)
        {
            int first=op.kind==OperationKind.Expansion?SelectFreeBoundary(op.faction):EnemyContact(op.faction,op.enemy,op.target);
            if(first<0){EndCampaignOperation(op,ArmyState.Arrived);return;}
            if(Owners[first]>=0&&AvailableTroops(Owners[first])>.001f){ResolveCampaignCell(op,first,dt,false);return;}
            float rate=(op.kind==OperationKind.Expansion?Rules.tacticalExpansionSpeed:Rules.tacticalOccupationSpeed)*Mathf.Clamp(Mathf.Sqrt(op.troops/2500f),.25f,4);
            op.clock=Mathf.Min(op.clock+rate*dt,Rules.tacticalCapturesPerTick*2);
            for(int n=0;n<Rules.tacticalCapturesPerTick&&op.active;n++)
            {
                int cell=n==0?first:op.kind==OperationKind.Expansion?SelectFreeBoundary(op.faction):EnemyContact(op.faction,op.enemy,op.target);
                if(cell<0){EndCampaignOperation(op,ArmyState.Arrived);return;}
                float time=TerrainResistance(cell)*(Owners[cell]>=0&&OutpostAt(Owners[cell],cell)?Rules.outpostTimeMultiplier:1);
                if(op.clock<time)return;op.clock-=time;op.frontCell=cell;if(op.kind==OperationKind.Expansion)op.target=cell;
                if(!ResolveCampaignCell(op,cell,dt,false))return;if(op.kind==OperationKind.Expansion)RecordDirectedCapture(op.faction,cell);
            }
        }
    }
}
