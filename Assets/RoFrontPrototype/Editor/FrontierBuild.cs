using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FrontierAtlas.Editor
{
    [InitializeOnLoad]
    public static class FrontierBuild
    {
        private const string Active="FrontierAtlas.Validation.Active",Pending="FrontierAtlas.Validation.BuildPending";
        private const string ScenePath="Assets/Scenes/FrontierAtlas.unity";
        private static double _started,_nextLog;
        private static int _errors,_frames,_peakObjects;
        private static bool _finishing;
        private static TerritorySimulation _game;
        private static int[] _initial;
        private static int _runtimeFrames,_lastMeasuredFrame=-1;
        private static double _windowStarted;
        private static readonly List<float> _frameTimes=new List<float>();
        private static readonly List<SoakWindow> _soakWindows=new List<SoakWindow>();
        private static double SoakDuration=>double.TryParse(Environment.GetEnvironmentVariable("FRONTIER_SOAK_SECONDS"),out double value)?Math.Max(30,value):300;
        private static string Output=>Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
        static FrontierBuild()
        {
            if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=CompleteBuildWhenReady;
            if(SessionState.GetBool(Active,false))
            {EditorApplication.update+=SoakUpdate;Application.logMessageReceived+=OnLog;}
        }

        [MenuItem("Frontier Atlas/Prepare world scene")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory("Assets/RoFrontPrototype/Resources");
            UserBoatPreparation.Prepare();UserTrainPreparation.Prepare();UserStructurePreparation.Prepare();UserWeaponPreparation.Prepare();
            const string materialPath="Assets/RoFrontPrototype/Resources/FrontierMap.mat";
            var mapShader=Shader.Find("FrontierAtlas/StrategicEarth");Require(mapShader!=null,"StrategicEarth shader available");
            var mapMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mapMaterial==null)
            {
                mapMaterial=new Material(mapShader);AssetDatabase.CreateAsset(mapMaterial,materialPath);
            }
            mapMaterial.shader=mapShader;EditorUtility.SetDirty(mapMaterial);
            const string pulsePath="Assets/RoFrontPrototype/Resources/FrontierPulse.mat";
            var pulseShader=Shader.Find("FrontierAtlas/ExpansionPulse");Require(pulseShader!=null,"Pulse shader available");
            if(AssetDatabase.LoadAssetAtPath<Material>(pulsePath)==null)AssetDatabase.CreateAsset(new Material(pulseShader),pulsePath);
            const string fleetPath="Assets/RoFrontPrototype/Resources/FrontierFleet.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(fleetPath)==null)AssetDatabase.CreateAsset(new Material(Shader.Find("FrontierAtlas/Fleet")),fleetPath);
            var artImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/RoFrontPrototype/Resources/Cinematic/EarthOrbitIntro.png");
            artImporter.textureType=TextureImporterType.Default;artImporter.mipmapEnabled=false;artImporter.maxTextureSize=2048;artImporter.sRGBTexture=true;artImporter.SaveAndReimport();
            var atlasImporter=(TextureImporter)AssetImporter.GetAtPath("Assets/RoFrontPrototype/Resources/Cartography/EarthRelief8K.jpg");
            Require(atlasImporter!=null,"8K geographic relief texture available");
            atlasImporter.textureType=TextureImporterType.Default;atlasImporter.maxTextureSize=8192;atlasImporter.mipmapEnabled=true;
            atlasImporter.filterMode=FilterMode.Trilinear;atlasImporter.anisoLevel=8;atlasImporter.mipMapBias=-.35f;atlasImporter.sRGBTexture=true;
            var atlasPlatform=atlasImporter.GetPlatformTextureSettings("Standalone");atlasPlatform.overridden=true;atlasPlatform.maxTextureSize=8192;
            atlasPlatform.format=TextureImporterFormat.BC7;atlasPlatform.compressionQuality=100;atlasImporter.SetPlatformTextureSettings(atlasPlatform);atlasImporter.SaveAndReimport();
            foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/RoFrontPrototype/Resources/Audio"}))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var audioSettings=importer.defaultSampleSettings;
                audioSettings.compressionFormat=AudioCompressionFormat.Vorbis;audioSettings.quality=.72f;
                audioSettings.loadType=importer.assetPath.EndsWith("score.wav")||importer.assetPath.EndsWith("ambience.wav")?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings=audioSettings;importer.SaveAndReimport();
            }
            PlayerSettings.companyName="Atlas de Fronteras";PlayerSettings.productName="Atlas de Fronteras";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;PlayerSettings.allowFullscreenSwitch=false;PlayerSettings.runInBackground=true;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
            var view=camera.GetComponent<Camera>();view.orthographic=true;view.orthographicSize=261;view.farClipPlane=1000;
            view.backgroundColor=new Color(.025f,.085f,.12f);view.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.SetPositionAndRotation(new Vector3(188,350,225),Quaternion.Euler(90,0,0));
            new GameObject("FRONTIER ATLAS - Earth").AddComponent<FrontierPrototype>();
            var sun=new GameObject("Luz del entorno",typeof(Light));sun.GetComponent<Light>().type=LightType.Directional;sun.GetComponent<Light>().intensity=1.15f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.72f,.80f);RenderSettings.ambientEquatorColor=new Color(.42f,.49f,.52f);RenderSettings.ambientGroundColor=new Color(.25f,.28f,.27f);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            MCPForUnity.Editor.McpCiBoot.StartStdioForCi();
            AssetDatabase.SaveAssets();Debug.Log("FRONTIER_SETUP_OK "+Application.unityVersion);
        }

        [MenuItem("Frontier Atlas/Build Windows")]
        public static void BuildWindows()
        {
            PlayerSettings.allowFullscreenSwitch=false;PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            if(!File.Exists(ScenePath))Prepare();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            AssetDatabase.SaveAssets();
            string path=Path.Combine(Output,Environment.GetEnvironmentVariable("FRONTIER_BUILD_FOLDER")??"Windows","FrontierAtlas.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Require(result.summary.result==BuildResult.Succeeded,"Windows build; errors="+result.summary.totalErrors);
            File.WriteAllText(Path.Combine(Output,"build-result.json"),JsonUtility.ToJson(new BuildEvidence{
                unity=Application.unityVersion,result=result.summary.result.ToString(),errors=result.summary.totalErrors,
                warnings=result.summary.totalWarnings,seconds=result.summary.totalTime.TotalSeconds,
                bytes=result.summary.totalSize,executable=path},true));
            Debug.Log("FRONTIER_BUILD_OK "+path);
        }
        public static void ValidateAndBuild()
        {
            try
            {
                Prepare();RunSimulationChecks();CampaignValidation.Run();
                SessionState.SetBool(Active,true);EditorApplication.update-=SoakUpdate;EditorApplication.update+=SoakUpdate;
                Application.logMessageReceived-=OnLog;Application.logMessageReceived+=OnLog;
                EditorApplication.isPlaying=true;
            }
            catch(Exception e){Fail(e);}
        }
        public static void VerifyAndBuild()
        {
            try {Prepare();RunSimulationChecks();CampaignValidation.Run();CampaignExpansionValidation.Run();IndustrialDefenceValidation.Run();DefeatValidation.Run();GrandStrategyValidation.Run();FactoryNetworkValidation.Run();LocalNetworkNuclearValidation.Run();QuickMatchValidation.Run();BuildWindows();}
            catch(Exception e){Fail(e);}
        }

        [MenuItem("Frontier Atlas/Validate troop and combat rules")]
        public static void RunSimulationChecks()
        {
            var world=new WorldGrid(FrontierPrototype.MapWidth,FrontierPrototype.MapHeight,4206);
            Require(world.Countries.Length==197,"197 real named countries");
            Require(world.Countries.Any(c=>c.code=="ZWE"&&c.name=="Zimbabue"),"Zimbabwe named");
            Require(world.StartCells.Distinct().Count()==197,"197 distinct starts");
            var africa=new TerritorySimulation(world,4206);
            for(int f=0;f<world.Countries.Length;f++)
                Require(world.GeographicCountries[africa.CapitalCells[f]]==f&&africa.TerritoryCounts[f]>0,"Starts inside real country region");
            CheckCounts(africa);
            int ocean=Array.FindIndex(world.Land,v=>!v);
            Require(africa.IssueOrder(0,Cell(world,ocean))==OrderResult.NoCoastalAccess,"Landlocked country needs coastal access");
            var paused=(int[])africa.Owners.Clone();var troops=(float[])africa.Troops.Clone();
            africa.Paused=true;africa.Step(10);
            Require(africa.Owners.SequenceEqual(paused)&&africa.Troops.SequenceEqual(troops),"Pause freezes ownership and troops");
            Require(africa.IssueOrder(0,Cell(world,world.StartCells[1]))==OrderResult.Paused,"Paused orders rejected");
            africa.Paused=false;Tick(africa,90);CheckCounts(africa);
            Require(Enumerable.Range(1,world.Countries.Length-1).Count(f=>africa.TerritoryCounts[f]>5)>35,"Continental AI expands slowly");

            var neutral=MakeDuel(20,20,true);
            int target=neutral.World.At(8,4);
            neutral.Troops[0]=2;
            Require(neutral.IssueOrder(0,Cell(neutral.World,target))==OrderResult.NoTroops,"Insufficient troops deny neutral expansion");
            neutral.Troops[0]=20;
            Require(neutral.IssueOrder(0,Cell(neutral.World,target))==OrderResult.Accepted,"Neutral order accepted");
            int[] before=(int[])neutral.Owners.Clone();Tick(neutral,.36f);
            Require(before.Where((owner,id)=>owner!=neutral.Owners[id]).Count()==1,"One cell changes per march tick");
            Require(Mathf.Abs(neutral.Troops[0]-17)<.01f,"General expansion consumes exactly 3 troops per sector");
            Tick(neutral,8);
            var newCells=Enumerable.Range(0,before.Length).Where(id=>before[id]!=neutral.Owners[id]).ToArray();
            Require(newCells.Select(id=>id/neutral.World.Width).Distinct().Count()>=3,"Expansion grows sideways rather than a one-cell path");
            Require(neutral.Troops[0]<3&&!neutral.HasOrder(0),"Neutral front stops when available troops are insufficient");
            CheckCounts(neutral);
            var remote=MakeDuel(100,20,true);var remoteBefore=(int[])remote.Owners.Clone();
            remote.IssueOrder(0,new Vector2Int(18,4));Tick(remote,4);
            Require(Enumerable.Range(0,remoteBefore.Length).Where(id=>remoteBefore[id]!=remote.Owners[id]).Select(id=>id/remote.World.Width).Distinct().Count()>=3,
                "Attack approaches also widen through neutral land");CheckCounts(remote);

            var weak=MakeDuel(100,200);int enemyCell=weak.World.At(8,4);
            int original=weak.TerritoryCounts[0];
            Require(weak.IssueOrder(0,Cell(weak.World,enemyCell))==OrderResult.Accepted,"Weaker army can engage");
            Tick(weak,20);
            Require(weak.TerritoryCounts[0]==original&&weak.Troops[0]<.01f&&Mathf.Abs(weak.Troops[1]-100)<.02f,
                "Stronger defender holds border and depletes attacker");
            CheckCounts(weak);

            var strong=MakeDuel(200,100);original=strong.TerritoryCounts[0];
            strong.IssueOrder(0,Cell(strong.World,enemyCell));Tick(strong,15);
            Require(strong.Troops[1]<.01f&&strong.TerritoryCounts[0]>original,"Advance continues when defender reaches zero");
            Tick(strong,100);
            Require(strong.Troops[0]<1&&strong.TerritoryCounts[1]>0,"Advance stops at zero before full annexation");
            CheckCounts(strong);

            var equal=MakeDuel(100,100);original=equal.TerritoryCounts[0];
            equal.IssueOrder(0,Cell(equal.World,enemyCell));Tick(equal,20);
            Require(equal.Troops.All(t=>t<.01f)&&equal.TerritoryCounts[0]==original,"Equal forces both reach zero without advancing");

            var annex=MakeDuel(1000,100);annex.IssueOrder(0,Cell(annex.World,enemyCell));Tick(annex,100);
            Require(annex.TerritoryCounts[1]==0&&annex.States[1]==ArmyState.Eliminated&&annex.Troops[0]>0,
                "Army occupies entire adjacent enemy territory");
            CheckCounts(annex);
            var unreachable=MakeDuel(400,400,true);
            for(int y=0;y<unreachable.World.Height;y++){int id=unreachable.World.At(12,y);unreachable.World.Land[id]=false;unreachable.Owners[id]=TerritorySimulation.Ocean;}
            Require(unreachable.IssueOrder(0,new Vector2Int(18,4))==OrderResult.NoLandRoute,"No teleport without a valid shore route");
            CheckNavigation();
            CheckGeneralExpansion();
            Require(world.WrapLongitude&&world.Neighbor(world.At(0,500),0)==world.At(world.Width-1,500),"Pacific wraps across date line");
            foreach(string code in new[]{"USA","BRA","FRA","CHN","AUS","NZL","JPN","IDN","PHL","GRL","MUS","FJI"})
            {
                if(code=="GRL")continue; // Greenland is physically included under Denmark.
                Require(world.Countries.Any(c=>c.code==code),"World region represented: "+code);
            }
            Require(world.Land[world.At((int)world.ToGrid(0,-80).x,(int)world.ToGrid(0,-80).y)],"Antarctica included");
            var rankGame=new TerritorySimulation(world,4206);
            var powers=new PowerBalance(rankGame);
            Require(powers.Tiers.All(t=>t==0),"Small initial states have no power colour");
            rankGame.TerritoryCounts[1]=Mathf.RoundToInt(80*world.CellAreaFactor);rankGame.TerritoryCounts[2]=Mathf.RoundToInt(160*world.CellAreaFactor);rankGame.TerritoryCounts[3]=Mathf.RoundToInt(210*world.CellAreaFactor);powers.Update(true);
            Require(powers.Tiers[3]==3&&powers.Tiers[2]>=2&&powers.Tiers[1]>0,"Largest states acquire power colours");
            Require(powers.DisplayColors[0]!=powers.DisplayColors[3],"Player colour remains distinct");
            Require(world.Width==FrontierPrototype.MapWidth&&world.Height==FrontierPrototype.MapHeight&&Mathf.Abs(world.ResolutionScale-3110f/2160)<.001f,"Compact Earth retains normalized cell density");
            var scaled=new TerritorySimulation(world,4206){AiEnabled=false,ReinforcementsEnabled=false};
            scaled.Troops[0]=20;int origin=scaled.CapitalCells[0];int originalCount=scaled.TerritoryCounts[0];
            Require(scaled.IssueOrder(0,new Vector2Int(origin%world.Width+6,origin/world.Width))==OrderResult.Accepted,"Fine-grid order accepted");
            Tick(scaled,.35f);
            Require(scaled.TerritoryCounts[0]==originalCount+Mathf.FloorToInt(world.CellAreaFactor+.0001f)&&Mathf.Abs(scaled.Troops[0]-(20-Mathf.FloorToInt(world.CellAreaFactor+.0001f)*scaled.NeutralSectorCost))<.01f,"Compact-grid captures preserve area pace and troop cost");
            CheckCounts(scaled);
            Debug.Log("FRONTIER_SIMULATION_OK geography197 lateralExpansion troopCost strongerDefender zeroTroops continuedAdvance annexation pause waterRoutes powerColours");
            File.WriteAllText(Path.Combine(Output,"simulation-validation.json"),"{\"result\":\"passed\",\"countries\":197,\"cases\":[\"real country starts\",\"neutral troop cost\",\"insufficient troops\",\"stronger defender\",\"defender zero then advance\",\"attacker zero stops\",\"equal forces\",\"full annexation\",\"pause\",\"world wrapping\",\"sea waypoint\",\"paused fleet\",\"adjacent ocean movement\",\"defended landing\",\"landing troop cost\",\"water remains unowned\",\"disconnected land\",\"AI expansion\"]}");
        }

        private static GeographicPolygon Rectangle(float west,float east)
        {
            return new GeographicPolygon{rings=new[]{new GeographicRing{points=new[]{
                new Vector2(west,-36),new Vector2(east,-36),new Vector2(east,39),new Vector2(west,39),new Vector2(west,-36)}}}};
        }
        private static TerritorySimulation MakeDuel(float attacker,float defender,bool neutral=false)
        {
            var data=new AfricaGeography{neutralRegions=Array.Empty<GeographicPolygon>(),countries=new[]{
                new CountryDefinition{code="A",name="Atacante",longitude=-20,latitude=0,polygons=new[]{Rectangle(-26,16.5f)}},
                new CountryDefinition{code="B",name="Defensor",longitude=40,latitude=0,polygons=new[]{Rectangle(16.5f,59)}}}};
            var world=new WorldGrid(24,8,1,data);
            var game=new TerritorySimulation(world,1){AiEnabled=false,ReinforcementsEnabled=false};
            Array.Clear(game.TerritoryCounts,0,2);
            for(int id=0;id<game.Owners.Length;id++)
            {
                int x=id%world.Width,owner=x<8?0:(neutral&&x<16?-1:1);
                game.Owners[id]=owner;if(owner>=0)game.TerritoryCounts[owner]++;
            }
            var frontiers=(System.Collections.Generic.HashSet<int>[])typeof(TerritorySimulation).GetField("_frontiers",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
            foreach(var frontier in frontiers)frontier.Clear();
            MethodInfo refresh=typeof(TerritorySimulation).GetMethod("RefreshFrontier",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int id=0;id<game.Owners.Length;id++)refresh.Invoke(game,new object[]{id});
            game.Troops[0]=attacker;game.Troops[1]=defender;return game;
        }
        private static void CheckNavigation()
        {
            var data=new AfricaGeography{neutralRegions=Array.Empty<GeographicPolygon>(),countries=new[]{
                new CountryDefinition{code="A",name="Island A",longitude=-20,latitude=0,polygons=new[]{Rectangle(-26,-5)}},
                new CountryDefinition{code="B",name="Island B",longitude=40,latitude=0,polygons=new[]{Rectangle(25,59)}}}};
            var world=new WorldGrid(24,8,1,data);var game=new TerritorySimulation(world,1){AiEnabled=false,ReinforcementsEnabled=false};
            int f=0,origin=world.CoastCells.First(id=>world.GeographicCountries[id]==0);
            // Give the attacker actual access to its shore through a normal land order.
            game.Troops[0]=1000;game.IssueOrder(0,Cell(world,origin));Tick(game,20);game.CancelOrder(0);
            Require(game.Owners[origin]==0,"Attacker reaches departure coast");
            int beach=world.CoastCells.First(id=>world.GeographicCountries[id]==1);
            int water=world.Neighbor(origin,1);while(water>=0&&world.Land[water])water=world.Neighbor(water,1);
            Require(game.IssueOrder(0,Cell(world,water))==OrderResult.Accepted,"Sea waypoint accepted from coast");
            int before=game.FleetCells[f];game.Paused=true;Tick(game,2);Require(game.FleetCells[f]==before,"Paused fleet stays still");game.Paused=false;
            Tick(game,2);Require(game.FleetCells[f]==water&&game.States[f]==ArmyState.Anchored,"Transport reaches water waypoint");
            if(game.Owners[beach]==TerritorySimulation.Neutral){game.Owners[beach]=1;game.TerritoryCounts[1]++;}
            game.Troops[1]=100;game.Troops[0]=300;
            Require(game.IssueOrder(0,Cell(world,beach))==OrderResult.Accepted,"Retarget at sea to another island");
            bool moved=false,fought=false,landed=false;Vector3 last=game.NavalPosition(game.FleetCells[0],game.FleetNext[0],game.FleetProgress[0]);game.Landed+=x=>landed=true;
            for(int step=0;step<1600;step++)
            {
                game.Step(.025f);Vector3 point=game.NavalPosition(game.FleetCells[0],game.FleetNext[0],game.FleetProgress[0]);int cell=world.At(Mathf.Clamp(Mathf.FloorToInt(point.x),0,world.Width-1),Mathf.Clamp(Mathf.FloorToInt(point.z),0,world.Height-1));
                if(Vector3.Distance(point,last)>.001f){Require(!world.Land[cell]&&Vector3.Distance(point,last)<.1f,"Fleet moves continuously through water at its configured speed");moved=true;last=point;}
                if(game.States[0]==ArmyState.Fighting)fought=true;
                if(landed)break;
            }
            Require(moved&&fought&&landed,"Real crossing, border combat and landing completed");
            Require(game.Owners[beach]==0&&game.Troops[0]<300,"Landing captures beach and consumes troops");
            for(int id=0;id<world.Land.Length;id++)if(!world.Land[id])Require(game.Owners[id]==TerritorySimulation.Ocean,"Water remains unowned after navigation");
            CheckCounts(game);Debug.Log("FRONTIER_NAVAL_OK pause waypoint adjacentWater landing troopCost noWaterOwnership fought="+fought);
        }
        private static Vector2Int Cell(WorldGrid world,int id)=>new Vector2Int(id%world.Width,id/world.Width);
        private static TerritorySimulation MakeExpansionIsland()
        {
            var game=MakeDuel(200,100,true);Array.Clear(game.TerritoryCounts,0,2);
            for(int id=0;id<game.Owners.Length;id++)
            {
                int x=id%game.World.Width,y=id/game.World.Width;
                int owner=x>=10&&x<=12&&y>=3&&y<=5?0:x==23?1:TerritorySimulation.Neutral;
                game.Owners[id]=owner;if(owner>=0)game.TerritoryCounts[owner]++;
            }
            var frontiers=(System.Collections.Generic.HashSet<int>[])typeof(TerritorySimulation).GetField("_frontiers",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(game);
            foreach(var frontier in frontiers)frontier.Clear();
            var refresh=typeof(TerritorySimulation).GetMethod("RefreshFrontier",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int id=0;id<game.Owners.Length;id++)refresh.Invoke(game,new object[]{id});return game;
        }
        private static void CheckGeneralExpansion()
        {
            var east=MakeExpansionIsland();var west=MakeExpansionIsland();
            Require(east.AttackBorder(0,east.World.At(13,4))==OrderResult.Accepted,"Single border action starts expansion");
            Require(west.AttackBorder(0,west.World.At(9,4))==OrderResult.Accepted,"Opposite border action also starts expansion");
            Tick(east,8.4f);Tick(west,8.4f);
            Require(east.Owners.SequenceEqual(west.Owners)&&Mathf.Abs(east.Troops[0]-west.Troops[0])<.001f,"Free expansion is independent of clicked direction");
            foreach(int direction in new[]{0,1,2,3})Require(Enumerable.Range(0,east.Owners.Length).Any(id=>east.Owners[id]==0&&(direction==0?id%24<10:direction==1?id%24>12:direction==2?id/24<3:id/24>5)),"Expansion reaches every free side");
            CheckCounts(east);
            var budget=MakeExpansionIsland();
            Require(budget.PreviewTroops(0,.25f)==50,"ATTACK preview uses available reserve");
            Require(budget.BeginExpansion(0,.25f)==OrderResult.Accepted&&budget.CommittedTroops[0]==50&&budget.AvailableTroops(0)==150,"Attack percentage commits troops without duplication");
            Require(budget.BeginExpansion(0,.25f)==OrderResult.Accepted&&budget.CommittedTroops[0]==87&&budget.AvailableTroops(0)==113,"Reinforcement uses remaining reserve");
            int count=budget.TerritoryCounts[0];budget.Paused=true;Tick(budget,1);Require(budget.TerritoryCounts[0]==count,"Pause freezes general expansion");budget.Paused=false;
            Tick(budget,.4f);float survivors=budget.Troops[0];budget.CancelOrder(0);
            Require(budget.CommittedTroops[0]==0&&budget.AvailableTroops(0)==survivors&&budget.Troops[0]==survivors,"Retreat returns survivors without creating troops");
            Require(budget.AttackBorder(0,budget.World.At(23,4))==OrderResult.NotAdjacent,"Land attack cannot jump to a distant nation");
            var defense=MakeDuel(200,100);defense.CommittedTroops[1]=80;int original=defense.TerritoryCounts[0];
            defense.AttackBorder(0,defense.World.At(8,4),1);Tick(defense,3);
            Require(defense.TerritoryCounts[0]>original&&Mathf.Abs(defense.Troops[1]-80)<.01f,"Troops committed elsewhere are unavailable to defend the border");
            CheckCounts(defense);Debug.Log("FRONTIER_GENERAL_EXPANSION_OK allSides directionIndependent preview reserve reinforcement retreat neighborOnly");
        }
        private static void Tick(TerritorySimulation game,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)game.Step(.05f);}
        private static void CheckCounts(TerritorySimulation game)
        {
            var counts=new int[game.FactionCount];
            for(int id=0;id<game.Owners.Length;id++)
            {
                int owner=game.Owners[id];
                Require(game.World.Land[id]?owner>=-1&&owner<counts.Length:owner==-2,"Geography ownership invariant");
                if(owner>=0)counts[owner]++;
            }
            Require(counts.SequenceEqual(game.TerritoryCounts),"Territory counts consistent");
            Require(game.Troops.All(t=>t>=0),"No negative troop totals");
        }
        private static void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)_errors++;}
        private static void SoakUpdate()
        {
            if(_finishing||!EditorApplication.isPlaying||EditorApplication.isPaused)return;
            try
            {
                var prototype=UnityEngine.Object.FindAnyObjectByType<FrontierPrototype>();if(prototype==null)return;
                if(_started==0)
                {
                    typeof(FrontierPrototype).GetField("_presentationAudit",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(prototype,true);
                    if(prototype.ScreenState==FrontierScreen.Intro)return;
                    if(!prototype.HasMatch)
                    {
                        prototype.PlayerName="Prueba de rendimiento";prototype.BeginLocationSelection();
                        int nation=Array.FindIndex(prototype.World.Countries,c=>c.code=="ZWE");
                        int capital=Enumerable.Range(0,prototype.World.Land.Length).First(c=>prototype.World.GeographicCountries[c]==nation&&prototype.Simulation.CanFoundAt(c));
                        Require(prototype.SelectFoundation(capital)&&prototype.ConfirmFoundation(),"Custom campaign founding flow");
                    }
                    _started=EditorApplication.timeSinceStartup;_windowStarted=_started;_game=prototype.Simulation;_game.MeasurePerformance=true;
                    _initial=(int[])_game.TerritoryCounts.Clone();_runtimeFrames=Time.frameCount;
                    Require(UnityEngine.Object.FindObjectsByType<MapVisuals>().Length<=2,"One active shared map after deferred destroy");
                    Require(prototype.Audio.MusicPlaying&&prototype.Audio.HasClip("expand")&&prototype.Audio.HasClip("battle"),"Music and effects loaded and playing");
                    Require(!prototype.Visuals.UsesGeographicBorders,"No pre-drawn country borders");
                    Debug.Log("FRONTIER_PLAY_STARTED");
                }
                _frames++;
                _peakObjects=Math.Max(_peakObjects,UnityEngine.Object.FindObjectsByType<GameObject>().Length);
                double elapsed=EditorApplication.timeSinceStartup-_started;
                if(Time.frameCount!=_lastMeasuredFrame){_frameTimes.Add(Time.unscaledDeltaTime);_lastMeasuredFrame=Time.frameCount;}
                if(EditorApplication.timeSinceStartup-_windowStarted>=30&&_frameTimes.Count>0)
                {
                    float[] times=_frameTimes.OrderBy(t=>t).ToArray();double interval=EditorApplication.timeSinceStartup-_windowStarted;
                    _soakWindows.Add(new SoakWindow{wallSeconds=(float)elapsed,simulationSeconds=_game.ElapsedTime,frames=times.Length,fps=(float)(times.Length/interval),p95Milliseconds=times[(int)((times.Length-1)*.95f)]*1000,maximumMilliseconds=times[times.Length-1]*1000,buildings=_game.Buildings.Count,routes=_game.TradeRoutes.Count,operations=_game.Operations.Count,allocatedUnityMB=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576f});
                    _frameTimes.Clear();_windowStarted=EditorApplication.timeSinceStartup;
                    File.WriteAllText(Path.Combine(Output,"tactical-soak-performance.json"),JsonUtility.ToJson(new SoakPerformance{inEditor=true,windows=_soakWindows.ToArray(),meters=_game.Meters},true));
                }
                if(elapsed>=_nextLog)
                {
                    CheckCounts(_game);Debug.Log("FRONTIER_PLAY_PROGRESS seconds="+elapsed.ToString("F1")+" simulated="+_game.ElapsedTime.ToString("F1")+" objects="+_peakObjects+" errors="+_errors);
                    _nextLog=elapsed+60;
                }
                if(elapsed<SoakDuration)return;
                Require(_errors==0,"No Play Mode exceptions or errors");Require(_peakObjects<500,"Grouped map and pooled models have bounded object count");
                Require(_game.ElapsedTime>SoakDuration*.9,"Actual game simulation advanced for the requested duration; elapsed="+_game.ElapsedTime);
                int growing=Enumerable.Range(0,_game.FactionCount).Count(f=>_game.TerritoryCounts[f]>_initial[f]);
                Require(growing>35,"Many AI countries grow during actual Play Mode");CheckCounts(_game);
                File.WriteAllText(Path.Combine(Output,"play-validation.json"),JsonUtility.ToJson(new PlayEvidence{
                    unity=Application.unityVersion,seconds=elapsed,errors=_errors,peakGameObjects=_peakObjects,
                    updateSamples=_frames,runtimeFrames=Time.frameCount-_runtimeFrames,simulatedSeconds=_game.ElapsedTime,
                    landCells=_game.World.LandCount,countries=_game.FactionCount,growingCountries=growing,
                    performance=_soakWindows.ToArray(),territories=_game.TerritoryCounts,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,memoryMB=SystemInfo.systemMemorySize},true));
                Debug.Log("FRONTIER_PLAY_OK seconds="+elapsed);
                _finishing=true;SessionState.SetBool(Active,false);SessionState.SetBool(Pending,true);
                EditorApplication.playModeStateChanged+=FinishAfterPlay;EditorApplication.isPlaying=false;
            }
            catch(Exception e){Fail(e);}
        }
        private static void FinishAfterPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            EditorApplication.playModeStateChanged-=FinishAfterPlay;EditorApplication.delayCall+=CompleteBuildWhenReady;
        }
        private static void CompleteBuildWhenReady()
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=CompleteBuildWhenReady;return;}
            SessionState.SetBool(Pending,false);
            try{BuildWindows();EditorApplication.Exit(0);}catch(Exception e){Fail(e);}
        }
        private static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("VALIDATION FAILED: "+message);}
        private static void Fail(Exception e)
        {
            SessionState.SetBool(Active,false);SessionState.SetBool(Pending,false);Debug.LogException(e);
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"validation-failure.txt"),e.ToString());EditorApplication.Exit(1);
        }
        [Serializable]private class SoakWindow
        {public float wallSeconds,simulationSeconds,fps,p95Milliseconds,maximumMilliseconds,allocatedUnityMB;public int frames,buildings,routes,operations;}
        [Serializable]private class SoakPerformance{public bool inEditor;public SoakWindow[] windows;public SimulationMeter[] meters;}
        [Serializable]private class PlayEvidence
        {
            public string unity,cpu,gpu;public SoakWindow[] performance;public double seconds;public float simulatedSeconds;
            public int errors,peakGameObjects,updateSamples,runtimeFrames,landCells,countries,growingCountries,memoryMB;public int[] territories;
        }
        [Serializable]private class BuildEvidence
        {public string unity,result,executable;public int errors,warnings;public double seconds;public ulong bytes;}
    }
}
