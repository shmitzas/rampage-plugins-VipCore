using SwiftlyS2.Shared.Players;

namespace VIPCore.Contract;

public interface IVipCoreApiV1
{
    public void RegisterFeature(string featureKey, FeatureType featureType = FeatureType.Toggle, Action<IPlayer, FeatureState>? onSelectItem = null, Func<IPlayer, string>? displayNameResolver = null);
    public void UnregisterFeature(string featureKey);

    public T GetPlayerCookie<T>(IPlayer player, string key);
    public void SetPlayerCookie<T>(IPlayer player, string key, T value);

    public IEnumerable<string> GetAllRegisteredFeatures();

    public FeatureState GetPlayerFeatureState(IPlayer player, string featureKey);
    public void SetPlayerFeatureState(IPlayer player, string featureKey, FeatureState newState);

    public void DisableAllFeatures();
    public void EnableAllFeatures();

    public bool IsClientVip(IPlayer player);
    public bool PlayerHasFeature(IPlayer player, string featureKey);

    /// <summary>
    /// Grants VIP to a player. Time interpretation depends on the server's TimeMode setting.
    /// If the player is already VIP, this does nothing.
    /// </summary>
    public void GiveClientVip(IPlayer player, string group, int time);

    /// <summary>
    /// Removes VIP from a player. Does nothing if the player is not VIP.
    /// </summary>
    public void RemoveClientVip(IPlayer player);

    /// <summary>
    /// Overrides the player's active VIP group in memory only. No database write.
    /// Works for both VIP and non-VIP players. For non-VIP players a temporary
    /// in-memory entry is created; clearing the override via ClearClientVipGroupOverride
    /// will remove it. Feature states are re-initialized for the override group.
    /// </summary>
    public void OverrideClientVipGroup(IPlayer player, string group);

    /// <summary>
    /// Clears any in-memory group override and reloads the player's real group from the database.
    /// </summary>
    public void ClearClientVipGroupOverride(IPlayer player);

    public bool IsCoreReady();

    public string GetClientVipGroup(IPlayer player);
    public string[] GetClientVipGroups(IPlayer player);
    public string[] GetVipGroups();
    public int GetVipGroupWeight(string group);

    /// <summary>
    /// Reads the feature's config value from the player's VIP group and binds it to T.
    /// Any module can define its own settings class — VIPCore will auto-map the JSON properties.
    /// Returns default(T) if the player is not VIP or the feature has no config section.
    /// </summary>
    public T? GetFeatureValue<T>(IPlayer player, string featureKey) where T : class, new();

    public event Action<IPlayer>? OnPlayerSpawn;
    public event Action<IPlayer, string>? PlayerLoaded;
    public event Action<IPlayer, string>? PlayerRemoved;
    public event Action? OnCoreReady;
    public event Func<IPlayer, string, FeatureState, FeatureType, bool?>? OnPlayerUseFeature;
}
