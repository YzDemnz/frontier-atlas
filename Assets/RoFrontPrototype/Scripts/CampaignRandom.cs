using System;
namespace FrontierAtlas
{
    public sealed class CampaignRandom:Random
    {
        public ulong State;
        public CampaignRandom(int seed):base(seed){State=(ulong)(uint)seed+0x9E3779B97F4A7C15UL;}
        private ulong Value(){ulong x=State;x^=x>>12;x^=x<<25;x^=x>>27;State=x;return x*2685821657736338717UL;}
        protected override double Sample()=>(Value()>>11)*(1.0/9007199254740992.0);
        public override double NextDouble()=>Sample();
        public override int Next()=>Next(int.MaxValue);
        public override int Next(int max){if(max<0)throw new ArgumentOutOfRangeException(nameof(max));return max==0?0:(int)(Value()%(uint)max);}
        public override int Next(int min,int max){if(max<min)throw new ArgumentOutOfRangeException(nameof(max));long range=(long)max-min;return range==0?min:(int)(min+(long)(Value()%(ulong)range));}
        public override void NextBytes(byte[] data){if(data==null)throw new ArgumentNullException(nameof(data));for(int n=0;n<data.Length;n++)data[n]=(byte)Value();}
    }
}
