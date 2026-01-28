using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace NoClippy
{
    public static unsafe class Game
    {
        public static Structures.ActionManager* actionManager;

        public const float DefaultClientAnimationLock = 0.5f;

        public delegate void UseActionEventDelegate(ActionManager* actionManager, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted, bool ret);
        public static event UseActionEventDelegate OnUseAction;

        private static Hook<ActionManager.Delegates.UseAction> UseActionHook;

        private static bool UseActionDetour(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
        {
            var ret = UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
            OnUseAction?.Invoke(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted, ret);
            return ret;
        }

        public delegate void UseActionLocationEventDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte ret);
        public static event UseActionLocationEventDelegate OnUseActionLocation;
        private delegate byte UseActionLocationDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte c);
        private static Hook<UseActionLocationDelegate> UseActionLocationHook;
        private static byte UseActionLocationDetour(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte c)
        {
            var ret =  UseActionLocationHook.Original(actionManager, actionType, actionID, targetedActorID, vectorLocation, param, c);
            OnUseActionLocation?.Invoke(actionManager, actionType, actionID, targetedActorID, vectorLocation, param, ret);
            return ret;
        }

        private static bool invokeCastInterrupt = false;
        public delegate void CastBeginDelegate(ulong objectID, nint packetData);
        public static event CastBeginDelegate OnCastBegin;
        private static Hook<CastBeginDelegate> CastBeginHook;
        private static void CastBeginDetour(ulong objectID, nint packetData)
        {
            CastBeginHook.Original(objectID, packetData);
            if (objectID != DalamudApi.ObjectTable.LocalPlayer?.GameObjectId) return;
            OnCastBegin?.Invoke(objectID, packetData);
            invokeCastInterrupt = true;
        }

        // Seems to always be called twice?
        public delegate void CastInterruptDelegate(nint actionManager);
        public static event CastInterruptDelegate OnCastInterrupt;
        private static Hook<CastInterruptDelegate> CastInterruptHook;
        private static void CastInterruptDetour(nint actionManager)
        {
            CastInterruptHook.Original(actionManager);
            if (!invokeCastInterrupt) return;
            OnCastInterrupt?.Invoke(actionManager);
            invokeCastInterrupt = false;
        }

        public delegate void ReceiveActionEffectEventDelegate(uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds, float oldLock, float newLock);
        public static event ReceiveActionEffectEventDelegate OnReceiveActionEffect;
        private static Hook<ActionEffectHandler.Delegates.Receive> ReceiveActionEffectHook;

        private static void ReceiveActionEffectDetour(uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetEntityIds)
        {
            var oldLock = actionManager->animationLock;
            ReceiveActionEffectHook.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);
            OnReceiveActionEffect?.Invoke(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds, oldLock, actionManager->animationLock);
        }

        private static Hook<ZoneClient.Delegates.SendPacket> SendPacketHook;

        public delegate void NetworkMessageEventDelegate();
        public static event NetworkMessageEventDelegate OnNetworkMessageDelegate;


        private static bool SendPacketDetour(ZoneClient* zoneClient, nint packet, uint a3, uint a4, bool a5)
        {
            OnNetworkMessageDelegate?.Invoke();
            return SendPacketHook.Original(zoneClient, packet, a3, a4, a5);
        }

        public static void Initialize()
        {
            actionManager = (Structures.ActionManager*)ActionManager.Instance();

            UseActionHook = DalamudApi.GameInteropProvider.HookFromAddress<ActionManager.Delegates.UseAction>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
            UseActionLocationHook = DalamudApi.GameInteropProvider.HookFromAddress<UseActionLocationDelegate>((nint)ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
            CastBeginHook = DalamudApi.GameInteropProvider.HookFromAddress<CastBeginDelegate>(DalamudApi.SigScanner.ScanText("40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B D1"), CastBeginDetour); // Bad sig, found within ActorCast packet
            CastInterruptHook = DalamudApi.GameInteropProvider.HookFromAddress<CastInterruptDelegate>(DalamudApi.SigScanner.ScanText("48 8B C4 48 83 EC 48 48 89 58 08"), CastInterruptDetour);
            ReceiveActionEffectHook = DalamudApi.GameInteropProvider.HookFromAddress<ActionEffectHandler.Delegates.Receive>(ActionEffectHandler.Addresses.Receive.Value, ReceiveActionEffectDetour);
            SendPacketHook = DalamudApi.GameInteropProvider.HookFromAddress<ZoneClient.Delegates.SendPacket>((nint)ZoneClient.MemberFunctionPointers.SendPacket, SendPacketDetour);

            UseActionHook.Enable();
            UseActionLocationHook.Enable();
            CastBeginHook.Enable();
            CastInterruptHook.Enable();
            ReceiveActionEffectHook.Enable();
            SendPacketHook.Enable();
        }

        public static event Action OnUpdate;
        public static void Update() => OnUpdate?.Invoke();

        public static void Dispose()
        {
            UseActionHook?.Dispose();
            OnUseAction = null;
            UseActionLocationHook?.Dispose();
            OnUseActionLocation = null;
            CastBeginHook?.Dispose();
            OnCastBegin = null;
            CastInterruptHook?.Dispose();
            OnCastInterrupt = null;
            ReceiveActionEffectHook?.Dispose();
            OnReceiveActionEffect = null;
            SendPacketHook?.Dispose();
            OnNetworkMessageDelegate = null;

            OnUpdate = null;
        }
    }
}
