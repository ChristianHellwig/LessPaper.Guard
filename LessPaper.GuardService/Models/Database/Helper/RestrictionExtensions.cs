using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.GuardService.Models.Database.Implement;
using LessPaper.Shared.Enums;

namespace LessPaper.GuardService.Models.Database.Helper
{
    public static class RestrictionExtensions
    {
        public static List<FileDto> RestrictPermissions(this List<FileDto> files, string userId)
        {
            return files.Select(x => x.RestrictPermissions(userId)).ToList();
        }

        public static FileDto RestrictPermissions(this FileDto file, string userId)
        {
            file.Permissions = file.Permissions.RestrictPermissions(userId).ToArray();
            return file;
        }

        public static List<FileDto> RestrictAccessKeys(this List<FileDto> files, string userId)
        {
            return files.Select(x => x.RestrictAccessKeys(userId)).ToList();
        }

        public static FileDto RestrictAccessKeys(this FileDto file, string userId)
        {
            foreach (var fileRevisionDto in file.Revisions)
            {
                var accessKeyForRequestingUser = fileRevisionDto.AccessKeys.FirstOrDefault(x => x.User.Id.AsString == userId);
                if (accessKeyForRequestingUser == null)
                    continue;

                fileRevisionDto.AccessKeys = new[] { accessKeyForRequestingUser };
            }

            return file;
        }


        public static BasicPermissionDto[] RestrictPermissions(this BasicPermissionDto[] permissionDtos, string userId)
        {
            var permissionEntry = permissionDtos.FirstOrDefault(x => x.User.Id.AsString == userId);
            if (permissionEntry == null)
                return new BasicPermissionDto[0];

            if (permissionEntry.Permission.HasFlag(Permission.ReadPermissions))
                return permissionDtos;

            return permissionEntry.Permission.HasFlag(Permission.Read) ? new[] { permissionEntry } : new BasicPermissionDto[0];
        }

    }
}
