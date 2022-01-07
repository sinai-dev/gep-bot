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
        private static IRole helpNeededRole;

        internal static void Init()
        {
            helpNeededRole = ModdingServicesManager.ModdingDiscord.Roles.FirstOrDefault(x => x.Name == "needs-help");
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
                    user = await ModdingServicesManager.ModdingDiscord.GetUserAsync(reaction.UserId);

                Program.Log($"Handling needs-help reaction for {user.Username}");

                try
                {
                    if (!user.RoleIds.Contains(helpNeededRole.Id))
                        await user.AddRoleAsync(helpNeededRole.Id);
                }
                catch (Exception ex)
                {
                    Program.Log(ex);
                }
            }
        }
    }
}
