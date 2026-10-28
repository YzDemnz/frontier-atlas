using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public CampaignAction ProposeCampaign(int source,int target,ProposalKind kind,NegotiationTerms terms,string note="")
        {
            if(Paused||!Negotiable(source,target)||terms==null)return CampaignAction.Fail("Reanuda la partida y elige otra nación viva.");
            if(Affairs.proposals.FindAll(p=>p.state==ProposalState.Pending&&(p.source==source||p.target==source)).Count>=24)return CampaignAction.Fail("Resuelve tus negociaciones pendientes antes de enviar más.");
            if((kind==ProposalKind.Treaty||kind==ProposalKind.Peace||kind==ProposalKind.Demand)&&terms.compact==CompactKind.Alliance&&AllianceUnavailable(source,target))return CampaignAction.Fail("La guerra o rivalidad impide proponer una alianza.");
            if(IsDeclaredWar(source,target)&&(kind==ProposalKind.Peace||terms.compact==CompactKind.Peace||terms.compact==CompactKind.Ceasefire))terms.durationDays=WarTruceDays;
            if(!ValidTerms(source,target,terms))return CampaignAction.Fail("Revisa importes, plazo, tropas y territorios de la oferta.");
            Affairs.proposals.Add(new CampaignProposal{id=++Affairs.sequence,source=source,target=target,kind=kind,terms=JsonUtility.FromJson<NegotiationTerms>(JsonUtility.ToJson(terms)),createdDay=CampaignDay,expiresDay=CampaignDay+7,responseDay=CampaignDay+.08+_random.NextDouble()*.1,note=note.Length>240?note.Substring(0,240):note});
            Chronicle("oferta",source,target,Names[source]+" abrió una negociación con "+Names[target]+".");return CampaignAction.Ok("Oferta enviada. La otra nación decidirá de forma independiente.");
        }
        private bool OwnsTerms(int f,int[] cells)
        {if(cells==null||cells.Length>Owners.Length)return false;var seen=new HashSet<int>();foreach(int c in cells)if(c<0||c>=Owners.Length||Owners[c]!=f||!seen.Add(c))return false;return true;}
        private bool ValidTerms(int a,int b,NegotiationTerms t)
        {
            return !float.IsNaN(t.sourceTerritoryFraction)&&!float.IsNaN(t.targetTerritoryFraction)&&t.sourceTerritoryFraction>=0&&t.sourceTerritoryFraction<=.5f&&t.targetTerritoryFraction>=0&&t.targetTerritoryFraction<=.5f&&(t.sourceTerritoryFraction==0||t.sourceTerritory.Length==0)&&(t.targetTerritoryFraction==0||t.targetTerritory.Length==0)&&MoneyValue(t.sourceGold)&&MoneyValue(t.targetGold)&&MoneyValue(t.sourceTroops)&&MoneyValue(t.targetTroops)&&MoneyValue(t.principal)&&MoneyValue(t.periodicGold)&&MoneyValue(t.collateralValue)&&MoneyValue(t.collateralGold)&&MoneyValue(t.interestPerPeriod)&&t.interestPerPeriod<=.5&&t.durationDays>=-1&&t.durationDays<=3650&&t.periodDays>=1&&t.periodDays<=365&&t.loanDays>=15&&t.loanDays<=3650&&OwnsTerms(a,t.sourceTerritory)&&OwnsTerms(b,t.targetTerritory)&&OwnsTerms(t.loanOfferedByLender?b:a,t.collateral);
        }
        private bool CanExchange(int a,int b,NegotiationTerms t)
        {
            if(!ValidTerms(a,b,t)||Gold[a]+.001<t.sourceGold||Gold[b]+.001<t.targetGold||AvailableTroops(a)+.001<t.sourceTroops||AvailableTroops(b)+.001<t.targetTroops)return false;
            return (t.targetTroops<=t.sourceTroops||StoredTroops(a)-t.sourceTroops+t.targetTroops<=Capacity[a]+.001)&&(t.sourceTroops<=t.targetTroops||StoredTroops(b)-t.targetTroops+t.sourceTroops<=Capacity[b]+.001);
        }
        public CampaignAction AnswerCampaign(int recipient,int proposalId,bool accept)
        {
            var p=Affairs.proposals.Find(x=>x.id==proposalId&&x.target==recipient&&x.state==ProposalState.Pending);
            if(Paused||p==null||p.expiresDay<=CampaignDay)return CampaignAction.Fail("La oferta ya no está disponible.");
            if(!accept){p.state=ProposalState.Rejected;Chronicle("rechazo",recipient,p.source,Names[recipient]+" rechazó una oferta.");return CampaignAction.Ok("Oferta rechazada.");}
            int a=p.source,b=p.target;var t=p.terms;
            if((p.kind==ProposalKind.Treaty||p.kind==ProposalKind.Peace||p.kind==ProposalKind.Demand)&&t.compact==CompactKind.Alliance&&AllianceUnavailable(a,b))return CampaignAction.Fail("La rivalidad actual impide firmar la alianza.");
            if(IsDeclaredWar(a,b)&&(p.kind==ProposalKind.Peace||t.compact==CompactKind.Peace||t.compact==CompactKind.Ceasefire))t.durationDays=WarTruceDays;
            if(!Negotiable(a,b)||!CanExchange(a,b,t))return CampaignAction.Fail("Las condiciones cambiaron: faltan fondos, tropas, capacidad o territorio.");
            if(t.militaryEnemy>=0&&(!LivingNation(t.militaryEnemy)||t.militaryEnemy==a||!CanAttackNation(b,t.militaryEnemy)))return CampaignAction.Fail("El compromiso militar es incompatible con los tratados o el objetivo.");
            CampaignAction result=CampaignAction.Ok("Acuerdo firmado.");
            int[] sourceLand=t.sourceTerritoryFraction>0?TerritoryConcession(a,b,t.sourceTerritoryFraction):t.sourceTerritory;
            int[] targetLand=t.targetTerritoryFraction>0?TerritoryConcession(b,a,t.targetTerritoryFraction):t.targetTerritory;
            if(p.kind==ProposalKind.Loan){if(t.sourceGold!=0||t.targetGold!=0||t.sourceTroops!=0||t.targetTroops!=0||t.sourceTerritory.Length+t.targetTerritory.Length>0)return CampaignAction.Fail("Negocia el préstamo separado de las concesiones.");result=IssueLoan(t.loanOfferedByLender?a:b,t.loanOfferedByLender?b:a,t);}
            else if(p.kind==ProposalKind.RenegotiateDebt)
            {
                var loan=Affairs.loans.Find(l=>l.id==t.loanId&&((l.debtor==a&&l.creditor==b)||(l.debtor==b&&l.creditor==a))&&Outstanding(l));if(loan==null)return CampaignAction.Fail("La deuda ya no existe.");
                loan.maturityDay=Math.Max(CampaignDay,loan.maturityDay)+t.loanDays;loan.interestPerPeriod=t.interestPerPeriod;loan.status=DebtStatus.Active;loan.missedPayments=0;
                if(Affairs.nextDebtPayment[loan.debtor]<0){Affairs.periodIncome[loan.debtor]=0;Affairs.nextDebtPayment[loan.debtor]=CampaignDay+15;}
            }
            else if(p.kind==ProposalKind.CoalitionInvite){result=JoinCoalition(b,t.coalitionId);}
            else if(p.kind==ProposalKind.Mediation)
            {
                if(!LivingNation(t.mediatedNation)||!IsDeclaredWar(b,t.mediatedNation))return CampaignAction.Fail("La guerra que se intentaba mediar ya terminó.");
                // A neutral mediator needs consent from both belligerents. The second offer is sent by the consenting side.
                result=ProposeCampaign(b,t.mediatedNation,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Ceasefire,durationDays=t.durationDays},"Mediación propuesta por "+Names[a]+"; la otra parte ya consintió negociar.");
            }
            else
            {
                if(t.forgiveDebt&&t.loanId>=0){var loan=Affairs.loans.Find(l=>l.id==t.loanId&&l.creditor==a&&l.debtor==b&&Outstanding(l));if(loan==null)return CampaignAction.Fail("La condonación ofrecida ya no es válida.");}
                if(t.loanId>=0&&!t.forgiveDebt){var settlement=Affairs.loans.Find(l=>l.id==t.loanId&&Outstanding(l)&&((l.creditor==a&&l.debtor==b)||(l.creditor==b&&l.debtor==a)));if(settlement==null)return CampaignAction.Fail("La deuda de esta liquidación ya no existe.");double payment=Math.Min(settlement.Outstanding,settlement.debtor==a?t.sourceGold:t.targetGold);ReduceDebt(settlement,payment);settlement.paid+=payment;settlement.lastPaymentDay=CampaignDay;}
                Gold[a]+=t.targetGold-t.sourceGold;Gold[b]+=t.sourceGold-t.targetGold;
                Troops[a]+=(float)(t.targetTroops-t.sourceTroops);Troops[b]+=(float)(t.sourceTroops-t.targetTroops);
                // Apply both sides atomically before elimination checks, so a territory swap cannot eliminate either side midway.
                ExchangeCampaignTerritory(a,b,sourceLand,targetLand);
                if(t.forgiveDebt&&t.loanId>=0)ForgiveDebt(a,t.loanId,1e12);
                if(p.kind==ProposalKind.Peace||t.compact==CompactKind.Ceasefire||t.compact==CompactKind.Peace||t.compact==CompactKind.Alliance)EndWar(a,b);
                var previous=Affairs.compacts.Find(c=>c.kind==t.compact&&Active(c)&&((c.source==a&&c.target==b)||(c.source==b&&c.target==a)));
                if(previous!=null)previous.active=false;
                Affairs.compacts.Add(new CampaignCompact{id=++Affairs.sequence,source=a,target=b,kind=t.compact,militaryEnemy=t.militaryEnemy,beganDay=CampaignDay,endsDay=t.durationDays<0?-1:CampaignDay+t.durationDays,periodicGold=t.periodicGold,periodDays=t.periodDays,nextPaymentDay=CampaignDay+t.periodDays});
                if(p.kind==ProposalKind.Peace&&t.compact!=CompactKind.Peace&&t.compact!=CompactKind.Ceasefire)Affairs.compacts.Add(new CampaignCompact{id=++Affairs.sequence,source=a,target=b,kind=CompactKind.Peace,beganDay=CampaignDay,endsDay=t.durationDays<0?-1:CampaignDay+t.durationDays});
                if(t.compact==CompactKind.Alliance){ReconcileCoalitionAlliances(a,b);}if(t.compact==CompactKind.Alliance||CampaignPeaceBlocks(a,b))StopOperationsBetween(a,b);
                if(t.militaryEnemy>=0){Affairs.support.Add(new MilitarySupportRequest{id=++Affairs.sequence,source=a,target=b,enemy=t.militaryEnemy,cell=CapitalCells[t.militaryEnemy],kind=SupportKind.Intervention,state=RequestState.Accepted,obligation=true,createdDay=CampaignDay,expiresDay=CampaignDay+10,response="Compromiso militar aceptado mediante tratado"});}
            }
            if(!result.success)return result;p.state=ProposalState.Accepted;ChangeTrust(a,b,8);ChangeTrust(b,a,8);Memory(a,b).promisesKept++;Memory(b,a).promisesKept++;InvalidateDiplomaticTrade();Chronicle("acuerdo",a,b,Names[a]+" y "+Names[b]+" firmaron: "+PoliticalName(p.kind)+" / "+PoliticalName(t.compact)+".");return result;
        }
        public CampaignAction CounterCampaign(int recipient,int proposalId,NegotiationTerms revised)
        {
            var p=Affairs.proposals.Find(x=>x.id==proposalId&&x.target==recipient&&x.state==ProposalState.Pending);if(p==null)return CampaignAction.Fail("La oferta ya no está pendiente.");
            // Loan parties keep their economic roles even when a lender authors the counteroffer.
            if(p.kind==ProposalKind.Loan)revised.loanOfferedByLender=!p.terms.loanOfferedByLender;
            var result=ProposeCampaign(recipient,p.source,p.kind,revised,"Contraoferta a la propuesta "+p.id);
            if(result.success){p.state=ProposalState.Countered;Affairs.proposals[Affairs.proposals.Count-1].parent=p.id;}return result;
        }
        public CampaignAction BreakCompact(int actor,int id)
        {
            var p=Affairs.compacts.Find(c=>c.id==id&&Active(c)&&(c.source==actor||c.target==actor));if(Paused||p==null)return CampaignAction.Fail("El acuerdo ya terminó.");
            if(p.kind==CompactKind.Peace||p.kind==CompactKind.Ceasefire)return CampaignAction.Fail("La tregua debe respetarse hasta su vencimiento.");
            int other=p.source==actor?p.target:p.source;p.active=false;Memory(other,actor).promisesBroken++;ChangeTrust(other,actor,-20);Affairs.reputation[actor]=Mathf.Max(0,Affairs.reputation[actor]-8);
            if(p.kind==CompactKind.Alliance||p.kind==CompactKind.NonAggression){BetrayalUntil[actor]=ElapsedTime+Rules.betrayalSeconds;Memory(other,actor).betrayals++;}
            InvalidateDiplomaticTrade();Chronicle("ruptura",actor,other,Names[actor]+" rompió un acuerdo con "+Names[other]+".");return CampaignAction.Ok("Acuerdo roto; reputación y memoria diplomática actualizadas.");
        }
        private void ExchangeCampaignTerritory(int a,int b,int[] toB,int[] toA)
        {
            if(toB.Length==0&&toA.Length==0)return;
            foreach(int c in toB){TradeOwnershipChanged(c);Owners[c]=b;TerritoryCounts[a]--;TerritoryCounts[b]++;_owned[a].Remove(c);_owned[b].Add(c);CaptureInfrastructure(b,c);Changed=true;TerritoryCaptured?.Invoke(b,c,a);}
            foreach(int c in toA){TradeOwnershipChanged(c);Owners[c]=a;TerritoryCounts[b]--;TerritoryCounts[a]++;_owned[b].Remove(c);_owned[a].Add(c);CaptureInfrastructure(a,c);Changed=true;TerritoryCaptured?.Invoke(a,c,b);}
            foreach(int c in toB){_frontiers[a].Remove(c);RefreshFrontier(c);World.ForEachNeighbor(c,RefreshFrontier);}foreach(int c in toA){_frontiers[b].Remove(c);RefreshFrontier(c);World.ForEachNeighbor(c,RefreshFrontier);}
            for(int i=0;i<2;i++){int f=i==0?a:b;if(TerritoryCounts[f]>0&&Owners[CapitalCells[f]]!=f)foreach(int c in _owned[f]){CapitalCells[f]=c;break;}RecomputeCapacity(f);}
            RebuildContacts();if(_freeSeeded!=null){_freeSeeded[a]=false;_freeSeeded[b]=false;}if(TerritoryCounts[a]==0)NationEliminated(b,a);if(TerritoryCounts[b]==0)NationEliminated(a,b);
        }
        private void TransferCampaignTerritory(int source,int target,int[] cells){if(OwnsTerms(source,cells))ExchangeCampaignTerritory(source,target,cells,Array.Empty<int>());}
        public bool Embargoed(int a,int b){if(Affairs==null)return false;EnsureCampaignRelationIndex();return _embargoIndex.Contains(RelationKey(a,b));}
        private void SetEmbargoInternal(int source,int target,bool enabled,double days)
        {var e=Affairs.embargoes.Find(x=>x.source==source&&x.target==target);if(e==null){e=new Embargo{source=source,target=target};Affairs.embargoes.Add(e);}e.active=enabled;e.endsDay=days<0?-1:CampaignDay+days;InvalidateDiplomaticTrade();}
        public CampaignAction SetEmbargo(int source,int target,bool enabled,double days=-1)
        {if(Paused||!Negotiable(source,target))return CampaignAction.Fail("Elige una nación viva y reanuda la partida.");SetEmbargoInternal(source,target,enabled,days);if(enabled)ChangeTrust(target,source,-8);Chronicle("embargo",source,target,Names[source]+(enabled?" cerró":" reabrió")+" el comercio con "+Names[target]+".");return CampaignAction.Ok(enabled?"Embargo comercial en vigor.":"Embargo propio levantado; la otra nación puede mantener el suyo.");}
        public CampaignAction DeclareWar(int actor,int enemy,WarObjective objective=WarObjective.Territory,int cell=-1)
        {if(!CanDeclareWar(actor,enemy))return CampaignAction.Fail(Paused?"Reanuda la partida para declarar la guerra.":WarDeclarationBlock(actor,enemy));RegisterAggression(actor,enemy,objective,cell);return CampaignAction.Ok("Guerra declarada; comercio interrumpido.");}
        private void RegisterAggression(int actor,int enemy,WarObjective objective=WarObjective.Territory,int cell=-1)
        {
            if(enemy<0||actor==enemy||Affairs==null)return;RecordHostility(actor,enemy);if(IsDeclaredWar(actor,enemy))return;
            Affairs.wars.Add(new CampaignWar{id=++Affairs.sequence,attacker=actor,defender=enemy,objective=objective,goalCell=cell,beganDay=CampaignDay,attackerLossStart=Statistics[actor].troopsLost,defenderLossStart=Statistics[enemy].troopsLost});
            Memory(enemy,actor).wars++;Memory(enemy,actor).lastWarDay=CampaignDay;ChangeTrust(enemy,actor,-25);Affairs.reputation[actor]=Mathf.Max(0,Affairs.reputation[actor]-2);InvalidateDiplomaticTrade();Chronicle("guerra",actor,enemy,Names[actor]+" entró en guerra con "+Names[enemy]+".");
            foreach(var pact in Affairs.compacts)if(Active(pact)&&(pact.kind==CompactKind.MutualDefense||pact.kind==CompactKind.Protection||pact.kind==CompactKind.Alliance))
            {int helper=-1;if(pact.target==enemy)helper=pact.source;else if(pact.kind!=CompactKind.Protection&&pact.source==enemy)helper=pact.target;if(helper>=0&&helper!=actor&&LivingNation(helper))RequestSupport(enemy,helper,SupportKind.JointDefense,0,actor,CapitalCells[enemy],-1,true);}
        }
        private void EndWar(int a,int b)
        {
            foreach(var war in Affairs.wars)if(war.active&&((war.attacker==a&&war.defender==b)||(war.attacker==b&&war.defender==a))){war.active=false;war.endedDay=CampaignDay;}
            _hostilityUntil.Remove(RelationKey(a,b));StopOperationsBetween(a,b);InvalidateDiplomaticTrade();
            foreach(var request in Affairs.support)if(request.state==RequestState.Accepted&&((request.target==a&&request.enemy==b)||(request.target==b&&request.enemy==a)))request.state=RequestState.Fulfilled;
        }
        private void AdvanceCampaignPolitics(float dt)
        {
            if(IsQuickMatch)return;_politicsAccumulator+=dt;if(_politicsAccumulator<.5f)return;_politicsAccumulator-=.5f;
            foreach(var p in Affairs.compacts)if(p.active&&p.endsDay>=0&&p.endsDay<=CampaignDay){p.active=false;p.commitmentFulfilled=true;Memory(p.source,p.target).promisesKept++;Memory(p.target,p.source).promisesKept++;Chronicle("caducidad",p.source,p.target,"Terminó un acuerdo entre "+Names[p.source]+" y "+Names[p.target]+".");InvalidateDiplomaticTrade();}
            AdvanceInternationalFinance();AdvanceCampaignNegotiations();AdvanceSupportMissions();
            if(Affairs.wars.Count>600)Affairs.wars.RemoveAll(w=>!w.active&&CampaignDay-w.endedDay>180);
            // Bounded histories retain all active obligations; only settled records are pruned.
            if(Affairs.proposals.Count>600)Affairs.proposals.RemoveAll(p=>p.state!=ProposalState.Pending&&CampaignDay-p.createdDay>30);
            if(Affairs.support.Count>600)Affairs.support.RemoveAll(p=>p.state!=RequestState.Pending&&p.state!=RequestState.Accepted&&CampaignDay-p.createdDay>30);
        }
    }
}
