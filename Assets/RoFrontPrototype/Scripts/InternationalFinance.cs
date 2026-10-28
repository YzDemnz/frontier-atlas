using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private static bool MoneyValue(double amount)=>!double.IsNaN(amount)&&!double.IsInfinity(amount)&&amount>=0&&amount<=1e12;
        private bool Outstanding(InternationalLoan loan)=>loan.Outstanding>.001&&loan.status!=DebtStatus.Repaid&&loan.status!=DebtStatus.Forgiven&&loan.status!=DebtStatus.WrittenOff;
        public double DebtTotal(int debtor){double sum=0;foreach(var loan in Affairs.loans)if(loan.debtor==debtor&&Outstanding(loan))sum+=loan.Outstanding;return sum;}
        public double CreditTotal(int creditor){double sum=0;foreach(var loan in Affairs.loans)if(loan.creditor==creditor&&Outstanding(loan))sum+=loan.Outstanding;return sum;}
        public double NextDebtPayment(int debtor)=>Affairs.nextDebtPayment[debtor];
        public bool FinancialStress(int f)
        {
            double due=Affairs.periodIncome[f]*.15;
            foreach(var pact in Affairs.compacts)if(Active(pact)&&pact.periodicGold>0&&(pact.kind==CompactKind.Tribute?pact.target:pact.source)==f&&pact.nextPaymentDay<=CampaignDay+15)due+=pact.periodicGold;
            return due>Gold[f]+.001;
        }
        private void RegisterCampaignIncome(int f,double amount,GoldFlow flow)
        {if(Affairs!=null&&amount>0&&flow!=GoldFlow.Donation&&DebtTotal(f)>0)Affairs.periodIncome[f]+=amount;}
        private CampaignAction TransferGold(int source,int target,double amount,string reason)
        {
            if(!Negotiable(source,target)||!MoneyValue(amount)||Gold[source]+.001<amount)return CampaignAction.Fail("No hay oro suficiente para la transferencia acordada.");
            Gold[source]-=amount;Gold[target]+=amount;Chronicle("transferencia",source,target,reason,amount);return CampaignAction.Ok("Transferencia realizada.");
        }
        public CampaignAction RequestLoan(int borrower,int lender,double amount,double interest=0,double days=90,int[] collateral=null)
        {
            var terms=new NegotiationTerms{principal=amount,interestPerPeriod=interest,loanDays=days,collateral=collateral??Array.Empty<int>(),collateralValue=collateral!=null&&collateral.Length>0?amount:0};
            return ProposeCampaign(borrower,lender,ProposalKind.Loan,terms,"Solicitud de préstamo. Cuota global del 15% de ingresos nuevos cada 15 días.");
        }
        private CampaignAction IssueLoan(int lender,int borrower,NegotiationTerms terms)
        {
            if(!Negotiable(lender,borrower)||!MoneyValue(terms.principal)||terms.principal<=0||Gold[lender]+.001<terms.principal)return CampaignAction.Fail("El prestamista no dispone del oro solicitado.");
            if(!MoneyValue(terms.interestPerPeriod)||terms.interestPerPeriod>.5||terms.loanDays<15||terms.loanDays>3650||!MoneyValue(terms.collateralGold)||terms.collateralGold>Gold[borrower]+terms.principal)return CampaignAction.Fail("Revisa interés, plazo y oro en garantía del préstamo.");
            foreach(int cell in terms.collateral)
            {
                if(cell<0||cell>=Owners.Length||Owners[cell]!=borrower)return CampaignAction.Fail("La garantía debe ser territorio del prestatario.");
                foreach(var other in Affairs.loans)if(Outstanding(other)&&Array.IndexOf(other.collateral,cell)>=0)return CampaignAction.Fail("Ese territorio ya garantiza otro préstamo.");
            }
            if(Affairs.nextDebtPayment[borrower]<0||DebtTotal(borrower)<.001){Affairs.periodIncome[borrower]=0;Affairs.nextDebtPayment[borrower]=CampaignDay+15;}
            Gold[lender]-=terms.principal;Gold[borrower]+=terms.principal;
            var loan=new InternationalLoan{id=++Affairs.sequence,creditor=lender,debtor=borrower,status=DebtStatus.Active,originalPrincipal=terms.principal,principal=terms.principal,interestPerPeriod=terms.interestPerPeriod,beganDay=CampaignDay,maturityDay=CampaignDay+terms.loanDays,nextInterestDay=CampaignDay+15,collateral=(int[])terms.collateral.Clone(),collateralValue=terms.collateral.Length>0?(terms.collateralValue>0?Math.Min(terms.collateralValue,terms.principal):terms.principal):0};
            loan.escrowGold=terms.collateralGold;Gold[borrower]-=loan.escrowGold;Affairs.loans.Add(loan);ChangeTrust(borrower,lender,3);Chronicle("préstamo",lender,borrower,Names[lender]+" prestó oro a "+Names[borrower]+". El saldo y la deuda se registran por separado.",terms.principal);return CampaignAction.Ok("Préstamo recibido; vencimiento y acreedor registrados.");
        }
        private double ReduceDebt(InternationalLoan loan,double amount)
        {
            amount=Math.Min(Math.Max(0,amount),loan.Outstanding);double interest=Math.Min(amount,loan.interest);loan.interest-=interest;loan.principal=Math.Max(0,loan.principal-(amount-interest));
            if(loan.Outstanding<=.001){loan.principal=loan.interest=0;loan.status=DebtStatus.Repaid;if(loan.escrowGold>0){Gold[loan.debtor]+=loan.escrowGold;loan.escrowGold=0;}}
            return interest;
        }
        private double PayDebtInternal(InternationalLoan loan,double amount)
        {
            amount=Math.Min(Math.Max(0,amount),Math.Min(Gold[loan.debtor],loan.Outstanding));if(amount<=.001)return 0;
            Gold[loan.debtor]-=amount;Gold[loan.creditor]+=amount;double interest=ReduceDebt(loan,amount);loan.paid+=amount;loan.lastPaymentDay=CampaignDay;
            if(DebtTotal(loan.creditor)>0)Affairs.periodIncome[loan.creditor]+=interest;
            Memory(loan.creditor,loan.debtor).promisesKept++;ChangeTrust(loan.creditor,loan.debtor,1);Affairs.reputation[loan.debtor]=Mathf.Min(100,Affairs.reputation[loan.debtor]+.25f);
            Chronicle("pago",loan.debtor,loan.creditor,"Pago de deuda de "+Names[loan.debtor]+" a "+Names[loan.creditor]+".",amount);return amount;
        }
        public CampaignAction PayDebt(int debtor,int loanId,double amount)
        {
            var loan=Affairs.loans.Find(l=>l.id==loanId&&l.debtor==debtor);if(Paused||loan==null||!Outstanding(loan)||!MoneyValue(amount)||amount<=0)return CampaignAction.Fail("Selecciona una deuda y un importe válido.");
            double paid=PayDebtInternal(loan,amount);return paid>0?CampaignAction.Ok("Pago anticipado transferido al acreedor."):CampaignAction.Fail("No tienes oro disponible para pagar.");
        }
        public CampaignAction ForgiveDebt(int creditor,int loanId,double amount)
        {
            var loan=Affairs.loans.Find(l=>l.id==loanId&&l.creditor==creditor);if(Paused||loan==null||!Outstanding(loan)||!MoneyValue(amount)||amount<=0)return CampaignAction.Fail("Selecciona un préstamo concedido y un importe válido.");
            amount=Math.Min(amount,loan.Outstanding);ReduceDebt(loan,amount);loan.forgiven+=amount;if(loan.Outstanding<=.001)loan.status=DebtStatus.Forgiven;
            ChangeTrust(loan.debtor,creditor,10);Chronicle("condonación",creditor,loan.debtor,"Se condonó parte o toda la deuda de "+Names[loan.debtor]+".",amount);return CampaignAction.Ok("Deuda condonada sin crear ni transferir oro.");
        }
        public CampaignAction RenegotiateDebt(int debtor,int loanId,double extraDays,double interest)
        {
            var loan=Affairs.loans.Find(l=>l.id==loanId&&l.debtor==debtor);if(loan==null||!Outstanding(loan))return CampaignAction.Fail("La deuda ya no está pendiente.");
            return ProposeCampaign(debtor,loan.creditor,ProposalKind.RenegotiateDebt,new NegotiationTerms{loanId=loanId,loanDays=extraDays,interestPerPeriod=interest},"Propuesta de ampliación de plazo y revisión del interés.");
        }
        public CampaignAction RefuseDebt(int debtor,int loanId)
        {
            var loan=Affairs.loans.Find(l=>l.id==loanId&&l.debtor==debtor);if(Paused||loan==null||!Outstanding(loan)||loan.status==DebtStatus.Refused)return CampaignAction.Fail("La deuda no está activa o su pago ya fue rechazado.");
            loan.status=DebtStatus.Refused;Memory(loan.creditor,debtor).defaults++;ChangeTrust(loan.creditor,debtor,-35);Affairs.reputation[debtor]=Mathf.Max(0,Affairs.reputation[debtor]-15);
            SetEmbargoInternal(loan.creditor,debtor,true,-1);Chronicle("impago",debtor,loan.creditor,Names[debtor]+" se negó a pagar. Su acceso al crédito y su relación con el acreedor empeoran.");return CampaignAction.Ok("Impago registrado; se suspendió la cuota automática de ese préstamo.");
        }
        private void AdvanceInternationalFinance()
        {
            foreach(var loan in Affairs.loans)
            {
                if(!Outstanding(loan))continue;
                for(int period=0;period<8&&CampaignDay>=loan.nextInterestDay;period++){loan.interest=Math.Min(1e15,loan.interest+loan.Outstanding*loan.interestPerPeriod);loan.nextInterestDay+=15;}
                if(CampaignDay>=loan.maturityDay&&loan.status==DebtStatus.Active)
                {loan.status=DebtStatus.Defaulted;loan.missedPayments++;Memory(loan.creditor,loan.debtor).defaults++;ChangeTrust(loan.creditor,loan.debtor,-12);Affairs.reputation[loan.debtor]=Mathf.Max(0,Affairs.reputation[loan.debtor]-5);Chronicle("vencimiento",loan.debtor,loan.creditor,"Una deuda venció con saldo pendiente; puede renegociarse o pagarse.",loan.Outstanding);}
                if((loan.status==DebtStatus.Defaulted||loan.status==DebtStatus.Refused)&&CampaignDay>=loan.maturityDay+15&&loan.escrowGold>0)
                {double payment=Math.Min(loan.Outstanding,loan.escrowGold);loan.escrowGold-=payment;Gold[loan.creditor]+=payment;ReduceDebt(loan,payment);loan.paid+=payment;Chronicle("garantía",loan.debtor,loan.creditor,"Se aplicó el oro retenido como garantía al saldo de la deuda.",payment);}
                if((loan.status==DebtStatus.Defaulted||loan.status==DebtStatus.Refused)&&CampaignDay>=loan.maturityDay+15&&loan.collateral.Length>0)
                {
                    var valid=new List<int>();foreach(int cell in loan.collateral)if(Owners[cell]==loan.debtor)valid.Add(cell);
                    double value=loan.collateralValue*valid.Count/loan.collateral.Length;if(valid.Count>0){value=Math.Min(value,loan.Outstanding);ReduceDebt(loan,value);loan.collateralSettled+=value;TransferCampaignTerritory(loan.debtor,loan.creditor,valid.ToArray());Chronicle("garantía",loan.creditor,loan.debtor,"Se ejecutó la garantía territorial acordada.",value);}loan.collateral=Array.Empty<int>();
                }
            }
            for(int debtor=0;debtor<FactionCount;debtor++)
            {
                if(Affairs.nextDebtPayment[debtor]<0||CampaignDay<Affairs.nextDebtPayment[debtor])continue;
                var active=Affairs.loans.FindAll(l=>l.debtor==debtor&&l.Payable&&l.Outstanding>.001);double total=0;foreach(var loan in active)total+=loan.Outstanding;
                // One global 15% quota, never one quota per creditor and never historical income again.
                double budget=Math.Min(Gold[debtor],Math.Min(total,Affairs.periodIncome[debtor]*.15));double originalBudget=budget;
                for(int index=0;index<active.Count;index++){var loan=active[index];double share=index==active.Count-1?budget:Math.Min(budget,originalBudget*loan.Outstanding/Math.Max(.001,total));double paid=PayDebtInternal(loan,share);budget-=paid;}
                Affairs.periodIncome[debtor]=0;Affairs.nextDebtPayment[debtor]=total>.001?CampaignDay+15:-1;
            }
            foreach(var pact in Affairs.compacts)
            {
                if(!Active(pact)||pact.periodicGold<=0||CampaignDay<pact.nextPaymentDay)continue;
                int payer=pact.kind==CompactKind.Tribute?pact.target:pact.source,payee=pact.kind==CompactKind.Tribute?pact.source:pact.target;
                var result=TransferGold(payer,payee,pact.periodicGold,"Pago periódico de "+PoliticalName(pact.kind)+".");
                if(result.success)RegisterCampaignIncome(payee,pact.periodicGold,GoldFlow.Passive);
                if(!result.success){Memory(payee,payer).promisesBroken++;ChangeTrust(payee,payer,-5);Affairs.reputation[payer]=Mathf.Max(0,Affairs.reputation[payer]-2);Chronicle("crisis",payer,payee,"Fondos insuficientes para un compromiso periódico.",pact.periodicGold);}
                pact.nextPaymentDay=CampaignDay+Math.Max(1,pact.periodDays);
            }
        }
        private readonly HashSet<int> _settlingDebtors=new HashSet<int>();
        private void SettleEliminatedDebts(int faction)
        {
            if(Affairs==null||!_settlingDebtors.Add(faction))return;
            foreach(var loan in Affairs.loans)if(Outstanding(loan)&&(loan.debtor==faction||loan.creditor==faction))
            {
                if(loan.escrowGold>0){if(loan.debtor==faction){double held=loan.escrowGold;loan.escrowGold=0;double payment=Math.Min(held,loan.Outstanding);Gold[loan.creditor]+=payment;Gold[loan.debtor]+=held-payment;ReduceDebt(loan,payment);loan.paid+=payment;}else{Gold[loan.debtor]+=loan.escrowGold;loan.escrowGold=0;}}
                if(loan.debtor==faction)PayDebtInternal(loan,loan.Outstanding);
                if(Outstanding(loan)){loan.status=DebtStatus.WrittenOff;Chronicle("liquidación",loan.debtor,loan.creditor,"Una obligación se liquidó con saldo incobrable al desaparecer una nación.",loan.Outstanding);}
            }
            _settlingDebtors.Remove(faction);
        }
    }
}
