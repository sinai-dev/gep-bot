using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot.Modding
{
    public static class ReactionRoles
    {
        internal static IGuild moddingDiscordGuild;
        private static IRole helpNeededRole;

        static ReactionRoles()
        {
            moddingDiscordGuild = BotManager.DiscordClient.GetGuild(DiscordUtils.OUTWARD_MODDING_DISCORD_ID);

            helpNeededRole = moddingDiscordGuild.Roles.FirstOrDefault(x => x.Name == "needs-help");

            if (helpNeededRole == null)
            {
                Console.WriteLine("ITS NULL");
            }
        }

        internal static async Task OnReaction(Cacheable<IUserMessage, ulong> origMsg, 
                                              Cacheable<IMessageChannel, ulong> channel, 
                                              SocketReaction reaction)
        {
            // Troubleshooting mods message
            if (origMsg.Id == 927460457474830336)
            {
                IGuildUser user;
                if (reaction.User.Value != null)
                    user = (IGuildUser)reaction.User.Value;
                else
                    user = await moddingDiscordGuild.GetUserAsync(reaction.UserId);

                try
                {
                    if (!user.RoleIds.Contains(helpNeededRole.Id))
                        await user.AddRoleAsync(helpNeededRole.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
