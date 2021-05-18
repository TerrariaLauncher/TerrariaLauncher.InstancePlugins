using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerrariaLauncher.Commons.Database;
using TerrariaLauncher.TShockPlugins.InGameEconomy.Database.Entities;

namespace TerrariaLauncher.TShockPlugins.InGameEconomy.Database.Repositories
{
    class TransactionRepository : IRepository<Transaction, int>
    {
        private IUnitOfWork unitOfWork;

        public TransactionRepository(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        private System.Data.Common.DbCommand CreateInsertCommand(Transaction entity)
        {
            var command = this.unitOfWork.Connection.CreateCommand();

            command.Transaction = this.unitOfWork.Transaction;
            command.CommandText = "INSERT INTO transactions (fromBankAccountId, toBankAccountId, amount, currencyId) " +
                "VALUES (@fromBankAccountId, @toBankAccountId, @amount, @currencyId)";

            var fromBankAccount = command.CreateParameter();
            fromBankAccount.ParameterName = "fromBankAccount";
            fromBankAccount.DbType = System.Data.DbType.Int32;
            fromBankAccount.Value = entity.FromBankAccountId;

            var toBankAccountId = command.CreateParameter();
            toBankAccountId.ParameterName = "toBankAccountId";
            toBankAccountId.DbType = System.Data.DbType.Int32;
            toBankAccountId.Value = entity.ToBankAccountId;

            var amount = command.CreateParameter();
            amount.ParameterName = "amount";
            amount.DbType = System.Data.DbType.Decimal;
            amount.Precision = 19;
            amount.Scale = 4;
            amount.Value = entity.Amount;

            var currencyId = command.CreateParameter();
            currencyId.ParameterName = "currencyId";
            currencyId.DbType = System.Data.DbType.Int32;
            currencyId.Value = entity.CurrencyId;

            command.Parameters.Add(fromBankAccount);
            command.Parameters.Add(toBankAccountId);
            command.Parameters.Add(amount);
            command.Parameters.Add(currencyId);

            return command;
        }

        public async Task CreateAsync(Transaction entity, CancellationToken cancellationToken)
        {
            using (var command = this.CreateInsertCommand(entity))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                entity.Id = (int)(command as MySqlConnector.MySqlCommand).LastInsertedId;
            }

            var insertedEntity = await this.GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
            entity.CreatedAt = insertedEntity.CreatedAt;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<Transaction> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            using (var command = this.unitOfWork.Connection.CreateCommand())
            {
                command.Transaction = this.unitOfWork.Transaction;
                command.CommandText = "SELECT * FROM transactions WHERE id = @id";

                var idParam = command.CreateParameter();
                idParam.ParameterName = "id";
                idParam.DbType = System.Data.DbType.Int32;
                idParam.Value = id;

                command.Parameters.Add(idParam);

                using (var reader = await command.ExecuteReaderAsync(behavior: System.Data.CommandBehavior.SingleRow).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
                    return new Transaction()
                    {
                        Id = await reader.GetFieldValueAsync<int>(reader.GetOrdinal("id"), cancellationToken).ConfigureAwait(false),
                        FromBankAccountId = await reader.GetFieldValueAsync<int>(reader.GetOrdinal("fromBankAccountId"), cancellationToken).ConfigureAwait(false),
                        ToBankAccountId = await reader.GetFieldValueAsync<int>(reader.GetOrdinal("toBankAccountId"), cancellationToken).ConfigureAwait(false),
                        Amount = await reader.GetFieldValueAsync<decimal>(reader.GetOrdinal("amount"), cancellationToken).ConfigureAwait(false),
                        CurrencyId = await reader.GetFieldValueAsync<int>(reader.GetOrdinal("currencyId"), cancellationToken).ConfigureAwait(false),
                        Reason = await reader.GetFieldValueAsync<string>(reader.GetOrdinal("reason"), cancellationToken).ConfigureAwait(false),
                        CreatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(reader.GetOrdinal("createdAt"), cancellationToken).ConfigureAwait(false)
                    };
                }
            }
        }

        public Task<bool> UpdateAsync(Transaction entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
