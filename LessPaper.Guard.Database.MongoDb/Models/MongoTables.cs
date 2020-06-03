using LessPaper.Guard.Database.MongoDb.Interfaces;

namespace LessPaper.Guard.Database.MongoDb.Models
{
    public class MongoTables : IMongoTables
    {
        /// <inheritdoc />
        public string RevisionTable { get; set; } = "fileRevisions";

        /// <inheritdoc />
        public string FilesTable { get; set; } = "files";

        /// <inheritdoc />
        public string DirectoryTable { get; set; } = "directories";

        /// <inheritdoc />
        public string UserTable { get; set; } = "user";

        /// <inheritdoc />
        public string DatabaseName { get; set; } = "lesspaper";
    }
}
