using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TerrariaLauncher.Commons.Database
{
    public class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        public string ConnectionString { get; }

        public UnitOfWorkFactory (string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public IUnitOfWork Create()
        {
            var connection = new MySqlConnector.MySqlConnection(this.ConnectionString);
            connection.Open();
            return new UnitOfWork(connection);
        }

        public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnector.MySqlConnection(this.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return new UnitOfWork(connection);
        }
    }
}
