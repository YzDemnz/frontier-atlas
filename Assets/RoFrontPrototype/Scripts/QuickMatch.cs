using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public enum MatchMode { Campaign, QuickMatch }
    public sealed partial class TerritorySimulation
    {
        public bool IsQuickMatch=>Options!=null&&Options.mode==MatchMode.QuickMatch;
        public const int QuickNationSlots=88,QuickTribes=160;
        private static readonly string[] TribeNames={"Escudo","Manada","Jinetes","Guardia","Casa","Ojo","Colmillo","Alce","Centinelas","Martillo","Cuervo","Corona","Legión","Senda","Garra","Tormenta"};
        private static readonly string[] TribeLands={"sílex","arena","marea","jade","roble","pino","bruma","coral","cobre","luna"};
        private string QuickTribeName(int faction){int index=faction-World.Countries.Length-(Options.customPlayer?1:0);return TribeNames[index%TribeNames.Length]+" de "+TribeLands[index/TribeNames.Length%TribeLands.Length];}
        private void ApplyQuickMatchRules()
        {
            if(!IsQuickMatch)return;
            Rules.initialTroops=2500;Rules.tribeTroops=1000;Rules.cityCapacity=25000;Rules.tacticalInitialGold=0;
            Rules.tacticalPassiveIncome=1000;Rules.protectionSeconds=60;Rules.brutalProtectionSeconds=120;
            Rules.victoryFraction=.8f;Rules.betrayalSeconds=30;Rules.betrayalVulnerability=2;
            Rules.Building(BuildingKind.Port).cost=125000;Rules.Building(BuildingKind.Silo).maxCost=1000000;
            Rules.maximumOperations=6;Rules.allianceSeconds=240;Rules.Weapon(WeaponKind.Atomic).cost=750000;
        }
        private void SelectQuickNations()
        {
            if(!IsQuickMatch)return;int limit=QuickNationSlots-(Options.customPlayer?1:0);
            var available=new List<int>();for(int f=0;f<World.Countries.Length;f++)if(CapitalCells[f]>=0)available.Add(f);
            if(available.Count<=limit)return;var chosen=new HashSet<int>();chosen.Add(available[0]);
            // Farthest-point sampling spreads real nations over the planet without fictional geography.
            while(chosen.Count<limit)
            {
                int best=-1;float farthest=-1;
                foreach(int f in available)
                {
                    if(chosen.Contains(f))continue;float nearest=float.PositiveInfinity;int cell=CapitalCells[f];
                    foreach(int other in chosen){float dx=Mathf.Abs(cell%World.Width-CapitalCells[other]%World.Width);if(World.WrapLongitude)dx=Mathf.Min(dx,World.Width-dx);float dy=cell/World.Width-CapitalCells[other]/World.Width;nearest=Mathf.Min(nearest,dx*dx+dy*dy);}
                    if(nearest>farthest){best=f;farthest=nearest;}
                }
                if(best<0)break;chosen.Add(best);
            }
            for(int f=0;f<World.Countries.Length;f++)if(!chosen.Contains(f)&&f!=PlayerFaction)CapitalCells[f]=-1;
        }
        private float QuickCapacity(int f)
        {
            float cap=2*(Mathf.Pow(Mathf.Max(0,NormalizedArea(f)),.6f)*100+5000)+_cityLevels[f]*Rules.cityCapacity;
            if(IsTribe(f))return cap/3;if(f==PlayerFaction)return cap;
            return cap*(Options.difficulty==Difficulty.Relaxed?.5f:Options.difficulty==Difficulty.Brutal?1.25f:.75f);
        }
        private float QuickGrowth(int f)
        {
            float rate=CampaignBalance.GrowthPerTick(AvailableTroops(f),Capacity[f])*4;
            if(IsTribe(f))return rate*.5f;return rate*(f==PlayerFaction?1:Options.difficulty==Difficulty.Relaxed?.9f:Options.difficulty==Difficulty.Brutal?1.05f:.95f);
        }
        private void ThinkQuickDiplomacy(int f)
        {
            if(IsTribe(f)||_random.NextDouble()>.35)return;int ally=-1;float best=0;
            foreach(int other in _neighborNations[f])
            {if(other==f||IsTribe(other)||TerritoryCounts[other]==0||AreAllied(f,other))continue;float value=TotalTroops(other)/Mathf.Max(1,TotalTroops(f));if(value>best&&value>.75f){best=value;ally=other;}}
            if(ally>=0)ProposeAlliance(f,ally);
        }
    }
}
