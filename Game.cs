using System;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Game.Network;
using Dalamud.Hooking;

namespace NoClippy
{
    public static class Game
    {
        private static IntPtr animationLockPtr;
        public static unsafe ref float AnimationLock => ref *(float*)animationLockPtr;

        private static IntPtr isCastingPtr;
        public static unsafe ref bool IsCasting => ref *(bool*)isCastingPtr;

        private static IntPtr comboTimerPtr;
        public static unsafe ref float ComboTimer => ref *(float*)comboTimerPtr;

        private static IntPtr isQueuedPtr;
        public static unsafe ref bool IsQueued => ref *(bool*)isQueuedPtr;

        private static IntPtr actionCountPtr;
        public static unsafe ref ushort ActionCount => ref *(ushort*)actionCountPtr;

        private static IntPtr isGCDRecastActivePtr;
        public static unsafe ref bool IsGCDRecastActive => ref *(bool*)isGCDRecastActivePtr;

        private static IntPtr defaultClientAnimationLockPtr;
        public static unsafe float DefaultClientAnimationLock
        {
            get => *(float*)defaultClientAnimationLockPtr;
            set
            {
                if (defaultClientAnimationLockPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(defaultClientAnimationLockPtr, BitConverter.GetBytes(value));
            }
        }

        public delegate void UseActionEventDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, ref byte ret);
        public static event UseActionEventDelegate OnUseAction;
        public delegate byte UseActionDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp);
        public static Hook<UseActionDelegate> UseActionHook;
        public static byte UseActionDetour(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetedActorID, param, useType, pvp);
            OnUseAction?.Invoke(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, ref ret);
            return ret;
        }

        public delegate void UseActionLocationEventDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param, ref byte ret);
        public static event UseActionLocationEventDelegate OnUseActionLocation;
        public delegate byte UseActionLocationDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param);
        public static Hook<UseActionLocationDelegate> UseActionLocationHook;
        public static byte UseActionLocationDetour(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param)
        {
            var ret =  UseActionLocationHook.Original(actionManager, actionType, actionID, targetedActorID, vectorLocation, param);
            OnUseActionLocation?.Invoke(actionManager, actionType, actionID, targetedActorID, vectorLocation, param, ref ret);
            return ret;
        }

        public delegate void ReceiveActionEffectEventDelegate(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail, float oldLock, float newLock);
        public static event ReceiveActionEffectEventDelegate OnReceiveActionEffect;
        public delegate void ReceiveActionEffectDelegate(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        public static Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;
        public static void ReceiveActionEffectDetour(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        {
            var oldLock = AnimationLock;
            ReceiveActionEffectHook.Original(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail);
            OnReceiveActionEffect?.Invoke(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail, oldLock, AnimationLock);
        }

        public static event GameNetwork.OnNetworkMessageDelegate OnNetworkMessage;
        private static void NetworkMessage(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction) =>
            OnNetworkMessage?.Invoke(dataPtr, opCode, sourceActorId, targetActorId, direction);

        public static void Initialize()
        {
            var actionManager = DalamudApi.SigScanner.GetStaticAddressFromSig("41 0F B7 57 04"); // g_ActionManager
            animationLockPtr = actionManager + 0x8;
            isCastingPtr = actionManager + 0x28;
            comboTimerPtr = actionManager + 0x60;
            isQueuedPtr = actionManager + 0x68;
            actionCountPtr = actionManager + 0x110;
            isGCDRecastActivePtr = actionManager + 0x610;
            // 0x614 is previous gcd skill, 0x618 is current gcd recast time (counts up), 0x61C is gcd recast (counted up to)

            UseActionHook = new Hook<UseActionDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 9F BC 76 02 00"), UseActionDetour);
            UseActionLocationHook = new Hook<UseActionLocationDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 5C 24 50 0F B6 F0"), UseActionLocationDetour);
            ReceiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 8D F0 03 00 00"), ReceiveActionEffectDetour); // 4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9

            defaultClientAnimationLockPtr = DalamudApi.SigScanner.ScanModule("33 33 B3 3E ?? ?? ?? ?? ?? ?? 00 00 00 3F") + 0xA;

            // This is normally 0.5f but it causes the client to be sanity checked at high ping, so I'm increasing it to see clips better and see higher pings more accurately
            DefaultClientAnimationLock = 0.6f;

            DalamudApi.GameNetwork.NetworkMessage += NetworkMessage;

            UseActionHook.Enable();
            UseActionLocationHook.Enable();
            ReceiveActionEffectHook.Enable();
        }

        public static event Action OnUpdate;
        public static void Update() => OnUpdate?.Invoke();

        public static void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetworkMessage;

            UseActionHook?.Dispose();
            OnUseAction = null;
            UseActionLocationHook?.Dispose();
            OnUseActionLocation = null;
            ReceiveActionEffectHook?.Dispose();
            OnReceiveActionEffect = null;

            OnUpdate = null;

            DefaultClientAnimationLock = 0.5f;
        }
    }
}
