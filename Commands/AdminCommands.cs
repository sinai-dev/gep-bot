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
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        //[Command("toptemp")]
        //[Summary("temp command")]
        //[RequireBotPermission(ChannelPermission.SendMessages)]
        //public async Task Temp()
        //{
        //    await TopBuildsManager.SendTopBuildQuickLinks();
        //}

        [Command("purge")]
        [Summary("Bulk deletes messages in chat")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task PurgeMod(int delNumber)
        {
            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            IEnumerable<IMessage> items = await channel.GetMessagesAsync(delNumber + 1).FlattenAsync();
            await channel.DeleteMessagesAsync(items);
        }

        [Command("purge")]
        [Summary("Bulk deletes messages in chat")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireOwner]
        public async Task PurgeOwner(int delNumber)
        {
            SocketTextChannel channel = Context.Channel as SocketTextChannel;
            IEnumerable<IMessage> items = await channel.GetMessagesAsync(delNumber + 1).FlattenAsync();
            await channel.DeleteMessagesAsync(items);
        }
    }
    
}
