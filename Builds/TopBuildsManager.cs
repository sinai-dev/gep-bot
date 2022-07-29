using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GepBot
{
    public class TopBuildsManager
    {
        public static readonly string[] BUILD_CATEGORIES = new string[]
        {
            "Archer",
            "Brawler",
            "Hex Mage",
            "Mage",
            "Mercenary",
            "Rogue",
            "Spellblade",
            "Tank",
            "Co-op",
            "Other",
        };

        public static void RegexFieldFromBuildPost(string fieldName, string content, out string result)
        {
            Regex regex = new(@$"(?:{fieldName}: \*\*)[^\*\n\r\|]*");
            Match match = regex.Match(content);
            if (match.Success)
            {
                string matchResult = match.Value;
                // remove the field name (for some reason Regex.Replace isn't working, idk, just do this for now)
                regex = new(@$"(?:{fieldName}: \*\*)");
                Match match2 = regex.Match(matchResult);
                if (match2.Success)
                {
                    matchResult = matchResult[match2.Value.Length..];
                    if (!string.IsNullOrEmpty(matchResult))
                    {
                        result = matchResult;
                        return;
                    }
                }
            }
            throw new Exception($"Could not find field '{fieldName}'!");
        }

        public static string GenerateMessageLink(ulong channel, ulong message)
        {
            StringBuilder messageLink = new StringBuilder();
            messageLink.Append("https://discord.com/channels/")
                .Append(DiscordUtils.OUTWARD_DISCORD_ID)
                .Append('/')
                .Append(channel)
                .Append('/')
                .Append(message);
            return messageLink.ToString();
        }

        public static async Task SendTopBuildQuickLinks()
        {
            SocketTextChannel topChannel = GepBot.DiscordClient.GetChannel(DiscordUtils.TOP_BUILDS_CHANNELID) as SocketTextChannel;
            IEnumerable<IMessage> topMessages = await topChannel.GetMessagesAsync(20).FlattenAsync();

            EmbedBuilder embedBuilder = new EmbedBuilder();

            foreach (string category in BUILD_CATEGORIES)
            {
                string title = $"Top {category} Builds";
                RestUserMessage topMessage = topMessages.FirstOrDefault(it => it.Content.Contains(title)) as RestUserMessage;
                
                string messageLink = GenerateMessageLink(topMessage.Channel.Id, topMessage.Id);

                embedBuilder.AddField(category, $"[{title}]({messageLink})", false);
            }

            await topChannel.SendMessageAsync(embed: embedBuilder.Build());
        }

        /// <summary>
        /// Update the top builds post.
        /// </summary>
        public static async Task UpdateTopBuilds()
        {
            Program.Log($"Updating top builds");

            SocketTextChannel topChannel = GepBot.DiscordClient.GetChannel(DiscordUtils.TOP_BUILDS_CHANNELID) as SocketTextChannel;
            IEnumerable<IMessage> topMessages = await topChannel.GetMessagesAsync(20).FlattenAsync();

            BuildPostManager.AlreadyPostedBuildURLs.Clear();

            // Get the build channel messages and add them into a sorted list
            SocketTextChannel buildChannel = GepBot.DiscordClient.GetChannel(DiscordUtils.POST_YOUR_BUILDS_CHANNELID) as SocketTextChannel;
            IEnumerable<IMessage> messages = await buildChannel.GetMessagesAsync(999).FlattenAsync();
            //var messages = new List<IMessage>();
            //foreach (var thread in DiscordUtils.BUILD_THREADS.Values)
            //    messages.AddRange(await thread.GetMessagesAsync(999).FlattenAsync());

            Dictionary<string, List<IMessage>> buildCategories = new Dictionary<string, List<IMessage>>(StringComparer.OrdinalIgnoreCase);
            foreach (string ctg in BUILD_CATEGORIES)
                buildCategories.Add(ctg, new());

            foreach (IMessage message in messages)
            {
                if (!message.Embeds.Any())
                    continue;

                RegexFieldFromBuildPost("Type", message.Embeds.First().Description, out string category);

                if (buildCategories.TryGetValue(category, out List<IMessage> list))
                    list.Add(message);
                else
                    Program.Log($"Category key not found? '{category}'");
            }

            foreach (KeyValuePair<string, List<IMessage>> category in buildCategories)
            {
                category.Value.Sort(BuildComparer.Instance);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"```fix");
                sb.AppendLine($"~~~ Top {category.Key} Builds ~~~");
                sb.AppendLine($"```");

                //var embed = new EmbedBuilder();

                int count = 0;
                foreach (IMessage buildMessage in category.Value)
                {
                    string url = buildMessage.Embeds.First().Url;
                    if (BuildPostManager.AlreadyPostedBuildURLs.Contains(url))
                        continue;
                    BuildPostManager.AlreadyPostedBuildURLs.Add(url);

                    if (count < 10)
                    {
                        count++;
                        string name = WikiUtils.ExtractBuildNameFromWikiLink(url).Replace("%27", "'");
                        int votes = BuildComparer.GetVoteTally(buildMessage);
                        string messageLink = GenerateMessageLink(DiscordUtils.POST_YOUR_BUILDS_CHANNELID, buildMessage.Id);
                        sb.AppendLine($"{count}: {name} ({votes}) | {url}");
                        
                        //Program.Log($"{name} ({votes}) | [Wiki Page]({url}) | [Message link]({messageLink})".Count());
                        //embed.AddField($"{count}", $"");
                    }
                }

                // get the message in the top builds channel
                RestUserMessage topMessage = topMessages.FirstOrDefault(it => it.Content.Contains($"Top {category.Key} Builds")) as RestUserMessage;

                // if no message, send one first so we can edit it.
                if (topMessage == null)
                    topMessage = await topChannel.SendMessageAsync("temp");

                // modify the top message to contain the new sorted top builds
                await topMessage.ModifyAsync((MessageProperties msg) =>
                {
                    msg.Content = sb.ToString();
                    //msg.Embeds = new[] { embed.Build() };
                });
            }
        }

        private static Dictionary<ulong, IEmote> pendingReactionCache = new();

        /// <summary>
        /// Handle a reaction to a post
        /// </summary>
        public static async Task OnReaction(Cacheable<IUserMessage, ulong> _, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            IUser sender = reaction.User.Value;
            if (sender.IsBot)
                return;

            if (channel.Id == DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
            {
                Program.Log($"Handling reaction from {sender.Username}, on message {reaction.MessageId}");

                try
                {
                    // the user is probably spam clicking on the reactions.
                    if (pendingReactionCache.TryGetValue(sender.Id, out IEmote pending))
                    {
                        // if they re-clicked the same one, just do nothing and return
                        if (pending == reaction.Emote)
                            return;
                        else // otherwise remove this reaction and return
                        {
                            IMessage message = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                            await message.RemoveReactionAsync(reaction.Emote, sender);
                            return;
                        }
                    }
                    else // no pending reactions. keep this one, remove other reactions.
                    {
                        pendingReactionCache.Add(sender.Id, reaction.Emote);

                        IMessage message = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                        foreach (KeyValuePair<IEmote, ReactionMetadata> otherReaction in message.Reactions)
                        {
                            if (otherReaction.Key.Name == reaction.Emote.Name)
                                continue;
                            await message.RemoveReactionAsync(otherReaction.Key, sender);
                        }

                        pendingReactionCache.Remove(sender.Id);
                    }
                }
                catch (Exception ex)
                {
                    Program.Log(ex);
                }
            }
        }
    }
}
