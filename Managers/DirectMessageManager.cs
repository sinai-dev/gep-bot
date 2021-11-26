using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot
{
    public static class DirectMessageManager
    {
        public static async Task SendDirectMessage(string message, IUser user)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(message);
                sb.AppendLine("*I am a bot, and this action was performed automatically. Please message Sinai#4637 if you need further assistance!*");
                await user.SendMessageAsync(message);
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
            sb.AppendLine($"Your message was removed because I did not recognise it as a valid Outward Wiki Build URL! Please post ONLY the URL.");
            sb.AppendLine("Here is the original message for reference:");
            sb.AppendLine("```");
            sb.AppendLine(message.Content);
            sb.AppendLine("```");
            await SendDirectMessage(sb.ToString(), message.Author);
        }

        public static async Task SendExceptionMessage(IMessage message, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Hello {message.Author.Username}, unfortunately I encountered an exception trying to process your build!");
            sb.AppendLine("Here is the original message for reference:");
            sb.AppendLine("```");
            sb.AppendLine(message.Content);
            sb.AppendLine("```");
            sb.AppendLine("And here is the exception message, Sinai might ask for this:");
            sb.AppendLine("```");
            sb.AppendLine(ex.ToString());
            sb.AppendLine("```");
            await SendDirectMessage(sb.ToString(), message.Author);
        }
    }
}
