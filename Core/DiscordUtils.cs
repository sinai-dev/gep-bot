using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot
{
    public static class DiscordUtils
    {
        public static Emoji ThumbsUp { get; } = new("👍");
        public static Emote Gold { get; private set; }
        public static Emote Tsar { get; private set; }
        public static Emote Trog { get; private set; }

        public const ulong OUTWARD_MODDING_DISCORD_ID = 293810842225606656;

        public const ulong OUTWARD_DISCORD_ID = 245626447568437249;
        public const ulong TOP_BUILDS_CHANNELID = 916290943840305192;
        public const ulong POST_YOUR_BUILDS_CHANNELID = 916290836013142026;

        //public const ulong OUTWARD_BUILDS_DISCORD_ID = 913322914986737674;
        //public const ulong OUTWARD_BUILDS_TOP_BUILDS_CHANNEL_ID = 913335770566258709;
        //public const ulong OUTWARD_BUILDS_POST_CHANNEL_ID = 913335663573729290;

        public static int GetOlderMessage(IMessage a, IMessage b)
        {
            return a.Id.CompareTo(b.Id);
        }

        public static void OnDiscordReady()
        {
            var guild = BotManager.DiscordClient.GetGuild(OUTWARD_DISCORD_ID);

            Gold = guild.Emotes.First(it => it.Name == "gold");
            Tsar = guild.Emotes.First(it => it.Name == "alexandrite"); // tsar
            Trog = guild.Emotes.First(it => it.Name == "trog");
        }

        public static void ExtractIdsFromMessageLink(string messageLink, out ulong channelID, out ulong messageID)
        {
            var split = messageLink.Split('/');
            channelID = ulong.Parse(split[^2]);
            messageID = ulong.Parse(split[^1]);
        }

        public static async Task SendDirectMessage(string message, IUser user)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(message);
                sb.AppendLine("*I am a bot, and this action was performed automatically. Please message Sinai#4637 if you need further assistance!*");
                await user.SendMessageAsync(sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception sending DM to user '{user.Username}':");
                Console.WriteLine(ex);
            }
        }

        public static async Task SendDeletedBuildLinkMessage(IMessage message)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Your message was removed because I did not recognise it as a valid Outward Wiki Build link! Please post the link by itself.");
            sb.AppendLine("Here is the original message for reference:");
            sb.AppendLine("```");
            sb.AppendLine(message.Content);
            sb.AppendLine("```");
            sb.AppendLine($"A valid Outward Wiki Build link should look like: `https://outward.fandom.com/wiki/Build:Build_Name`");
            await SendDirectMessage(sb.ToString(), message.Author);
        }

        public static async Task SendExceptionMessage(IMessage message, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {message.Author.Username}, unfortunately I encountered an error trying to process your build!");
            sb.AppendLine("Here is the original message for reference:");
            sb.AppendLine("```");
            sb.AppendLine(message.Content);
            sb.AppendLine("```");
            sb.AppendLine("And here is the error message, Sinai might ask for this:");
            sb.AppendLine("```");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("```");
            await SendDirectMessage(sb.ToString(), message.Author);
        }
    }
}
