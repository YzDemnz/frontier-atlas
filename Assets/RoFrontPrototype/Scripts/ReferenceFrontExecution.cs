using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private void AdvanceReferenceFront(MilitaryOperation op,float dt)
        {
            // Budget accumulates on the actual frontier; it is not a circle or a cursor ray.
            float border=op.kind==OperationKind.Expansion?_freeBoundary[op.faction].Count:_frontiers[op.faction].Count;
            op.clock+=Mathf.Clamp(border,8,160)*Rules.frontBudgetMultiplier*dt/.1f;
            op.clock=Mathf.Min(op.clock,1000);
            for(int n=0;n<Rules.maximumCapturesPerOperationTick&&op.active;n++)
            {
                int cell=op.kind==OperationKind.Expansion?SelectFreeBoundary(op.faction):EnemyContact(op.faction,op.enemy,op.target);
                if(cell<0){EndCampaignOperation(op,ArmyState.Arrived);return;}
                float time=ReferenceTime(op,cell);if(op.clock<time)return;op.clock-=time;op.frontCell=cell;
                if(op.kind==OperationKind.Expansion)op.target=cell;
                if(!ResolveCampaignCell(op,cell,.1f,false))return;
                if(op.kind==OperationKind.Expansion)RecordDirectedCapture(op.faction,cell);
            }
        }
    }
}
