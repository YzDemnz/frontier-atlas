using UnityEngine;
using System.Collections.Generic;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private bool ThinkStrategicDevelopment(int f)
        {
            int cities=0,factories=0,ports=0,sam=0,silos=0;Building factory=null;bool nuclearThreat=Projectiles.Exists(p=>p.active&&p.owner!=f&&p.targetNation==f&&Rules.Weapon(p.kind).radiation);var stations=new List<Building>();
            foreach(var b in Buildings)if(b.owner==f&&!b.destroyed){if(b.kind==BuildingKind.City)cities++;if(b.kind==BuildingKind.Factory){factories++;if(b.Operational)factory=b;}if(b.kind==BuildingKind.Port)ports++;if(b.kind==BuildingKind.SAM)sam++;if(b.kind==BuildingKind.Silo)silos++;if(b.Operational&&EconomicBuilding(b)&&stations.Count<12)stations.Add(b);}
            var plan=StrategyFor(f);
            BuildingKind kind=cities==0?BuildingKind.City:factories==0?BuildingKind.Factory:cities<factories*3?BuildingKind.City:BuildingKind.Factory;
            if(nuclearThreat&&sam==0&&Gold[f]>=Rules.Building(BuildingKind.SAM).cost)kind=BuildingKind.SAM;
            else if(plan.phase==StrategicPhase.Offensive&&silos==0&&Gold[f]>3000000)kind=BuildingKind.Silo;
            else if(ports==0&&factories>0&&cities>1&&HasCoastalAccess(f))kind=BuildingKind.Port;
            int chosen=-1,examined=0;float best=float.NegativeInfinity;int core=CapitalCells[f];var candidates=new HashSet<int>();
            foreach(int cell in _owned[f]){candidates.Add(cell);if(++examined>=64)break;}examined=0;
            foreach(int cell in _frontiers[f]){candidates.Add(cell);if(++examined>=96)break;}
            foreach(var station in stations)for(int ring=0;ring<3;ring++)for(int direction=0;direction<8;direction++)
            {float angle=direction*Mathf.PI/4;int x=station.cell%World.Width+Mathf.RoundToInt(Mathf.Cos(angle)*(18+ring*14)*World.ResolutionScale),y=station.cell/World.Width+Mathf.RoundToInt(Mathf.Sin(angle)*(18+ring*14)*World.ResolutionScale);if(World.WrapLongitude)x=(x%World.Width+World.Width)%World.Width;if(World.InBounds(x,y))candidates.Add(World.At(x,y));}
            foreach(int cell in candidates)
            {
                if(Owners[cell]!=f||BuildingAt(cell)!=null||IsIrradiated(cell)||IsImpassable(cell))continue;if(kind==BuildingKind.Port&&AdjacentWater(cell)<0)continue;
                float score=-Distance(cell,core)*.02f;bool connected=stations.Count==0;
                if(kind==BuildingKind.City||kind==BuildingKind.Factory)
                {
                    foreach(var station in stations)
                    {float d=Distance(cell,station.cell);if(d>=FactoryConnectionMinimum&&d<=FactoryConnectionRadius*.85f){score+=(station.kind!=kind?12:2);connected=true;}if(d<FactoryConnectionMinimum)score-=20;}
                }
                if(!connected&&(kind==BuildingKind.Factory||kind==BuildingKind.City))continue;
                if(score>best){best=score;chosen=cell;}
            }
            if(chosen>=0)Construct(f,chosen,kind);
            // Existing productive infrastructure can be upgraded without waiting for random build choices.
            if(factory!=null&&Gold[f]>ConstructionPrice(f,BuildingKind.Factory)*2&&factory.level<3)Construct(f,factory.cell,BuildingKind.Factory);
            if(plan.phase==StrategicPhase.Offensive&&plan.enemy>=0&&plan.goal>=0&&Owners[plan.goal]==plan.enemy&&IsDeclaredWar(f,plan.enemy)&&Gold[f]>2500000)
            {var silo=Buildings.Find(b=>b.owner==f&&b.kind==BuildingKind.Silo&&b.Operational&&b.ammunition>0);if(silo!=null&&!NuclearFriendlyRisk(f,plan.goal,Rules.Weapon(WeaponKind.Atomic).radius))LaunchWeapon(f,plan.goal,WeaponKind.Atomic);}
            return true;
        }
    }
}
