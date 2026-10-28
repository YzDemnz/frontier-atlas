using UnityEngine;
namespace FrontierAtlas
{
    public sealed partial class FrontierPrototype
    {
        public static readonly BuildingKind[] QuickBuildings={BuildingKind.City,BuildingKind.Port,BuildingKind.Factory,BuildingKind.Outpost,BuildingKind.SAM,BuildingKind.Silo};
        public bool QuickBuildActive {get;private set;}
        public void SelectQuickBuilding(int index)
        {
            if(!HasMatch||!_game.CampaignEnabled||index<0||index>=QuickBuildings.Length)return;
            _interface?.CloseSimpleAlliance();SelectedBuilding=QuickBuildings[index];QuickBuildActive=true;CloseRadial();CloseCampaignPanel();
            Notify(_game.Rules.Building(SelectedBuilding).name+": clic para colocar · Mayús para repetir · Esc para cancelar.");
        }
        public void CancelQuickBuilding(){QuickBuildActive=false;QuickWarshipPlacement=false;QuickWeaponSelection=-1;WeaponAim.Cancel();}
        public CampaignAction PlaceQuickBuilding(int cell,bool repeat=false)
        {
            var result=_game.Construct(_game.PlayerFaction,cell,SelectedBuilding);Notify(result.message);if(result.success&&!repeat)QuickBuildActive=false;return result;
        }
        private void UpdateQuickBuildingKeys()
        {
            if(!_game.CampaignEnabled||Panel==CampaignPanel.Diplomacy)return;
            if(_game.IsQuickMatch){for(int i=0;i<10;i++){KeyCode top=i==9?KeyCode.Alpha0:(KeyCode)((int)KeyCode.Alpha1+i),pad=i==9?KeyCode.Keypad0:(KeyCode)((int)KeyCode.Keypad1+i);if(Input.GetKeyDown(top)||Input.GetKeyDown(pad))SelectQuickSlot(i);}return;}
            for(int i=0;i<QuickBuildings.Length;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))||Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1+i)))SelectQuickBuilding(i);
        }
    }
    public sealed partial class FrontierInterface
    {
        private readonly string[] _quickNames={"CIUDAD","PUERTO","FÁBRICA","DEFENSA","SAM","SILO"};
        private void DrawQuickConstructionToolbar(Rect card)
        {
            int player=G.PlayerFaction;float gap=6,width=(card.width-32-gap*5)/6;
            for(int i=0;i<FrontierPrototype.QuickBuildings.Length;i++)
            {
                var kind=FrontierPrototype.QuickBuildings[i];Rect slot=new Rect(card.x+16+i*(width+gap),card.y+12,width,84);bool selected=_p.QuickBuildActive&&_p.SelectedBuilding==kind;
                double cost=G.ConstructionPrice(player,kind,_p.HoverCell);bool affordable=G.Gold[player]>=cost;
                FrontierUI.Rounded(slot,selected?new Color(.10f,.29f,.29f):new Color(.07f,.11f,.14f),8);
                Label(slot.x+7,slot.y+3,24,(i+1).ToString(),_ui.Caption,20,selected?FrontierUI.Accent:FrontierUI.Muted);
                DrawSymbol(new Rect(slot.center.x-13,slot.y+12,26,26),(int)kind,affordable?FrontierUI.White:FrontierUI.Muted);
                _ui.Label(new Rect(slot.x+2,slot.y+40,slot.width-4,19),_quickNames[i],new GUIStyle(_ui.Caption){fontSize=11,alignment=TextAnchor.MiddleCenter},FrontierUI.White);
                _ui.Label(new Rect(slot.x+2,slot.y+61,slot.width-4,20),Amount(cost),new GUIStyle(_ui.Small){fontSize=12,alignment=TextAnchor.MiddleCenter},affordable?FrontierUI.Gold:new Color(1,.43f,.38f));
                if(GUI.Button(slot,GUIContent.none,GUIStyle.none))_p.SelectQuickBuilding(i);
            }
            float y=card.y+105,buttonWidth=(card.width-32-24)/5;int priorSize=_ui.ButtonStyle.fontSize;_ui.ButtonStyle.fontSize=12;
            if(_ui.Button(new Rect(card.x+16,y,buttonWidth,28),"RETIRAR [X]",false,G.HasOrder(player)))_p.StopOrder();
            if(_ui.Button(new Rect(card.x+16+(buttonWidth+6),y,buttonWidth,28),"CAPITAL [C]"))_p.FocusPlayer();
            if(_ui.Button(new Rect(card.x+16+(buttonWidth+6)*2,y,buttonWidth,28),"MUNDO"))_p.ViewContinent();
            if(_ui.Button(new Rect(card.x+16+(buttonWidth+6)*3,y,buttonWidth,28),"DIPLOMACIA")){OpenWorldBoard(0);if(_p.InspectedFaction>=0&&_p.InspectedFaction!=player)_worldNation=_p.InspectedFaction;}
            if(_ui.Button(new Rect(card.x+16+(buttonWidth+6)*4,y,buttonWidth,28),_p.QuickBuildActive?"ESC":"INFO"))
            {if(_p.QuickBuildActive)_p.CancelQuickBuilding();else _p.OpenCampaignPanel(CampaignPanel.Information,G.CapitalCells[player]);}
            _ui.ButtonStyle.fontSize=priorSize;
        }
        private void DrawQuickBuildPreview()
        {
            if(!_p.QuickBuildActive||_p.HoverCell<0)return;int cell=_p.HoverCell;var result=G.ValidateConstruction(G.PlayerFaction,cell,_p.SelectedBuilding);Vector2 point=Project(cell);
            Color colour=result.success?FrontierUI.Accent:new Color(1,.40f,.34f);DrawSymbol(new Rect(point.x-17,point.y-17,34,34),(int)_p.SelectedBuilding,colour);
            Rect hint=new Rect(Mathf.Clamp(point.x+22,20,W-330),Mathf.Clamp(point.y+20,100,Dock.y-85),310,66);FrontierUI.Panel(hint);
            Label(hint.x+12,hint.y+8,hint.width-24,G.Rules.Building(_p.SelectedBuilding).name+" · "+Amount(G.ConstructionPrice(G.PlayerFaction,_p.SelectedBuilding,cell)),_ui.Body,26,colour);
            Label(hint.x+12,hint.y+35,hint.width-24,result.success?"Clic: colocar · Mayús: repetir · Esc: cancelar":result.message,new GUIStyle(_ui.Small){fontSize=12},26);
        }
    }
}
