using System;
using System.Diagnostics;
namespace FrontierAtlas
{
    [Serializable] public sealed class SimulationMeter
    {
        public string name;public long calls;public double totalMilliseconds,maxMilliseconds;
        public SimulationMeter(string label){name=label;}
    }
    public sealed partial class TerritorySimulation
    {
        public bool MeasurePerformance;
        public readonly SimulationMeter[] Meters={new SimulationMeter("economy"),new SimulationMeter("diplomacy"),new SimulationMeter("weapons"),new SimulationMeter("warships"),new SimulationMeter("operations"),new SimulationMeter("military AI"),new SimulationMeter("development AI"),new SimulationMeter("route planning"),new SimulationMeter("free frontier"),new SimulationMeter("land pathfinding")};
        private long MeterStart()=>MeasurePerformance?Stopwatch.GetTimestamp():0;
        private void MeterEnd(int index,long start)
        {
            if(start==0)return;double ms=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;var meter=Meters[index];meter.calls++;meter.totalMilliseconds+=ms;meter.maxMilliseconds=Math.Max(meter.maxMilliseconds,ms);
        }
    }
}
