using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace GepBot.Commands
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("hellogep")] 
        [Summary("Say hello to Gep")] 
        public async Task Hello()
        {
            string userName = Context.User.Username;

            string[] options = new string[]
            {
                $"Greetings friend, **{userName}**!",
                $"This worries me, **{userName}**!",
                $"Blessing of Elatt upon you, **{userName}**!",
                $"Hey there, **{userName}**!",
                $"Amba my friend, **{userName}**!",
            };

            await ReplyAsync(options[new Random().Next(0, options.Length)]);
        }

        [Command("updatetop")]
        [Summary("Update the 'top builds' messages")]
        public async Task UpdateTopBuilds()
        {
            await TopBuildsManager.UpdateTopBuilds();
            await ReplyAsync("Top builds posts have been updated manually.");
        }

        [Command("updatebuild")]
        [Summary("Updated the build post with the supplied message link")]
        public async Task UpdateBuild(string messageLink)
        {
            var result = await BuildPostManager.UpdateBuildMessage(messageLink);
            await ReplyAsync(result);
        }

        [Command("relink")]
        [Summary("Re-link a build to a new wiki page, if it was moved.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task RelinkBuildModsOnly(string messageLink, string newWikiLink)
        {
            var result = await BuildPostManager.RelinkBuildPost(messageLink, newWikiLink);
            await ReplyAsync(result);
        }

        [Command("relink")]
        [Summary("Re-link a build to a new wiki page, if it was moved.")]
        [RequireOwner]
        public async Task RelinkBuild(string messageLink, string newWikiLink)
        {
            var result = await BuildPostManager.RelinkBuildPost(messageLink, newWikiLink);
            await ReplyAsync(result);
        }
    }
}
