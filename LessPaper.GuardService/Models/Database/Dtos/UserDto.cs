namespace LessPaper.GuardService.Models.Database.Dtos
{
    public class UserDto : BaseDto
    {

        public string Email { get; set; }

        public string Salt { get; set; }

        public string HashedPassword { get; set; }
        
        public DirectoryDto RootDirectory { get; set; }

    }


}
