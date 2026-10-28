using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public bool CampaignEnabled=>Options!=null&&Options.advancedSystems;
        public CampaignRules Rules {get;private set;}
        public double[] Gold {get;private set;}
        public int[] Stars {get;private set;}
        public float[] Capacity,ProtectionUntil,BetrayalUntil;
        public int[] Teams;
        public FactionStatistics[] Statistics;
        public CampaignOutcome Outcome {get;private set;}=CampaignOutcome.Playing;
        public int WinningFaction {get;private set;}=-1;
        public string EndReason {get;private set;}="";
        public event Action<StrategicEvent> StrategicEventRaised;
        public event Action<int> StrategicCellChanged;
        public readonly HashSet<int> Obstacles=new HashSet<int>();
        private HashSet<int>[] _owned;
        public IEnumerable<int> OwnedCells(int faction)=>_owned[faction];
        private float[] _starRemainder,_economyThink;
        private float[] _cityLevels;
        private float _campaignAccumulator,_victoryClock;
        public bool LimitFrameWork=true;
        public bool IsTribe(int f)=>Options!=null&&f>=World.Countries.Length+(Options.customPlayer?1:0);
        public int ValidLandCount=>Mathf.Max(1,World.LandCount-(CampaignEnabled?Radiation.Count:0));
        public float WorldControl(int f)=>TerritoryCounts[f]/(float)ValidLandCount;
        public float ProtectionRemaining(int f)=>Mathf.Max(0,ProtectionUntil[f]-ElapsedTime);
        public bool IsImpassable(int cell)=>!World.Land[cell]||Obstacles.Contains(cell)||(CampaignEnabled&&World.Elevation[cell]>=Rules.impassableElevation);
        private void InitializeCampaign()
        {
            Rules=CampaignRules.Load();ApplyQuickMatchRules();Ledgers=new EconomyLedger[FactionCount];Gold=new double[FactionCount];Stars=new int[FactionCount];Capacity=new float[FactionCount];ProtectionUntil=new float[FactionCount];BetrayalUntil=new float[FactionCount];
            Teams=new int[FactionCount];Statistics=new FactionStatistics[FactionCount];_starRemainder=new float[FactionCount];_economyThink=new float[FactionCount];_owned=new HashSet<int>[FactionCount];
            _cityLevels=new float[FactionCount];
            for(int f=0;f<FactionCount;f++)
            {
                _owned[f]=new HashSet<int>();Ledgers[f]=new EconomyLedger();Statistics[f]=new FactionStatistics();Capacity[f]=CampaignEnabled?Rules.baseCapacity:2000;
                Teams[f]=Options!=null&&Options.teams?(f==PlayerFaction?0:f%4):f;
                if(!CampaignEnabled)continue;
                Troops[f]=CapitalCells[f]<0&&f!=PlayerFaction?0:IsTribe(f)?Rules.tribeTroops:IsQuickMatch&&f!=PlayerFaction?(Options.difficulty==Difficulty.Relaxed?1250:Options.difficulty==Difficulty.Brutal?3125:1875):Rules.initialTroops;Capacity[f]=IsTribe(f)?Rules.tribeTroops*3:Rules.baseCapacity;
                Gold[f]=IsTribe(f)?0:TacticalBalance?Rules.tacticalInitialGold:Rules.initialGold;ProtectionUntil[f]=IsTribe(f)?0:(Options.difficulty==Difficulty.Brutal?Rules.brutalProtectionSeconds:Rules.protectionSeconds);
                _aiTimers[f]=f%17*.29f;_economyThink[f]=15+(float)_random.NextDouble()*25;
            }
            InitializeCampaignAffairs();
            if(CampaignEnabled){RebuildCampaignIndexes();for(int f=0;f<FactionCount;f++)RecomputeCapacity(f);}
        }
        public void RebuildCampaignIndexes()
        {
            ResetTradePathIndex();foreach(var set in _owned)set.Clear();foreach(var border in _frontiers)border.Clear();
            for(int cell=0;cell<Owners.Length;cell++)if(Owners[cell]>=0){_owned[Owners[cell]].Add(cell);RefreshFrontier(cell);}
            for(int f=0;f<FactionCount;f++)RecomputeCapacity(f);RebuildContacts();
            if(_freeSeeded!=null)System.Array.Clear(_freeSeeded,0,_freeSeeded.Length);
        }
        private void CampaignFounded()
        {
            if(!CampaignEnabled)return;int f=PlayerFaction;
            ProtectionUntil[f]=ElapsedTime+(Options.difficulty==Difficulty.Brutal?Rules.brutalProtectionSeconds:Rules.protectionSeconds);
            for(int y=-4;y<=4;y++)for(int x=-4;x<=4;x++){int px=CapitalCells[f]%World.Width+x,py=CapitalCells[f]/World.Width+y;if(World.InBounds(px,py)){int id=World.At(px,py);if(Owners[id]==f)_owned[f].Add(id);}}
            foreach(int cell in _owned[f])UpdateContacts(f,cell);RecomputeCapacity(f);Statistics[f].peakTerritory=TerritoryCounts[f];Statistics[f].peakTroops=Troops[f];
        }
        public bool CanAttackNation(int f,int other)
        {
            if(other<0)return true;if(other==f)return false;
            return !AreAllied(f,other)&&!CampaignPeaceBlocks(f,other)&&ProtectionRemaining(other)<=0;
        }
        public float TerrainResistance(int cell)
        {
            if(!CampaignEnabled)return 1;float value=World.Elevation[cell]>.60f?Rules.mountainResistance:World.Elevation[cell]>.42f?Rules.highlandResistance:World.Moisture[cell]<.3f?Rules.desertResistance:1;
            return value*(IsIrradiated(cell)?Rules.radiationResistance:1);
        }
        private float CampaignRegeneration(int f)=>(IsQuickMatch?1f:RecruitmentBoost)*UnboostedRegeneration(f);
        private float UnboostedRegeneration(int f)
        {
            if(TerritoryCounts[f]==0||StoredTroops(f)>=Capacity[f])return 0;if(IsQuickMatch)return QuickGrowth(f);if(TacticalBalance)return IsTribe(f)?.6f:TacticalRecruitment(f);if(IsTribe(f))return .5f;
            if(ReferenceBalance)return CampaignBalance.GrowthPerTick(AvailableTroops(f),Capacity[f])*10*AIMultiplier(f);
            float cities=_cityLevels[f];
            float rate=Rules.baseRegeneration+Mathf.Sqrt(TerritoryCounts[f]/World.CellAreaFactor)*.6f+cities*Rules.cityRegeneration;
            float fullness=Troops[f]/Mathf.Max(1,Capacity[f]);return rate*(1-fullness*fullness)*AIMultiplier(f);
        }
        private float AIMultiplier(int f)=>f==PlayerFaction||Options==null?1:Options.difficulty==Difficulty.Relaxed? .8f:Options.difficulty==Difficulty.Brutal?1.2f:1;
        private void CampaignCaptured(int f,int cell,int previous)
        {
            if(!CampaignEnabled)return;TradeOwnershipChanged(cell);if(previous>=0)_owned[previous].Remove(cell);_owned[f].Add(cell);
            if(Radiation.Remove(cell)){NuclearScars.Remove(cell);StrategicCellChanged?.Invoke(cell);}
            UpdateContacts(f,cell);CaptureInfrastructure(f,cell);if(previous>=0&&CapitalCells[previous]==cell&&TerritoryCounts[previous]>0){foreach(int relocation in _owned[previous]){CapitalCells[previous]=relocation;break;}Chronicle("capital",f,previous,Names[f]+" ocupó la capital de "+Names[previous]+"; su gobierno se trasladó a territorio superviviente.");}if(ReferenceBalance){RefreshTerritorialCapacity(f);if(previous>=0)RefreshTerritorialCapacity(previous);}_starRemainder[f]+=1/World.CellAreaFactor;
            if(_starRemainder[f]>=4){int reward=(int)(_starRemainder[f]/4);Stars[f]+=reward;_starRemainder[f]-=reward*4;}
            if(previous>=0&&TerritoryCounts[previous]==0)NationEliminated(f,previous);
        }
        private void NationEliminated(int attacker,int victim)
        {
            if(attacker==victim)attacker=-1;
            if(attacker>=0){Statistics[attacker].nationsEliminated++;Stars[attacker]+=500;}
            if(attacker>=0&&Rules.captureGoldOnElimination){double taken=Gold[victim]*Mathf.Clamp01(Rules.capturedGoldFraction);Gold[victim]-=taken;CreditGold(attacker,taken,GoldFlow.Capture);}
            SettleEliminatedDebts(victim);
            foreach(var war in Affairs.wars)if(war.active&&(war.attacker==victim||war.defender==victim)){war.active=false;war.endedDay=CampaignDay;}
            CancelCampaignOperations(victim);Troops[victim]=0;States[victim]=ArmyState.Eliminated;
            Emit(StrategicEventKind.Information,attacker,victim,CapitalCells[victim],Names[victim]+" ha sido eliminada.");
            if(victim==PlayerFaction)FinishCampaign(attacker,false,"Tu nación ha perdido todo su territorio.");
        }
        private void Emit(StrategicEventKind kind,int faction,int other,int cell,string text,float radius=0)
            =>StrategicEventRaised?.Invoke(new StrategicEvent(kind,faction,other,cell,text,radius));
        private void UnclaimCell(int cell,int responsible)
        {
            int previous=Owners[cell];if(previous<0)return;TradeOwnershipChanged(cell);
            Owners[cell]=Neutral;TerritoryCounts[previous]--;if(ReferenceBalance)RefreshTerritorialCapacity(previous);_owned[previous].Remove(cell);_frontiers[previous].Remove(cell);
            for(int d=0;d<4;d++){int n=World.Neighbor(cell,d);if(n>=0)RefreshFrontier(n);}
            for(int d=0;d<4;d++){int n=World.Neighbor(cell,d);if(n>=0&&Owners[n]>=0)OfferFreeBoundary(Owners[n],cell,0);}
            Changed=true;TerritoryCaptured?.Invoke(-1,cell,previous);
            if(TerritoryCounts[previous]==0)NationEliminated(responsible,previous);
        }
        public void InflictLoss(int f,float amount)
        {
            float loss=Mathf.Min(Mathf.Max(0,amount),Troops[f]);Troops[f]-=loss;Statistics[f].troopsLost+=loss;
            if(TacticalBalance)return;
            float excess=CommittedTroops[f]-Troops[f];
            if(excess>0)foreach(var op in Operations)
            {if(!op.active||op.faction!=f)continue;float take=Mathf.Min(op.troops,excess);op.troops-=take;excess-=take;if(op.troops<.001f)op.active=false;if(excess<=0)break;}
            SyncOperations();
        }
        private void FinishCampaign(int winner,bool won,string reason)
        {
            if(Outcome!=CampaignOutcome.Playing)return;WinningFaction=winner;Outcome=won?CampaignOutcome.Victory:CampaignOutcome.Defeat;EndReason=reason;Paused=true;
            Emit(StrategicEventKind.Victory,winner,PlayerFaction,winner>=0?CapitalCells[winner]:-1,reason);
        }
        public void EvaluateVictory()
        {
            if(!CampaignEnabled||Outcome!=CampaignOutcome.Playing||CapitalCells[PlayerFaction]<0)return;
            if(TerritoryCounts[PlayerFaction]==0)
            {CancelCampaignOperations(PlayerFaction);Troops[PlayerFaction]=0;States[PlayerFaction]=ArmyState.Eliminated;FinishCampaign(-1,false,"Tu nación ha perdido todo su territorio.");return;}
            for(int f=0;f<FactionCount;f++)
            {
                int total=0;for(int g=0;g<FactionCount;g++)if(g==f||(Options.teams&&Teams[g]==Teams[f]))total+=TerritoryCounts[g];
                if(total/(float)ValidLandCount<Rules.victoryFraction)continue;
                bool won=f==PlayerFaction||(Options.teams&&Teams[f]==Teams[PlayerFaction]);
                FinishCampaign(f,won,Names[f]+" ha alcanzado el "+(Rules.victoryFraction*100).ToString("0")+"% de control mundial.");break;
            }
        }
        private void AdvanceCampaign(float dt)
        {
            if(Outcome!=CampaignOutcome.Playing)return;_campaignAccumulator+=dt*Mathf.Clamp(CampaignSpeed,.25f,20);
            if(LimitFrameWork)_campaignAccumulator=Mathf.Min(_campaignAccumulator,2);
            float fixedTick=ReferenceBalance?.1f:.05f;
            long frameStart=System.Diagnostics.Stopwatch.GetTimestamp();int steps=0;while(_campaignAccumulator>=fixedTick&&!Paused&&steps<64)
            {
                if(LimitFrameWork&&steps>0&&(System.Diagnostics.Stopwatch.GetTimestamp()-frameStart)/(double)System.Diagnostics.Stopwatch.Frequency>.008)break;steps++;
                float tick=fixedTick;_campaignAccumulator-=tick;ElapsedTime+=tick;
                AdvanceSeaPlanning();
                long stamp=MeterStart();AdvanceEconomy(tick);MeterEnd(0,stamp);
                stamp=MeterStart();AdvanceDiplomacy(tick);AdvanceCampaignPolitics(tick);MeterEnd(1,stamp);
                stamp=MeterStart();AdvanceWeapons(tick);MeterEnd(2,stamp);
                stamp=MeterStart();AdvanceWarships(tick);MeterEnd(3,stamp);
                stamp=MeterStart();AdvanceCampaignOperations(tick);MeterEnd(4,stamp);
                for(int f=0;f<FactionCount;f++)
                {
                    if(TerritoryCounts[f]==0)continue;
                    if(ReinforcementsEnabled&&(ReferenceBalance||ElapsedTime-LastCombat[f]>15)&&StoredTroops(f)<Capacity[f])Troops[f]+=Mathf.Min(Capacity[f]-StoredTroops(f),CampaignRegeneration(f)*tick);
                    Statistics[f].peakTerritory=Mathf.Max(Statistics[f].peakTerritory,TerritoryCounts[f]);Statistics[f].peakTroops=Mathf.Max(Statistics[f].peakTroops,TotalTroops(f));
                    if(!AiEnabled||f==PlayerFaction)continue;_aiTimers[f]-=tick;_economyThink[f]-=tick;
                    if(_aiTimers[f]<=0){_aiTimers[f]=Options.difficulty==Difficulty.Brutal?5+f%11*.13f:Options.difficulty==Difficulty.Relaxed?15:9;long army=MeterStart();ThinkCampaign(f);MeterEnd(5,army);}
                    if(_economyThink[f]<=0){_economyThink[f]=Options.difficulty==Difficulty.Brutal?20:35;long development=MeterStart();ThinkDevelopment(f);MeterEnd(6,development);}
                }
                _victoryClock+=tick;if(_victoryClock>=1){_victoryClock=0;EvaluateVictory();}
            }
        }
    }
}
