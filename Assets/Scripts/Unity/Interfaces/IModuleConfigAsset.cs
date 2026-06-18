using System;
using Core.Configs;

namespace Unity.Interfaces
{
    public interface IModuleConfigAsset
    {
        ModuleConfig GetConfig();
        Type GetTargetModuleType();
    }
}