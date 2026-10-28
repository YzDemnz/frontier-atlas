using UnityEngine;
namespace FrontierAtlas
{
    // Presentation-only arming state: changing game, target or panel cancels it; nothing is paid here.
    public sealed class StrategicLaunchConfirmation
    {
        private TerritorySimulation game;
        public int Target {get;private set;}=-1;
        public WeaponKind Weapon {get;private set;}
        public bool FromArsenal {get;private set;}
        public ArsenalKind Arsenal {get;private set;}
        public bool FriendlyRisk {get;private set;}
        public float ArmedAt {get;private set;}
        public bool Active=>game!=null;
        public void Cancel(){game=null;Target=-1;}
        public CampaignAction Arm(TerritorySimulation simulation,int target,WeaponKind weapon,float now,bool arsenal=false,ArsenalKind ability=ArsenalKind.Mega)
        {
            Cancel();var valid=simulation.ValidateWeapon(simulation.PlayerFaction,target,weapon,arsenal);if(!valid.success)return valid;
            if(arsenal&&simulation.ArsenalCharges(simulation.PlayerFaction,ability)<1)return CampaignAction.Fail("Primero adquiere un uso de esta habilidad.");
            game=simulation;Target=target;Weapon=weapon;FromArsenal=arsenal;Arsenal=ability;ArmedAt=now;
            FriendlyRisk=simulation.Rules.Weapon(weapon).radiation&&simulation.NuclearFriendlyRisk(simulation.PlayerFaction,target,simulation.Rules.Weapon(weapon).radius);
            return CampaignAction.Ok(FriendlyRisk?"PELIGRO: el radio incluye territorio propio o aliado. Confirma para lanzar.":"Revisa el radio de efecto y confirma el lanzamiento.");
        }
        public bool Matches(TerritorySimulation simulation,int target)=>game==simulation&&Target==target;
        public bool Ready(float now)=>Active&&now-ArmedAt>=.4f;
        public CampaignAction Confirm(TerritorySimulation simulation,int target,float now)
        {
            if(!Matches(simulation,target)){Cancel();return CampaignAction.Fail("El objetivo cambió. Vuelve a previsualizar el ataque.");}
            if(!Ready(now))return CampaignAction.Fail("Revisa el radio antes de confirmar.");
            int cell=Target;var weapon=Weapon;bool arsenal=FromArsenal;var ability=Arsenal;Cancel();
            return arsenal?simulation.UseArsenal(simulation.PlayerFaction,ability,cell):simulation.LaunchWeapon(simulation.PlayerFaction,cell,weapon);
        }
    }
    public sealed partial class FrontierPrototype
    {
        public readonly StrategicLaunchConfirmation WeaponAim=new StrategicLaunchConfirmation();
        public bool HasWeaponPreview
        {
            get
            {
                if(!WeaponAim.Matches(_game,ContextCell)||ScreenState!=FrontierScreen.Playing||_game.Paused||_game.Outcome!=CampaignOutcome.Playing||Panel!=(WeaponAim.FromArsenal?CampaignPanel.Arsenal:CampaignPanel.Nuclear))WeaponAim.Cancel();
                return WeaponAim.Active;
            }
        }
        public CampaignAction PreviewStrategicWeapon(WeaponKind weapon,bool arsenal=false,ArsenalKind ability=ArsenalKind.Mega)
        {var result=WeaponAim.Arm(_game,ContextCell,weapon,Time.unscaledTime,arsenal,ability);Notify(result.message);return result;}
        public CampaignAction ConfirmStrategicWeapon()
        {
            if(!HasWeaponPreview)return CampaignAction.Fail("Vuelve a seleccionar y revisar el objetivo.");
            var result=WeaponAim.Confirm(_game,ContextCell,Time.unscaledTime);Notify(result.message);return result;
        }
    }
    public sealed partial class FrontierInterface
    {
        private Rect WeaponConfirmationBounds=>new Rect(Mathf.Max(18,(W-620)*.5f-120),H-310,620,154);
        private void DrawWeaponPreview()
        {
            if(!_p.HasWeaponPreview)return;var aim=_p.WeaponAim;float radius=G.Rules.Weapon(aim.Weapon).radius;
            Color danger=new Color(1,.28f,.12f,.92f);CoverageCircle(aim.Target,radius,danger,2.5f);
            var point=RangePoint(aim.Target%G.World.Width+.5f,aim.Target/G.World.Width+.5f);
            RangeLine(point-Vector2.right*9,point+Vector2.right*9,danger,2);RangeLine(point-Vector2.up*9,point+Vector2.up*9,danger,2);
        }
        private void DrawWeaponConfirmation()
        {
            if(!_p.HasWeaponPreview)return;var aim=_p.WeaponAim;Rect r=WeaponConfirmationBounds;FrontierUI.Panel(r);
            Label(r.x+18,r.y+10,r.width-36,"CONFIRMAR · "+G.Rules.Weapon(aim.Weapon).name.ToUpperInvariant(),_ui.Heading,30);
            string text=aim.Weapon==WeaponKind.MIRV?"Cada ojiva afecta un radio de 6 celdas en distintos puntos de la nación.":"Radio de efecto: "+G.Rules.Weapon(aim.Weapon).radius.ToString("0.#")+" celdas. El impacto nuclear también destruye territorio propio y aliado.";
            if(aim.FriendlyRisk)text="PELIGRO: TU TERRITORIO O UN ALIADO ESTÁ DENTRO DEL RADIO.";
            Label(r.x+18,r.y+46,r.width-36,text,_ui.Small,47,aim.FriendlyRisk?new Color(1,.47f,.29f):FrontierUI.Muted);
            if(_ui.Button(new Rect(r.x+18,r.y+102,180,36),"CANCELAR"))aim.Cancel();
            if(_ui.Button(new Rect(r.x+212,r.y+102,r.width-230,36),"CONFIRMAR LANZAMIENTO",true,aim.Ready(Time.unscaledTime)))_p.ConfirmStrategicWeapon();
        }
    }
}
