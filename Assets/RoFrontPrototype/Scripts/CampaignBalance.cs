using System;
using UnityEngine;
namespace FrontierAtlas
{
    public enum BalanceProfile { RoFrontPrototype, OpenFront34Reference, LegacyPrototype, TacticalRealtime }
    public enum GoldFlow { Passive, LandTrade, SeaTrade, Donation, Capture }
    [Serializable] public sealed class EconomyLedger
    {public double passive,landTrade,seaTrade,donationsReceived,donationsSent,captured,constructionSpent,militarySpent;}
    [Serializable] public sealed class CombatModifiers
    {public float largeAttackerLoss=1,largeDefenderLoss=1,largeAttackerSpeed=1,largeDefenderSpeed=1,traitorLoss=1,traitorSpeed=1;}
    public static class CampaignBalance
    {
        public static float Capacity(float area,float cityLevels,float cityBonus)=>2*(Mathf.Pow(Mathf.Max(0,area),.6f)*1000+50000)+Mathf.Max(0,cityLevels)*cityBonus;
        public static float GrowthPerTick(float reserve,float capacity)
        {if(capacity<=0||reserve>=capacity)return 0;reserve=Mathf.Max(0,reserve);return Mathf.Max(0,(10+Mathf.Pow(reserve,.73f)/4)*(1-reserve/capacity));}
        public static float NeutralTime(float terrainCost,float attackTroops)=>Mathf.Clamp(2000*terrainCost/Mathf.Max(.001f,attackTroops),5,100);
        public static float AttackLoss(float magnitude,float defender,float attackers,float tiles,CombatModifiers m)
        {float ratio=Mathf.Max(0,defender)/Mathf.Max(.001f,attackers),density=Mathf.Max(0,defender)/Mathf.Max(1,tiles);return magnitude*m.traitorLoss*Mathf.Clamp(ratio,.6f,2)*(.463f*m.largeAttackerLoss*m.largeDefenderLoss+.0039f*density);}
        public static float EnemyTime(float terrainCost,float defender,float attackers,CombatModifiers m)
        {float ratio=Mathf.Max(0,defender)/Mathf.Max(.001f,attackers),speed=Mathf.Clamp(ratio,.82f,7.5f)*Mathf.Clamp(ratio/20,1,50)/8.55f;return speed*terrainCost*m.largeAttackerSpeed*m.largeDefenderSpeed*m.traitorSpeed;}
        public static double MaritimeReward(float distance)=>10000+150*Math.Pow(Math.Max(0,distance),1.1);
    }
    public sealed partial class TerritorySimulation
    {
        public EconomyLedger[] Ledgers {get;private set;}
        private float _captureCostOverride=-1;private bool _captureContested;
        public bool ReferenceBalance=>CampaignEnabled&&Rules.balanceProfile!=BalanceProfile.LegacyPrototype;
        private float NormalizedArea(int f)=>TerritoryCounts[f]/(Rules.normalizeTerritoryArea?World.CellAreaFactor:1);
        private float CityBonus=>Rules.balanceProfile==BalanceProfile.OpenFront34Reference?Rules.openFrontCityCapacity:Rules.cityCapacity;
        private float TerrainMagnitude(int cell)=>World.Elevation[cell]>.60f?120:World.Elevation[cell]>.42f?100:80;
        private float TerrainTime(int cell)=>World.Elevation[cell]>.60f?25:World.Elevation[cell]>.42f?20:16.5f;
        private float ReferenceLoss(MilitaryOperation op,int cell)
        {
            float factor=Rules.normalizeTerritoryArea?World.CellAreaFactor:1;int defender=Owners[cell];float radiation=IsIrradiated(cell)?Rules.radiationResistance:1;
            if(defender<0)return TerrainMagnitude(cell)/5/factor*radiation*(op.faction==PlayerFaction?1:Rules.neutralBotLossMultiplier);
            var m=Modifiers(op.faction,defender);return CampaignBalance.AttackLoss(TerrainMagnitude(cell),AvailableTroops(defender),op.troops,NormalizedArea(defender),m)/factor*radiation*(OutpostAt(defender,cell)?Rules.outpostLossMultiplier:1);
        }
        private CombatModifiers Modifiers(int attacker,int defender)
        {
            var m=Rules.combatModifiers;
            if(defender<0||BetrayalUntil[defender]<=ElapsedTime)return m;
            return new CombatModifiers{largeAttackerLoss=m.largeAttackerLoss,largeDefenderLoss=m.largeDefenderLoss,largeAttackerSpeed=m.largeAttackerSpeed,largeDefenderSpeed=m.largeDefenderSpeed,traitorLoss=m.traitorLoss/Rules.betrayalVulnerability,traitorSpeed=m.traitorSpeed/Rules.betrayalVulnerability};
        }
        private float ReferenceTime(MilitaryOperation op,int cell)
        {
            int defender=Owners[cell];float time=defender<0?CampaignBalance.NeutralTime(TerrainTime(cell),op.troops):CampaignBalance.EnemyTime(TerrainTime(cell),AvailableTroops(defender),op.troops,Modifiers(op.faction,defender));
            return time*(defender>=0&&OutpostAt(defender,cell)?Rules.outpostTimeMultiplier:1)*(IsIrradiated(cell)?Rules.radiationResistance:1);
        }
        private bool ResolveReferenceCell(MilitaryOperation op,int cell,bool landing)
        {
            int f=op.faction,defender=Owners[cell];float loss=ReferenceLoss(op,cell);
            if(op.troops+.001f<loss){SpendOperation(op,op.troops);EndCampaignOperation(op,ArmyState.NoTroops);return false;}
            if(defender>=0)
            {float defensiveLoss=Mathf.Min(AvailableTroops(defender),AvailableTroops(defender)/Mathf.Max(1,NormalizedArea(defender))/(Rules.normalizeTerritoryArea?World.CellAreaFactor:1));Troops[defender]-=defensiveLoss;Statistics[defender].troopsLost+=defensiveLoss;LastCombat[f]=LastCombat[defender]=ElapsedTime;BattleOccurred?.Invoke(f,defender,loss);}
            SpendOperation(op,loss);_resolvingOperation=landing?op:null;_captureCostOverride=0;_captureContested=true;
            bool captured;try{captured=Capture(f,cell);}finally{_captureCostOverride=-1;_captureContested=false;_resolvingOperation=null;}
            if(!captured){EndCampaignOperation(op,ArmyState.Blocked);return false;}op.state=defender>=0?ArmyState.Fighting:ArmyState.Marching;return true;
        }
        private void CreditGold(int faction,double amount,GoldFlow source)
        {
            if(amount<=0)return;RegisterCampaignIncome(faction,amount,source);Gold[faction]+=amount;var ledger=Ledgers[faction];
            switch(source){case GoldFlow.Passive:ledger.passive+=amount;break;case GoldFlow.LandTrade:ledger.landTrade+=amount;break;case GoldFlow.SeaTrade:ledger.seaTrade+=amount;break;case GoldFlow.Donation:ledger.donationsReceived+=amount;break;case GoldFlow.Capture:ledger.captured+=amount;break;}
            if(source!=GoldFlow.Donation)Statistics[faction].goldEarned+=amount;
        }
    }
}
