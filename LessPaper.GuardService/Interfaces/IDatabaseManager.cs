using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LessPaper.GuardService.Interfaces;

namespace LessPaper.GuardService
{
    interface IDatabaseManager
    {
        IUserDataManager UserDataManager { get; }

    }
}
