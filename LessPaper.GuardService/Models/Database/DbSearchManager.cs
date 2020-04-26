using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.Shared.Interfaces.Database.Manager;
using LessPaper.Shared.Interfaces.General;
using MongoDB.Driver;

namespace LessPaper.GuardService.Models.Database
{
    public class DbSearchManager : IDbSearchManager
    {
        private readonly IMongoDatabase db;

        public DbSearchManager(IMongoDatabase db)
        {
            this.db = db;
        }

        /// <inheritdoc />
        public async Task<ISearchResult> Search(string requestingUserId, string directoryId, string searchQuery, uint count, uint page)
        {
            throw new NotImplementedException();
        }
    }
}
