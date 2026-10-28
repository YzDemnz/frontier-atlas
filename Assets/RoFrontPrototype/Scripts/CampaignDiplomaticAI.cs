using System;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private int _diplomacyCursor;
        private double OfferUtility(CampaignProposal p)
        {
            int me=p.target,other=p.source;var t=p.terms;double trust=Memory(me,other).trust,repute=(Affairs.reputation[other]-50)*.3;var personality=Personality(me);
            double value=trust+repute+(AreAllied(me,other)?20:0)+(t.sourceGold-t.targetGold)/5000+(t.sourceTroops-t.targetTroops)/100;
            value+=(t.sourceTerritory.Length-t.targetTerritory.Length)*2;
            if(p.kind==ProposalKind.Loan)return t.loanOfferedByLender?value+(Gold[me]<100000?30:5)-t.interestPerPeriod*300-(DebtTotal(me)>Gold[me]*2?25:0):value+(Gold[me]>t.principal*3?15:-60)+t.interestPerPeriod*200-(FinancialStress(other)?25:0);
            if(p.kind==ProposalKind.RenegotiateDebt)return value+10+(DebtTotal(other)>Gold[other]?10:0)-t.interestPerPeriod*50;
            if(p.kind==ProposalKind.CoalitionInvite){var c=Affairs.coalitions.Find(x=>x.id==t.coalitionId&&x.active);if(c==null||AreAllied(me,c.enemy))return -100;return value+(IsHostile(me,c.enemy)?35:0)+(StrategicStrength(c.enemy)>StrategicStrength(me)*1.4f?35:0)+(SharedWar(me,other)?20:0)+(personality==DiplomaticPersonality.Defensive?10:0)+15;}
            if((p.kind==ProposalKind.Peace||t.compact==CompactKind.Ceasefire||t.compact==CompactKind.Peace)&&IsDeclaredWar(me,other))return StrategicPeaceUtility(p);
            if(t.compact==CompactKind.Alliance&&AllianceUnavailable(me,other))return -1000;
            if(Options.difficulty==Difficulty.Brutal&&p.kind==ProposalKind.Treaty&&t.compact==CompactKind.Alliance)return AllyUtility(me,other)-22+(t.sourceGold-t.targetGold)/10000;
            if(p.kind==ProposalKind.Peace||t.compact==CompactKind.Ceasefire||t.compact==CompactKind.Peace)return value+10+(TotalTroops(other)>TotalTroops(me)?30:-10)+(personality==DiplomaticPersonality.Peaceful?25:0)+(CampaignDay>3?5:0);
            if(p.kind==ProposalKind.Mediation)return value+10+(personality==DiplomaticPersonality.Peaceful?25:0);
            if(t.compact==CompactKind.Tribute)value-=t.periodicGold/1000;
            if(t.compact==CompactKind.Subsidy)value+=t.periodicGold/1000;
            if(t.compact==CompactKind.FreeTrade)value+=personality==DiplomaticPersonality.Commercial?30:15;
            if(t.compact==CompactKind.Protection)value-=15;
            if(IsDeclaredWar(me,other))value-=20;
            return value+(personality==DiplomaticPersonality.Peaceful?20:personality==DiplomaticPersonality.Opportunist?-5:10);
        }
        private void AdvanceCampaignNegotiations()
        {
            // Responses may append counteroffers; iterate a fixed initial count.
            for(int i=0,count=Affairs.proposals.Count;i<count;i++)
            {
                var p=Affairs.proposals[i];if(p.state!=ProposalState.Pending)continue;if(p.expiresDay<=CampaignDay||!Negotiable(p.source,p.target)){p.state=ProposalState.Expired;continue;}
                if(!AiEnabled||p.target==PlayerFaction||p.responseDay>CampaignDay)continue;
                double value=OfferUtility(p)+( _random.NextDouble()*16-8);
                if(value>=5){var result=AnswerCampaign(p.target,p.id,true);if(!result.success)p.state=ProposalState.Rejected;}
                else if(p.parent<0&&value> -70&&p.kind==ProposalKind.Loan)
                {
                    var revised=JsonUtility.FromJson<NegotiationTerms>(JsonUtility.ToJson(p.terms));revised.principal=p.terms.loanOfferedByLender?p.terms.principal:Math.Min(p.terms.principal,Gold[p.target]*.2);revised.interestPerPeriod=p.terms.loanOfferedByLender?p.terms.interestPerPeriod*.75:Math.Min(.5,Math.Max(.02,p.terms.interestPerPeriod+.02));if(revised.principal>0)CounterCampaign(p.target,p.id,revised);else AnswerCampaign(p.target,p.id,false);
                }
                else if(p.parent<0&&value> -25&&!(IsDeclaredWar(p.source,p.target)&&(p.kind==ProposalKind.Peace||p.terms.compact==CompactKind.Ceasefire||p.terms.compact==CompactKind.Peace))&&(p.kind==ProposalKind.Treaty||p.kind==ProposalKind.Peace||p.kind==ProposalKind.Demand))
                {
                    var old=p.terms;var revised=JsonUtility.FromJson<NegotiationTerms>(JsonUtility.ToJson(old));revised.sourceGold=old.targetGold;revised.targetGold=Math.Min(Gold[p.source],old.sourceGold+25000);revised.sourceTroops=old.targetTroops;revised.targetTroops=old.sourceTroops;revised.sourceTerritory=old.targetTerritory;revised.targetTerritory=old.sourceTerritory;revised.collateral=Array.Empty<int>();CounterCampaign(p.target,p.id,revised);
                }
                else AnswerCampaign(p.target,p.id,false);
            }
            foreach(var r in Affairs.support)
            {
                if(r.state!=RequestState.Pending||r.target==PlayerFaction||!AiEnabled||r.responseDay>CampaignDay)continue;
                double value=Memory(r.target,r.source).trust+Affairs.reputation[r.source]*.3+(AreAllied(r.target,r.source)?25:0)+(r.obligation?20:0)+(r.enemy>=0&&IsHostile(r.target,r.enemy)?20:0);
                if((r.kind==SupportKind.Gold||r.kind==SupportKind.EmergencyLoan)&&Gold[r.target]<r.amount*2)value-=60;
                if(r.kind==SupportKind.Troops&&AvailableTroops(r.target)<r.amount*2)value-=60;
                if(r.kind==SupportKind.Gold||r.kind==SupportKind.Troops)value-=r.amount/(r.kind==SupportKind.Gold?5000:100);
                bool accept=value+_random.NextDouble()*20>=20;var result=AnswerSupport(r.target,r.id,accept);
                if(!result.success){r.state=RequestState.Rejected;r.response=result.message;}
            }
            if(!AiEnabled)return;
            // At most two nations consider an initiative per half-second; no global diplomacy scan per frame.
            for(int n=0;n<2;n++){int f=_diplomacyCursor++%FactionCount;if(f==PlayerFaction||!LivingNation(f)||CampaignDay-Affairs.lastDiplomacyDay[f]<1+f%3)continue;Affairs.lastDiplomacyDay[f]=CampaignDay;ThinkDiplomaticNation(f);}
        }
        private void ThinkDiplomaticNation(int f)
        {
            ReviewRivalries(f);
            if(Options.difficulty==Difficulty.Brutal){ThinkGrandDiplomacy(f);return;}
            foreach(var loan in Affairs.loans)if(loan.creditor==f&&Outstanding(loan)&&(loan.status==DebtStatus.Refused||loan.status==DebtStatus.Defaulted)&&LivingNation(loan.debtor))
            {
                int debtor=loan.debtor;
                if(CampaignDay>loan.maturityDay+20&&TotalTroops(f)>TotalTroops(debtor)*1.5f&&!CampaignPeaceBlocks(f,debtor)&&!AreAllied(f,debtor))DeclareWar(f,debtor,WarObjective.Recovery,CapitalCells[debtor]);
                else if(!Affairs.proposals.Exists(p=>p.source==f&&p.target==debtor&&p.state==ProposalState.Pending))ProposeCampaign(f,debtor,ProposalKind.Demand,new NegotiationTerms{compact=CompactKind.Peace,durationDays=15,targetGold=Math.Min(loan.Outstanding,Gold[debtor]),loanId=loan.id},"Reclamación de compensación por deuda impagada.");
                return;
            }
            foreach(var war in Affairs.wars)if(war.active&&(war.attacker==f||war.defender==f))
            {
                int rival=war.attacker==f?war.defender:war.attacker;
                float ownLoss=Statistics[f].troopsLost-(war.attacker==f?war.attackerLossStart:war.defenderLossStart);
                if(TotalTroops(f)<TotalTroops(rival)*.8f||ownLoss>TotalTroops(f))
                {
                    int helper=FindFriendlyNation(f,rival);if(helper>=0){if(Gold[f]<50000)RequestSupport(f,helper,SupportKind.EmergencyLoan,50000,rival);else RequestSupport(f,helper,SupportKind.JointDefense,0,rival,CapitalCells[f]);}
                    if(!Affairs.proposals.Exists(p=>p.source==f&&p.target==rival&&p.state==ProposalState.Pending))ProposeCampaign(f,rival,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,sourceGold=Math.Min(Gold[f]*.1,25000)},"Oferta de paz por pérdidas militares y presión defensiva.");return;
                }
            }
            int other=-1;float best=float.PositiveInfinity;foreach(int candidate in _neighborNations[f])if(Negotiable(f,candidate)){float d=Distance(CapitalCells[f],CapitalCells[candidate]);if(d<best){best=d;other=candidate;}}
            if(other<0){for(int attempt=0;attempt<12;attempt++){int candidate=_random.Next(FactionCount);if(Negotiable(f,candidate)){other=candidate;break;}}}if(other<0)return;
            if(Affairs.proposals.Exists(p=>p.state==ProposalState.Pending&&p.source==f&&p.target==other))return;
            var personality=Personality(f);float strength=TotalTroops(f)/Mathf.Max(1,TotalTroops(other));
            if(IsDeclaredWar(f,other))
            {
                if(strength<.8f||personality==DiplomaticPersonality.Peaceful||FinancialStress(f)){ProposeCampaign(f,other,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,sourceGold=Math.Min(Gold[f]*.1,25000)},"Propuesta de paz ante el coste de la guerra.");return;}
                if(_random.NextDouble()<.2&&Personality(other)!=DiplomaticPersonality.Peaceful)RequestSupport(f,FindFriendlyNation(f,other),SupportKind.Diversion,0,other,CapitalCells[other]);return;
            }
            if(DebtTotal(f)>0&&FinancialStress(f)){var loan=Affairs.loans.Find(l=>l.debtor==f&&l.Payable);if(loan!=null)RenegotiateDebt(f,loan.id,30,loan.interestPerPeriod);return;}
            if(Gold[f]<50000&&Gold[other]>300000&&Memory(other,f).trust>=0){RequestLoan(f,other,50000,.01,90);return;}
            if(WorldControl(other)>.15f&&WorldControl(other)>WorldControl(f)*3&&!AreAllied(f,other))
            {
                var result=CreateCoalition(f,other,"Equilibrio mundial",WarObjective.Defense,CapitalCells[other]);if(result.success){var c=Affairs.coalitions[Affairs.coalitions.Count-1];int invite=FindFriendlyNation(f,other);if(invite>=0)InviteCoalition(f,c.id,invite);}return;
            }
            if(AreAllied(f,other)&&Memory(f,other).trust< -20&&personality==DiplomaticPersonality.Opportunist&&strength>1.8f){Betray(f,other);return;}
            if(personality==DiplomaticPersonality.Expansionist&&strength>2&&ContactCell(f,other)>=0){ProposeCampaign(f,other,ProposalKind.Demand,new NegotiationTerms{compact=CompactKind.Tribute,durationDays=30,periodicGold=5000,periodDays=15},"Exigencia de tributo. Su rechazo puede conducir a una guerra.");return;}
            CompactKind kind=personality==DiplomaticPersonality.Commercial?CompactKind.FreeTrade:personality==DiplomaticPersonality.Defensive?CompactKind.MutualDefense:personality==DiplomaticPersonality.Peaceful?CompactKind.NonAggression:CompactKind.Alliance;
            if(!HasCompact(f,other,kind)&&!AreAllied(f,other)&&!IsHostile(f,other)&&Memory(f,other).trust> -10)ProposeCampaign(f,other,ProposalKind.Treaty,new NegotiationTerms{compact=kind,durationDays=30},"Iniciativa diplomática según los intereses de "+Names[f]+".");
            foreach(var p in Affairs.proposals)if(p.source==f&&p.target==other&&p.kind==ProposalKind.Demand&&p.state==ProposalState.Rejected&&CampaignDay-p.createdDay<5&&strength>2&&!CampaignPeaceBlocks(f,other)){DeclareWar(f,other,WarObjective.Capital,CapitalCells[other]);break;}
        }
        private int FindFriendlyNation(int f,int enemy)
        {int selected=-1;float score=-1000;for(int i=0;i<FactionCount;i++)if(Negotiable(f,i)&&i!=enemy&&!AreAllied(i,enemy)){float value=Memory(f,i).trust+(AreAllied(f,i)?30:0)+(IsHostile(i,enemy)?25:0);if(value>score){score=value;selected=i;}}return selected;}
    }
}
