using System;
using System.Collections.Generic;
namespace FrontierAtlas
{
    public enum TreatyState { Pending, Allied, Rejected, Expired }
    public sealed class Treaty
    {public int source,target;public TreatyState state;public float expiresAt,responseAt;public bool warningSent;}
    public sealed partial class TerritorySimulation
    {
        public readonly List<Treaty> Treaties=new List<Treaty>();
        public Treaty TreatyBetween(int a,int b)=>Treaties.Find(t=>(t.source==a&&t.target==b)||(t.source==b&&t.target==a));
        public bool AreAllied(int a,int b)
        {
            if(!CampaignEnabled||a<0||b<0||a==b)return false;
            if(Options.teams&&Teams[a]==Teams[b])return true;
            if(CampaignAllied(a,b))return true;
            var treaty=TreatyBetween(a,b);return treaty!=null&&treaty.state==TreatyState.Allied&&treaty.expiresAt>ElapsedTime;
        }
        public CampaignAction ProposeAlliance(int from,int to)
        {
            if(!CampaignEnabled||Paused)return CampaignAction.Fail("Reanuda la partida para negociar.");
            if(to<0||to>=FactionCount||to==from||TerritoryCounts[to]==0||IsTribe(to))return CampaignAction.Fail("Elige una nación rival válida.");
            if(AllianceUnavailable(from,to))return CampaignAction.Fail("La guerra o rivalidad impide formar una alianza.");
            if(AreAllied(from,to))return CampaignAction.Fail("Ya existe una alianza. Puedes renovarla.");
            var treaty=TreatyBetween(from,to);
            if(treaty!=null&&treaty.state==TreatyState.Pending)return CampaignAction.Fail("Ya hay una propuesta pendiente.");
            if(treaty==null){treaty=new Treaty();Treaties.Add(treaty);}
            treaty.source=from;treaty.target=to;treaty.state=TreatyState.Pending;treaty.expiresAt=ElapsedTime+45;treaty.responseAt=ElapsedTime+2;
            Emit(StrategicEventKind.Alliance,from,to,CapitalCells[to],Names[from]+" propone una alianza a "+Names[to]+".");return CampaignAction.Ok("Propuesta de alianza enviada.");
        }
        public CampaignAction AnswerAlliance(int recipient,int sender,bool accept)
        {
            var treaty=TreatyBetween(recipient,sender);
            if(treaty==null||treaty.state!=TreatyState.Pending||treaty.target!=recipient)return CampaignAction.Fail("No existe una solicitud pendiente para esta nación.");
            if(accept&&AllianceUnavailable(recipient,sender))return CampaignAction.Fail("La guerra o rivalidad impide aceptar esta alianza.");
            treaty.state=accept?TreatyState.Allied:TreatyState.Rejected;treaty.expiresAt=accept?ElapsedTime+Rules.allianceSeconds:ElapsedTime+30;treaty.warningSent=false;
            if(accept)
            {
                _tradePermissions.Clear();_tradeRefresh=0;
                EndWar(recipient,sender);ReconcileCoalitionAlliances(recipient,sender);StopOperationsBetween(recipient,sender);
            }
            Emit(StrategicEventKind.Alliance,recipient,sender,CapitalCells[recipient],Names[recipient]+(accept?" aceptó la alianza.":" rechazó la alianza."));
            return CampaignAction.Ok(accept?"Alianza aceptada. Los ataques ordinarios entre ambos quedan bloqueados.":"Propuesta rechazada.");
        }
        public CampaignAction RenewAlliance(int from,int to)
        {
            if(AllianceUnavailable(from,to))return CampaignAction.Fail("La rivalidad impide renovar esta alianza.");
            if(!AreAllied(from,to))return CampaignAction.Fail("No hay una alianza activa que renovar.");
            if(Options.teams&&Teams[from]==Teams[to])return CampaignAction.Fail("La alianza de equipo no caduca.");
            var advanced=Affairs.compacts.Find(c=>c.kind==CompactKind.Alliance&&Active(c)&&((c.source==from&&c.target==to)||(c.target==from&&c.source==to)));
            if(advanced!=null)return ProposeCampaign(from,to,ProposalKind.Treaty,new NegotiationTerms{compact=CompactKind.Alliance,durationDays=30},"Renovación de alianza.");
            var treaty=TreatyBetween(from,to);treaty.expiresAt=ElapsedTime+Rules.allianceSeconds;treaty.warningSent=false;
            Emit(StrategicEventKind.Alliance,from,to,CapitalCells[to],"Alianza renovada entre "+Names[from]+" y "+Names[to]+".");return CampaignAction.Ok("Alianza renovada.");
        }
        public CampaignAction Betray(int from,int to)
        {
            if(!AreAllied(from,to))return CampaignAction.Fail("No existe una alianza que romper.");
            if(Options.teams&&Teams[from]==Teams[to])return CampaignAction.Fail("No puedes traicionar a tu equipo en este modo.");
            var old=TreatyBetween(from,to);if(old!=null)old.state=TreatyState.Expired;foreach(var pact in Affairs.compacts)if(pact.kind==CompactKind.Alliance&&Active(pact)&&((pact.source==from&&pact.target==to)||(pact.target==from&&pact.source==to)))pact.active=false;
            Memory(to,from).betrayals++;ChangeTrust(to,from,-35);Affairs.reputation[from]=UnityEngine.Mathf.Max(0,Affairs.reputation[from]-15);Chronicle("traición",from,to,Names[from]+" traicionó una alianza.");InvalidateDiplomaticTrade();RecordHostility(from,to);BetrayalUntil[from]=ElapsedTime+Rules.betrayalSeconds;
            Emit(StrategicEventKind.Alliance,from,to,CapitalCells[from],Names[from]+" ha roto su alianza y queda marcada por traición.");return CampaignAction.Ok("Alianza rota. Tu vulnerabilidad aumenta temporalmente.");
        }
        public CampaignAction Donate(int from,int to,double amount,bool troops)
        {
            if(Paused||!CampaignEnabled)return CampaignAction.Fail("Reanuda la partida para donar.");
            if(!AreAllied(from,to)||TerritoryCounts[to]==0)return CampaignAction.Fail("Las donaciones requieren un aliado activo.");
            if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<=0)return CampaignAction.Fail("Introduce una cantidad positiva.");
            if(troops)
            {
                if(amount>AvailableTroops(from)+.001)return CampaignAction.Fail("No tienes tantas tropas en reserva.");
                if(amount>Capacity[to]-StoredTroops(to)+.001)return CampaignAction.Fail("El aliado no tiene capacidad para recibir tantas tropas.");
                Troops[from]-=(float)amount;Troops[to]+=(float)amount;
            }
            else{if(amount>Gold[from]+.001)return CampaignAction.Fail("No tienes suficiente oro.");Gold[from]-=amount;Ledgers[from].donationsSent+=amount;CreditGold(to,amount,GoldFlow.Donation);}
            Emit(StrategicEventKind.Information,from,to,CapitalCells[to],Names[from]+" realizó una donación a "+Names[to]+".");return CampaignAction.Ok("Donación transferida.");
        }
        private void AdvanceDiplomacy(float dt)
        {
            foreach(var treaty in Treaties)
            {
                if(treaty.state==TreatyState.Pending&&treaty.expiresAt<=ElapsedTime){treaty.state=TreatyState.Expired;continue;}
                if(treaty.state==TreatyState.Pending&&treaty.target!=PlayerFaction&&AiEnabled&&treaty.responseAt<=ElapsedTime)
                {
                    double chance=Options.difficulty==Difficulty.Relaxed? .75:Options.difficulty==Difficulty.Brutal? .35:.55;
                    AnswerAlliance(treaty.target,treaty.source,!AllianceUnavailable(treaty.target,treaty.source)&&(Options.difficulty==Difficulty.Brutal?AllyUtility(treaty.target,treaty.source)>=22:_random.NextDouble()<chance));
                }
                if(treaty.state!=TreatyState.Allied)continue;
                if(treaty.expiresAt<=ElapsedTime){treaty.state=TreatyState.Expired;Emit(StrategicEventKind.Alliance,treaty.source,treaty.target,CapitalCells[treaty.source],"La alianza entre "+Names[treaty.source]+" y "+Names[treaty.target]+" ha terminado.");}
                else if(!treaty.warningSent&&treaty.expiresAt-ElapsedTime<=Rules.allianceWarningSeconds){treaty.warningSent=true;Emit(StrategicEventKind.Alliance,treaty.source,treaty.target,CapitalCells[treaty.source],"Una alianza está próxima a caducar. Puedes renovarla desde Diplomacia.");}
            }
        }
    }
}
