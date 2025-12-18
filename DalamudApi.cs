global using Dalamud;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// ReSharper disable CheckNamespace
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Dalamud;

public class DalamudApi
{
    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; }

    [PluginService]
    public static IChatGui ChatGui { get; private set; }

    [PluginService]
    public static IClientState ClientState { get; private set; }

    [PluginService]
    public static ICommandManager CommandManager { get; private set; }

    [PluginService]
    public static ICondition Condition { get; private set; }

    [PluginService]
    public static IFramework Framework { get; private set; }

    [PluginService]
    public static IGameInteropProvider GameInteropProvider { get; private set; }

    [PluginService]
    public static INotificationManager NotificationManager { get; private set; }


    [PluginService]
    public static IObjectTable ObjectTable { get; private set; }

    [PluginService]
    public static IPluginLog PluginLog { get; private set; }

    [PluginService]
    public static ISigScanner SigScanner { get; private set; }

    [PluginService]
    public static IToastGui ToastGui { get; private set; }

    private static PluginCommandManager<IDalamudPlugin> pluginCommandManager;
    private const string printName = "NoClippy";
    private const string printHeader = $"[{printName}] ";

    public DalamudApi() { }

    public DalamudApi(IDalamudPlugin plugin) => pluginCommandManager ??= new(plugin);

    public DalamudApi(IDalamudPlugin plugin, IDalamudPluginInterface pluginInterface)
    {
        if (!pluginInterface.Inject(this))
        {
            LogError("Failed loading DalamudApi!");
            return;
        }

        pluginCommandManager ??= new(plugin);
    }

    public static DalamudApi operator +(DalamudApi container, object o)
    {
        foreach (var f in typeof(DalamudApi).GetProperties())
        {
            if (f.PropertyType != o.GetType()) continue;
            if (f.GetValue(container) != null) break;
            f.SetValue(container, o);
            return container;
        }
        throw new InvalidOperationException();
    }

    public static void PrintEcho(string message) => ChatGui.Print($"{printHeader}{message}");

    public static void PrintError(string message) => ChatGui.PrintError($"{printHeader}{message}");

    public static void ShowNotification(string message, NotificationType type = NotificationType.None, uint msDelay = 3_000u) => NotificationManager.AddNotification(new Notification { Type = type, Title = printName, Content = message, InitialDuration = TimeSpan.FromMilliseconds(msDelay) });

    public static void ShowToast(string message, ToastOptions options = null) => ToastGui.ShowNormal($"{printHeader}{message}", options);

    public static void ShowQuestToast(string message, QuestToastOptions options = null) => ToastGui.ShowQuest($"{printHeader}{message}", options);

    public static void ShowErrorToast(string message) => ToastGui.ShowError($"{printHeader}{message}");

    public static void LogVerbose(string message, Exception exception = null) => PluginLog.Verbose(exception, message);

    public static void LogDebug(string message, Exception exception = null) => PluginLog.Debug(exception, message);

    public static void LogInfo(string message, Exception exception = null) => PluginLog.Information(exception, message);

    public static void LogWarning(string message, Exception exception = null) => PluginLog.Warning(exception, message);

    public static void LogError(string message, Exception exception = null) => PluginLog.Error(exception, message);

    public static void LogFatal(string message, Exception exception = null) => PluginLog.Fatal(exception, message);

    public static void Initialize(IDalamudPlugin plugin, IDalamudPluginInterface pluginInterface) => _ = new DalamudApi(plugin, pluginInterface);

    public static void Dispose() => pluginCommandManager?.Dispose();
}

#region PluginCommandManager
public class PluginCommandManager<T> : IDisposable where T : IDalamudPlugin
{
    private readonly T plugin;
    private readonly (string, CommandInfo)[] pluginCommands;

    public PluginCommandManager(T p)
    {
        plugin = p;
        pluginCommands = plugin.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
            .SelectMany(GetCommandInfoTuple)
            .ToArray();

        AddCommandHandlers();
    }

    private void AddCommandHandlers()
    {
        foreach (var (command, commandInfo) in pluginCommands)
            DalamudApi.CommandManager.AddHandler(command, commandInfo);
    }

    private void RemoveCommandHandlers()
    {
        foreach (var (command, _) in pluginCommands)
            DalamudApi.CommandManager.RemoveHandler(command);
    }

    private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
    {
        var handlerDelegate = (IReadOnlyCommandInfo.HandlerDelegate)Delegate.CreateDelegate(typeof(IReadOnlyCommandInfo.HandlerDelegate), plugin, method);

        var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
        var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
        var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
        var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

        var commandInfo = new CommandInfo(handlerDelegate)
        {
            HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
            ShowInHelp = doNotShowInHelp == null,
        };

        // Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
        var commandInfoTuples = new List<(string, CommandInfo)> { (command?.Command, commandInfo) };
        if (aliases != null)
            commandInfoTuples.AddRange(aliases.Aliases.Select(alias => (alias, commandInfo)));

        return commandInfoTuples;
    }

    public void Dispose()
    {
        RemoveCommandHandlers();
        GC.SuppressFinalize(this);
    }
}
#endregion

#region Attributes
[AttributeUsage(AttributeTargets.Method)]
public class AliasesAttribute : Attribute
{
    public string[] Aliases { get; }

    public AliasesAttribute(params string[] aliases)
    {
        Aliases = aliases;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute
{
    public string Command { get; }

    public CommandAttribute(string command)
    {
        Command = command;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class DoNotShowInHelpAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class HelpMessageAttribute : Attribute
{
    public string HelpMessage { get; }

    public HelpMessageAttribute(string helpMessage)
    {
        HelpMessage = helpMessage;
    }
}
#endregion