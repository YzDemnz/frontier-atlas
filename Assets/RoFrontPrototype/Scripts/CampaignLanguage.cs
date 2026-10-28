using System;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private static string PoliticalName(Enum value)
        {
            if(value is CompactKind)return new[]{"alianza","defensa mutua","no agresión","libre comercio","protección militar","alto el fuego","paz","subsidio","tributo"}[Convert.ToInt32(value)];
            if(value is ProposalKind)return new[]{"tratado","préstamo","renegociación de deuda","invitación a coalición","paz","exigencia","mediación"}[Convert.ToInt32(value)];
            if(value is SupportKind)return new[]{"tropas","oro","préstamo urgente","diversión terrestre","intervención","escolta naval","bombardeo costero","defensa conjunta","mediación"}[Convert.ToInt32(value)];
            return new[]{"territorio","capital","infraestructura","recuperación","defensa","diversión","apoyo naval","bombardeo"}[Convert.ToInt32(value)];
        }
    }
}
