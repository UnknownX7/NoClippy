using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Bindings.ImGui;

namespace NoClippy.Modules
{
    public static class Modules
    {
        private class ModuleInfo
        {
            public Module module = null;
            public bool isEnabled = true;
        }

        private static readonly Dictionary<Type, ModuleInfo> modules = new();
        private static IOrderedEnumerable<ModuleInfo> drawOrder;
        public static void Initialize()
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(Module)) && !t.IsAbstract))
            {
                var module = (Module)Activator.CreateInstance(t);
                if (module == null) continue;

                if (module.IsEnabled)
                {
                    try
                    {
                        module.Enable();
                        DalamudApi.LogInfo($"Loaded module: {module.GetType()}");
                    }
                    catch (Exception e)
                    {
                        DalamudApi.LogError($"Failed loading module: {module.GetType()}\n{e}");
                        module.IsEnabled = false;
                    }
                }

                modules.Add(t, new ModuleInfo{ module = module, isEnabled = module.IsEnabled });
            }

            drawOrder = modules.Values.OrderBy(info => info.module.DrawOrder);
        }

        public static Module GetInstance(Type type) => modules.TryGetValue(type, out var instance) ? instance.module : null;

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
                        DalamudApi.LogInfo($"Enabled module: {module.GetType()}");
                    }
                    else
                    {
                        module.Disable();
                        DalamudApi.LogInfo($"Disabled module: {module.GetType()}");
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

        public static void Draw()
        {
            var first = true;
            foreach (var info in drawOrder)
            {
                if (!first)
                    ImGui.Separator();
                info.module.DrawConfig();
                first = false;
            }
        }
    }
}
