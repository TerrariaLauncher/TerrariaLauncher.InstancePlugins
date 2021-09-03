using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;

namespace TerrariaLauncher.TShockPlugins.DebugDummy
{
    [TerrariaApi.Server.ApiVersion(2, 1)]
    public class DebugDummy : TerrariaApi.Server.TerrariaPlugin
    {
        public DebugDummy(Main game) : base(game)
        {

        }

        public override string Name => base.Name;

        public override Version Version => base.Version;

        public override string Author => base.Author;

        public override string Description => base.Description;

        public override bool Enabled { get => base.Enabled; set => base.Enabled = value; }

        public override string UpdateURL => base.UpdateURL;

        public override void Initialize()
        {
            
        }

        protected override void Dispose(bool disposing)
        {
            
        }
    }
}
