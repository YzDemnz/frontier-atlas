using System;
using System.Collections.Generic;
namespace FrontierAtlas
{
    public enum DiplomaticRelation { Hostility, Distrust, Neutrality, Friendship, Alliance }
    public enum DiplomaticPersonality { Peaceful, Expansionist, Opportunist, Defensive, Commercial }
    public enum CompactKind { Alliance, MutualDefense, NonAggression, FreeTrade, Protection, Ceasefire, Peace, Subsidy, Tribute }
    public enum ProposalKind { Treaty, Loan, RenegotiateDebt, CoalitionInvite, Peace, Demand, Mediation }
    public enum ProposalState { Pending, Accepted, Rejected, Countered, Expired }
    public enum DebtStatus { Active, Repaid, Refused, Defaulted, Forgiven, WrittenOff }
    public enum SupportKind { Troops, Gold, EmergencyLoan, Diversion, Intervention, NavalEscort, Bombardment, JointDefense, Mediation }
    public enum RequestState { Pending, Accepted, Rejected, Fulfilled, Expired }
    public enum WarObjective { Territory, Capital, Infrastructure, Recovery, Defense, Diversion, Naval, Bombardment }
    [Serializable] public sealed class DiplomaticMemory
    {
        public int observer,subject,wars,betrayals,promisesKept,promisesBroken,defaults;public float trust;
        public double goldHelp,troopHelp,lastWarDay=-1,lastAidDay=-1;
    }
    [Serializable] public sealed class CampaignEvent
    {public int id,actor,other;public double day,amount;public string kind,message;}
    [Serializable] public sealed class NegotiationTerms
    {
        public CompactKind compact=CompactKind.Alliance;public double durationDays=30;
        public double sourceGold,targetGold,sourceTroops,targetTroops,principal,interestPerPeriod,loanDays=90,periodicGold,periodDays=15,collateralValue,collateralGold;
        public float sourceTerritoryFraction,targetTerritoryFraction;
        public int[] sourceTerritory=Array.Empty<int>(),targetTerritory=Array.Empty<int>(),collateral=Array.Empty<int>();
        public int militaryEnemy=-1,loanId=-1,coalitionId=-1,mediatedNation=-1;public bool forgiveDebt,loanOfferedByLender;
    }
    [Serializable] public sealed class CampaignProposal
    {
        public int id,source,target,parent=-1;public ProposalKind kind;public ProposalState state;
        public double createdDay,expiresDay,responseDay;public NegotiationTerms terms=new NegotiationTerms();public string note="";
    }
    [Serializable] public sealed class CampaignCompact
    {
        public int id,source,target,militaryEnemy=-1;public CompactKind kind;public bool active=true,warningSent,commitmentFulfilled;
        public double beganDay,endsDay=-1,periodicGold,periodDays=15,nextPaymentDay;
    }
    [Serializable] public sealed class InternationalLoan
    {
        public int id,creditor,debtor,missedPayments;public DebtStatus status;
        public double originalPrincipal,principal,interest,interestPerPeriod,beganDay,maturityDay,nextInterestDay,collateralValue,collateralSettled;
        public int[] collateral=Array.Empty<int>();public double lastPaymentDay=-1,paid,forgiven,escrowGold;
        public double Outstanding=>Math.Max(0,principal+interest);
        public bool Payable=>status==DebtStatus.Active||status==DebtStatus.Defaulted;
    }
    [Serializable] public sealed class Embargo
    {public int source,target;public bool active=true;public double endsDay=-1;}
    [Serializable] public sealed class CampaignWar
    {
        public int id,attacker,defender,goalCell=-1;public WarObjective objective;public bool active=true;
        public double beganDay,endedDay=-1;public float attackerLossStart,defenderLossStart;
    }
    [Serializable] public sealed class Coalition
    {
        public int id,leader,enemy,goalCell=-1;public string name;public WarObjective objective;public bool active=true;
        public List<int> members=new List<int>();public double beganDay;
    }
    [Serializable] public sealed class MilitarySupportRequest
    {
        public int id,source,target,enemy=-1,cell=-1,coalitionId=-1;public SupportKind kind;public RequestState state;
        public double amount,createdDay,expiresDay,responseDay,nextAttemptDay;public string response="";public bool obligation,missionSent;
    }
    [Serializable] public sealed class CampaignWorldState
    {
        public int schema=1,sequence;public float[] reputation;public double[] periodIncome,nextDebtPayment,lastDiplomacyDay;
        public List<DiplomaticMemory> memories=new List<DiplomaticMemory>();public List<CampaignEvent> history=new List<CampaignEvent>();
        public List<CampaignProposal> proposals=new List<CampaignProposal>();public List<CampaignCompact> compacts=new List<CampaignCompact>();
        public List<InternationalLoan> loans=new List<InternationalLoan>();public List<Embargo> embargoes=new List<Embargo>();
        public List<CampaignWar> wars=new List<CampaignWar>();public List<Coalition> coalitions=new List<Coalition>();
        public List<MilitarySupportRequest> support=new List<MilitarySupportRequest>();
    }
    public sealed partial class TerritorySimulation
    {
        public CampaignWorldState Affairs {get;private set;}
        public float CampaignSpeed=1;public double CampaignDay=>ElapsedTime/Math.Max(1,Rules.campaignSecondsPerDay);
        public DateTime CampaignDate=>new DateTime(2026,1,1).AddDays(Math.Min(2000000,Math.Max(0,CampaignDay)));
        private readonly Dictionary<int,DiplomaticMemory> _memoryIndex=new Dictionary<int,DiplomaticMemory>();private float _politicsAccumulator;
        private void InitializeCampaignAffairs()
        {
            Affairs=new CampaignWorldState{reputation=new float[FactionCount],periodIncome=new double[FactionCount],nextDebtPayment=new double[FactionCount],lastDiplomacyDay=new double[FactionCount]};
            for(int f=0;f<FactionCount;f++){Affairs.reputation[f]=50;Affairs.nextDebtPayment[f]=-1;Affairs.lastDiplomacyDay[f]=-100;}
        }
        public DiplomaticPersonality Personality(int faction)=>(DiplomaticPersonality)(faction%5);
        public DiplomaticMemory Memory(int observer,int subject)
        {
            int key=observer*FactionCount+subject;if(_memoryIndex.TryGetValue(key,out var memory))return memory;
            memory=new DiplomaticMemory{observer=observer,subject=subject};Affairs.memories.Add(memory);_memoryIndex[key]=memory;return memory;
        }
        private bool LivingNation(int f)=>f>=0&&f<FactionCount&&TerritoryCounts[f]>0&&!IsTribe(f);
        private bool Negotiable(int a,int b)=>CampaignEnabled&&LivingNation(a)&&LivingNation(b)&&a!=b;
        private bool Active(CampaignCompact pact)=>pact.active&&(pact.endsDay<0||pact.endsDay>CampaignDay);
        public bool HasCompact(int a,int b,CompactKind kind)
        {if(Affairs==null)return false;EnsureCampaignRelationIndex();return _compactIndex.TryGetValue(RelationKey(a,b),out uint flags)&&(flags&(1u<<(int)kind))!=0;}
        public bool CampaignAllied(int a,int b)=>HasCompact(a,b,CompactKind.Alliance);
        public bool IsDeclaredWar(int a,int b){if(Affairs==null)return false;EnsureCampaignRelationIndex();return _warIndex.Contains(RelationKey(a,b));}
        public bool CampaignPeaceBlocks(int a,int b)=>HasCompact(a,b,CompactKind.NonAggression)||HasCompact(a,b,CompactKind.Ceasefire)||HasCompact(a,b,CompactKind.Peace);
        public DiplomaticRelation Relation(int a,int b)
        {
            if(AreAllied(a,b))return DiplomaticRelation.Alliance;if(IsHostile(a,b))return DiplomaticRelation.Hostility;
            float trust=Memory(a,b).trust;if(trust<=-20||Affairs.reputation[b]<25)return DiplomaticRelation.Distrust;return trust>=20?DiplomaticRelation.Friendship:DiplomaticRelation.Neutrality;
        }
        private void Chronicle(string kind,int actor,int other,string message,double amount=0)
        {
            Affairs.history.Add(new CampaignEvent{id=++Affairs.sequence,day=CampaignDay,actor=actor,other=other,kind=kind,message=message,amount=amount});
            if(Affairs.history.Count>1000)Affairs.history.RemoveRange(0,Affairs.history.Count-1000);
            if(actor==PlayerFaction||other==PlayerFaction)Emit(StrategicEventKind.Information,actor,other,other>=0&&other<FactionCount?CapitalCells[other]:-1,message);
        }
        private void ChangeTrust(int observer,int subject,float delta){Memory(observer,subject).trust=UnityEngine.Mathf.Clamp(Memory(observer,subject).trust+delta,-100,100);}
        private void InvalidateDiplomaticTrade(){_factoryPreviewRevision++;_relationIndexUntil=-1;_tradePermissions.Clear();_tradeRefresh=0;_incomeCacheUntil=0;}
        private void StopOperationsBetween(int a,int b)
        {
            foreach(var op in Operations)if(op.active&&((op.faction==a&&(op.enemy==b||op.target>=0&&Owners[op.target]==b))||(op.faction==b&&(op.enemy==a||op.target>=0&&Owners[op.target]==a))))EndCampaignOperation(op,ArmyState.Idle);
            foreach(var missile in Projectiles)if(missile.active&&((missile.owner==a&&missile.targetNation==b)||(missile.owner==b&&missile.targetNation==a)))missile.active=false;
            SyncOperations();
        }
    }
}
