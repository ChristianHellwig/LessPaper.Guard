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
        public MinimalDirectoryMetadata(MetadataDto dto, uint numberOfChilds) : base(dto)
        {
            NumberOfChilds = numberOfChilds;
        }
        
        /// <inheritdoc />
        public uint NumberOfChilds { get; }

    }
}