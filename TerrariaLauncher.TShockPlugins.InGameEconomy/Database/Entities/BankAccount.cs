using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaLauncher.TShockPlugins.InGameEconomy.Database.Entities
{
    class BankAccount
    {
        public class BankAccountStatus
        {
            public const int Normal = 1;
            public const int Closed = 2;
        }

        public int Id { get; set; }
        public int TShockAccountId { get; set; }
        public int CurrenyId { get; set; }
        public int StatusId { get; set; }
    }
}
