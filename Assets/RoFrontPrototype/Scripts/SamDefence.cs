using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class SamSlot{public float readyAt;public int interceptor;}
    public sealed class SamSite{public int building;public List<SamSlot> slots=new List<SamSlot>();}
    public sealed class SamInterceptor{public int id,site,slot,target,owner;public Vector3 position;public bool active=true;}
    public sealed partial class TerritorySimulation
    {
        private readonly Dictionary<int,SamSite> _samSites=new Dictionary<int,SamSite>();
        public readonly List<SamInterceptor> Interceptors=new List<SamInterceptor>();private int _interceptorId;
        private readonly Dictionary<int,StrategicProjectile> _projectilesById=new Dictionary<int,StrategicProjectile>();
        private readonly Dictionary<int,List<StrategicProjectile>> _threatBins=new Dictionary<int,List<StrategicProjectile>>();
        private readonly HashSet<int> _assignedThreats=new HashSet<int>();private int _threatBinColumns;
        public float SamRadius(int level)
        {level=Mathf.Clamp(level,1,5);float radius=Rules.samBalanceProfile==SamBalanceProfile.OpenFront34Reference?150-480f/(level+5):Rules.samRadiusByLevel[level-1];return radius*World.ResolutionScale;}
        public float SamCooldown=>Rules.samBalanceProfile==SamBalanceProfile.OpenFront34Reference?Rules.samReferenceCooldown:Rules.samCooldown;
        private SamSite SamState(Building building)
        {
            if(!_samSites.TryGetValue(building.id,out var site))_samSites[building.id]=site=new SamSite{building=building.id};
            int old=site.slots.Count;while(site.slots.Count<building.level)site.slots.Add(new SamSlot{readyAt=old==0?Mathf.Max(ElapsedTime,building.nextAction):ElapsedTime+SamCooldown});return site;
        }
        public int SamReadySlots(Building building)
        {if(building==null||!building.Operational||building.kind!=BuildingKind.SAM)return 0;int ready=0;foreach(var slot in SamState(building).slots)if(slot.interceptor==0&&slot.readyAt<=ElapsedTime)ready++;return ready;}
        public float SamReloadRemaining(Building building)
        {if(building==null)return 0;float remaining=float.PositiveInfinity;foreach(var slot in SamState(building).slots)if(slot.interceptor==0)remaining=Mathf.Min(remaining,Mathf.Max(0,slot.readyAt-ElapsedTime));return float.IsPositiveInfinity(remaining)?SamCooldown:remaining;}
        public Vector3 ProjectilePosition(StrategicProjectile projectile,float progress=-1)
        {
            float t=progress<0?projectile.Progress:Mathf.Clamp01(progress),x=projectile.origin%World.Width+.5f,z=projectile.origin/World.Width+.5f,dx=projectile.target%World.Width+.5f-x,dz=projectile.target/World.Width+.5f-z;
            if(World.WrapLongitude&&Mathf.Abs(dx)>World.Width*.5f)dx+=dx>0?-World.Width:World.Width;float distance=Mathf.Sqrt(dx*dx+dz*dz),arc=4*t*(1-t);bool ballistic=projectile.kind!=WeaponKind.Railgun&&projectile.kind!=WeaponKind.AirStrike;
            float curve=ballistic?Rules.missileProjectedCurve*arc:0;float px=x+dx*t-dz*curve,pz=z+dz*t+dx*curve;
            if(World.WrapLongitude)px=Mathf.Repeat(px,World.Width);pz=Mathf.Clamp(pz,0,World.Height-.001f);
            float y=Mathf.Lerp(World.SurfaceHeight(x,z),World.SurfaceHeight(projectile.target%World.Width+.5f,projectile.target/World.Width+.5f),t)+2*World.ResolutionScale;
            if(ballistic)y+=Mathf.Clamp(distance*Rules.missileArcHeight,18*World.ResolutionScale,140*World.ResolutionScale)*arc;
            return new Vector3(px,y,pz);
        }
        public double SamThreatPriority(Building sam,StrategicProjectile projectile)
        {
            float dx=Mathf.Abs(sam.cell%World.Width-projectile.target%World.Width);if(World.WrapLongitude)dx=Mathf.Min(dx,World.Width-dx);
            float d=(dx+Mathf.Abs(sam.cell/World.Width-projectile.target/World.Width))/World.ResolutionScale,ticks=Mathf.Max(0,projectile.duration-projectile.age)*10;
            return (projectile.kind==WeaponKind.Hydrogen?Rules.samHydrogenPriority:0)+Mathf.Max(0,200000-1000*d)+Mathf.Max(0,10000-100*ticks);
        }
        public bool SamCovers(Building sam,StrategicProjectile projectile)
        {
            if(sam==null||!sam.Operational||sam.kind!=BuildingKind.SAM||!projectile.active||!CompatibleSAM(projectile.kind)||sam.owner==projectile.owner||AreAllied(sam.owner,projectile.owner))return false;
            Vector3 p=ProjectilePosition(projectile);float dx=Mathf.Abs(sam.cell%World.Width+.5f-p.x),dz=sam.cell/World.Width+.5f-p.z;if(World.WrapLongitude)dx=Mathf.Min(dx,World.Width-dx);
            return dx*dx+dz*dz<=SamRadius(sam.level)*SamRadius(sam.level);
        }
        private Vector3 NearbyThreat(Vector3 origin,Vector3 target)
        {if(World.WrapLongitude&&Mathf.Abs(target.x-origin.x)>World.Width*.5f)target.x+=target.x>origin.x?-World.Width:World.Width;return target;}
        private void FinishInterceptor(SamInterceptor interceptor)
        {
            interceptor.active=false;if(_samSites.TryGetValue(interceptor.site,out var site)&&interceptor.slot<site.slots.Count)
            {var slot=site.slots[interceptor.slot];if(slot.interceptor==interceptor.id){slot.interceptor=0;slot.readyAt=ElapsedTime+SamCooldown;}}
        }
        private void AdvanceSamDefence(float dt)
        {
            _projectilesById.Clear();foreach(var p in Projectiles)if(p.active)_projectilesById[p.id]=p;
            foreach(var interceptor in Interceptors)
            {
                if(!interceptor.active)continue;var sam=BuildingById(interceptor.site);
                if(sam==null||sam.destroyed||sam.owner!=interceptor.owner||!_projectilesById.TryGetValue(interceptor.target,out var p)||!SamCovers(sam,p)){FinishInterceptor(interceptor);continue;}
                Vector3 target=NearbyThreat(interceptor.position,ProjectilePosition(p)),delta=target-interceptor.position;float step=Rules.samInterceptorSpeed*World.ResolutionScale*dt;
                interceptor.position=Vector3.MoveTowards(interceptor.position,target,step);if(World.WrapLongitude)interceptor.position.x=Mathf.Repeat(interceptor.position.x,World.Width);
                if(delta.magnitude<=step+World.ResolutionScale*.6f)
                {p.active=false;FinishInterceptor(interceptor);Emit(StrategicEventKind.Interception,sam.owner,p.owner,ProjectileCell(p),"Interceptor SAM: amenaza neutralizada.");}
            }
            Interceptors.RemoveAll(i=>!i.active);_assignedThreats.Clear();foreach(var i in Interceptors)_assignedThreats.Add(i.target);
            // Spatial broad phase runs at simulation ticks, never once per rendered frame per SAM/target pair.
            foreach(var bin in _threatBins.Values)bin.Clear();float binSize=128*World.ResolutionScale;_threatBinColumns=Mathf.CeilToInt(World.Width/binSize);
            foreach(var p in Projectiles)if(p.active&&CompatibleSAM(p.kind))
            {var position=ProjectilePosition(p);int key=Mathf.FloorToInt(position.z/binSize)*_threatBinColumns+Mathf.FloorToInt(position.x/binSize);if(!_threatBins.TryGetValue(key,out var bin))_threatBins[key]=bin=new List<StrategicProjectile>();bin.Add(p);}
            foreach(var sam in Buildings)
            {
                if(sam.kind!=BuildingKind.SAM||!sam.Operational)continue;var state=SamState(sam);float range=SamRadius(sam.level);int sx=sam.cell%World.Width,sz=sam.cell/World.Width,bx=Mathf.FloorToInt(sx/binSize),bz=Mathf.FloorToInt(sz/binSize),r=Mathf.CeilToInt(range/binSize);
                for(int slotIndex=0;slotIndex<state.slots.Count;slotIndex++)
                {
                    var slot=state.slots[slotIndex];if(slot.interceptor!=0||slot.readyAt>ElapsedTime)continue;StrategicProjectile best=null;double score=double.NegativeInfinity;
                    for(int y=bz-r;y<=bz+r;y++)for(int x=bx-r;x<=bx+r;x++)
                    {
                        int wrapped=World.WrapLongitude?(x%_threatBinColumns+_threatBinColumns)%_threatBinColumns:x;if(wrapped<0||wrapped>=_threatBinColumns||!_threatBins.TryGetValue(y*_threatBinColumns+wrapped,out var bin))continue;
                        foreach(var p in bin)
                        {
                            if(_assignedThreats.Contains(p.id)||!SamCovers(sam,p))continue;Vector3 origin=new Vector3(sx+.5f,World.RenderedSurfaceHeight(sx+.5f,sz+.5f)+2*World.ResolutionScale,sz+.5f),target=NearbyThreat(origin,ProjectilePosition(p));
                            float travel=Vector3.Distance(origin,target)/(Rules.samInterceptorSpeed*World.ResolutionScale);if(travel+dt>=p.duration-p.age)continue;double priority=SamThreatPriority(sam,p);if(priority>score){score=priority;best=p;}
                        }
                    }
                    if(best==null)continue;var interceptor=new SamInterceptor{id=++_interceptorId,site=sam.id,slot=slotIndex,target=best.id,owner=sam.owner,position=new Vector3(sx+.5f,World.RenderedSurfaceHeight(sx+.5f,sz+.5f)+2*World.ResolutionScale,sz+.5f)};
                    Interceptors.Add(interceptor);slot.interceptor=interceptor.id;_assignedThreats.Add(best.id);
                }
            }
        }
    }
}
