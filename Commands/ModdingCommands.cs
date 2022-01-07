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
        bool IsWrongChannel 
            => Context.Guild.Id != DiscordUtils.OUTWARD_MODDING_DISCORD_ID
            || Context.Channel.Id != IDReservationManager.reserveIdsChannel.Id;

        [Command("reserve")]
        public async Task ReserveItemOrStatus()
        {
            if (IsWrongChannel)
                return;

            await IDReservationManager.Cmd_RequestRangeReservation(Context.User, ReservationType.ItemOrStatus);
        }

        [Command("reserve-photon")]
        public async Task ReservePhotonViewId()
        {
            if (IsWrongChannel)
                return;

            await IDReservationManager.Cmd_RequestRangeReservation(Context.User, ReservationType.PhotonView);
        }

        [Command("confirm")]
        public async Task ConfirmReservation()
        {
            if (IsWrongChannel)
                return;

            await IDReservationManager.Cmd_Confirm(Context.User.Username);
        }

        [Command("listreserved")]
        public async Task GetItemOrStatusRanges()
        {
            if (IsWrongChannel)
                return;

            await IDReservationManager.Cmd_GetRanges(ReservationType.ItemOrStatus);
        }

        [Command("listreserved-photon")]
        public async Task GetPhotonRanges()
        {
            if (IsWrongChannel)
                return;

            await IDReservationManager.Cmd_GetRanges(ReservationType.PhotonView);
        }

    }
}
