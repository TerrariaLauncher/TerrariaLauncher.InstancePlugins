namespace TerrariaLauncher.TShockPlugins.TradingSystemAgent.InGameEconomy.NpcDamageTracking
{
    public class NpcStrike
    {
        public bool Critical { get; set; }
        public int Damage { get; set; }
        public float KnockBack { get; set; }
        public int HitDirection { get; set; }
        public bool NoEffect { get; set; }

        public int CurrentLife { get; set; }
        public int CurrentDefense { get; set; }

        public TShockAPI.TSPlayer Player { get; set; }
        public TShockAPI.DB.UserAccount User { get; set; }
    }
}
