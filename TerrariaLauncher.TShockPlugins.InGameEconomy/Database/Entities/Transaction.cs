using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaLauncher.TShockPlugins.InGameEconomy.Database.Entities
{
    class Transaction
    {
        public int Id { get; set; }
        public int FromBankAccountId { get; set; }
        public int ToBankAccountId { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string Reason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
