using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VIPCore.Services;

namespace VIPCore;

public sealed partial class VIPCore
{
    [Command("vip")]
    public void OnVipCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players!");
            return;
        }

        var serviceProvider = _serviceProvider;
        if (serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        var player = context.Sender!;
        if (player == null) return;

        var vipService = serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.LoadPlayer(player);
                var vipUser = vipService.GetVipUser(player.SteamID);

                Core.Scheduler.NextTick(() =>
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(player);

                    if (vipUser == null)
                    {
                        player.SendMessage(MessageType.Chat, localizer["vip.NoAccess"]);
                        return;
                    }

                    var menuService = serviceProvider.GetRequiredService<MenuService>();
                    menuService.OpenVipMenu(player, vipUser);
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to load VIP data for player.");
            }
        });
    }
}
