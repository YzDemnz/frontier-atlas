using System;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        public CampaignAction CreateCoalition(int leader,int enemy,string name,WarObjective objective,int goal=-1)
        {
            if(Paused||!Negotiable(leader,enemy)||AreAllied(leader,enemy))return CampaignAction.Fail("Elige un rival válido.");
            if(Affairs.coalitions.Exists(c=>c.active&&c.leader==leader&&c.enemy==enemy))return CampaignAction.Fail("Ya diriges una coalición contra esa nación.");
            var coalition=new Coalition{id=++Affairs.sequence,leader=leader,enemy=enemy,name=string.IsNullOrWhiteSpace(name)?"Coalición de "+Names[leader]:name.Substring(0,Math.Min(name.Length,40)),objective=objective,goalCell=goal,beganDay=CampaignDay};coalition.members.Add(leader);Affairs.coalitions.Add(coalition);
            Chronicle("coalición",leader,enemy,"Se creó "+coalition.name+". Cada miembro conserva sus tropas y sus fondos.");return CampaignAction.Ok("Coalición creada. Puedes invitar a varias naciones.");
        }
        public CampaignAction InviteCoalition(int actor,int coalitionId,int nation)
        {
            var c=Affairs.coalitions.Find(x=>x.id==coalitionId&&x.active&&x.members.Contains(actor));if(c==null||c.enemy==nation||c.members.Contains(nation))return CampaignAction.Fail("Invitación no válida.");
            return ProposeCampaign(actor,nation,ProposalKind.CoalitionInvite,new NegotiationTerms{coalitionId=coalitionId},"Invitación a "+c.name+" contra "+Names[c.enemy]+".");
        }
        private CampaignAction JoinCoalition(int nation,int id)
        {
            var c=Affairs.coalitions.Find(x=>x.id==id&&x.active);if(c==null||!LivingNation(c.enemy)||AreAllied(nation,c.enemy)||CampaignPeaceBlocks(nation,c.enemy))return CampaignAction.Fail("Tus acuerdos impiden unirte a esta coalición.");
            if(!c.members.Contains(nation))c.members.Add(nation);Chronicle("adhesión",nation,c.leader,Names[nation]+" se unió a "+c.name+".");return CampaignAction.Ok("Miembro de la coalición. Se comparten objetivos e información militar.");
        }
        public CampaignAction LeaveCoalition(int nation,int id)
        {
            var c=Affairs.coalitions.Find(x=>x.id==id&&x.active&&x.members.Contains(nation));if(Paused||c==null)return CampaignAction.Fail("No perteneces a esa coalición.");
            c.members.Remove(nation);foreach(int ally in c.members){ChangeTrust(ally,nation,IsDeclaredWar(nation,c.enemy)?-12:-3);Memory(ally,nation).promisesBroken++;}
            if(IsDeclaredWar(nation,c.enemy))Affairs.reputation[nation]=Mathf.Max(0,Affairs.reputation[nation]-5);
            if(c.members.Count==0)c.active=false;else if(c.leader==nation)c.leader=c.members[0];
            foreach(var r in Affairs.support)if(r.coalitionId==id&&r.target==nation&&r.state==RequestState.Accepted)r.state=RequestState.Rejected;
            Chronicle("salida",nation,c.enemy,Names[nation]+" salió de "+c.name+". Sus guerras y tratados requieren negociación separada.");return CampaignAction.Ok("Has salido de la coalición.");
        }
        public bool SharesMilitaryInfo(int a,int b)=>a==b||AreAllied(a,b)||Affairs.coalitions.Exists(c=>c.active&&c.members.Contains(a)&&c.members.Contains(b));
        public CampaignAction SetCoalitionObjective(int actor,int id,WarObjective objective,int cell)
        {
            var c=Affairs.coalitions.Find(x=>x.id==id&&x.active&&x.leader==actor);if(Paused||c==null)return CampaignAction.Fail("Solo el líder puede cambiar el objetivo común.");
            if(cell>=Owners.Length||cell< -1)return CampaignAction.Fail("Objetivo fuera del mapa.");c.objective=objective;c.goalCell=cell;
            foreach(int member in c.members)if(member!=actor)RequestSupport(actor,member,SupportKind.Intervention,0,c.enemy,cell,id);
            Chronicle("objetivo",actor,c.enemy,"Objetivo compartido actualizado: "+PoliticalName(objective)+".");return CampaignAction.Ok("Objetivo comunicado; cada aliado decidirá su aportación.");
        }
        public CampaignAction RequestSupport(int source,int target,SupportKind kind,double amount,int enemy=-1,int cell=-1,int coalitionId=-1,bool obligation=false)
        {
            if(Paused||!Negotiable(source,target)||!MoneyValue(amount)||cell< -1||cell>=Owners.Length)return CampaignAction.Fail("Solicitud de ayuda no válida.");
            if(Affairs.support.Exists(r=>r.source==source&&r.target==target&&r.kind==kind&&r.enemy==enemy&&(r.state==RequestState.Pending||r.state==RequestState.Accepted)))return CampaignAction.Fail("Ya existe una solicitud de ese tipo pendiente.");
            Affairs.support.Add(new MilitarySupportRequest{id=++Affairs.sequence,source=source,target=target,kind=kind,amount=amount,enemy=enemy,cell=cell,coalitionId=coalitionId,obligation=obligation,createdDay=CampaignDay,expiresDay=CampaignDay+5,responseDay=CampaignDay+.05+_random.NextDouble()*.12});
            Chronicle("ayuda",source,target,Names[source]+" solicitó "+PoliticalName(kind)+" a "+Names[target]+".");return CampaignAction.Ok("Solicitud enviada; no compromete recursos sin aceptación.");
        }
        public CampaignAction AnswerSupport(int recipient,int id,bool accept)
        {
            var r=Affairs.support.Find(x=>x.id==id&&x.target==recipient&&x.state==RequestState.Pending);if(Paused||r==null||r.expiresDay<=CampaignDay)return CampaignAction.Fail("La solicitud ya terminó.");
            if(!accept){r.state=RequestState.Rejected;r.response="Ayuda rechazada";if(r.obligation){ChangeTrust(r.source,recipient,-15);Memory(r.source,recipient).promisesBroken++;Affairs.reputation[recipient]=Mathf.Max(0,Affairs.reputation[recipient]-5);}return CampaignAction.Ok("Solicitud rechazada.");}
            if(!Negotiable(r.source,recipient))return CampaignAction.Fail("La nación solicitante ya no está activa.");
            if(r.kind==SupportKind.Gold)
            {var result=TransferGold(recipient,r.source,r.amount,"Ayuda económica de "+Names[recipient]+".");if(!result.success)return result;Memory(r.source,recipient).goldHelp+=r.amount;}
            else if(r.kind==SupportKind.Troops)
            {if(r.amount<=0||r.amount>AvailableTroops(recipient)+.001||r.amount>Capacity[r.source]-StoredTroops(r.source)+.001)return CampaignAction.Fail("Faltan tropas o capacidad del receptor.");Troops[recipient]-=(float)r.amount;Troops[r.source]+=(float)r.amount;Memory(r.source,recipient).troopHelp+=r.amount;}
            else if(r.kind==SupportKind.EmergencyLoan)
            {var terms=new NegotiationTerms{principal=r.amount,interestPerPeriod=.01,loanDays=90};var result=IssueLoan(recipient,r.source,terms);if(!result.success)return result;}
            else if(r.kind==SupportKind.Mediation)
            {if(!LivingNation(r.enemy)||IsHostile(recipient,r.source)||IsHostile(recipient,r.enemy))return CampaignAction.Fail("El mediador debe ser neutral.");var result=ProposeCampaign(recipient,r.source,ProposalKind.Mediation,new NegotiationTerms{mediatedNation=r.enemy,compact=CompactKind.Ceasefire,durationDays=15},"Oferta de mediación neutral.");if(!result.success)return result;}
            else if(!LivingNation(r.enemy)||!CanAttackNation(recipient,r.enemy))return CampaignAction.Fail("Un tratado o protección impide intervenir contra ese rival.");
            r.state=r.kind==SupportKind.Gold||r.kind==SupportKind.Troops||r.kind==SupportKind.EmergencyLoan||r.kind==SupportKind.Mediation?RequestState.Fulfilled:RequestState.Accepted;r.response="Ayuda aceptada";ChangeTrust(r.source,recipient,5);Memory(r.source,recipient).lastAidDay=CampaignDay;Chronicle("ayuda aceptada",recipient,r.source,Names[recipient]+" aceptó la solicitud de "+Names[r.source]+".",r.amount);return CampaignAction.Ok("Ayuda aceptada con recursos propios.");
        }
        private void AdvanceSupportMissions()
        {
            foreach(var c in Affairs.coalitions)if(c.active&&!LivingNation(c.enemy))c.active=false;
            int planBudget=2;for(int requestIndex=0,requestCount=Affairs.support.Count;requestIndex<requestCount;requestIndex++)
            {
                var r=Affairs.support[requestIndex];if(r.state==RequestState.Pending&&CampaignDay>=r.expiresDay){r.state=RequestState.Expired;continue;}
                if(r.state!=RequestState.Accepted)continue;if(CampaignDay-r.createdDay>10){r.state=r.missionSent?RequestState.Fulfilled:RequestState.Rejected;if(!r.missionSent&&r.obligation){Memory(r.source,r.target).promisesBroken++;ChangeTrust(r.source,r.target,-12);Affairs.reputation[r.target]=Mathf.Max(0,Affairs.reputation[r.target]-5);}r.response=r.missionSent?"Misión de apoyo terminada":"No fue posible alcanzar el frente con medios propios";continue;}if(!LivingNation(r.enemy)||!LivingNation(r.target)){r.state=RequestState.Fulfilled;continue;}
                if(!CanAttackNation(r.target,r.enemy)){r.state=RequestState.Rejected;r.response="Tratado incompatible con la intervención";continue;}
                if(r.missionSent){bool busy=Operations.Exists(o=>o.active&&o.faction==r.target&&o.enemy==r.enemy);if(!busy&&r.kind!=SupportKind.NavalEscort&&r.kind!=SupportKind.Bombardment)r.state=RequestState.Fulfilled;continue;}
                if(CampaignDay<r.nextAttemptDay||planBudget<=0)continue;r.nextAttemptDay=CampaignDay+.15;planBudget--;
                int goal=r.cell>=0?r.cell:CapitalCells[r.enemy];if(r.kind==SupportKind.Diversion){int best=-1;float distance=-1;foreach(int edge in _frontiers[r.target])for(int d=0;d<4;d++){int n=World.Neighbor(edge,d);if(n>=0&&Owners[n]==r.enemy){float value=Distance(n,CapitalCells[r.source]);if(value>distance){best=n;distance=value;}}}if(best>=0)goal=best;}
                if(r.kind==SupportKind.NavalEscort||r.kind==SupportKind.Bombardment)
                {
                    var ship=Warships.Find(s=>s.owner==r.target&&!s.sunk);if(ship==null){var port=Buildings.Find(b=>b.owner==r.target&&b.kind==BuildingKind.Port&&b.Operational);if(port!=null&&Gold[r.target]>=WarshipPrice(r.target)){DeployWarship(r.target,port.cell);ship=Warships.Find(s=>s.owner==r.target&&!s.sunk);}if(ship==null)continue;}
                    int target=r.kind==SupportKind.Bombardment?goal:AdjacentWater(CapitalCells[r.source]);if(target<0&&r.kind==SupportKind.NavalEscort){float nearest=float.PositiveInfinity;foreach(int coast in World.CoastCells)if(Owners[coast]==r.source){float distance=Distance(coast,CapitalCells[r.source]);if(distance<nearest){nearest=distance;target=AdjacentWater(coast);}}}if(target<0)target=AdjacentWater(goal);if(target>=0&&MoveWarship(ship.id,target).success){r.missionSent=true;RegisterAggression(r.target,r.enemy,WarObjective.Naval,goal);}continue;
                }
                int border=ContactCell(r.target,r.enemy);if(border>=0){if(goal<0||!IsBorderTarget(r.target,goal))goal=border;var result=StartCampaignAttack(r.target,goal,.4f);if(result==OrderResult.Accepted)r.missionSent=true;}
                else if(goal>=0&&HasCoastalAccess(r.target)&&StartCampaignInvasion(r.target,goal,.3f)==OrderResult.Accepted)r.missionSent=true;
                if(CampaignDay-r.createdDay>10&&!r.missionSent){r.state=RequestState.Rejected;r.response="Sin acceso ni medios para alcanzar el frente";ChangeTrust(r.source,r.target,-3);}
            }
        }
    }
}
