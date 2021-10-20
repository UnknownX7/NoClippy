using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Logging;

namespace NoClippy.Modules
{
    public static class Modules
    {
        private class ModuleInfo
        {
            public INoClippyModule module = null;
            public bool isEnabled = true;
        }

        private static readonly Dictionary<Type, ModuleInfo> modules = new();
        public static void Initialize()
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(INoClippyModule)) && !t.IsInterface))
            {
                var module = (INoClippyModule)Activator.CreateInstance(t);
                if (module == null) continue;

                if (module.IsEnabled)
                {
                    try
                    {
                        module.Enable();
                        PluginLog.LogInformation($"Loaded module: {module.GetType()}");
                    }
                    catch
                    {
                        PluginLog.LogError($"Failed loading module: {module.GetType()}");
                        module.IsEnabled = false;
                    }
                }

                modules.Add(t, new ModuleInfo{ module = module, isEnabled = module.IsEnabled });
            }
        }

        public static INoClippyModule GetInstance(Type type) => modules.TryGetValue(type, out var instance) ? instance.module : null;

        public static void CheckModules()
        {
            foreach (var (_, info) in modules)
            {
                var module = info.module;
                if (module.IsEnabled == info.isEnabled) continue;

                try
                {
                    if (module.IsEnabled)
                    {
                        module.Enable();
                        PluginLog.LogInformation($"Enabled module: {module.GetType()}");
                    }
                    else
                    {
                        module.Disable();
                        PluginLog.LogInformation($"Disabled module: {module.GetType()}");
                    }

                    info.isEnabled = module.IsEnabled;
                }
                catch { module.IsEnabled = false; }
            }
        }

        public static void Dispose()
        {
            foreach (var (_, info) in modules.Where(kv => kv.Value.isEnabled))
                info.module.Disable();
        }
    }
}
