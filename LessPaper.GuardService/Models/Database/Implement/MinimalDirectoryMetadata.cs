using System;
using System.Collections.Generic;
using System.Linq;
using LessPaper.GuardService.Models.Database.Dtos;
using LessPaper.Shared.Enums;
using LessPaper.Shared.Interfaces.General;

namespace LessPaper.GuardService.Models.Database.Implement
{
    public class MinimalDirectoryMetadata : Metadata, IMinimalDirectoryMetadata
    {
        private readonly MinimalDirectoryMetadataDto dto;

        public MinimalDirectoryMetadata(MinimalDirectoryMetadataDto dto) : base(dto)
        {
            this.dto = dto;
        }
        
        /// <inheritdoc />
        public uint NumberOfChilds => dto.NumberOfChilds;

    }
}