using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot.Modding
{
    public static class ModdingServicesManager
    {
        internal static IGuild ModdingDiscord;

        public static void Init()
        {
            Program.Log($"Initializing Modding Discord services...");

            ModdingDiscord = GepBot.DiscordClient.GetGuild(DiscordUtils.OUTWARD_MODDING_DISCORD_ID);

            ReactionRoles.Init();
            GithubManager.Init();
            IDReservationManager.Init();
        }
    }
}
