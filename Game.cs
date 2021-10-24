using System;
using Dalamud;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Status = FFXIVClientStructs.FFXIV.Client.Game.Status;

namespace NoClippy
{
    public static unsafe class Game
    {
        private static IntPtr animationLockPtr;
        public static ref float AnimationLock => ref *(float*)animationLockPtr;

        private static IntPtr isCastingPtr;
        public static bool IsCasting => *(bool*)isCastingPtr;

        private static IntPtr comboTimerPtr;
        public static float ComboTimer => *(float*)comboTimerPtr;

        private static IntPtr isQueuedPtr;
        public static bool IsQueued => *(bool*)isQueuedPtr;

        private static IntPtr actionCountPtr;
        public static ushort ActionCount => *(ushort*)actionCountPtr;

        private static IntPtr finishedActionCountPtr;
        public static ushort FinishedActionCount => *(ushort*)finishedActionCountPtr;

        private static IntPtr isGCDRecastActivePtr;
        public static bool IsGCDRecastActive => *(bool*)isGCDRecastActivePtr;

        private static IntPtr defaultClientAnimationLockPtr;
        public static float DefaultClientAnimationLock
        {
            get => *(float*)defaultClientAnimationLockPtr;
            set
            {
                if (defaultClientAnimationLockPtr != IntPtr.Zero)
                    SafeMemory.WriteBytes(defaultClientAnimationLockPtr, BitConverter.GetBytes(value));
            }
        }

        public delegate void UseActionEventDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp);
        public static event UseActionEventDelegate OnUseAction;
        private delegate byte UseActionDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp);
        private static Hook<UseActionDelegate> UseActionHook;
        private static byte UseActionDetour(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetedActorID, param, useType, pvp);
            if (ret > 0)
                OnUseAction?.Invoke(actionManager, actionType, actionID, targetedActorID, param, useType, pvp);
            return ret;
        }

        public delegate void UseActionLocationEventDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param);
        public static event UseActionLocationEventDelegate OnUseActionLocation;
        private delegate byte UseActionLocationDelegate(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param);
        private static Hook<UseActionLocationDelegate> UseActionLocationHook;
        private static byte UseActionLocationDetour(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param)
        {
            var ret =  UseActionLocationHook.Original(actionManager, actionType, actionID, targetedActorID, vectorLocation, param);
            if (ret > 0)
                OnUseActionLocation?.Invoke(actionManager, actionType, actionID, targetedActorID, vectorLocation, param);
            return ret;
        }

        // Not called for ground targets
        public delegate void SendActionDelegate(IntPtr a1, byte a2, int action, short sequence, long a5, long a6, long a7, long a8, long a9);
        public static event SendActionDelegate OnSendAction;
        private static Hook<SendActionDelegate> SendActionHook;
        private static void SendActionDetour(IntPtr a1, byte a2, int action, short sequence, long a5, long a6, long a7, long a8, long a9)
        {
            SendActionHook.Original(a1, a2, action, sequence, a5, a6, a7, a8, a9);
            OnSendAction?.Invoke(a1, a2, action, sequence, a5, a6, a7, a8, a9);
        }

        private static bool invokeCastInterrupt = false;
        public delegate void CastBeginDelegate(ulong objectID, IntPtr packetData);
        public static event CastBeginDelegate OnCastBegin;
        private static Hook<CastBeginDelegate> CastBeginHook;
        private static void CastBeginDetour(ulong objectID, IntPtr packetData)
        {
            CastBeginHook.Original(objectID, packetData);
            if (objectID != DalamudApi.ClientState.LocalPlayer?.ObjectId) return;
            OnCastBegin?.Invoke(objectID, packetData);
            invokeCastInterrupt = true;
        }

        // Seems to always be called twice?
        public delegate void CastInterruptDelegate(IntPtr actionManager, uint actionType, uint actionID);
        public static event CastInterruptDelegate OnCastInterrupt;
        private static Hook<CastInterruptDelegate> CastInterruptHook;
        private static void CastInterruptDetour(IntPtr actionManager, uint actionType, uint actionID)
        {
            CastInterruptHook.Original(actionManager, actionType, actionID);
            if (!invokeCastInterrupt) return;
            OnCastInterrupt?.Invoke(actionManager, actionType, actionID);
            invokeCastInterrupt = false;
        }

        public delegate void ReceiveActionEffectEventDelegate(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail, float oldLock, float newLock);
        public static event ReceiveActionEffectEventDelegate OnReceiveActionEffect;
        private delegate void ReceiveActionEffectDelegate(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private static Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;
        private static void ReceiveActionEffectDetour(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        {
            var oldLock = AnimationLock;
            ReceiveActionEffectHook.Original(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail);
            OnReceiveActionEffect?.Invoke(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail, oldLock, AnimationLock);
        }

        public delegate void UpdateStatusListEventDelegate(StatusList statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID);
        public static event UpdateStatusListEventDelegate OnUpdateStatusList;
        private delegate void UpdateStatusDelegate(IntPtr status, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID, bool individualUpdate);
        //public static event UpdateStatusDelegate OnUpdateStatus;
        private static Hook<UpdateStatusDelegate> UpdateStatusHook;
        private static void UpdateStatusDetour(IntPtr statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID, bool individualUpdate)
        {
            var statusPtr = (Status*)(statusList + 0x8 + 0xC * slot);
            var oldStatusID = statusPtr->StatusID;
            var oldSourceID = statusPtr->SourceID;
            UpdateStatusHook.Original(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (DalamudApi.ClientState.LocalPlayer is not { } p || statusList.ToInt64() != p.StatusList.Address.ToInt64()) return;

            //OnUpdateStatus?.Invoke(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (statusID != 0 && (oldStatusID != statusID || oldSourceID != sourceID))
                OnUpdateStatusList?.Invoke(p.StatusList, slot, statusID, remainingTime, stackParam, sourceID);

            if (!individualUpdate && slot == p.StatusList.Length - 1)
                OnUpdateStatusList?.Invoke(p.StatusList, -1, 0, 0, 0, 0);
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
            finishedActionCountPtr = actionManager + 0x112;
            isGCDRecastActivePtr = actionManager + 0x610;
            // 0x614 is previous gcd skill, 0x618 is current gcd recast time (counts up), 0x61C is gcd recast (counted up to)

            UseActionHook = new Hook<UseActionDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 9F BC 76 02 00"), UseActionDetour);
            UseActionLocationHook = new Hook<UseActionLocationDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 5C 24 50 0F B6 F0"), UseActionLocationDetour);
            SendActionHook = new Hook<SendActionDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? F3 0F 10 3D ?? ?? ?? ?? 48 8D 4D BF"), SendActionDetour); // Found inside UseActionLocation
            CastBeginHook = new Hook<CastBeginDelegate>(DalamudApi.SigScanner.ScanText("40 55 56 48 81 EC A8 00 00 00 48 8B EA"), CastBeginDetour); // Bad sig, found within ActorCast packet
            CastInterruptHook = new Hook<CastInterruptDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 30 0F 57 C0"), CastInterruptDetour); // Found inside ActorControl (15) packet
            ReceiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 8D F0 03 00 00"), ReceiveActionEffectDetour); // 4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9
            UpdateStatusHook = new Hook<UpdateStatusDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? FF C6 48 8D 5B 0C"), UpdateStatusDetour);

            defaultClientAnimationLockPtr = DalamudApi.SigScanner.ScanModule("33 33 B3 3E ?? ?? ?? ?? ?? ?? 00 00 00 3F") + 0xA;

            // This is normally 0.5f but it causes the client to be sanity checked at high ping, so I'm increasing it to see clips better and see higher pings more accurately
            DefaultClientAnimationLock = 0.6f;

            DalamudApi.GameNetwork.NetworkMessage += NetworkMessage;

            UseActionHook.Enable();
            UseActionLocationHook.Enable();
            SendActionHook.Enable();
            CastBeginHook.Enable();
            CastInterruptHook.Enable();
            ReceiveActionEffectHook.Enable();
            UpdateStatusHook.Enable();
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
            SendActionHook.Dispose();
            OnSendAction = null;
            CastBeginHook.Dispose();
            OnCastBegin = null;
            CastInterruptHook.Dispose();
            OnCastInterrupt = null;
            ReceiveActionEffectHook?.Dispose();
            OnReceiveActionEffect = null;
            UpdateStatusHook?.Dispose();
            OnUpdateStatusList = null;
            //OnUpdateStatus = null;

            OnUpdate = null;

            DefaultClientAnimationLock = 0.5f;
        }
    }
}
