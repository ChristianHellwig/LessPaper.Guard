namespace LessPaper.Guard.Database.MongoDb.Interfaces
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
