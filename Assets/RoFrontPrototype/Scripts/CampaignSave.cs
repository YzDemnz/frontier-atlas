using System;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
namespace FrontierAtlas
{
    // Versioned, bounded codec. Field types come from this assembly, never from type names in the file.
    // No BinaryFormatter, runtime code, Unity objects or event handlers are deserialized.
    internal static class CampaignCodec
    {
        internal static FieldInfo[] Fields(Type type)
        {var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));return fields;}
        private static int Count(BinaryReader r){int n=r.ReadInt32();if(n<0||n>10000000)throw new InvalidDataException("Tamaño de colección no válido.");return n;}
        internal static void Write(BinaryWriter w,Type t,object value)
        {
            if(!t.IsValueType){w.Write(value!=null);if(value==null)return;}
            if(t.IsEnum){w.Write(Convert.ToInt32(value));return;}
            if(t==typeof(string)){w.Write((string)value);return;}
            if(t.IsPrimitive){if(t==typeof(bool))w.Write((bool)value);else if(t==typeof(int))w.Write((int)value);else if(t==typeof(float))w.Write((float)value);else if(t==typeof(double))w.Write((double)value);else if(t==typeof(ulong))w.Write((ulong)value);else if(t==typeof(long))w.Write((long)value);else if(t==typeof(byte))w.Write((byte)value);else throw new InvalidDataException("Escalar no compatible.");return;}
            if(t.IsArray)
            {
                var array=(Array)value;w.Write(array.Length);Type element=t.GetElementType();
                if(element.IsPrimitive){var bytes=new byte[Buffer.ByteLength(array)];Buffer.BlockCopy(array,0,bytes,0,bytes.Length);w.Write(bytes);}
                else foreach(object item in array)Write(w,element,item);return;
            }
            if(t.IsGenericType)
            {
                var args=t.GetGenericArguments();var def=t.GetGenericTypeDefinition();int count=(int)t.GetProperty("Count").GetValue(value);w.Write(count);
                if(def==typeof(Dictionary<,>)){foreach(DictionaryEntry pair in (IDictionary)value){Write(w,args[0],pair.Key);Write(w,args[1],pair.Value);}}
                else if(def==typeof(List<>)||def==typeof(Queue<>)||def==typeof(HashSet<>)){foreach(object item in (IEnumerable)value)Write(w,args[0],item);}
                else throw new InvalidDataException("Colección no compatible.");return;
            }
            if(t.Namespace!="FrontierAtlas"&&t!=typeof(Vector2)&&t!=typeof(Vector3)&&t!=typeof(Color))throw new InvalidDataException("Tipo no permitido.");
            foreach(var field in Fields(t))Write(w,field.FieldType,field.GetValue(value));
        }
        private static readonly HashSet<string> NewRulesV3=new HashSet<string>{"factoryProductionProfile","samBalanceProfile","factoryConnectionMinimum","factoryConnectionRadius","factoryConnectionPathLimit","factoryDispatchSeconds","factoryLevelProductionBonus","trainMinimumDispatchSeconds","trainSaturationMidpoint","trainSaturationTail","maximumLogicalTrains","maximumTrainStops","trainOwnReward","trainNeutralReward","trainAlliedReward","samRadiusByLevel","samReferenceCooldown","samInterceptorSpeed","samHydrogenPriority","missileArcHeight","missileProjectedCurve"};
        internal static object Read(BinaryReader r,Type t,int depth=0,int version=6)
        {
            if(depth>32)throw new InvalidDataException("Estructura demasiado profunda.");if(!t.IsValueType&&!r.ReadBoolean())return null;
            if(t.IsEnum){int value=r.ReadInt32();if(!Enum.IsDefined(t,value))throw new InvalidDataException("Valor no compatible.");return Enum.ToObject(t,value);}
            if(t==typeof(string)){string text=r.ReadString();if(text.Length>100000)throw new InvalidDataException("Texto demasiado largo.");return text;}
            if(t==typeof(bool))return r.ReadBoolean();if(t==typeof(int))return r.ReadInt32();if(t==typeof(float)){float v=r.ReadSingle();if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Número no válido.");return v;}if(t==typeof(double)){double v=r.ReadDouble();if(double.IsNaN(v)||double.IsInfinity(v))throw new InvalidDataException("Número no válido.");return v;}if(t==typeof(ulong))return r.ReadUInt64();if(t==typeof(long))return r.ReadInt64();if(t==typeof(byte))return r.ReadByte();
            if(t.IsArray)
            {
                int count=Count(r);Type element=t.GetElementType();var array=Array.CreateInstance(element,count);
                if(element.IsPrimitive){int length=Buffer.ByteLength(array);byte[] bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();Buffer.BlockCopy(bytes,0,array,0,length);}
                else for(int i=0;i<count;i++)array.SetValue(Read(r,element,depth+1,version),i);return array;
            }
            if(t.IsGenericType)
            {
                int count=Count(r);object collection=Activator.CreateInstance(t);var args=t.GetGenericArguments();var def=t.GetGenericTypeDefinition();
                if(def==typeof(Dictionary<,>)){var dict=(IDictionary)collection;for(int i=0;i<count;i++)dict.Add(Read(r,args[0],depth+1,version),Read(r,args[1],depth+1,version));}
                else if(def==typeof(List<>)||def==typeof(Queue<>)||def==typeof(HashSet<>)){var add=t.GetMethod(def==typeof(Queue<>)?"Enqueue":"Add",new[]{args[0]});for(int i=0;i<count;i++)add.Invoke(collection,new[]{Read(r,args[0],depth+1,version)});}
                else throw new InvalidDataException("Colección no compatible.");return collection;
            }
            if(t.Namespace!="FrontierAtlas"&&t!=typeof(Vector2)&&t!=typeof(Vector3)&&t!=typeof(Color))throw new InvalidDataException("Tipo no permitido.");
            object instance=Activator.CreateInstance(t,true);foreach(var f in Fields(t))if((version>=6||t!=typeof(MatchOptions)||f.Name!="mode")&&(version>=5||t!=typeof(CampaignRules)||f.Name!="tacticalPassiveIncome")&&(version>=3||t!=typeof(CampaignRules)||!NewRulesV3.Contains(f.Name))&&(version>=4||t!=typeof(NegotiationTerms)||(f.Name!="sourceTerritoryFraction"&&f.Name!="targetTerritoryFraction")))f.SetValue(instance,Read(r,f.FieldType,depth+1,version));return instance;
        }
    }
    public sealed partial class TerritorySimulation
    {
        // Canonical state and ordering are saved. Navigation search scratch and presentation caches are rebuilt.
        private static readonly string[] SavedStateFields={"Owners","TerritoryCounts","CapitalCells","Targets","AttackFactions","LastAdvanceCells","Troops","CommittedTroops","LastCombat","States","Colors","Names","Paused","AiEnabled","ReinforcementsEnabled","<ElapsedTime>k__BackingField","_routes","_frontiers","_aiTimers","_aiHeadingUntil","_tick","<Rules>k__BackingField","<Gold>k__BackingField","<Stars>k__BackingField","Capacity","ProtectionUntil","BetrayalUntil","Teams","Statistics","<Outcome>k__BackingField","<WinningFaction>k__BackingField","<EndReason>k__BackingField","Obstacles","_owned","_starRemainder","_economyThink","_cityLevels","_campaignAccumulator","_victoryClock","<Ledgers>k__BackingField","Operations","_operationId","Buildings","TradeRoutes","_buildingId","_tradeId","_tradeRefresh","<TradeNetworkVersion>k__BackingField","Treaties","_hostilityUntil","Warships","_warshipId","_navalCombatClock","Projectiles","Radiation","_arsenalCharges","_projectileId","_radiationClock","_freeBoundary","_freeHeap","_freeSeeded","_directedFronts","_seaRoutes","_landingCells","_sailClock","<FleetActive>k__BackingField","<FleetCells>k__BackingField","<FleetNext>k__BackingField","<FleetProgress>k__BackingField","<Affairs>k__BackingField","CampaignSpeed","_politicsAccumulator","_diplomacyCursor","Trains","TradeDeliveries","_factoryDispatchAt","_trainId","_samSites","Interceptors","_interceptorId","<Strategy>k__BackingField","NuclearScars"};
        private static FieldInfo StateField(string name)=>typeof(TerritorySimulation).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)??throw new InvalidDataException("Falta un campo del esquema: "+name);
        public byte[] CaptureCampaign()
        {
            if(!CampaignEnabled)throw new InvalidOperationException("Esta partida no es una campaña.");
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write("ATLAS-CAMPAIGN");w.Write(6);w.Write(World.Width);w.Write(World.Height);w.Write(World.LandCount);w.Write(World.Countries.Length);foreach(var country in World.Countries)w.Write(country.code);
                w.Write(PlayerFaction);CampaignCodec.Write(w,typeof(MatchOptions),Options);w.Write(_random.State);w.Write(SavedStateFields.Length);
                foreach(string name in SavedStateFields){var field=StateField(name);w.Write(name);CampaignCodec.Write(w,field.FieldType,field.GetValue(this));}w.Flush();return stream.ToArray();
            }
        }
        public static TerritorySimulation RestoreCampaign(WorldGrid world,byte[] bytes)
        {
            if(bytes==null||bytes.Length>256*1024*1024)throw new InvalidDataException("Guardado demasiado grande.");
            using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadString()!="ATLAS-CAMPAIGN")throw new InvalidDataException("Cabecera de campaña no válida.");int version=r.ReadInt32();
                if((version!=2&&version!=3&&version!=4&&version!=5&&version!=6)||r.ReadInt32()!=world.Width||r.ReadInt32()!=world.Height||r.ReadInt32()!=world.LandCount||r.ReadInt32()!=world.Countries.Length)throw new InvalidDataException("El guardado pertenece a otra versión o a otro mapa.");
                foreach(var c in world.Countries)if(r.ReadString()!=c.code)throw new InvalidDataException("La geografía del guardado es incompatible.");
                int player=r.ReadInt32();var options=(MatchOptions)CampaignCodec.Read(r,typeof(MatchOptions),0,version);if(options==null||!options.advancedSystems)throw new InvalidDataException("Campaña no válida.");
                var game=new TerritorySimulation(world,4206,player,options);if(game.PlayerFaction!=player)throw new InvalidDataException("Jugador incompatible.");game._random.State=r.ReadUInt64();int savedCount=r.ReadInt32();if(savedCount!=SavedStateFields.Length-(version==2?8:version==3?1:0)-(version<6?1:0))throw new InvalidDataException("Esquema incompleto.");
                for(int index=0;index<savedCount;index++){string name=SavedStateFields[index];if(r.ReadString()!=name)throw new InvalidDataException("Esquema de guardado incompatible.");var f=StateField(name);object value=CampaignCodec.Read(r,f.FieldType,0,version);object old=f.GetValue(game);if(old is Array original&&value is Array restored&&original.Length!=restored.Length)throw new InvalidDataException("Dimensiones inválidas.");f.SetValue(game,value);}
                if(stream.Position!=stream.Length)throw new InvalidDataException("Datos adicionales no reconocidos.");game.ValidateRestoredCampaign();game.UpgradeTroopBalance();if(version<6&&game.TacticalBalance&&Mathf.Approximately(game.Rules.factoryConnectionRadius,100))game.Rules.factoryConnectionRadius=CampaignRules.Load().factoryConnectionRadius;game.RebindCampaignIndexes();game.RebindStrategy();game.Changed=true;return game;
            }
        }
        private void UpgradeTroopBalance()
        {
            if(!TacticalBalance||IsQuickMatch)return;var current=CampaignRules.Load();
            // The saved rules identify the old balance, so a second load never scales twice.
            if(Rules.initialTroops<current.initialTroops&&Rules.tacticalCityRecruitment>1)
            {
                float factor=current.initialTroops/Mathf.Max(1,Rules.initialTroops);
                for(int f=0;f<FactionCount;f++){Troops[f]*=factor;Statistics[f].peakTroops=Mathf.Max(Statistics[f].peakTroops,Troops[f]);}
                foreach(var op in Operations)if(op.active){op.troops*=factor;op.battleRate*=factor;}
                Rules.initialTroops=current.initialTroops;Rules.baseCapacity=current.baseCapacity;Rules.cityCapacity=current.cityCapacity;Rules.tribeTroops=current.tribeTroops;
                Rules.tacticalBaseRegeneration=current.tacticalBaseRegeneration;Rules.tacticalCityRecruitment=current.tacticalCityRecruitment;Rules.tacticalAiExpansionReserve=current.tacticalAiExpansionReserve;
                for(int f=0;f<FactionCount;f++)RecomputeCapacity(f);
            }
        }
        private void ValidateRestoredCampaign()
        {
            foreach(var scar in NuclearScars)if(scar.Key<0||scar.Key>=Owners.Length||scar.Value<0||scar.Value>1||!Radiation.ContainsKey(scar.Key))throw new InvalidDataException("Huella nuclear inválida.");
            var counts=new int[FactionCount];for(int c=0;c<Owners.Length;c++){int f=Owners[c];if(f<Ocean||f>=FactionCount||World.Land[c]&&f==Ocean||!World.Land[c]&&f!=Ocean)throw new InvalidDataException("Propiedad territorial inválida.");if(f>=0)counts[f]++;}
            for(int f=0;f<FactionCount;f++)if(counts[f]!=TerritoryCounts[f]||Gold[f]<0||double.IsNaN(Gold[f])||Troops[f]<0||float.IsNaN(Troops[f])||CapitalCells[f]< -1||CapitalCells[f]>=Owners.Length)throw new InvalidDataException("Recursos o territorio inconsistentes.");
            foreach(var train in Trains)if(train.owner<0||train.owner>=FactionCount||train.stops<0||train.visited==null)throw new InvalidDataException("Transporte comercial inválido.");
            foreach(var interceptor in Interceptors)if(interceptor.owner<0||interceptor.owner>=FactionCount||interceptor.slot<0||interceptor.slot>=5)throw new InvalidDataException("Interceptor inválido.");
            foreach(var b in Buildings)if(b.owner<0||b.owner>=FactionCount||b.cell<0||b.cell>=Owners.Length)throw new InvalidDataException("Edificio fuera del mapa.");
            foreach(var op in Operations)if(op.faction<0||op.faction>=FactionCount||op.target<0||op.target>=Owners.Length||op.troops<0)throw new InvalidDataException("Ofensiva inválida.");
            if(Affairs==null||Affairs.reputation.Length!=FactionCount||Affairs.periodIncome.Length!=FactionCount||Affairs.nextDebtPayment.Length!=FactionCount||Affairs.lastDiplomacyDay.Length!=FactionCount)throw new InvalidDataException("Diplomacia incompleta.");
        }
        private void RebindCampaignIndexes()
        {
            ResetTradePathIndex();
            _buildingsById.Clear();_buildingCells.Clear();_outposts.Clear();foreach(var b in Buildings){_buildingsById[b.id]=b;if(b.destroyed)continue;_buildingCells[b.cell]=b;if(b.kind==BuildingKind.Outpost){if(!_outposts.TryGetValue(b.owner,out var list))_outposts[b.owner]=list=new List<Building>();list.Add(b);}}
            _tradeByPair.Clear();_originRouteCounts.Clear();
            for(int i=0;i<TradeRoutes.Count;i++)
            {var route=TradeRoutes[i];if(TacticalBalance&&!route.naval){var a=BuildingById(route.fromBuilding);var b=BuildingById(route.toBuilding);if(a==null||b==null||LandStationDistance(a.cell,b.cell)>FactoryConnectionRadius+.01f){TradeRoutes.RemoveAt(i--);TradeNetworkVersion++;continue;}}long key=TradeKey(route.fromBuilding,route.toBuilding,route.naval);if(_tradeByPair.ContainsKey(key)){TradeRoutes.RemoveAt(i--);continue;}_tradeByPair[key]=route;IndexTradeRoute(route,1);}
            _tradeRefresh=0;_stationVersion=-1;_factoryLevelUntil=0;
            _memoryIndex.Clear();foreach(var memory in Affairs.memories)_memoryIndex[memory.observer*FactionCount+memory.subject]=memory;RebuildContacts();InvalidateDiplomaticTrade();_recruitmentUntil=0;
            // In-progress searches are derived work. Replan their exact saved destination; no troops or gold are committed twice.
            foreach(var op in Operations)if(op.active&&op.planning){int goal=World.Land[op.target]?(op.landing>=0?AdjacentWater(op.landing):-1):op.target;op.planning=false;if(goal>=0){op.planning=true;PlanSeaRoute(op.currentCell,goal,route=>{op.planning=false;if(!op.active)return;if(route==null){EndCampaignOperation(op,ArmyState.Blocked);SyncOperations();return;}op.route=route;op.nextCell=route.Count>0?route.Peek():op.currentCell;},op.faction==PlayerFaction);}else EndCampaignOperation(op,ArmyState.Blocked);}
            foreach(var ship in Warships)if(ship.planning&&!ship.sunk){ship.planning=false;if(ship.target>=0)MoveWarship(ship.id,ship.target);}
            SyncOperations();
        }
    }
    public static class CampaignFiles
    {
        public static WorldGrid CompatibleWorld(WorldGrid current,byte[] payload)
        {
            using(var reader=new BinaryReader(new MemoryStream(payload,false)))
            {
                if(reader.ReadString()!="ATLAS-CAMPAIGN")throw new InvalidDataException("Cabecera inválida.");int version=reader.ReadInt32(),width=reader.ReadInt32(),height=reader.ReadInt32();
                if(version!=2&&version!=3&&version!=4&&version!=5&&version!=6)throw new InvalidDataException("Versión no compatible.");if(width==current.Width&&height==current.Height)return current;
                if(current.Countries.Length!=197||!((width==4320&&height==2160)||(width==FrontierPrototype.MapWidth&&height==FrontierPrototype.MapHeight)))throw new InvalidDataException("Tamaño de mapa no compatible.");
                return new WorldGrid(width,height,4206);
            }
        }
        public static string DirectoryPath=>Path.Combine(Application.persistentDataPath,"Campanas");
        public static Task<string> SaveAsync(TerritorySimulation game)
        {
            byte[] payload=game.CaptureCampaign();string folder=DirectoryPath;string file=Path.Combine(folder,"Atlas_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".atlas");
            return Task.Run(()=>{Directory.CreateDirectory(folder);string temporary=file+".tmp";byte[] hash;using(var sha=SHA256.Create())hash=sha.ComputeHash(payload);using(var output=File.Create(temporary)){output.Write(hash,0,hash.Length);using(var zip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal,true))zip.Write(payload,0,payload.Length);}File.Move(temporary,file);return file;});
        }
        public static byte[] Read(string file)
        {
            string full=Path.GetFullPath(file),folder=Path.GetFullPath(DirectoryPath)+Path.DirectorySeparatorChar;if(!full.StartsWith(folder,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(full)!=".atlas")throw new InvalidDataException("Selecciona un guardado de la carpeta de campañas.");
            using(var input=File.OpenRead(full)){var hash=new byte[32];if(input.Read(hash,0,32)!=32)throw new InvalidDataException("Guardado incompleto.");using(var zip=new GZipStream(input,CompressionMode.Decompress))using(var memory=new MemoryStream()){var buffer=new byte[65536];int n;while((n=zip.Read(buffer,0,buffer.Length))>0){if(memory.Length+n>256*1024*1024)throw new InvalidDataException("Guardado demasiado grande.");memory.Write(buffer,0,n);}byte[] payload=memory.ToArray(),actual;using(var sha=SHA256.Create())actual=sha.ComputeHash(payload);for(int i=0;i<32;i++)if(hash[i]!=actual[i])throw new InvalidDataException("El guardado está dañado; la partida actual se conserva.");return payload;}}
        }
        public static string[] List(){if(!Directory.Exists(DirectoryPath))return Array.Empty<string>();var files=Directory.GetFiles(DirectoryPath,"*.atlas");Array.Sort(files,StringComparer.Ordinal);Array.Reverse(files);return files;}
    }
}
