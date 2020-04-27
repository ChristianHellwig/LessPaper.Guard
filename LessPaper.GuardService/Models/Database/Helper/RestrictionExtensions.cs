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
        public static List<FileDto> FilterHasPermission(this List<FileDto> files, string userId)
        {
            foreach (var fileDto in files)
            {
                fileDto.Permissions =
                    fileDto.Permissions.FilterHasPermission(userId).Cast<FilePermissionDto>().ToArray();
            }

            return files;
        }

        public static FileDto FilterHasPermission(this FileDto file, string userId)
        {
            file.Permissions = file.Permissions.FilterHasPermission(userId).Cast<FilePermissionDto>().ToArray();
            return file;
        }


        public static BasicPermissionDto[] FilterHasPermission(this BasicPermissionDto[] permissionDtos, string userId)
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
