using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace FrontierAtlas.Editor
{
    public static class DirectionalExpansionValidation
    {
        private static int _checks;
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("EXPANSIÓN DIRIGIDA: "+message);_checks++;}
        private static GeographicPolygon Rect(float x0,float x1,float y0,float y1)=>new GeographicPolygon{rings=new[]{new GeographicRing{points=new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1),new Vector2(x0,y0)}}}};
        private static TerritorySimulation Fixture(bool blocked=false,bool wrapped=false,BalanceProfile profile=BalanceProfile.RoFrontPrototype)
        {
            var geography=new AfricaGeography{west=0,east=256,south=0,north=128,wrapLongitude=wrapped,neutralRegions=new[]{Rect(0,256,16,128)},countries=new[]{
                new CountryDefinition{code="A",name="A",longitude=8,latitude=25,polygons=Array.Empty<GeographicPolygon>()},new CountryDefinition{code="B",name="B",longitude=235,latitude=110,polygons=Array.Empty<GeographicPolygon>()}}};
            var game=new TerritorySimulation(new WorldGrid(256,128,17,geography),17,0,new MatchOptions{customPlayer=true,advancedSystems=true,playerName="Prueba"}){AiEnabled=false,ReinforcementsEnabled=false};
            int f=game.PlayerFaction,cx=wrapped?253:110;
            Check(game.FoundPlayer(game.World.At(cx,65),"Prueba"),"Fundación de prueba");Array.Clear(game.TerritoryCounts,0,game.FactionCount);
            for(int cell=0;cell<game.Owners.Length;cell++)
            {
                int x=cell%256,y=cell/256,dx=Math.Abs(x-cx);if(wrapped)dx=Math.Min(dx,256-dx);
                int owner=!game.World.Land[cell]?-2:dx*dx+(y-65)*(y-65)<=100&&(!blocked||x<120)?f:(x<12&&y<30)?0:(blocked&&x>=121)||x>230&&y>105?1:-1;
                game.Owners[cell]=owner;if(owner>=0)game.TerritoryCounts[owner]++;
            }
            game.Rules.balanceProfile=profile;game.Rules.victoryFraction=1;game.RebuildCampaignIndexes();game.Troops[f]=100000;return game;
        }
        private static void Advance(TerritorySimulation game,int captures)
        {
            int f=game.PlayerFaction,initial=game.TerritoryCounts[f];
            for(int tick=0;tick<20000&&game.TerritoryCounts[f]-initial<captures&&game.Operations.Any(o=>o.active);tick++)game.Step(.1f);
            Check(game.TerritoryCounts[f]-initial>=captures,"Avance progresivo alcanza el número previsto de celdas");
        }
        public static void Run()
        {
            _checks=0;var clicks=new NeutralDoubleClick();var point=new Vector2(400,350);
            Check(!clicks.Click(point,1),"Un clic aislado no ordena expansión");Check(clicks.Click(point+Vector2.one*3,1.2),"Doble clic próximo aceptado");Check(!clicks.Click(point,1.3),"Triple clic no duplica el envío");
            clicks.Clear();Check(!clicks.Click(point,2)&&!clicks.Click(point,2.8),"Dos clics lentos no forman un doble clic");
            clicks.Clear();Check(!clicks.Click(point,3)&&!clicks.Click(point+Vector2.right*80,3.1),"Dos puntos distantes no forman un doble clic");clicks.Clear();Check(!clicks.Click(point,3.2),"Cancelar limpia el primer clic");
            var game=Fixture();int f=game.PlayerFaction;var captures=new List<int>();double spent=0;
            game.TerritoryCaptured+=(owner,cell,previous)=>{if(owner!=f)return;Check(previous==-1&&game.World.Land[cell],"Sólo se captura tierra neutral");Check(Enumerable.Range(0,4).Any(d=>{int n=game.World.Neighbor(cell,d);return n>=0&&game.Owners[n]==f;}),"Cada conquista toca la frontera real");captures.Add(cell);spent+=game.World.Elevation[cell]>.60f?24:game.World.Elevation[cell]>.42f?20:16;};
            float troops=game.Troops[f];Check(game.BeginDirectionalExpansion(f,game.World.At(200,65),.8f)==OrderResult.Accepted,"Orden hacia el este aceptada");
            Check(game.CommittedTroops[f]==80000&&game.AvailableTroops(f)==20000,"El compromiso separa la reserva inmediatamente");Advance(game,800);
            Check(game.ExpansionDirection(f,out var origin,out var heading,out int primary,out int other)&&heading.x>.99f,"Dirección este conservada");
            float fraction=primary/(float)(primary+other);Check(Mathf.Abs(fraction-75f/110)<.01f&&other>240,"Pesos 75:35 repartidos en conquistas reales");
            Check(captures.Count(c=>c%256>origin.x)>captures.Count(c=>c%256<origin.x)*2,"Predominio visible hacia el lado elegido");
            Check(captures.Select(c=>c/256).Distinct().Count()>30&&captures.Any(c=>c/256>80)&&captures.Any(c=>c/256<50),"Mancha amplia, deformada y con crecimiento lateral");
            Check(Math.Abs(troops-game.Troops[f]-spent)<.1,"Cada celda consume las tropas correspondientes al terreno");
            float committed=game.CommittedTroops[f],reserve=game.AvailableTroops(f);Check(game.BeginDirectionalExpansion(f,game.World.At(25,65),.5f)==OrderResult.Accepted,"Nuevo doble clic reorienta el mismo frente");
            Check(game.Operations.Count(o=>o.active&&o.kind==OperationKind.Expansion)==1&&Mathf.Abs(game.CommittedTroops[f]-committed-Mathf.Floor(reserve*.5f))<.01f,"Reorientar refuerza sin duplicar operaciones ni tropas");
            Advance(game,400);Check(game.ExpansionDirection(f,out origin,out heading,out primary,out other)&&heading.x<-.99f&&Mathf.Abs(primary/(float)(primary+other)-75f/110)<.01f,"Nueva prioridad hacia el oeste");
            committed=game.CommittedTroops[f];Check(game.BeginDirectionalExpansion(f,game.World.At(100,4),.5f)==OrderResult.Water&&game.CommittedTroops[f]==committed,"Mar rechazado sin descontar reserva");
            Check(game.BeginDirectionalExpansion(f,game.World.At(200,65),0)==OrderResult.NoTroops&&game.ExpansionDirection(f,out origin,out heading,out primary,out other)&&heading.x<0,"Orden sin tropas conserva la dirección anterior");
            var blocked=Fixture(true);f=blocked.PlayerFaction;int enemyArea=blocked.TerritoryCounts[1];Check(blocked.BeginDirectionalExpansion(f,blocked.World.At(120,65),.8f)==OrderResult.Accepted,"Dirección con barrera enemiga aceptada");Advance(blocked,1400);
            Check(blocked.TerritoryCounts[1]==enemyArea,"La expansión neutral no invade a un enemigo ni atraviesa su muro");Check(blocked.OwnedCells(f).All(c=>c%256<121),"El avance se redistribuye por el espacio libre sin saltar el muro");Check(blocked.ExpansionDirection(f,out origin,out heading,out primary,out other)&&other>primary,"Al bloquearse la dirección elegida el resto de la mancha sigue creciendo");
            var empty=Fixture();f=empty.PlayerFaction;empty.Troops[f]=81;Check(empty.BeginDirectionalExpansion(f,empty.World.At(200,65),1)==OrderResult.Accepted,"Últimas tropas enviadas");for(int k=0;k<600;k++)empty.Step(.1f);
            int area=empty.TerritoryCounts[f];Check(!empty.Operations.Any(o=>o.active)&&empty.AvailableTroops(f)<16,"Se detiene al agotar las tropas de la ofensiva");for(int k=0;k<100;k++)empty.Step(.1f);Check(empty.TerritoryCounts[f]==area,"Sin tropas no sigue conquistando");
            var wrap=Fixture(false,true);f=wrap.PlayerFaction;Check(wrap.BeginDirectionalExpansion(f,wrap.World.At(20,65),.8f)==OrderResult.Accepted&&wrap.ExpansionDirection(f,out origin,out heading,out primary,out other)&&heading.x>.99f,"La dirección cruza correctamente la unión de longitudes");Advance(wrap,300);Check(wrap.OwnedCells(f).Any(c=>c%256<20),"Expansión contigua al cruzar el borde del mapa");
            var legacy=Fixture(false,false,BalanceProfile.LegacyPrototype);f=legacy.PlayerFaction;Check(legacy.BeginDirectionalExpansion(f,legacy.World.At(200,65),.8f)==OrderResult.Accepted,"Perfil clásico compatible");Advance(legacy,100);Check(legacy.ExpansionDirection(f,out origin,out heading,out primary,out other)&&primary>other&&other>0,"Ambos sectores también avanzan con el perfil clásico");
            string output=Environment.GetEnvironmentVariable("FRONTIER_OUTPUT")??Path.GetFullPath("Builds");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"directional-validation.json"),"{\"result\":\"passed\",\"assertions\":"+_checks+",\"eastPrimaryShare\":"+fraction.ToString("0.00000",System.Globalization.CultureInfo.InvariantCulture)+",\"weights\":[75,35],\"eastAndWestCaptured\":"+captures.Count+"}");Debug.Log("DIRECTIONAL_VALIDATION_OK "+_checks);
        }
    }
}
