using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace GepBot
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
            await BotManager.UpdateTopBuilds();
        }
    }
}
