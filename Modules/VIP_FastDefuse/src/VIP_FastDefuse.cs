using System;
using System.Collections.Generic;
using System.Reflection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_FastDefuse;

[PluginMetadata(Id = "VIP_FastDefuse", Version = "1.0.0", Name = "VIP_FastDefuse", Author = "aga", Description = "No description.")]
public partial class VIP_FastDefuse : BasePlugin {
  private const string FeatureKey = "vip.fastdefuse";
  private const float DefaultDefuseTimeNoKitSeconds = 10.0f;
  private const float DefaultDefuseTimeKitSeconds = 5.0f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private long _defuseTokenCounter;
  private readonly Dictionary<uint, long> _defuseTokenByControllerIndex = new();

  public VIP_FastDefuse(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;
    _isFeatureRegistered = false;

    try
    {
      if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
        _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

      RegisterVipFeaturesWhenReady();
    }
    catch
    {
    }
  }

  public override void Load(bool hotReload) {
    Core.GameEvent.HookPre<EventBombBegindefuse>(OnBombBeginDefuse);
    Core.GameEvent.HookPost<EventBombDefused>(OnBombDefused);
    Core.GameEvent.HookPost<EventBombAbortdefuse>(OnBombAbortDefuse);
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      if (_isFeatureRegistered)
        _vipApi.UnregisterFeature(FeatureKey);
    }
  }

  private void RegisterVipFeaturesWhenReady()
  {
    if (_vipApi == null) return;

    if (_vipApi.IsCoreReady())
      RegisterVipFeatures();
    else
      _vipApi.OnCoreReady += RegisterVipFeatures;
  }

  private void RegisterVipFeatures()
  {
    if (_vipApi == null || _isFeatureRegistered) return;

    _vipApi.RegisterFeature(
      FeatureKey,
      FeatureType.Toggle,
      null,
      displayNameResolver: p => "Fast Defuse"
    );

    _isFeatureRegistered = true;
  }

  private HookResult OnBombBeginDefuse(EventBombBegindefuse @event)
  {
    if (_vipApi == null) return HookResult.Continue;

    var defuserController = @event.UserIdController;
    if (defuserController == null || !defuserController.IsValid) return HookResult.Continue;

    var controller = defuserController;
    if (!controller.PawnIsAlive) return HookResult.Continue;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return HookResult.Continue;

    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid)
      player = Core.PlayerManager.GetPlayerFromPawn(pawn);

    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

    var pawnBase = pawn as CCSPlayerPawnBase;
    if (pawnBase == null) return HookResult.Continue;

    var plantedC4 = GetPlantedC4();

    if (plantedC4 == null || !plantedC4.IsValid || plantedC4.CannotBeDefused) return HookResult.Continue;

    var currentTime = Core.Engine.GlobalVars.CurrentTime;
    var defaultDefuseSeconds = @event.HasKit ? DefaultDefuseTimeKitSeconds : DefaultDefuseTimeNoKitSeconds;
    var remaining = defaultDefuseSeconds;

    try
    {
      var defuseCountdown = plantedC4.DefuseCountDown.Value;
      if (defuseCountdown > currentTime)
        remaining = defuseCountdown - currentTime;
    }
    catch
    {
    }

    if (remaining <= 0)
      remaining = defaultDefuseSeconds;

    var multiplier = 0.5f;
    var secondsOverride = 0.0f;
    try
    {
      var config = _vipApi.GetFeatureValue<FastDefuseConfig>(player, FeatureKey);
      if (config != null)
      {
        if (config.Multiplier > 0)
        {
          if (config.Multiplier <= 1.0f)
            multiplier = config.Multiplier;
          else
            multiplier = 1.0f / config.Multiplier;
        }
        if (config.Seconds > 0)
          secondsOverride = config.Seconds;
      }
    }
    catch
    {
    }

    var newRemaining = secondsOverride > 0 ? secondsOverride : (remaining * multiplier);
    if (newRemaining < 0.25f)
      newRemaining = 0.25f;

    var token = ++_defuseTokenCounter;
    _defuseTokenByControllerIndex[controller.Index] = token;

    Core.Scheduler.NextTick(() =>
    {
      if (controller == null || !controller.IsValid) return;
      if (_defuseTokenByControllerIndex.TryGetValue(controller.Index, out var t) && t != token) return;

      if (plantedC4 == null || !plantedC4.IsValid) return;

      void ApplyNow()
      {
        if (controller == null || !controller.IsValid) return;
        if (_defuseTokenByControllerIndex.TryGetValue(controller.Index, out var t2) && t2 != token) return;
        if (plantedC4 == null || !plantedC4.IsValid) return;

        plantedC4.DefuseLength = newRemaining;
        plantedC4.DefuseLengthUpdated();

        plantedC4.DefuseCountDown.Value = Core.Engine.GlobalVars.CurrentTime + newRemaining;
        plantedC4.DefuseCountDownUpdated();

        TrySetMember(plantedC4, "LastDefuseTime", Core.Engine.GlobalVars.CurrentTime);
        TrySetMember(plantedC4, "FLastDefuseTime", Core.Engine.GlobalVars.CurrentTime);
        TrySetMember(plantedC4, "ProgressBarTime", (int)MathF.Ceiling(newRemaining));
        TrySetMember(plantedC4, "IProgressBarTime", (int)MathF.Ceiling(newRemaining));

        pawnBase.ProgressBarStartTime = Core.Engine.GlobalVars.CurrentTime;
        pawnBase.ProgressBarStartTimeUpdated();

        pawnBase.ProgressBarDuration = Math.Max(1, (int)MathF.Ceiling(newRemaining));
        pawnBase.ProgressBarDurationUpdated();
      }

      ApplyNow();

      for (var i = 1; i <= 10; i++)
      {
        var delay = i * 0.05f;
        Core.Scheduler.DelayBySeconds(delay, ApplyNow);
      }
    });

    return HookResult.Continue;
  }

  private HookResult OnBombDefused(EventBombDefused @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;

    _defuseTokenByControllerIndex.Remove(controller.Index);
    ResetProgressBar(controller);

    return HookResult.Continue;
  }

  private HookResult OnBombAbortDefuse(EventBombAbortdefuse @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;

    _defuseTokenByControllerIndex.Remove(controller.Index);
    ResetProgressBar(controller);

    return HookResult.Continue;
  }

  private CPlantedC4? GetPlantedC4()
  {
    try
    {
      foreach (var bomb in Core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4"))
      {
        if (bomb != null)
          return bomb;
      }
    }
    catch
    {
    }

    return null;
  }

  private void ResetProgressBar(CCSPlayerController controller)
  {
    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return;

    var pawnBase = pawn as CCSPlayerPawnBase;
    if (pawnBase == null) return;

    pawnBase.ProgressBarStartTime = 0;
    pawnBase.ProgressBarStartTimeUpdated();

    pawnBase.ProgressBarDuration = 0;
    pawnBase.ProgressBarDurationUpdated();
  }

  private static void TrySetMember(object target, string name, object value)
  {
    try
    {
      var type = target.GetType();

      var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (prop != null && prop.CanWrite)
      {
        prop.SetValue(target, ChangeType(value, prop.PropertyType));
        TryInvokeUpdated(target, name + "Updated");
        return;
      }

      var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (field != null)
      {
        field.SetValue(target, ChangeType(value, field.FieldType));
        TryInvokeUpdated(target, name + "Updated");
      }
    }
    catch
    {
    }
  }

  private static object ChangeType(object value, Type targetType)
  {
    try
    {
      if (targetType.IsInstanceOfType(value)) return value;
      return Convert.ChangeType(value, targetType);
    }
    catch
    {
      return value;
    }
  }

  private static void TryInvokeUpdated(object target, string methodName)
  {
    try
    {
      var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      method?.Invoke(target, null);
    }
    catch
    {
    }
  }
}

public class FastDefuseConfig
{
  public float Multiplier { get; set; } = 0.5f;
  public float Seconds { get; set; } = 0.0f;
}