using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private float[] _aiHeadingUntil;
        private int PickExpansionHeading(int f)
        {
            if(_aiHeadingUntil==null)_aiHeadingUntil=new float[FactionCount];
            Vector2 origin=CapitalCells[f]>=0?new Vector2(CapitalCells[f]%World.Width,CapitalCells[f]/World.Width):Vector2.zero;
            if(_directedFronts[f]!=null&&ElapsedTime<_aiHeadingUntil[f])return _directedFronts[f].target;
            Vector2 preferred=Vector2.zero;float nearest=float.PositiveInfinity;
            // Nearby stronger neighbours, including an exposed player, influence the heading.
            foreach(int other in _neighborNations[f])
            {
                if(other==f||AvailableTroops(other)<AvailableTroops(f)*.9f||CapitalCells[other]<0)continue;
                Vector2 delta=new Vector2(WrappedDeltaX(origin.x-CapitalCells[other]%World.Width),origin.y-CapitalCells[other]/World.Width);
                if(delta.sqrMagnitude<nearest){nearest=delta.sqrMagnitude;preferred=delta.normalized;}
            }
            int playerHome=CapitalCells[PlayerFaction];
            if(f!=PlayerFaction&&playerHome>=0)
            {
                Vector2 away=new Vector2(WrappedDeltaX(origin.x-playerHome%World.Width),origin.y-playerHome/World.Width);
                if(away.sqrMagnitude<250*250*World.CellAreaFactor&&away.sqrMagnitude<nearest)preferred=away.normalized;
            }
            if(preferred.sqrMagnitude<.01f){float angle=(float)_random.NextDouble()*Mathf.PI*2;preferred=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
            int best=-1,examined=0;float bestScore=float.NegativeInfinity;
            foreach(var entry in _freeBoundary[f])
            {
                int cell=entry.Key;if(Owners[cell]!=Neutral||IsImpassable(cell))continue;
                Vector2 delta=new Vector2(WrappedDeltaX(cell%World.Width-origin.x),cell/World.Width-origin.y);
                float score=Vector2.Dot(delta.normalized,preferred)*4;
                for(int step=1;step<=6;step++){int x=cell%World.Width+Mathf.RoundToInt(preferred.x*step*2),y=cell/World.Width+Mathf.RoundToInt(preferred.y*step*2);if(y<0||y>=World.Height||!World.WrapLongitude&&(x<0||x>=World.Width))break;if(World.WrapLongitude)x=(x%World.Width+World.Width)%World.Width;int probe=World.At(x,y);score+=Owners[probe]==Neutral&&!IsImpassable(probe)?.4f:-.7f;}
                if(score>bestScore){bestScore=score;best=cell;}if(++examined>=128)break;
            }
            _aiHeadingUntil[f]=ElapsedTime+35+(float)_random.NextDouble()*25;return best;
        }
        private void ThinkTactical(int f)
        {
            // Preserve the central reserve when an enemy offensive threatens the capital or core industry.
            bool coreThreat=false;foreach(var attack in Operations)if(attack.active&&attack.faction!=f&&attack.enemy==f&&attack.frontCell>=0)
            {if(Distance(attack.frontCell,CapitalCells[f])<25){coreThreat=true;break;}foreach(var b in Buildings)if(b.owner==f&&b.Operational&&(b.kind==BuildingKind.City||b.kind==BuildingKind.Factory)&&Distance(attack.frontCell,b.cell)<12){coreThreat=true;break;}if(coreThreat)break;}
            if(coreThreat){foreach(var operation in Operations)if(operation.active&&operation.faction==f&&operation.kind==OperationKind.Expansion)EndCampaignOperation(operation,ArmyState.Idle);SyncOperations();return;}
            if(AvailableTroops(f)<MinimumExpansionTroops*2||OperationCount(f)>=Rules.maximumOperations)return;
            int enemy=-1,contact=-1;float advantage=0;
            foreach(int other in _neighborNations[f])
            {
                if(!CanAttackNation(f,other))continue;int point=ContactCell(f,other);if(point<0)continue;
                float score=AvailableTroops(f)*.65f-AvailableTroops(other)*1.1f;
                if(score>advantage){enemy=other;contact=point;advantage=score;}
            }
            if(enemy>=0&&AvailableTroops(f)>150){StartCampaignAttack(f,contact,.65f);return;}
            if(AvailableTroops(f)<(IsTribe(f)?150:Rules.tacticalAiExpansionReserve))return;
            EnsureFreeBoundary(f);if(SelectFreeBoundary(f)>=0)
            {
                int target=PickExpansionHeading(f);if(target<0)return;
                if(_directedFronts[f]==null||_directedFronts[f].target!=target)BeginDirectionalExpansion(f,target,IsTribe(f)?.25f:.45f);
                else StartCampaignExpansion(f,IsTribe(f)?.25f:.35f);
            }
            else if(HasCoastalAccess(f)&&AvailableTroops(f)>750&&_random.NextDouble()<.2)
            {
                int target=-1;float best=float.PositiveInfinity;
                foreach(int coast in World.CoastCells){if(Owners[coast]==f||!CanAttackNation(f,Owners[coast]))continue;float distance=Distance(CapitalCells[f],coast);if(distance<best){best=distance;target=coast;}}
                if(target>=0)StartCampaignInvasion(f,target,.45f);
            }
        }
    }
}
