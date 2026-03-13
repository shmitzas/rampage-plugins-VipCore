using SwiftlyS2.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Services;

namespace VIPCore;

public sealed partial class VIPCore
{
    [Command("vip_adduser", registerRaw: true, permission: "vipcore.adduser")]
    [CommandAlias("vipadd", registerRaw: true)]
    public void OnAddUserCommand(ICommandContext context)
    {
        if (_serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        if (context.Args.Length < 3)
        {
            context.Reply("Usage: vip_adduser <steamid> <group> <time>");
            return;
        }

        if (!long.TryParse(context.Args[0], out var steamId))
        {
            context.Reply($"Invalid SteamID format: {context.Args[0]}");
            return;
        }

        var group = context.Args[1];

        if (!int.TryParse(context.Args[2], out var time))
        {
            context.Reply($"Invalid time format: {context.Args[2]}");
            return;
        }

        var vipService = _serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.AddVip(steamId, "unknown", group, time);

                Core.Scheduler.NextTick(() =>
                {
                    var target = FindOnlinePlayerBySteamId((ulong)steamId);
                    if (target != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await vipService.LoadPlayer(target);
                            }
                            catch (Exception loadEx)
                            {
                                Core.Logger.LogError(loadEx, "[VIPCore] Failed to load newly added VIP player {SteamId}", steamId);
                            }
                        });
                    }
                });

                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Added VIP: {steamId} to group {group}");
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Failed to add VIP: {ex.Message}");
                });
            }
        });
    }
}
