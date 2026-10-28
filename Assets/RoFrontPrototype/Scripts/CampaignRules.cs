using System;
using UnityEngine;
namespace FrontierAtlas
{
    public enum FactoryProductionProfile { Atlas, OpenFront35Reference, HistoricalProbability }
    public enum SamBalanceProfile { Atlas, OpenFront34Reference }
    public enum BuildingKind { City, Factory, Port, Outpost, Silo, SAM }
    public enum WeaponKind { Atomic, Mega, Hydrogen, MIRV, Warhead, Scatter, Artillery, AirStrike, Railgun }
    public enum ArsenalKind { Mega, Scatter, Reinforcements, Artillery, AirStrike, Railgun }
    public enum CampaignOutcome { Playing, Victory, Defeat }
    [Serializable] public sealed class BuildingDefinition
    {
        public BuildingKind kind;public string name;public double cost,maxCost;public float radius,health=500;public bool coastal,destroyOnCapture;
        public BuildingDefinition(){}
        public BuildingDefinition(BuildingKind k,string n,double c,double max,float r=0,bool coast=false,bool destroy=false){kind=k;name=n;cost=c;maxCost=max;radius=r;coastal=coast;destroyOnCapture=destroy;}
    }
    [Serializable] public sealed class WeaponDefinition
    {
        public WeaponKind kind;public string name;public double cost;public float radius,range,damage;public bool radiation=true;
        public WeaponDefinition(){}
        public WeaponDefinition(WeaponKind k,string n,double c,float r,float reach,float d,bool irradiates=true){kind=k;name=n;cost=c;radius=r;range=reach;damage=d;radiation=irradiates;}
    }
    [Serializable] public sealed class CampaignRules
    {
        public FactoryProductionProfile factoryProductionProfile=FactoryProductionProfile.Atlas;
        public SamBalanceProfile samBalanceProfile=SamBalanceProfile.Atlas;
        public float factoryConnectionMinimum=15,factoryConnectionRadius=18,factoryConnectionPathLimit=120;
        public float factoryDispatchSeconds=6,factoryLevelProductionBonus=.35f,trainMinimumDispatchSeconds=1;
        public int trainSaturationMidpoint=560,trainSaturationTail=900,maximumLogicalTrains=1024,maximumTrainStops=8;
        public double trainOwnReward=10000,trainNeutralReward=25000,trainAlliedReward=50000;
        public float[] samRadiusByLevel={70,81.42857f,90,96.66667f,102};
        public float samReferenceCooldown=9,samInterceptorSpeed=110,samHydrogenPriority=70001;
        public float missileArcHeight=.25f,missileProjectedCurve=.12f;
        public float campaignSecondsPerDay=30;
        public BalanceProfile balanceProfile=BalanceProfile.TacticalRealtime;
        public bool normalizeTerritoryArea=true,priceCountsPending=true,sharedEconomicPriceCounter=false,allowCapitalTrade=false,allowDomesticSeaTrade=true,payBothPortOwners=true,distanceBasedSeaReward=true;
        public float openFrontCityCapacity=250000,neutralBotLossMultiplier=.8f,frontBudgetMultiplier=.25f;
        public int maximumCapturesPerOperationTick=4;
        public double tacticalPassiveIncome=1800;
        public float tacticalInitialGold=250000,tacticalBaseRegeneration=8,tacticalCityRecruitment=.35f,tacticalAiExpansionReserve=3000;
        public float tacticalNeutralCost=18,tacticalOccupationCost=.6f,tacticalExpansionSpeed=48,tacticalOccupationSpeed=160;
        public float tacticalBattleSeconds=2.4f,tacticalMinimumBattleRate=250,tacticalBoatSpeed=5;
        public int tacticalCapturesPerTick=48;
        public CombatModifiers combatModifiers=new CombatModifiers();
        public bool captureGoldOnElimination=false;public float capturedGoldFraction=.25f;
        public float initialTroops=10000,baseCapacity=20000,cityCapacity=100000,baseRegeneration=15,cityRegeneration=5;
        public double initialGold=500000,baseIncome=1000,tradeReward=15000,warshipCost=250000;
        public float protectionSeconds=60,brutalProtectionSeconds=120,victoryFraction=.8f;
        public float buildSeconds=4,buildingSpacing=6,landTradeRange=180,seaTradeRange=1000,truckSpeed=6,tradeShipSpeed=1.4f;
        public float portDwellSeconds=6;
        public int navigationNodesPerTick=4096;
        public float directionalExpansionWeight=75,otherExpansionWeight=35,directionalSectorCosine=.25f;
        public int factoryConnections=3,portConnections=2;public bool peacefulNeighborTrade=true;public float tradeWarCooldown=120,landDestinationRewardShare=.5f;
        public float boatSpeed=.8f,warshipSpeed=1.1f,warshipHealth=500,warshipDamage=55,warshipDetection=100,warshipRange=12;
        public float outpostLossMultiplier=5,outpostTimeMultiplier=3,samCooldown=8,missileSpeed=35,missileMinimumSeconds=8;
        public float siloCooldown=35,radiationSeconds=180,radiationResistance=1.8f,mountainResistance=2.5f,highlandResistance=1.5f,desertResistance=1.25f;
        public float allianceSeconds=240,allianceWarningSeconds=30,betrayalSeconds=30,betrayalVulnerability=1.3f;
        public int maximumOperations=6,mirvWarheads=350,tribes=16;public float tribeTroops=720,impassableElevation=1.1f;
        public bool samInterceptsMIRV=false,attackEndsProtection=true;
        public int[] arsenalCosts={20000,30000,40000,50000,60000,75000};
        public BuildingDefinition[] buildings={
            new BuildingDefinition(BuildingKind.City,"Ciudad",125000,1000000),new BuildingDefinition(BuildingKind.Factory,"Fábrica",125000,1000000),
            new BuildingDefinition(BuildingKind.Port,"Puerto",250000,1000000,0,true),new BuildingDefinition(BuildingKind.Outpost,"Puesto defensivo",50000,250000,30),
            new BuildingDefinition(BuildingKind.Silo,"Silo de misiles",1000000,4000000,0,false,true),new BuildingDefinition(BuildingKind.SAM,"Defensa antiaérea",1500000,3000000,70,false,true)};
        public WeaponDefinition[] weapons={
            new WeaponDefinition(WeaponKind.Atomic,"Bomba atómica",1000000,30,1100,1800),new WeaponDefinition(WeaponKind.Mega,"Megabomba",2500000,60,1600,4000),
            new WeaponDefinition(WeaponKind.Hydrogen,"Bomba de hidrógeno",5000000,100,2100,9000),new WeaponDefinition(WeaponKind.MIRV,"Misil de ojivas múltiples",25000000,6,5000,600),
            new WeaponDefinition(WeaponKind.Warhead,"Ojiva",0,6,5000,600),new WeaponDefinition(WeaponKind.Scatter,"Ataque disperso",0,10,600,350,false),
            new WeaponDefinition(WeaponKind.Artillery,"Artillería",0,14,200,700,false),new WeaponDefinition(WeaponKind.AirStrike,"Ataque aéreo",0,22,700,1000,false),
            new WeaponDefinition(WeaponKind.Railgun,"Cañón electromagnético",0,4,1400,1400,false)};
        public static CampaignRules Load()
        {
            var rules=new CampaignRules();var text=Resources.Load<TextAsset>("CampaignRules");if(text!=null)JsonUtility.FromJsonOverwrite(text.text,rules);return rules;
        }
        public BuildingDefinition Building(BuildingKind kind)=>Array.Find(buildings,x=>x.kind==kind);
        public WeaponDefinition Weapon(WeaponKind kind)=>Array.Find(weapons,x=>x.kind==kind);
    }
    public readonly struct CampaignAction
    {
        public readonly bool success;public readonly string message;
        public CampaignAction(bool ok,string text){success=ok;message=text;}
        public static CampaignAction Ok(string text)=>new CampaignAction(true,text);
        public static CampaignAction Fail(string text)=>new CampaignAction(false,text);
    }
    [Serializable] public sealed class FactionStatistics
    {public int peakTerritory,nationsEliminated,buildingsDestroyed;public float peakTroops,troopsLost;public double goldEarned;}
    public enum StrategicEventKind { Information, IncomingMissile, Explosion, Interception, BuildingComplete, ShipSunk, Alliance, Victory }
    public readonly struct StrategicEvent
    {
        public readonly StrategicEventKind kind;public readonly int faction,other,cell;public readonly float radius;public readonly string message;
        public StrategicEvent(StrategicEventKind k,int f,int o,int c,string text,float r=0){kind=k;faction=f;other=o;cell=c;message=text;radius=r;}
    }
}
