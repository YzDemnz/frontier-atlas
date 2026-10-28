using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public enum StrategicPhase { Develop, Mobilize, Isolate, Offensive, Recover }
    [Serializable] public sealed class StrategicRivalry
    {public int a,b;public float pressure;public double updatedDay;}
    [Serializable] public sealed class NationalStrategy
    {public int nation,enemy=-1,goal=-1;public StrategicPhase phase;public float reserve=.4f;public double reviewedDay=-100,operationDay=-100;public string reason="Desarrollo y seguridad de fronteras";}
    [Serializable] public sealed class GrandStrategyState
    {public List<StrategicRivalry> rivalries=new List<StrategicRivalry>();public List<NationalStrategy> plans=new List<NationalStrategy>();}
    public sealed partial class TerritorySimulation
    {
        public const float RecruitmentBoost=1.62f*1.8f;
        public GrandStrategyState Strategy {get;private set;}=new GrandStrategyState();
        private readonly Dictionary<int,StrategicRivalry> _rivalryIndex=new Dictionary<int,StrategicRivalry>();
        private readonly Dictionary<int,NationalStrategy> _nationalPlans=new Dictionary<int,NationalStrategy>();
        private float[] _strategicEconomy,_strategicSupport;private float _strategicSnapshotUntil=-1;
        public bool Neighbors(int a,int b)=>a>=0&&b>=0&&a<FactionCount&&b<FactionCount&&_neighborNations[a].Contains(b)&&ContactCell(a,b)>=0;
        public bool Superpower(int f)=>TotalTroops(f)>=1500000||(WorldControl(f)>=.12f&&TotalTroops(f)>=250000);
        public float RivalryPressure(int a,int b)=>_rivalryIndex.TryGetValue(RelationKey(a,b),out var value)?value.pressure:0;
        public bool AllianceUnavailable(int a,int b)=>!IsQuickMatch&&a!=b&&(IsDeclaredWar(a,b)||IsHostile(a,b)||RivalryPressure(a,b)>=60);
        public float TradeActivity(int a,int b)
        {if(a==b)return 1;if(a<0||b<0||IsDeclaredWar(a,b)||Embargoed(a,b))return 0;return Mathf.Clamp01((100-RivalryPressure(a,b))/80);}
        public NationalStrategy StrategyFor(int nation)
        {if(!_nationalPlans.TryGetValue(nation,out var plan)){plan=new NationalStrategy{nation=nation};_nationalPlans[nation]=plan;Strategy.plans.Add(plan);}return plan;}
        private void RebindStrategy()
        {
            if(Strategy==null)Strategy=new GrandStrategyState();_rivalryIndex.Clear();_nationalPlans.Clear();
            foreach(var r in Strategy.rivalries){if(r.a<0||r.b<0||r.a>=FactionCount||r.b>=FactionCount||r.a==r.b||r.pressure<0||r.pressure>100)throw new System.IO.InvalidDataException("Rivalidad inválida.");_rivalryIndex[RelationKey(r.a,r.b)]=r;}
            foreach(var p in Strategy.plans){if(p.nation<0||p.nation>=FactionCount||p.enemy>=FactionCount||p.enemy< -1||p.goal>=Owners.Length||p.goal< -1)throw new System.IO.InvalidDataException("Plan estratégico inválido.");_nationalPlans[p.nation]=p;}_strategicSnapshotUntil=-1;
        }
        private bool InvitedToWar(int actor,int enemy)
        {
            foreach(var c in Affairs.coalitions)if(c.active&&c.enemy==enemy)
            {
                if(c.members.Contains(actor)&&c.members.Count>1)return true;
                foreach(var offer in Affairs.proposals)if(offer.target==actor&&offer.kind==ProposalKind.CoalitionInvite&&offer.state==ProposalState.Pending&&offer.expiresDay>CampaignDay&&offer.terms.coalitionId==c.id)return true;
            }
            foreach(var r in Affairs.support)if(r.target==actor&&r.enemy==enemy&&r.expiresDay>CampaignDay&&(r.state==RequestState.Pending||r.state==RequestState.Accepted)&&AreAllied(actor,r.source))return true;
            return false;
        }
        public string WarDeclarationBlock(int actor,int enemy)
        {
            if(!Negotiable(actor,enemy))return "Elige otra nación activa.";
            if(IsDeclaredWar(actor,enemy))return "Ya estáis en guerra.";
            if(AreAllied(actor,enemy))return "Rompe primero la alianza existente.";
            if(CampaignPeaceBlocks(actor,enemy))return "Hay una tregua o pacto de no agresión vigente.";
            if(ProtectionRemaining(enemy)>0)return "El objetivo todavía tiene protección inicial.";
            if(!Neighbors(actor,enemy)&&!InvitedToWar(actor,enemy))return "Necesitas una frontera vecina o una invitación contra este enemigo común.";
            return "";
        }
        public bool CanDeclareWar(int actor,int enemy)=>!Paused&&Outcome==CampaignOutcome.Playing&&WarDeclarationBlock(actor,enemy).Length==0;
        public double WarTruceDays=>(CampaignDate.AddMonths(5)-CampaignDate).TotalDays;
        public CampaignAction ProposeWarTruce(int source,int target,float territoryFraction=0)
        {
            if(!IsDeclaredWar(source,target))return CampaignAction.Fail("Esta tregua requiere una guerra activa.");
            float fraction=Mathf.Clamp(territoryFraction,0,.5f);
            return ProposeCampaign(source,target,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Ceasefire,durationDays=WarTruceDays,sourceTerritoryFraction=fraction},fraction>0?"Tregua de cinco meses; cesión del "+(fraction*100).ToString("0")+"% del territorio al aceptar.":"Solicitud de cese al fuego durante cinco meses.");
        }
        private int[] TerritoryConcession(int source,int target,float territoryFraction)
        {
            int amount=Mathf.Min(TerritoryCounts[source]-1,Mathf.FloorToInt(TerritoryCounts[source]*Mathf.Clamp(territoryFraction,0,.5f)));
            // Choose connected border layers first, retaining the capital and at least one tile.
            var selected=new List<int>();var seen=new HashSet<int>();var queue=new Queue<int>();
            if(amount>0)
            {
                foreach(int c in _owned[source])if(IsAdjacent(c,target)&&c!=CapitalCells[source]){queue.Enqueue(c);seen.Add(c);if(queue.Count>=256)break;}
                if(queue.Count==0)foreach(int c in _frontiers[source])if(c!=CapitalCells[source]){queue.Enqueue(c);seen.Add(c);if(queue.Count>=256)break;}
                while(selected.Count<amount&&queue.Count>0){int c=queue.Dequeue();selected.Add(c);for(int d=0;d<4;d++){int n=World.Neighbor(c,d);if(n>=0&&Owners[n]==source&&n!=CapitalCells[source]&&seen.Add(n))queue.Enqueue(n);}}
                if(selected.Count<amount)foreach(int c in _owned[source])if(c!=CapitalCells[source]&&seen.Add(c)){selected.Add(c);if(selected.Count>=amount)break;}
            }
            return selected.ToArray();
        }
        private bool SharedWar(int a,int b)
        {foreach(var w in Affairs.wars)if(w.active&&(w.attacker==a||w.defender==a)){int enemy=w.attacker==a?w.defender:w.attacker;if(IsDeclaredWar(b,enemy))return true;}return false;}
        private void ReviewRivalries(int f)
        {
            foreach(int other in _neighborNations[f])
            {
                if(!LivingNation(other))continue;int key=RelationKey(f,other);bool competing=Superpower(f)&&Superpower(other);
                if(!_rivalryIndex.TryGetValue(key,out var r))
                {if(!competing)continue;r=new StrategicRivalry{a=Mathf.Min(f,other),b=Mathf.Max(f,other),updatedDay=CampaignDay};_rivalryIndex[key]=r;Strategy.rivalries.Add(r);}
                double days=Math.Min(30,Math.Max(0,CampaignDay-r.updatedDay));if(days<.01)continue;r.updatedDay=CampaignDay;
                float rate=IsDeclaredWar(f,other)?12:CampaignPeaceBlocks(f,other)||SharedWar(f,other)?-5:competing?(AreAllied(f,other)?1.4f:4):-4;
                float before=r.pressure;r.pressure=Mathf.Clamp(r.pressure+(float)days*rate,0,100);
                if(before<60&&r.pressure>=60){ChangeTrust(f,other,-25);ChangeTrust(other,f,-25);Chronicle("rivalidad",f,other,"La rivalidad entre "+Names[f]+" y "+Names[other]+" bloquea nuevas alianzas y reduce el comercio.");InvalidateDiplomaticTrade();}
                if(before<100&&r.pressure>=100){Chronicle("cierre comercial",f,other,"El comercio entre "+Names[f]+" y "+Names[other]+" cesó tras una rivalidad prolongada.");InvalidateDiplomaticTrade();}
            }
            // Old rivalries also cool down after losing their shared frontier.
            foreach(var r in Strategy.rivalries)if((r.a==f||r.b==f)&&!Neighbors(r.a,r.b)&&CampaignDay-r.updatedDay>=1){r.pressure=Mathf.Max(0,r.pressure-(float)(CampaignDay-r.updatedDay)*4);r.updatedDay=CampaignDay;}
        }
        private void EnsureStrategicSnapshot()
        {
            if(_strategicSnapshotUntil>ElapsedTime)return;_strategicSnapshotUntil=ElapsedTime+2;
            if(_strategicEconomy==null){_strategicEconomy=new float[FactionCount];_strategicSupport=new float[FactionCount];}Array.Clear(_strategicEconomy,0,FactionCount);Array.Clear(_strategicSupport,0,FactionCount);
            foreach(var b in Buildings)if(b.Operational)_strategicEconomy[b.owner]+=b.kind==BuildingKind.Factory?3:b.kind==BuildingKind.Port?2:b.kind==BuildingKind.City?1:0;
            var pairs=new HashSet<int>();foreach(var c in Affairs.compacts)if(Active(c)&&(c.kind==CompactKind.Alliance||c.kind==CompactKind.MutualDefense))pairs.Add(RelationKey(c.source,c.target));
            foreach(var t in Treaties)if(t.state==TreatyState.Allied&&t.expiresAt>ElapsedTime)pairs.Add(RelationKey(t.source,t.target));
            foreach(int key in pairs){int a=key/FactionCount,b=key%FactionCount;_strategicSupport[a]+=AvailableTroops(b)*.28f;_strategicSupport[b]+=AvailableTroops(a)*.28f;}
        }
        private float StrategicStrength(int f){EnsureStrategicSnapshot();return AvailableTroops(f)+CommittedTroops[f]*.45f+Mathf.Min(TotalTroops(f),_strategicSupport[f]);}
        public bool ValuableTradingBuffer(int f,int other)
        {EnsureStrategicSnapshot();return Negotiable(f,other)&&!IsDeclaredWar(f,other)&&!IsHostile(f,other)&&TotalTroops(f)>TotalTroops(other)*3&&_strategicEconomy[other]>=2&&TradeActivity(f,other)>.4f;}
        public double StrategicPeaceUtility(CampaignProposal p)
        {
            int me=p.target,other=p.source;var t=p.terms;double ratio=StrategicStrength(me)/Math.Max(1,StrategicStrength(other));
            double concession=t.sourceTerritoryFraction-t.targetTerritoryFraction+(t.sourceTerritory.Length/(double)Math.Max(1,TerritoryCounts[other]))-(t.targetTerritory.Length/(double)Math.Max(1,TerritoryCounts[me]));
            double material=(t.sourceGold-t.targetGold)/Math.Max(100000,Gold[me]) *20+(t.sourceTroops-t.targetTroops)/Math.Max(10000,TotalTroops(me))*30;
            int enemies=0;foreach(var w in Affairs.wars)if(w.active&&(w.attacker==me||w.defender==me))enemies++;
            double advantage=ratio>1.15?-45*Math.Min(3,ratio-1):ratio<.85?40*(1/Math.Max(.2,ratio)-1):8;
            double utility=advantage+concession*300+material+(enemies>1?(enemies-1)*12:0)+(FinancialStress(me)?12:0)+(Personality(me)==DiplomaticPersonality.Peaceful?8:0)-5;
            return concession>=.49&&material>=-10?Math.Max(20,utility):utility;
        }
        private double AllyUtility(int f,int other)
        {
            if(!Negotiable(f,other)||AllianceUnavailable(f,other))return -1000;EnsureStrategicSnapshot();
            return Memory(f,other).trust*.6+Affairs.reputation[other]*.15+(SharedWar(f,other)?45:0)+(ValuableTradingBuffer(f,other)?55:0)+(Neighbors(f,other)?12:0)+Math.Min(20,_strategicEconomy[other]*2)+Math.Min(20,StrategicStrength(other)/Math.Max(1,StrategicStrength(f))*10)-RivalryPressure(f,other);
        }
        private void ThinkGrandDiplomacy(int f)
        {
            int strongest=-1;float threat=0;
            foreach(int other in _neighborNations[f])if(LivingNation(other)&&!AreAllied(f,other)&&!CampaignPeaceBlocks(f,other))
            {float value=StrategicStrength(other)*(IsDeclaredWar(f,other)?2:1);if(value>threat){threat=value;strongest=other;}}
            foreach(var war in Affairs.wars)if(war.active&&(war.attacker==f||war.defender==f))
            {
                int enemy=war.attacker==f?war.defender:war.attacker;
                if(StrategicStrength(f)<StrategicStrength(enemy)*.8f&&!PendingBetween(f,enemy))ProposeWarTruce(f,enemy,TotalTroops(f)<TotalTroops(enemy)*.45f?.5f:0);
                strongest=enemy;break;
            }
            if(strongest>=0&&(threat>StrategicStrength(f)*1.4f||IsDeclaredWar(f,strongest)))
            {
                Coalition coalition=null;foreach(var c in Affairs.coalitions)if(c.active&&c.enemy==strongest&&c.members.Contains(f)){coalition=c;break;}
                if(coalition==null&&CreateCoalition(f,strongest,"Pacto contra "+Names[strongest],WarObjective.Infrastructure,CapitalCells[strongest]).success)coalition=Affairs.coalitions[Affairs.coalitions.Count-1];
                if(coalition!=null)
                {
                    int invites=0;var candidates=new List<int>(_neighborNations[f]);foreach(int n in _neighborNations[strongest])if(!candidates.Contains(n))candidates.Add(n);
                    candidates.Sort((a,b)=>AllyUtility(f,b).CompareTo(AllyUtility(f,a)));
                    foreach(int ally in candidates)if(ally!=strongest&&ally!=f&&!AreAllied(ally,strongest)&&!coalition.members.Contains(ally)&&!PendingBetween(f,ally)&&AllyUtility(f,ally)>15)
                    {InviteCoalition(f,coalition.id,ally);if(++invites>=3)break;}
                    if(coalition.members.Count>1)foreach(int member in coalition.members)if(member!=f&&member!=PlayerFaction&&CanAttackNation(member,strongest))RequestSupport(f,member,SupportKind.Intervention,0,strongest,StrategicGoal(strongest),coalition.id);
                }
            }
            int choice=-1;double best=22;foreach(int other in _neighborNations[f])if(!AreAllied(f,other)&&!PendingBetween(f,other)){double score=AllyUtility(f,other);if(score>best){best=score;choice=other;}}
            if(choice>=0)ProposeCampaign(f,choice,ProposalKind.Treaty,new NegotiationTerms{compact=CompactKind.Alliance,durationDays=90},ValuableTradingBuffer(f,choice)?"Preservar un socio comercial y proteger la frontera común.":"Seguridad compartida y equilibrio frente a amenazas comunes.");
        }
        private bool PendingBetween(int source,int target)=>Affairs.proposals.Exists(p=>p.source==source&&p.target==target&&p.state==ProposalState.Pending);
        private void ReconcileCoalitionAlliances(int a,int b)
        {
            foreach(var c in Affairs.coalitions)if(c.active&&(c.enemy==a||c.enemy==b))
            {int member=c.enemy==a?b:a;if(!c.members.Remove(member))continue;if(c.members.Count==0)c.active=false;else if(c.leader==member)c.leader=c.members[0];}
        }
        private int StrategicGoal(int enemy)
        {int goal=CapitalCells[enemy];float value=-1;int inspected=0;foreach(var b in Buildings){if(++inspected>4096)break;if(b.owner!=enemy||!b.Operational)continue;float score=(b.kind==BuildingKind.Factory?50:b.kind==BuildingKind.Silo?45:b.kind==BuildingKind.Port?35:20)*b.level;if(score>value){value=score;goal=b.cell;}}return goal;}
        private void ThinkGrandMilitary(int f)
        {
            if(IsTribe(f)){ThinkTactical(f);return;}var plan=StrategyFor(f);float reserve=AvailableTroops(f);if(reserve<MinimumExpansionTroops*2)return;
            bool invaded=Operations.Exists(o=>o.active&&o.faction!=f&&o.enemy==f);int chosen=-1;float best=0;
            if(!invaded)foreach(int other in _neighborNations[f])if(IsTribe(other)&&CanAttackNation(f,other)&&reserve*.4f>AvailableTroops(other)*1.2f){int point=ContactCell(f,other);if(point>=0){StartCampaignAttack(f,point,.4f);return;}}
            foreach(int enemy in _neighborNations[f])
            {
                if(!CanAttackNation(f,enemy)||IsTribe(enemy)||ValuableTradingBuffer(f,enemy))continue;
                float ratio=StrategicStrength(f)/Mathf.Max(1,StrategicStrength(enemy));
                float score=(IsDeclaredWar(f,enemy)?80:0)+(ratio-1.2f)*40+RivalryPressure(f,enemy)*.3f;
                if(ratio<1.15f&&!IsDeclaredWar(f,enemy))continue;if(score>best){best=score;chosen=enemy;}
            }
            if(invaded&&chosen<0)
            {plan.phase=StrategicPhase.Recover;plan.reserve=.7f;plan.reason="Recuperar reservas y defender el núcleo";foreach(var op in Operations)if(op.active&&op.faction==f&&op.kind==OperationKind.Expansion)EndCampaignOperation(op,ArmyState.Idle);SyncOperations();return;}
            if(chosen>=0)
            {
                bool changed=plan.enemy!=chosen;plan.enemy=chosen;plan.goal=StrategicGoal(chosen);plan.reserve=invaded?.6f:.4f;float ratio=StrategicStrength(f)/Mathf.Max(1,StrategicStrength(chosen));
                if(changed){plan.phase=StrategicPhase.Mobilize;plan.reviewedDay=CampaignDay;plan.reason="Preparar reservas y localizar industria del rival";foreach(var op in Operations)if(op.active&&op.faction==f&&op.kind==OperationKind.Expansion)EndCampaignOperation(op,ArmyState.Idle);SyncOperations();return;}
                if(ratio<1.1f){plan.phase=StrategicPhase.Isolate;plan.reason="Buscar apoyo y evitar una ofensiva desfavorable";return;}
                if(CampaignDay-plan.reviewedDay<.08)return;
                if(!IsDeclaredWar(f,chosen)&&!DeclareWar(f,chosen,WarObjective.Infrastructure,plan.goal).success)return;
                int primary=StrategicContact(f,chosen,plan.goal);if(primary<0)return;
                plan.phase=StrategicPhase.Offensive;plan.reason="Atacar industria, conservar reserva y abrir un segundo frente";
                int count=0;foreach(var op in Operations)if(op.active&&op.faction==f&&op.kind==OperationKind.Land&&op.enemy==chosen)count++;
                if(count==0&&reserve>TotalTroops(chosen)*.75f)
                {if(StartCampaignAttack(f,primary,1-plan.reserve)==OrderResult.Accepted){Operations[Operations.Count-1].target=plan.goal;plan.operationDay=CampaignDay;}return;}
                if(count==1&&reserve>TotalTroops(chosen)*.8f&&CampaignDay-plan.operationDay>.12)
                {int flank=-1;float separation=18*World.ResolutionScale;if(_contacts.TryGetValue(f*FactionCount+chosen,out var contact)){int n=0;foreach(int cell in contact.cells){if(++n>256)break;if(Owners[cell]==chosen&&IsAdjacent(cell,f)&&!IsImpassable(cell)&&Distance(cell,primary)>separation){flank=cell;break;}}}if(flank>=0&&StartCampaignAttack(f,flank,.25f,true)==OrderResult.Accepted)Operations[Operations.Count-1].target=flank;}
                return;
            }
            plan.phase=StrategicPhase.Develop;plan.enemy=-1;plan.reason="Expandir hacia espacio libre y preservar socios comerciales";
            if(reserve<Rules.tacticalAiExpansionReserve||OperationCount(f)>=2)return;EnsureFreeBoundary(f);if(SelectFreeBoundary(f)<0)return;int target=PickExpansionHeading(f);if(target>=0)BeginDirectionalExpansion(f,target,.3f);
        }
    }
}
