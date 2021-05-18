using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaLauncher.TShockPlugins.InGameEconomy.Database.Entities
{
    class Currency
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Tradeable { get; set; }
        public string Description { get; set; }
    }
}
