using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LessPaper.GuardService.Models.Database
{
    public interface IMongoTables
    {
        string RevisionTable { get; }
        string FilesTable { get; }
        string DirectoryTable { get; }
        string UserTable { get; }
        string DatabaseName { get; }
    }
}
