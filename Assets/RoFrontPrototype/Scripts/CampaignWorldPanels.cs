using System;
using System.IO;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierInterface
    {
        private void DrawCoalitionBoard(int player,int other,float w,ref float y)
        {
            Text("OBJETIVOS COMUNES, RECURSOS PROPIOS",ref y,w,32,FrontierUI.Accent);Field(ref _coalitionName,"Nombre de la coalición",ref y,w);_goalChoice=Choice(GoalNames,_goalChoice,ref y,w);
            if(BoardButton("CREAR COALICIÓN CONTRA "+G.Names[other],ref y,w))Action(G.CreateCoalition(player,other,_coalitionName,(WarObjective)_goalChoice,_pickedCell));
            foreach(var c in G.Affairs.coalitions)if(c.active)
            {
                string members="";foreach(int f in c.members)members+=G.Names[f]+" · ";Text(c.name+" · rival "+G.Names[c.enemy]+"\n"+members+"\nObjetivo: "+GoalNames[(int)c.objective],ref y,w,78);
                if(!c.members.Contains(player))continue;
                if(BoardButton("INVITAR A "+G.Names[other]+" · #"+c.id,ref y,w))Action(G.InviteCoalition(player,c.id,other));
                if(c.leader==player&&BoardButton("COMUNICAR OBJETIVO ELEGIDO A TODOS",ref y,w))Action(G.SetCoalitionObjective(player,c.id,(WarObjective)_goalChoice,_pickedCell));
                foreach(int member in c.members)if(member!=player){Text(G.Names[member]+": reserva "+TroopNumber(G.AvailableTroops(member))+" / enviadas "+TroopNumber(G.CommittedTroops[member]),ref y,w,30);foreach(var op in G.Operations)if(op.active&&op.faction==member){Text("Frente #"+op.id+" · "+TroopNumber(op.troops)+" tropas",ref y,w,28);if(BoardButton("LOCALIZAR FRENTE ALIADO #"+op.id,ref y,w)){_p.FocusCell(op.frontCell);CloseWorldBoard();}}}
                if(BoardButton("SALIR DE ESTA COALICIÓN",ref y,w))Action(G.LeaveCoalition(player,c.id));
            }
            DrawPendingOffers(player,w,ref y);
        }
        private void DrawSupportBoard(int player,int other,float w,ref float y)
        {
            Text("SOLICITAR APOYO A "+G.Names[other],ref y,w,32,FrontierUI.Accent);_aidChoice=Choice(AidNames,_aidChoice,ref y,w);Field(ref _helpAmount,"Cantidad de tropas u oro (el préstamo urgente tiene 1% / 15 días, 90 días)",ref y,w);
            Text("En intervenciones, el rival es el propietario del punto marcado. En mediación, es tu adversario. Marca el frente antes de solicitar ayuda.",ref y,w,65);
            if(BoardButton("MARCAR FRENTE / OBJETIVO",ref y,w)){WorldBoardOpen=false;_pickBoardCell=true;_p.CloseCampaignPanel();_p.Notify("Haz clic en la posición del frente que quieres comunicar.");}
            int enemy=_pickedCell>=0?G.Owners[_pickedCell]:-1;
            if(BoardButton("SOLICITAR AYUDA",ref y,w))Action(G.RequestSupport(player,other,(SupportKind)_aidChoice,Number(_helpAmount),enemy,_pickedCell));
            foreach(var r in G.Affairs.support)if(r.source==player||r.target==player)
            {string[] states={"Pendiente","Aceptada","Rechazada","Cumplida","Caducada"};Text("#"+r.id+" · "+AidNames[(int)r.kind]+" · "+G.Names[r.source]+" → "+G.Names[r.target]+"\n"+states[(int)r.state]+" · "+Amount(r.amount)+" · "+r.response,ref y,w,65);
                if(r.state==RequestState.Pending&&r.target==player){if(BoardButton("ACEPTAR AYUDA SOLICITADA #"+r.id,ref y,w))Action(G.AnswerSupport(player,r.id,true));if(BoardButton("RECHAZAR #"+r.id,ref y,w))Action(G.AnswerSupport(player,r.id,false));}}
        }
        private void DrawFinanceBoard(int player,int other,float w,ref float y)
        {
            Text("TESORERÍA",ref y,w,32,FrontierUI.Gold);Text("Oro disponible: "+Amount(G.Gold[player])+"\nDeuda pendiente: "+Amount(G.DebtTotal(player))+" · créditos concedidos: "+Amount(G.CreditTotal(player))+"\nIngresos nuevos del periodo: "+Amount(G.Affairs.periodIncome[player])+" · cuota global prevista: "+Amount(G.Affairs.periodIncome[player]*.15),ref y,w,90);
            Text("Cada 15 días de juego se dedica como máximo el 15% de ingresos nuevos a todas las deudas pagaderas, repartido según sus saldos. El préstamo y las donaciones no cuentan como ingreso nuevo.",ref y,w,78);
            Field(ref _loanAmount,"Oro que solicitas prestado a "+G.Names[other],ref y,w);Field(ref _interest,"Interés porcentual por cada 15 días (0–50)",ref y,w);Field(ref _days,"Plazo del préstamo: 15–3650 días",ref y,w);Field(ref _collateralGold,"Oro a retener como garantía (se libera al pagar o condonar)",ref y,w);
            if(BoardButton("MARCAR GARANTÍA TERRITORIAL",ref y,w)){WorldBoardOpen=false;_pickBoardCell=true;_p.CloseCampaignPanel();}
            _pledgeLand=_ui.Toggle(new Rect(0,y,w,32),"Ofrecer área propia como garantía (valor pactado igual al principal)",_pledgeLand);y+=42;
            if(BoardButton("SOLICITAR PRÉSTAMO NEGOCIADO",ref y,w))Action(G.ProposeCampaign(player,other,ProposalKind.Loan,new NegotiationTerms{principal=Number(_loanAmount),interestPerPeriod=Number(_interest)/100,loanDays=Number(_days,90),collateralGold=Number(_collateralGold),collateral=_pledgeLand?NegotiatedCells(player):Array.Empty<int>(),collateralValue=_pledgeLand?Number(_loanAmount):0},"Préstamo negociado con garantía pactada."));
            Text("Un impago permite ejecutar la garantía 15 días después del vencimiento. El territorio cedido reduce la deuda por su valor pactado; no produce oro.",ref y,w,70);
            if(BoardButton("IMPONER EMBARGO A "+G.Names[other],ref y,w))Action(G.SetEmbargo(player,other,true));if(BoardButton("LEVANTAR MI EMBARGO",ref y,w))Action(G.SetEmbargo(player,other,false));
            Text(G.FinancialStress(player)?"Hay compromisos que superan tu oro disponible.":"Tus compromisos inmediatos caben en tu tesorería.",ref y,w,44,G.FinancialStress(player)?FrontierUI.Gold:FrontierUI.Accent);DrawPendingOffers(player,w,ref y);
        }
        private void DrawDebtBoard(int player,float w,ref float y)
        {
            Text("DEUDAS Y CRÉDITOS",ref y,w,32,FrontierUI.Accent);Field(ref _loanAmount,"Importe del pago anticipado o condonación",ref y,w);Field(ref _days,"Días adicionales para renegociar (15–3650)",ref y,w);Field(ref _interest,"Nuevo interés por periodo de 15 días (%)",ref y,w);
            string[] states={"Activa","Pagada","Pago rechazado","Vencida","Condonada","Incobrable"};
            foreach(var loan in G.Affairs.loans)if(loan.creditor==player||loan.debtor==player)
            {
                Text("#"+loan.id+" · "+G.Names[loan.debtor]+" debe a "+G.Names[loan.creditor]+"\n"+states[(int)loan.status]+" · principal "+Amount(loan.principal)+" + interés "+Amount(loan.interest)+"\nPagado "+Amount(loan.paid)+" · interés "+(loan.interestPerPeriod*100).ToString("0.##")+"% / 15 días · vence día "+loan.maturityDay.ToString("0.0")+"\nGarantía de oro retenida: "+Amount(loan.escrowGold)+" · Próxima cuota global: día "+G.NextDebtPayment(loan.debtor).ToString("0.0"),ref y,w,115);
                if(loan.Outstanding<.001)continue;
                if(loan.debtor==player){if(BoardButton("PAGAR ANTICIPADAMENTE #"+loan.id,ref y,w))Action(G.PayDebt(player,loan.id,Number(_loanAmount)));if(BoardButton("RENEGOCIAR #"+loan.id,ref y,w))Action(G.RenegotiateDebt(player,loan.id,Number(_days,30),Number(_interest)/100));if(BoardButton("RECHAZAR PAGO · AFECTA TU REPUTACIÓN #"+loan.id,ref y,w))Action(G.RefuseDebt(player,loan.id));}
                else if(BoardButton("CONDONAR IMPORTE #"+loan.id,ref y,w))Action(G.ForgiveDebt(player,loan.id,Number(_loanAmount)));
            }
        }
        private void DrawPeaceBoard(int player,int other,float w,ref float y)
        {
            Text("GUERRA Y PAZ CON "+G.Names[other],ref y,w,32,FrontierUI.Accent);_goalChoice=Choice(GoalNames,_goalChoice,ref y,w);
            if(BoardButton("DECLARAR GUERRA CON ESTE OBJETIVO",ref y,w,G.CanDeclareWar(player,other)))Action(G.DeclareWar(player,other,(WarObjective)_goalChoice,_pickedCell));
            foreach(var war in G.Affairs.wars)if(war.active&&(war.attacker==player||war.defender==player))Text(G.Names[war.attacker]+" / "+G.Names[war.defender]+" · objetivo "+GoalNames[(int)war.objective]+"\nPérdidas desde el inicio: "+TroopNumber(G.Statistics[war.attacker].troopsLost-war.attackerLossStart)+" / "+TroopNumber(G.Statistics[war.defender].troopsLost-war.defenderLossStart),ref y,w,68);
            Field(ref _offerGold,"Oro que cedes como reparación",ref y,w);Field(ref _askGold,"Oro que exiges como reparación",ref y,w);Field(ref _periodic,"Reparación periódica: tributo exigido cada 15 días (0: paz simple)",ref y,w);Field(ref _days,"Días de paz o alto el fuego",ref y,w);DrawTerritoryTerms(w,ref y);
            if(BoardButton("PROPONER PAZ / CONCESIONES",ref y,w)){var t=BoardTerms();t.compact=Number(_periodic)>0?CompactKind.Tribute:CompactKind.Peace;Action(G.ProposeCampaign(player,other,ProposalKind.Peace,t,"Propuesta de paz negociada, reparaciones y concesiones."));}
            if(BoardButton("EXIGIR CAPITULACIÓN Y REPARACIÓN DEL 25%",ref y,w))Action(G.ProposeCampaign(player,other,ProposalKind.Demand,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,targetGold=G.Gold[other]*.25},"Exigencia de capitulación con reparación y conservación de territorio restante."));
            if(BoardButton("SOLICITAR ALTO EL FUEGO DE CINCO MESES",ref y,w))Action(G.ProposeWarTruce(player,other));
            if(BoardButton("OFRECER RENDICIÓN SIN ELIMINAR MI NACIÓN",ref y,w))Action(G.ProposeCampaign(player,other,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,sourceGold=G.Gold[player]*.25},"Rendición negociada con reparación del 25% del oro disponible y conservación del territorio restante."));
            foreach(var loan in G.Affairs.loans)if(loan.creditor==player&&loan.debtor==other&&loan.Outstanding>.001&&BoardButton("OFRECER CONDONACIÓN #"+loan.id+" A CAMBIO DE PAZ",ref y,w))Action(G.ProposeCampaign(player,other,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,forgiveDebt=true,loanId=loan.id},"Condonación del saldo pendiente a cambio de paz."));
            foreach(var loan in G.Affairs.loans)if(loan.creditor==player&&loan.debtor==other&&loan.Outstanding>.001&&BoardButton("LIQUIDAR DEUDA #"+loan.id+" CON EL ORO EXIGIDO Y PAZ",ref y,w))Action(G.ProposeCampaign(player,other,ProposalKind.Peace,new NegotiationTerms{compact=CompactKind.Peace,durationDays=30,loanId=loan.id,targetGold=Math.Min(loan.Outstanding,Number(_askGold))},"Liquidación de obligaciones mediante tratado de paz."));
            DrawPendingOffers(player,w,ref y);
        }
        private void DrawHistoryBoard(float w,ref float y)
        {
            Text("CALENDARIO Y REGISTRO MUNDIAL",ref y,w,32,FrontierUI.Accent);Text("Día de campaña "+G.CampaignDay.ToString("0.00")+" · "+G.Rules.campaignSecondsPerDay.ToString("0")+" segundos de simulación por día\nLos tratados, vencimientos y pagos usan este calendario.",ref y,w,65);
            float[] speeds={1,2,5,10,20};for(int i=0;i<speeds.Length;i++)if(_ui.Button(new Rect(i*w/5,y,w/5-8,34),speeds[i]+"×",G.CampaignSpeed==speeds[i]))G.CampaignSpeed=speeds[i];y+=47;
            for(int i=G.Affairs.history.Count-1;i>=0;i--){var e=G.Affairs.history[i];Text("Día "+e.day.ToString("0.00")+" · "+e.message+(e.amount>0?" · "+Amount(e.amount):""),ref y,w,58);}
        }
        private void DrawSaveBoard(float w,ref float y)
        {
            Text("GUARDADO COMPLETO DE CAMPAÑA",ref y,w,32,FrontierUI.Accent);Text("Incluye mapa, recursos, edificios, rutas, ejércitos, guerras, tratados, calendario y deudas. La partida cargada comienza en pausa.",ref y,w,66);
            if(BoardButton(_p.SavingCampaign?"GUARDANDO…":"GUARDAR AHORA [F5]",ref y,w,_p.HasMatch&&!_p.SavingCampaign))_p.SaveFullCampaign();
            if(BoardButton("ACTUALIZAR LISTA",ref y,w))_saveFiles=CampaignFiles.List();Text(_p.CampaignFileNotice,ref y,w,66);Text(CampaignFiles.DirectoryPath,ref y,w,65);
            foreach(string file in _saveFiles){Text(Path.GetFileName(file),ref y,w,30);if(BoardButton("CARGAR ESTE GUARDADO",ref y,w)){_p.LoadFullCampaign(file);if(_p.CampaignFileNotice.StartsWith("Partida restaurada"))CloseWorldBoard();}}
        }
    }
}
