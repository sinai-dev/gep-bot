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

        /// <summary>
        /// Update the top builds post.
        /// </summary>
        public static async Task UpdateTopBuilds()
        {
            Program.Log($"[{DateTime.Now}] Updating top builds");

            var topChannel = BotManager.DiscordClient.GetChannel(DiscordUtils.TOP_BUILDS_CHANNELID) as SocketTextChannel;
            var topMessages = await topChannel.GetMessagesAsync(20).FlattenAsync();

            BuildPostManager.AlreadyPostedBuildURLs.Clear();

            var buildChannel = BotManager.DiscordClient.GetChannel(DiscordUtils.POST_YOUR_BUILDS_CHANNELID) as SocketTextChannel;

            // Get the build channel messages and add them into a sorted set
            var messages = await buildChannel.GetMessagesAsync(999).FlattenAsync();

            var buildCategories = new Dictionary<string, List<IMessage>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Archer", new() },
                { "Brawler", new() },
                { "Hex Mage", new() },
                { "Mage", new() },
                { "Mercenary", new() },
                { "Rogue", new() },
                { "Spellblade", new() },
                { "Tank", new() },
                { "Co-op", new() },
                { "Other", new() },
            };

            foreach (var message in messages)
            {
                if (!message.Embeds.Any())
                    continue;

                RegexFieldFromBuildPost("Type", message.Embeds.First().Description, out string category);

                if (buildCategories.TryGetValue(category, out List<IMessage> list))
                    list.Add(message);
                else
                    Console.WriteLine("Category key not found? '" + category + "'");
            }

            foreach (var category in buildCategories)
            {
                category.Value.Sort(BuildComparer.Instance);

                var sb = new StringBuilder();
                sb.AppendLine($"```fix");
                sb.AppendLine($"~~~ Top {category.Key} Builds ~~~");
                sb.AppendLine($"```");

                int count = 0;
                foreach (var buildMessage in category.Value)
                {
                    string url = buildMessage.Embeds.First().Url;
                    if (BuildPostManager.AlreadyPostedBuildURLs.Contains(url))
                        continue;
                    BuildPostManager.AlreadyPostedBuildURLs.Add(url);

                    if (count < 5)
                    {
                        count++;
                        string name = WikiUtils.ExtractBuildNameFromWikiLink(url);
                        int votes = BuildComparer.GetVoteTally(buildMessage);

                        sb.AppendLine($"{count}: **{name}** ({votes}) | {url}");
                    }
                }

                // get the message in the top builds channel
                var topMessage = topMessages.FirstOrDefault(it => it.Content.Contains($"Top {category.Key} Builds")) as RestUserMessage;

                // if no message, send one first so we can edit it.
                if (topMessage == null)
                    topMessage = await topChannel.SendMessageAsync("temp");

                // modify the top message to contain the new sorted top builds
                await topMessage.ModifyAsync((MessageProperties msg) =>
                {
                    msg.Flags = MessageFlags.SuppressEmbeds;
                    msg.Content = sb.ToString();
                });
            }
        }

        private static Dictionary<ulong, IEmote> pendingReactionCache = new();

        /// <summary>
        /// Handle a reaction to a post
        /// </summary>
        public static async Task OnReaction(Cacheable<IUserMessage, ulong> _, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var sender = reaction.User.Value;
            if (sender.IsBot)
                return;

            if (channel.Id == DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
            {
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
                            var message = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                            await message.RemoveReactionAsync(reaction.Emote, sender);
                            return;
                        }
                    }
                    else // no pending reactions. keep this one, remove other reactions.
                    {
                        pendingReactionCache.Add(sender.Id, reaction.Emote);

                        var message = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                        foreach (var otherReaction in message.Reactions)
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
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
