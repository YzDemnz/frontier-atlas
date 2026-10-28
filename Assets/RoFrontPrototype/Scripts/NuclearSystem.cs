using System;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas
{
    public sealed class StrategicProjectile
    {
        public int id,owner,origin,target,targetNation=-1;public WeaponKind kind;public float age,duration;public bool active=true;
        public float Progress=>Mathf.Clamp01(age/Mathf.Max(.01f,duration));
    }
    public sealed partial class TerritorySimulation
    {
        public readonly List<StrategicProjectile> Projectiles=new List<StrategicProjectile>();
        public readonly Dictionary<int,float> Radiation=new Dictionary<int,float>();
        // Normalized distance from the latest nuclear epicentre; saved independently of rendering.
        public readonly Dictionary<int,float> NuclearScars=new Dictionary<int,float>();
        private readonly Dictionary<int,int> _arsenalCharges=new Dictionary<int,int>();
        public event Action<int,float> NuclearDetonated;
        private int _projectileId;private float _radiationClock;
        public bool IsIrradiated(int cell)=>CampaignEnabled&&Radiation.ContainsKey(cell);
        public int ArsenalCharges(int f,ArsenalKind kind)=>_arsenalCharges.TryGetValue(f*16+(int)kind,out int count)?count:0;
        public static string ArsenalName(ArsenalKind kind)
        {
            switch(kind){case ArsenalKind.Mega:return "Megabomba";case ArsenalKind.Scatter:return "Ataque disperso";case ArsenalKind.Reinforcements:return "Refuerzos";case ArsenalKind.Artillery:return "Artillería";case ArsenalKind.AirStrike:return "Ataque aéreo";default:return "Cañón electromagnético";}
        }
        public CampaignAction BuyArsenal(int f,ArsenalKind kind)
        {
            if(!CampaignEnabled||Paused)return CampaignAction.Fail("Reanuda la partida para adquirir una habilidad.");
            int cost=Rules.arsenalCosts[(int)kind];if(Stars[f]<cost)return CampaignAction.Fail("No tienes suficientes estrellas.");
            Stars[f]-=cost;int key=f*16+(int)kind;_arsenalCharges[key]=ArsenalCharges(f,kind)+1;return CampaignAction.Ok("Habilidad adquirida con estrellas.");
        }
        public CampaignAction UseArsenal(int f,ArsenalKind kind,int target)
        {
            if(Paused||!CampaignEnabled)return CampaignAction.Fail("Reanuda la partida para usar el arsenal.");
            if(ArsenalCharges(f,kind)<1)return CampaignAction.Fail("Primero adquiere un uso de esta habilidad.");
            CampaignAction result;
            if(kind==ArsenalKind.Reinforcements)
            {
                if(StoredTroops(f)>=Capacity[f])return CampaignAction.Fail("Tu capacidad de tropas está completa.");
                Troops[f]+=Mathf.Min(Capacity[f]-StoredTroops(f),TacticalBalance?20000:5000);result=CampaignAction.Ok("Refuerzos incorporados sin superar tu capacidad.");
            }
            else
            {
                WeaponKind weapon=kind==ArsenalKind.Mega?WeaponKind.Mega:kind==ArsenalKind.Scatter?WeaponKind.Scatter:kind==ArsenalKind.Artillery?WeaponKind.Artillery:kind==ArsenalKind.AirStrike?WeaponKind.AirStrike:WeaponKind.Railgun;
                result=LaunchWeapon(f,target,weapon,true);
            }
            if(result.success)_arsenalCharges[f*16+(int)kind]--;return result;
        }
        public CampaignAction ValidateWeapon(int f,int target,WeaponKind kind,bool arsenal=false)
        {
            if(!CampaignEnabled||Paused||Outcome!=CampaignOutcome.Playing)return CampaignAction.Fail("Reanuda la partida para lanzar un ataque.");
            if(target<0||target>=Owners.Length||!World.Land[target]||(Owners[target]==f&&!Rules.Weapon(kind).radiation))return CampaignAction.Fail("Elige un objetivo terrestre válido.");
            int other=Owners[target];if(other>=0&&other!=f&&!CanAttackNation(f,other))return CampaignAction.Fail(AreAllied(f,other)?"No puedes atacar a un aliado.":"El objetivo tiene protección inicial.");
            if(kind==WeaponKind.MIRV&&(other<0||TerritoryCounts[other]==0))return CampaignAction.Fail("El misil de ojivas múltiples necesita una nación objetivo.");
            var definition=Rules.Weapon(kind);int origin=CapitalCells[f];
            if(!arsenal)
            {
                var silo=Buildings.Find(b=>b.owner==f&&b.kind==BuildingKind.Silo&&b.Operational&&b.ammunition>0&&Distance(b.cell,target)<=definition.range);
                if(silo==null)return CampaignAction.Fail("Necesitas un silo operativo, con munición y dentro del alcance.");origin=silo.cell;
                if(Gold[f]<definition.cost)return CampaignAction.Fail("No hay suficiente oro para ese lanzamiento.");
            }
            if(origin<0||Distance(origin,target)>definition.range)return CampaignAction.Fail("El objetivo está fuera del alcance.");
            return CampaignAction.Ok("Objetivo válido.");
        }
        public CampaignAction LaunchWeapon(int f,int target,WeaponKind kind,bool arsenal=false)
        {
            var validation=ValidateWeapon(f,target,kind,arsenal);if(!validation.success)return validation;
            var definition=Rules.Weapon(kind);int origin=CapitalCells[f];
            if(!arsenal)
            {
                var silo=Buildings.Find(b=>b.owner==f&&b.kind==BuildingKind.Silo&&b.Operational&&b.ammunition>0&&Distance(b.cell,target)<=definition.range);
                if(Owners[target]!=f)RecordHostility(f,Owners[target]);origin=silo.cell;silo.ammunition--;silo.nextReload=ElapsedTime+Rules.siloCooldown;Gold[f]-=definition.cost;Ledgers[f].militarySpent+=definition.cost;
            }
            if(Owners[target]!=f)RegisterAggression(f,Owners[target],WarObjective.Infrastructure,target);if(Rules.attackEndsProtection)ProtectionUntil[f]=0;
            CreateProjectile(f,origin,target,kind,true);return CampaignAction.Ok(definition.name+" lanzada.");
        }
        private void CreateProjectile(int owner,int origin,int target,WeaponKind kind,bool notify)
        {
            var projectile=new StrategicProjectile{id=++_projectileId,owner=owner,origin=origin,target=target,targetNation=Owners[target],kind=kind,
                duration=Mathf.Max(kind==WeaponKind.Warhead?2:Rules.missileMinimumSeconds,Distance(origin,target)/Mathf.Max(1,Rules.missileSpeed))};
            Projectiles.Add(projectile);if(notify)Emit(StrategicEventKind.IncomingMissile,owner,Owners[target],target,Rules.Weapon(kind).name+" en camino.",Rules.Weapon(kind).radius);
        }
        public int ProjectileCell(StrategicProjectile p)
        {var position=ProjectilePosition(p);return World.At(Mathf.Clamp(Mathf.FloorToInt(position.x),0,World.Width-1),Mathf.Clamp(Mathf.FloorToInt(position.z),0,World.Height-1));}
        private bool CompatibleSAM(WeaponKind kind)=>kind==WeaponKind.Atomic||kind==WeaponKind.Mega||kind==WeaponKind.Hydrogen||(kind==WeaponKind.Warhead&&Rules.samInterceptsMIRV);
        private void AdvanceWeapons(float dt)
        {
            foreach(var b in Buildings)if(b.kind==BuildingKind.Silo&&b.Operational&&b.ammunition<b.level+1&&b.nextReload<=ElapsedTime){b.ammunition++;b.nextReload=ElapsedTime+Rules.siloCooldown;}
            AdvanceSamDefence(dt);
            for(int i=0;i<Projectiles.Count;i++)
            {
                var p=Projectiles[i];if(!p.active)continue;
                if(p.targetNation>=0&&p.targetNation!=p.owner&&AreAllied(p.owner,p.targetNation)){p.active=false;continue;}
                p.age+=dt;int cell=ProjectileCell(p);
                if(!p.active||p.Progress<1)continue;p.active=false;
                if(p.kind==WeaponKind.MIRV)
                {
                    int target=p.targetNation;if(target<0||_owned[target].Count==0)continue;
                    var reservoir=new List<int>();int seen=0;foreach(int owned in _owned[target]){seen++;if(reservoir.Count<Rules.mirvWarheads)reservoir.Add(owned);else{int choose=_random.Next(seen);if(choose<reservoir.Count)reservoir[choose]=owned;}}
                    foreach(int point in reservoir)CreateProjectile(p.owner,p.target,point,WeaponKind.Warhead,false);
                }
                else if(p.kind==WeaponKind.Scatter)
                {
                    for(int k=0;k<5;k++){int x=Mathf.Clamp(p.target%World.Width+_random.Next(-18,19),0,World.Width-1),y=Mathf.Clamp(p.target/World.Width+_random.Next(-18,19),0,World.Height-1);ResolveImpact(p.owner,World.At(x,y),p.kind);}
                }
                else if(p.kind==WeaponKind.Railgun)ResolveRailgun(p);
                else ResolveImpact(p.owner,p.target,p.kind);
            }
            Projectiles.RemoveAll(p=>!p.active);_radiationClock+=dt;
            if(_radiationClock>=1)
            {
                _radiationClock=0;var expired=new List<int>();foreach(var entry in Radiation)if(entry.Value<=ElapsedTime)expired.Add(entry.Key);
                foreach(int id in expired){Radiation.Remove(id);NuclearScars.Remove(id);StrategicCellChanged?.Invoke(id);}
            }
        }
        private void ResolveImpact(int owner,int target,WeaponKind kind)
        {
            var definition=Rules.Weapon(kind);int radius=Mathf.CeilToInt(definition.radius),cx=target%World.Width,cy=target/World.Width;var cells=new HashSet<int>();
            for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
            {
                if(dx*dx+dy*dy>definition.radius*definition.radius)continue;int x=cx+dx,y=cy+dy;if(World.WrapLongitude)x=(x%World.Width+World.Width)%World.Width;
                if(World.InBounds(x,y)&&World.Land[World.At(x,y)])cells.Add(World.At(x,y));
            }
            ApplyImpactCells(owner,cells,definition,target);if(definition.radiation)NuclearDetonated?.Invoke(target,definition.radius);Emit(StrategicEventKind.Explosion,owner,-1,target,definition.name+" impactó.",definition.radius);
        }
        private void ResolveRailgun(StrategicProjectile projectile)
        {
            var cells=new HashSet<int>();float dx=projectile.target%World.Width-projectile.origin%World.Width;
            if(World.WrapLongitude&&Mathf.Abs(dx)>World.Width*.5f)dx+=dx>0?-World.Width:World.Width;
            float dy=projectile.target/World.Width-projectile.origin/World.Width;int steps=Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dy)));
            for(int s=0;s<=steps;s++)
            {
                float t=s/(float)Mathf.Max(1,steps);int cx=Mathf.RoundToInt(projectile.origin%World.Width+dx*t),cy=Mathf.RoundToInt(projectile.origin/World.Width+dy*t);
                for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++){int px=cx+x,py=cy+y;if(World.WrapLongitude)px=(px%World.Width+World.Width)%World.Width;if(World.InBounds(px,py)&&World.IsLand(px,py))cells.Add(World.At(px,py));}
            }
            ApplyImpactCells(projectile.owner,cells,Rules.Weapon(WeaponKind.Railgun));Emit(StrategicEventKind.Explosion,projectile.owner,-1,projectile.target,"Descarga electromagnética resuelta.",4);
        }
        private bool ImpactCanHarm(int attacker,int victim,bool nuclear)=>victim>=0&&(nuclear?(victim==attacker||ProtectionRemaining(victim)<=0):(victim!=attacker&&!AreAllied(attacker,victim)&&ProtectionRemaining(victim)<=0));
        public bool NuclearFriendlyRisk(int faction,int target,float radius,bool includeAllies=true)
        {
            if(target<0||target>=Owners.Length)return false;int r=Mathf.CeilToInt(radius),cx=target%World.Width,cy=target/World.Width;
            for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++){if(x*x+y*y>radius*radius)continue;int px=cx+x,py=cy+y;if(World.WrapLongitude)px=(px%World.Width+World.Width)%World.Width;if(!World.InBounds(px,py))continue;int owner=Owners[World.At(px,py)];if(owner==faction||(includeAllies&&owner>=0&&AreAllied(faction,owner)))return true;}return false;
        }
        public Color32 RadiationPixel(int cell)=>!IsIrradiated(cell)?new Color32(0,0,0,0):new Color32(255,(byte)Mathf.RoundToInt(255*(NuclearScars.TryGetValue(cell,out float distance)?distance:1)),0,255);
        private void ApplyImpactCells(int attacker,HashSet<int> cells,WeaponDefinition definition,int epicentre=-1)
        {
            var hits=new Dictionary<int,int>();foreach(int cell in cells)
            {int f=Owners[cell];if(!ImpactCanHarm(attacker,f,definition.radiation))continue;hits[f]=hits.TryGetValue(f,out int count)?count+1:1;}
            foreach(var hit in hits)
            {float fraction=Mathf.Clamp01(hit.Value/(float)Mathf.Max(1,TerritoryCounts[hit.Key]));InflictLoss(hit.Key,Mathf.Max(Troops[hit.Key],definition.damage)*fraction);LastCombat[hit.Key]=ElapsedTime;}
            foreach(var b in Buildings)if(!b.destroyed&&cells.Contains(b.cell)&&ImpactCanHarm(attacker,b.owner,definition.radiation))DestroyBuilding(b,b.owner==attacker?-1:attacker);
            if(!definition.radiation)return;
            foreach(int cell in cells)
            {
                int previous=Owners[cell];if(previous>=0&&!ImpactCanHarm(attacker,previous,true))continue;
                UnclaimCell(cell,attacker);Radiation[cell]=ElapsedTime+Rules.radiationSeconds;NuclearScars[cell]=epicentre>=0?Mathf.Clamp01(SeaDistance(cell,epicentre)/Mathf.Max(.01f,definition.radius)):1;StrategicCellChanged?.Invoke(cell);
            }
        }
    }
}
