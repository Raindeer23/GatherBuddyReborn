global using SharableRecord = ((uint ItemId, ushort Size, ushort BiteTime, byte Amount, byte Tug, bool Collectible, bool Large) Catch,
    (int Timestamp, uint BaitItemId, uint ContentIdHash, ushort FishingSpotId, ushort Gathering, ushort Perception, byte HookSet) Cast,
    (bool Snagging, bool Chum, bool Intuition, bool FishEyes, bool IdenticalCast, bool SurfaceSlap, bool PrizeCatch, bool Patience, bool
    Patience2, bool BigGameFishing, byte AmbitiousLure, byte ModestLure) Effects);
using Dalamud.Plugin.Ipc;
using System.Numerics;
using GatherBuddy.Classes;
using GatherBuddy.Config;
using GatherBuddy.Models;
using ElliLib.Text;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using GatherBuddy.FishTimer;
using GatherBuddy.Gui;
using GatherBuddy.Interfaces;
using GatherBuddy.Time;

namespace GatherBuddy.Plugin;

public sealed class GatherBuddyIpc : IDisposable
{
    public const int    IpcVersion          = 2;
    public const int    IpcVersionMinor     = 1;
    public const string InitializedName     = $"{GatherBuddy.InternalName}.Initialized";
    public const string DisposedName        = $"{GatherBuddy.InternalName}.Disposed";
    public const string VersionName         = $"{GatherBuddy.InternalName}.Version";
    public const string VersionNameV2       = $"{GatherBuddy.InternalName}.Version.V2";
    public const string IdentifyName        = $"{GatherBuddy.InternalName}.Identify";
    public const string DrawFishTooltipName = $"{GatherBuddy.InternalName}.DrawFishToolitp";
    public const string RecordCreatedName   = $"{GatherBuddy.InternalName}.RecordCreated";
    public const string QueryUptimesName    = $"{GatherBuddy.InternalName}.QueryUptimes";

    private readonly  GatherBuddy                                                              _plugin;
    internal readonly ICallGateProvider<(int Major, int Minor)>?                               _versionProviderV2;
    internal readonly ICallGateProvider<uint, uint?, Vector2, Vector2, Vector2, bool, object>? _drawFishTooltip;
    internal readonly ICallGateProvider<SharableRecord, object>?                               _recordCreated;
    internal readonly ICallGateProvider<int, int, object>?                                     _initialized;
    internal readonly ICallGateProvider<object>?                                               _disposed;
    internal readonly ICallGateProvider<uint, byte, IEnumerable<(DateTimeOffset, TimeSpan)>>?  _queryUptimes;

    public GatherBuddyIpc(GatherBuddy plugin)
    {
        _plugin = plugin;
        EzIPC.Init(this, GatherBuddy.InternalName);
        Debug.Assert(AutoGatherWaiting != null);
        Debug.Assert(AutoGatherEnabledChanged != null);

        try
        {
            _versionProviderV2 = Dalamud.PluginInterface.GetIpcProvider<(int Major, int Minor)>(VersionNameV2);
            _versionProviderV2.RegisterFunc(VersionV2);
        }
        catch (Exception e)
        {
            _versionProviderV2 = null;
            GatherBuddy.Log.Error($"Could not obtain provider for {VersionNameV2}:\n{e}");
        }

        try
        {
            _drawFishTooltip =
                Dalamud.PluginInterface.GetIpcProvider<uint, uint?, Vector2, Vector2, Vector2, bool, object>(DrawFishTooltipName);
            _drawFishTooltip.RegisterAction(DrawTooltip);
        }
        catch (Exception e)
        {
            _drawFishTooltip = null;
            GatherBuddy.Log.Error($"Could not obtain provider for {DrawFishTooltipName}:\n{e}");
        }

        try
        {
            _queryUptimes =
                Dalamud.PluginInterface.GetIpcProvider<uint, byte, IEnumerable<(DateTimeOffset Start, TimeSpan Length)>>(QueryUptimesName);
            _queryUptimes.RegisterFunc(QueryUptimes);
        }
        catch (Exception e)
        {
            _queryUptimes = null;
            GatherBuddy.Log.Error($"Could not obtain provider for {QueryUptimesName}:\n{e}");
        }

        _recordCreated                     =  Dalamud.PluginInterface.GetIpcProvider<SharableRecord, object>(RecordCreatedName);
        _initialized                       =  Dalamud.PluginInterface.GetIpcProvider<int, int, object>(InitializedName);
        _disposed                          =  Dalamud.PluginInterface.GetIpcProvider<object>(DisposedName);
        _plugin.FishRecorder.RecordCreated += InvokeRecordCreated;
        try
        {
            _initialized.SendMessage(IpcVersion, IpcVersionMinor);
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"Error invoking Initialized event:\n{ex}");
        }
    }

    private IEnumerable<(DateTimeOffset Start, TimeSpan Length)> QueryUptimes(uint itemId, byte numUptimes)
    {
        IGatherable? item = GatherBuddy.GameData.Fishes.TryGetValue(itemId, out var f)
            ? f
            : GatherBuddy.GameData.Gatherables.GetValueOrDefault(itemId);
        if (item is null)
            yield break;

        var uptimes = GatherBuddy.UptimeManager.GetUpcomingUptimes(item, numUptimes);
        if (uptimes.Count is 0)
            yield return (DateTimeOffset.Now, TimeSpan.MaxValue);
        else
            foreach (var uptime in uptimes)
                yield return (uptime.Start.LocalTime, TimeSpan.FromMilliseconds(uptime.Duration));
    }

    private (int Major, int Minor) VersionV2()
        => (IpcVersion, IpcVersionMinor);

    private void DrawTooltip(uint fishId, uint? territoryId, Vector2 iconSize, Vector2 smallIconSize, Vector2 weatherIconSize, bool printName)
    {
        if (!GatherBuddy.GameData.Fishes.TryGetValue(fishId, out var fish))
        {
            ImUtf8.TextFramed($"Invalid Fish #{fishId}", ColorId.WarningBg.Value());
            return;
        }

        var territory = territoryId.HasValue && GatherBuddy.GameData.Territories.TryGetValue(territoryId.Value, out var t)
            ? t
            : Territory.Invalid;

        var extendedFish = Interface.ExtendedFishList.FirstOrDefault(f => f.Data == fish) ?? new Interface.ExtendedFish(fish);
        extendedFish.SetTooltip(territory, iconSize, smallIconSize, weatherIconSize, printName, false);
    }

#pragma warning disable CA1822 // Mark members as static
    [EzIPC]
    public int Version()
        => IpcVersion;

    [EzIPC]
    public uint Identify(string text)
        => _plugin.Executor.Identificator.IdentifyGatherable(text)?.ItemId
         ?? _plugin.Executor.Identificator.IdentifyFish(text)?.ItemId ?? 0;

    [EzIPC]
    public bool IsAutoGatherEnabled()
        => GatherBuddy.AutoGather.Enabled;

    [EzIPC]
    public string GetAutoGatherStatusText()
        => GatherBuddy.AutoGather.AutoStatus;

    [EzIPC]
    public void SetAutoGatherEnabled(bool enabled)
        => GatherBuddy.AutoGather.Enabled = enabled;

    [EzIPC]
    public bool IsAutoGatherWaiting()
        => GatherBuddy.AutoGather.Waiting;

    [EzIPCEvent]
    public Action AutoGatherWaiting;

    [EzIPCEvent]
    public Action<bool> AutoGatherEnabledChanged;

#pragma warning restore CA1822 // Mark members as static

    public void Dispose()
    {
        _plugin.FishRecorder.RecordCreated -= InvokeRecordCreated;
        _versionProviderV2?.UnregisterFunc();
        _queryUptimes?.UnregisterFunc();
        _drawFishTooltip?.UnregisterAction();
        try
        {
            _disposed?.SendMessage();
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"Error invoking Disposed event:\n{ex}");
        }
    }

    private void InvokeRecordCreated(in FishRecord record)
    {
        if (_recordCreated?.SubscriptionCount is null or 0)
            return;

        if (!record.Flags.HasFlag(Effects.Valid))
            return;

        try
        {
            _recordCreated.SendMessage(record.ToSharable());
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"Error invoking RecordCreated event:\n{ex}");
        }
    }
}
