using System;
namespace FrontierAtlas
{
    public enum Difficulty { Relaxed, Standard, Brutal }
    [Serializable] public sealed class MatchOptions
    {
        public string playerName="Jugador";
        public bool customPlayer=true,advancedSystems;
        public MatchMode mode=MatchMode.Campaign;
        public Difficulty difficulty=Difficulty.Standard;
        public bool teams;
        public int tribeCount;
    }
}
