using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;

namespace TerrariaLauncher.TShockPlugins.TradingSystemAgent
{
    [TerrariaApi.Server.ApiVersion(2, 1)]
    public class TradingSystemAgent : TerrariaApi.Server.TerrariaPlugin
    {
        public TradingSystemAgent(Main game) : base(game)
        {

        }

        public override string Name => "Trading System Agent";
        public override Version Version => new Version(1, 0, 0, 0);
        public override string Author => "Terraria Launcher";
        public override string Description => "Running an agent of TerrariaLauncher.Services.TradingSystem.";
        public override string UpdateURL => "https://github.com/TerrariaLauncher";

        public override void Initialize()
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }
    }
}
