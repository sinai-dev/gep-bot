using Discord.Commands;
using GepBot.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot.Commands
{
    public class ModdingCommands : ModuleBase<SocketCommandContext>
    {
        [Command("reserve")]
        public async Task ReserveItemOrStatus()
        {
            if (Context.Guild.Id != DiscordUtils.OUTWARD_MODDING_DISCORD_ID
                || Context.Channel.Id != IDReservationManager.reserveIdsChannel.Id)
                return;

            await IDReservationManager.Cmd_RequestRangeReservation(Context.User, IDReservationManager.ReservationType.ItemOrStatus);
        }

        [Command("reserve-photon")]
        public async Task ReservePhotonViewId()
        {
            if (Context.Guild.Id != DiscordUtils.OUTWARD_MODDING_DISCORD_ID
                || Context.Channel.Id != IDReservationManager.reserveIdsChannel.Id)
                return;

            await IDReservationManager.Cmd_RequestRangeReservation(Context.User, IDReservationManager.ReservationType.PhotonView);
        }

        [Command("confirm")]
        public async Task ConfirmReservation()
        {
            if (Context.Guild.Id != DiscordUtils.OUTWARD_MODDING_DISCORD_ID
                || Context.Channel.Id != IDReservationManager.reserveIdsChannel.Id)
                return;

            await IDReservationManager.Cmd_Confirm(Context.User.Username);
        }
    }
}
