using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Hooking;
using Dalamud.Logging;

namespace NoClippy;

public static class Memory
{
    public class Replacer : IDisposable
    {
        public nint Address { get; private set; } = nint.Zero;
        private readonly byte[] newBytes;
        private readonly byte[] oldBytes;
        private readonly AsmHook hook = null;
        public bool IsEnabled { get; private set; } = false;
        public bool IsValid => Address != nint.Zero;
        public string ReadBytes => !IsValid ? string.Empty : oldBytes.Aggregate(string.Empty, (current, b) => current + (b.ToString("X2") + " "));

        public Replacer(nint addr, byte[] bytes, bool startEnabled = false, bool useASMHook = false)
        {
            if (addr == nint.Zero) return;

            Address = addr;
            newBytes = bytes;
            SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
            createdReplacers.Add(this);

            if (useASMHook)
                hook = new(addr, newBytes, $"{Assembly.GetExecutingAssembly().GetName().Name} Replacer#{createdReplacers.Count}", AsmHookBehaviour.DoNotExecuteOriginal);

            if (startEnabled)
                Enable();
        }

        public Replacer(string sig, byte[] bytes, bool startEnabled = false, bool useASMHook = false)
        {
            var addr = nint.Zero;
            try { addr = DalamudApi.SigScanner.ScanModule(sig); }
            catch { PluginLog.LogError($"Failed to find signature {sig}"); }
            if (addr == nint.Zero) return;

            Address = addr;
            newBytes = bytes;
            SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
            createdReplacers.Add(this);

            if (useASMHook)
                hook = new(addr, newBytes, $"{Assembly.GetExecutingAssembly().GetName().Name} Replacer#{createdReplacers.Count}", AsmHookBehaviour.DoNotExecuteOriginal);

            if (startEnabled)
                Enable();
        }

        public Replacer(string sig, string[] asm, bool startEnabled = false)
        {
            var addr = nint.Zero;
            try { addr = DalamudApi.SigScanner.ScanModule(sig); }
            catch { PluginLog.LogError($"Failed to find signature {sig}"); }
            if (addr == nint.Zero) return;

            Address = addr;
            SafeMemory.ReadBytes(addr, 7, out oldBytes);
            createdReplacers.Add(this);
            hook = new(addr, asm, $"{Assembly.GetExecutingAssembly().GetName().Name} Replacer#{createdReplacers.Count}", AsmHookBehaviour.DoNotExecuteOriginal);

            if (startEnabled)
                Enable();
        }

        public void Enable()
        {
            if (!IsValid) return;

            if (hook == null)
                SafeMemory.WriteBytes(Address, newBytes);
            else
                hook.Enable();

            IsEnabled = true;
        }

        public void Disable()
        {
            if (!IsValid) return;

            if (hook == null)
                SafeMemory.WriteBytes(Address, oldBytes);
            else
                hook.Disable();

            IsEnabled = false;
        }

        public void Toggle()
        {
            if (!IsEnabled)
                Enable();
            else
                Disable();
        }

        public void Dispose()
        {
            if (IsEnabled)
                Disable();

            if (hook == null) return;
            hook.Dispose();
            SafeMemory.WriteBytes(Address, oldBytes);
        }
    }

    private static readonly List<Replacer> createdReplacers = new();

    public static void Dispose()
    {
        foreach (var rep in createdReplacers)
            rep.Dispose();
    }
}