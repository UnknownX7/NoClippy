using System;
using System.Reflection;
using Dalamud;
using Dalamud.Game.ClientState;
using Dalamud.Hooking;
using Dalamud.Plugin;
using ImGuiNET;
using ImPlotNET;

[assembly: AssemblyTitle("NoClippy")]
[assembly: AssemblyVersion("0.2.0.0")]

namespace NoClippy
{
    public class NoClippy : IDalamudPlugin
    {
        public string Name => "NoClippy";
        public static DalamudPluginInterface Interface { get; private set; }
        private PluginCommandManager commandManager;
        public static Configuration Config { get; private set; }
        public static NoClippy Plugin { get; private set; }

        private IntPtr _imPlotContext;

        // This is the typical time range that passes between the time when the client sets a lock and then receives the new lock from the server on a low ping environment
        // This data is an estimate of what near 0 ping would be, based on 20 ms ping logs (feel free to show me logs if you actually have near 0 ms ping)
        private const float MinSimDelay = 0.04f;
        private const float MaxSimDelay = 0.06f;

        // This will allow a portion of actual spikes (either from your internet or the server) to bleed into the simulated delay
        // This makes your delay look natural to other people since networks aren't perfect (notably, sending multiple packets at the same time can add 50-100 ms)
        private const bool AllowSpikes = true;


        private IntPtr animationLockPtr;
        private unsafe ref float AnimationLock => ref *(float*)animationLockPtr;

        private IntPtr isCastingPtr;
        private unsafe ref bool IsCasting => ref *(bool*)isCastingPtr;

        private IntPtr comboTimerPtr;
        private unsafe ref float ComboTimer => ref *(float*)comboTimerPtr;

        private IntPtr isQueuedPtr;
        private unsafe ref bool IsQueued => ref *(bool*)isQueuedPtr;

        private IntPtr actionCountPtr;
        private unsafe ref ushort ActionCount => ref *(ushort*)actionCountPtr;

        private IntPtr isGCDRecastActivePtr;
        private unsafe ref bool IsGCDRecastActive => ref *(bool*)isGCDRecastActivePtr;


        private IntPtr defaultClientAnimationLockPtr;
        public unsafe float DefaultClientAnimationLock
        {
            get => *(float*)defaultClientAnimationLockPtr;
            set
            {
                if (defaultClientAnimationLockPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(defaultClientAnimationLockPtr, BitConverter.GetBytes(value));
            }
        }

        private IntPtr shortClientAnimationLockPtr;
        public unsafe float ShortClientAnimationLock
        {
            get => *(float*)shortClientAnimationLockPtr;
            set
            {
                if (shortClientAnimationLockPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(shortClientAnimationLockPtr, BitConverter.GetBytes(value));
            }
        }

        private byte ignoreNext = 0;
        private float delay = -1;
        private float simDelay = (MaxSimDelay - MinSimDelay) / 2f + MinSimDelay;
        private readonly Random rand = new();

        private delegate void ReceiveActionEffectDelegate(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private static Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;

        private DateTime begunEncounter = DateTime.MinValue;
        private ushort lastDetectedClip = 0;
        private float currentWastedGCD = 0;
        private float encounterTotalClip = 0;
        private float encounterTotalWaste = 0;

        public void Initialize(DalamudPluginInterface p)
        {
            Plugin = this;
            Interface = p;

            Config = (Configuration)Interface.GetPluginConfig() ?? new();
            Config.Initialize(Interface);

            commandManager = new();

            ImPlot.SetImGuiContext(ImGui.GetCurrentContext());
            _imPlotContext = ImPlot.CreateContext();
            ImPlot.SetCurrentContext(_imPlotContext);

            try
            {
                var actionManager = Interface.TargetModuleScanner.GetStaticAddressFromSig("41 0F B7 57 04"); // g_ActionManager
                animationLockPtr = actionManager + 0x8;
                isCastingPtr = actionManager + 0x28;
                comboTimerPtr = actionManager + 0x60;
                isQueuedPtr = actionManager + 0x68;
                actionCountPtr = actionManager + 0x110;
                isGCDRecastActivePtr = actionManager + 0x610;
                // 0x614 is previous gcd skill, 0x618 is current gcd recast time (counts up), 0x61C is gcd recast (counted up to)
                ReceiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 8D F0 03 00 00"), ReceiveActionEffectDetour); // 4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9

                shortClientAnimationLockPtr = Interface.TargetModuleScanner.ScanModule("33 33 B3 3E ?? ?? ?? ?? ?? ?? 00 00 00 3F");
                defaultClientAnimationLockPtr = shortClientAnimationLockPtr + 0xA;

                Interface.Framework.OnUpdateEvent += Update;
                Interface.UiBuilder.OnBuildUi += PluginUI.Draw;

                DefaultClientAnimationLock = 0.6f; // Yes, I am going to make the clientside default the same as the server default

                if (!Config.Enable) return;

                TogglePlugin(true);
            }
            catch { PrintError("Failed to load!"); }
        }

        private float AverageDelay(float currentDelay) => delay > 0 ? delay = delay * 0.5f + currentDelay * 0.5f : delay = currentDelay * 0.75f;
        private float SimulateDelay() => simDelay = Math.Min(Math.Max(simDelay + (float)(rand.NextDouble() - 0.5) * 0.016f, MinSimDelay), MaxSimDelay);
        private static int F2MS(float f) => (int)(f * 1000);
        private void ReceiveActionEffectDetour(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        {
            var oldLock = AnimationLock;

            ReceiveActionEffectHook.Original(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail);

            var newLock = AnimationLock;
            if (oldLock == newLock) return;

            // Ignore cast locks (caster tax, teleport, lb)
            if (IsCasting || newLock <= 0.11f) // Unfortunately this isn't always true for casting if the user is above 500 ms ping
            {
                if (Config.EnableLogging)
                    PrintLog($"Ignored reducing server cast lock of {F2MS(newLock)} ms");
                return;
            }

            // Special case to (mostly) prevent accidentally using XivAlexander at the same time
            if (!Config.EnableDryRun && newLock % 0.01 is >= 0.0005f and <= 0.0095f)
            {
                ignoreNext = 2;
                PrintError($"Unexpected lock of {F2MS(newLock)} ms");
                return;
            }

            if (ignoreNext > 0)
            {
                ignoreNext--;
                PrintError("Detected possible use of XivAlexander");
                return;
            }

            var responseTime = DefaultClientAnimationLock - oldLock;
            var reduction = AllowSpikes ? Math.Min(AverageDelay(responseTime), responseTime) : responseTime;
            var delayOverride = Math.Min(Math.Max(newLock - reduction + SimulateDelay(), 0), newLock);

            if (!Config.EnableDryRun)
                AnimationLock = delayOverride;

            if (!Config.EnableLogging && oldLock != 0) return;

            var spikeDelay = responseTime - reduction;
            PrintLog($"{(Config.EnableDryRun ? "[DRY] " : string.Empty)}" +
                $"Response: {F2MS(responseTime)} ({F2MS(delay)}) > {F2MS(simDelay + spikeDelay)} ({F2MS(simDelay)} + {F2MS(spikeDelay)}) ms" +
                $"{(Config.EnableDryRun && newLock <= 0.6f && newLock % 0.01 is >= 0.0005f and <= 0.0095f ? $" [Alexander: {F2MS(responseTime - (0.6f - newLock))} ms]" : string.Empty)}" +
                $" || Lock: {F2MS(newLock)} > {F2MS(delayOverride)} ({F2MS(delayOverride - newLock) - 1}) ms");
        }

        [Command("/noclippy")]
        [HelpMessage("/noclippy [on|off|toggle|dry|help] - Toggles the config window if no option is specified.")]
        private void OnNoClippy(string command, string argument)
        {
            switch (argument)
            {
                case "on":
                case "toggle" when !Config.Enable:
                case "t" when !Config.Enable:
                    TogglePlugin(Config.Enable = true);
                    Config.Save();
                    PrintEcho("Enabled!");
                    break;
                case "off":
                case "toggle" when Config.Enable:
                case "t" when Config.Enable:
                    TogglePlugin(Config.Enable = false);
                    Config.Save();
                    PrintEcho("Disabled!");
                    break;
                case "dry":
                case "d":
                    PrintEcho($"Dry run is now {((Config.EnableDryRun = !Config.EnableDryRun) ? "enabled" : "disabled")}.");
                    Config.Save();
                    break;
                case "":
                    ConfigUI.isVisible = !ConfigUI.isVisible;
                    break;
                default:
                    PrintEcho("Invalid usage: Command must be \"/noclippy <option>\"." +
                        "\non / off / toggle - Enables or disables the plugin." +
                        "\ndry - Toggles dry run (will not override the animation lock).");
                    break;
            }
        }

        public static void PrintEcho(string message) => Interface.Framework.Gui.Chat.Print($"[NoClippy] {message}");
        public static void PrintError(string message) => Interface.Framework.Gui.Chat.PrintError($"[NoClippy] {message}");

        public static void PrintLog(string message)
        {
            if (Config.LogToChat)
                PrintEcho(message);
            else
                PluginLog.LogInformation(message);
        }

        public void TogglePlugin(bool enable)
        {
            if (enable)
                ReceiveActionEffectHook?.Enable();
            else
                ReceiveActionEffectHook?.Disable();
        }

        private void BeginEncounter()
        {
            begunEncounter = DateTime.Now;
            encounterTotalClip = 0;
            encounterTotalWaste = 0;
            currentWastedGCD = 0;
        }

        private void EndEncounter()
        {
            var span = DateTime.Now - begunEncounter;
            var formattedTime = $"{Math.Floor(span.TotalMinutes):00}:{span.Seconds:00}";
            PrintLog($"[{formattedTime}] Encounter stats: {encounterTotalClip:0.00} seconds of clipping, {encounterTotalWaste:0.00} seconds of wasted GCD.");
            begunEncounter = DateTime.MinValue;
        }

        private void DetectClipping()
        {
            if (lastDetectedClip == ActionCount || IsGCDRecastActive || AnimationLock <= 0) return;

            if (AnimationLock != 0.1f) // TODO need better way of detecting cast tax, IsCasting is not reliable here, additionally, this will detect LB
            {
                encounterTotalClip += AnimationLock;
                if (Config.EnableEncounterStatsLogging)
                    PrintLog($"GCD Clip: {F2MS(AnimationLock)} ms");
            }

            lastDetectedClip = ActionCount;
        }

        private void DetectWastedGCD()
        {
            if (!IsGCDRecastActive && !IsQueued)
            {
                if (AnimationLock > 0) return;
                currentWastedGCD += ImGui.GetIO().DeltaTime;
            }
            else if (currentWastedGCD > 0)
            {
                encounterTotalWaste += currentWastedGCD;
                if (Config.EnableEncounterStatsLogging)
                    PrintLog($"Wasted GCD: {F2MS(currentWastedGCD)} ms");
                currentWastedGCD = 0;
            }
        }

        private void UpdateEncounter()
        {
            if (!Config.EnableEncounterStats) return;

            if (Interface.ClientState.Condition[ConditionFlag.InCombat])
            {
                if (begunEncounter == DateTime.MinValue)
                    BeginEncounter();

                DetectClipping();
                DetectWastedGCD();
            }
            else if (begunEncounter != DateTime.MinValue)
            {
                EndEncounter();
            }
        }

        private void Update(Dalamud.Game.Internal.Framework framework)
        {
            if (!Config.Enable) return;
            UpdateEncounter();
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            commandManager.Dispose();

            Interface.Framework.OnUpdateEvent -= Update;
            Interface.UiBuilder.OnBuildUi -= PluginUI.Draw;

            ImPlot.DestroyContext(_imPlotContext);

            ReceiveActionEffectHook?.Dispose();
            DefaultClientAnimationLock = 0.5f;
            Interface.SavePluginConfig(Config);
            Interface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
