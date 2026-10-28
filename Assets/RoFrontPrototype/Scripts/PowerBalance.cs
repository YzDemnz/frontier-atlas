using System;
using UnityEngine;

namespace FrontierAtlas
{
    // Presentation only: power is territory-based and never changes simulation authority.
    public sealed class PowerBalance
    {
        public readonly int[] Ranking,Tiers;
        public readonly Color[] DisplayColors;
        public event Action<int> Promoted;
        private readonly TerritorySimulation _game;
        private float _nextUpdate;
        public PowerBalance(TerritorySimulation game)
        {
            _game=game;Ranking=new int[game.FactionCount];Tiers=new int[game.FactionCount];DisplayColors=new Color[game.FactionCount];
            for(int i=0;i<Ranking.Length;i++)Ranking[i]=i;
            Update(true);
        }
        public void Update(bool force=false)
        {
            if(!force&&_game.ElapsedTime<_nextUpdate)return;
            _nextUpdate=_game.ElapsedTime+1;
            Array.Sort(Ranking,(a,b)=>{int c=_game.TerritoryCounts[b].CompareTo(_game.TerritoryCounts[a]);return c!=0?c:a.CompareTo(b);});
            int owned=0,alive=0;
            foreach(int count in _game.TerritoryCounts){owned+=count;if(count>0)alive++;}
            float mean=(float)owned/Mathf.Max(1,alive);
            for(int rank=0;rank<Ranking.Length;rank++)
            {
                int f=Ranking[rank],previous=Tiers[f];float count=_game.TerritoryCounts[f]/_game.World.CellAreaFactor;
                int tier=rank<5&&count>=(previous>0?24:28)?1:0;
                if(rank<3&&count>=75)tier=2;
                if(rank==0&&count>=150)tier=3;
                Tiers[f]=tier;
                float hue=(f*.61803399f+.06f)%1;
                if(hue>.40f&&hue<.61f)hue=(hue+.28f)%1;
                Color nation=Color.HSVToRGB(hue,.67f,.91f);
                DisplayColors[f]=f==_game.PlayerFaction?(_game.IsQuickMatch?new Color(.24f,.68f,.40f):new Color(.18f,.86f,.83f)):_game.IsQuickMatch?(_game.IsTribe(f)?new Color(.78f,.77f,.71f):Color.Lerp(nation,Color.white,.38f)):tier>0?nation:new Color(.43f,.49f,.53f);
                if(tier>previous&&f!=_game.PlayerFaction)Promoted?.Invoke(f);
            }
        }
        public string Title(int f)=>Tiers[f]>=3?"GRAN POTENCIA":Tiers[f]>=2?"POTENCIA REGIONAL":Tiers[f]==1?"ESTADO EMERGENTE":"ESTADO LOCAL";
    }
}
