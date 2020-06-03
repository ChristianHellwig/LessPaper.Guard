namespace LessPaper.Guard.Database.MongoDb.IntegrationTest
{
    public class BasicUser
    {
        public BasicUser(string userId, string rootDirectoryId, string email)
        {
            UserId = userId;
            RootDirectoryId = rootDirectoryId;
            Email = email;
        }

        public string UserId { get;  }

        public string RootDirectoryId { get; }

        public string Email { get;  }

    }
}
