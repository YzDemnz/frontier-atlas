using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class TradeConnectionPreview{public int station;public List<int> path;public bool pending=true,requested;}
    public sealed partial class TerritorySimulation
    {
        public readonly List<TradeConnectionPreview> TradePreview=new List<TradeConnectionPreview>();public int TradePreviewCell {get;private set;}=-1;
        private int _previewOwner=-1,_previewVersion=-1,_previewBuildingCount=-1,_previewEpoch,_previewInFlight;private float _previewNext;
        public void PreviewFactoryConnections(int owner,int cell)
        {
            if(cell==TradePreviewCell&&owner==_previewOwner&&_previewVersion==_factoryPreviewRevision&&_previewBuildingCount==Buildings.Count)return;
            if(ElapsedTime<_previewNext||cell<0||cell>=Owners.Length||Owners[cell]!=owner)return;
            TradePreviewCell=cell;_previewOwner=owner;_previewVersion=_factoryPreviewRevision;_previewBuildingCount=Buildings.Count;_previewNext=ElapsedTime+.3f;TradePreview.Clear();
            _previewEpoch++;_previewInFlight=0;
            var candidates=new List<Building>();foreach(var b in FactoryTargetsNear(cell))if(b.cell!=cell&&b.Operational&&CanTrade(owner,b.owner)){float d=LandStationDistance(cell,b.cell);if(d<=FactoryConnectionRadius+.01f)candidates.Add(b);}
            candidates.Sort((a,b)=>{int city=(a.kind==BuildingKind.City?0:1).CompareTo(b.kind==BuildingKind.City?0:1);return city!=0?city:LandStationDistance(cell,a.cell).CompareTo(LandStationDistance(cell,b.cell));});
            foreach(var b in candidates)TradePreview.Add(new TradeConnectionPreview{station=b.id});AdvanceTradePreviews();
        }
        private void AdvanceTradePreviews()
        {
            int planned=0;foreach(var record in TradePreview)
            {
                if(record.requested)continue;if(planned>=2||_previewInFlight>=2||PendingLandPlans>=16)break;record.requested=true;var station=BuildingById(record.station);if(station==null){record.pending=false;continue;}
                int epoch=_previewEpoch,origin=TradePreviewCell;_previewInFlight++;planned++;var item=record;
                PlanLandRoute(_previewOwner,origin,station.cell,route=>{if(epoch!=_previewEpoch)return;_previewInFlight--;item.pending=false;if(route!=null){item.path=new List<int>{origin};item.path.AddRange(route);}});
            }
        }
    }
}
