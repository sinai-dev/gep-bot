using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GepBot
{
    public static class BuildsManager
    {
        public static Emoji ThumbsUp { get; } = new("👍");
        public static Emote Gold { get; private set; }
        public static Emote Tsar { get; private set; }

        public const ulong OUTWARD_BUILDS_GUILDID = 913322914986737674;
        public const ulong TOP_BUILDS_CHANNELID = 913335770566258709;
        public const ulong POST_YOUR_BUILDS_CHANNELID = 913335663573729290;

        private static readonly HashSet<string> AlreadyPostedBuilds = new();

        public static void OnClientReady()
        {
            var guild = BotManager.DiscordClient.GetGuild(OUTWARD_BUILDS_GUILDID);

            Gold = guild.Emotes.First(it => it.Name == "gold");
            Tsar = guild.Emotes.First(it => it.Name == "tsar");
        }

        public static async Task HandleMessageAsync(SocketUserMessage message)
        {
            // Handle messages posted in the "post your build" channel
            if (message.Channel.Id == POST_YOUR_BUILDS_CHANNELID)
                await HandleBuildPost(message);
        }

        /// <summary>
        /// Handle a user's build url message and post an embed
        /// </summary>
        public static async Task HandleBuildPost(IMessage message)
        {
            // ignore the "no-embed" brackets if they were used
            string content = message.Content.Replace("<", "").Replace(">", "");

            Uri buildPageUri;
            try
            {
                // Ensure the message.Content is a valid URL and nothing else.
                buildPageUri = new(content);
                if (!buildPageUri.IsAbsoluteUri)
                    throw new ArgumentException("Invalid Wiki link format.");
                // Ensure it starts with the valid build string.
                if (!content.StartsWith(WikiManager.VALID_BUILD_LINK))
                    throw new ArgumentException("Invalid Wiki link format.");
            }
            catch
            {
                // No wiki URL found, or was invalid. Delete the message.
                await message.DeleteAsync(new RequestOptions() { AuditLogReason = "Invalid wiki build link." });
                // PM the user and explain.
                await DirectMessageManager.SendDeletedBuildLinkMessage(message);
                return;
            }

            if (AlreadyPostedBuilds.Contains(buildPageUri.ToString()))
            {
                await message.DeleteAsync();
                await DirectMessageManager.SendDirectMessage(
                    "Your build was removed because it was a duplicate of a build which was already posted!", 
                    message.Author);
                return;
            }

            try
            {
                // query the wiki for the source of the page.
                EmbedBuilder embed = await GenerateBuildEmbedContent(buildPageUri);

                // remove the original message
                await message.DeleteAsync(new RequestOptions() { AuditLogReason = "Removing to replace with bot post" });

                // post our new message
                var newMessage = await message.Channel.SendMessageAsync(embed: embed.Build());
                // add reactions
                await newMessage.AddReactionAsync(ThumbsUp);
                await newMessage.AddReactionAsync(Gold);
                await newMessage.AddReactionAsync(Tsar);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception loading/parsing wiki page!");
                Console.WriteLine(ex);

                await message.DeleteAsync();
                await DirectMessageManager.SendExceptionMessage(message, ex);
            }
        }

        /// <summary>
        /// Update a build post by the bot
        /// </summary>
        public static async Task<string> UpdateBuildPost(string messageUrl)
        {
            var split = messageUrl.Split('/');

            ulong channelID = ulong.Parse(split[^2]);
            ulong messageID = ulong.Parse(split[^1]);

            if (channelID == POST_YOUR_BUILDS_CHANNELID)
            {
                var channel = BotManager.DiscordClient.GetGuild(OUTWARD_BUILDS_GUILDID).GetChannel(channelID) as IMessageChannel;
                if (await channel.GetMessageAsync(messageID) is RestUserMessage message)
                {
                    var buildUrl = new Uri(message.Embeds.First().Url);
                    var embed = await GenerateBuildEmbedContent(buildUrl);
                    await message.ModifyAsync((MessageProperties msg) =>
                    {
                        msg.Embed = embed.Build();
                    });

                    return "Updated the build post.";
                }
                else
                    return "Could not find a valid build post from the provided link!";
            }
            else
                return "This message is not in a valid builds category channel!";
        }

        /// <summary>
        /// Generate the embed for a build post
        /// </summary>
        public static async Task<EmbedBuilder> GenerateBuildEmbedContent(Uri buildPageUri)
        {
            // get the page name by taking a substring from the end of the url
            var buildName = buildPageUri.ToString()[WikiManager.VALID_BUILD_LINK.Length..];

            // query that page
            string wikiResponse = await WikiManager.WikiQuery($"Build:{buildName}");

            // Start creating our embed message
            var embed = new EmbedBuilder
            {
                Url = buildPageUri.ToString(),
                Color = Color.DarkGrey
            };

            // parse the json to get to the actual source
            var result = JsonConvert.DeserializeObject<JToken>(wikiResponse);
            string wikiContent = result["query"]["pages"].First.First["revisions"].First["*"].ToString();

            // start building our actual message (we'll use regex to extract the metadata and build a description)
            var description = new StringBuilder();

            // build name. if no match, default to page name.
            WikiManager.RegexWikiFieldRef("name", wikiContent, ref buildName);
            embed.Title = buildName;

            // author and discord user
            string authorName = "Unknown";
            WikiManager.RegexWikiFieldRef("author", wikiContent, ref authorName);
            description.AppendLine($"by **{authorName}**");

            // build description
            if (WikiManager.RegexWikiField("description", wikiContent, out string desc))
            {
                desc = desc.Replace("[[", "").Replace("]]", "");
                description.AppendLine($"*\"{desc}\"*");
            }
            description.AppendLine();

            // build category
            WikiManager.RegexWikiField("type", wikiContent, out string category);
            description.AppendLine($"Type: **{category}**");

            // faction
            string faction = "Any";
            WikiManager.RegexWikiFieldRef("faction", wikiContent, ref faction);
            description.AppendLine($"Faction: **{faction}**");

            // breakthroughs
            description.Append($"Breakthroughs: ");
            var breakthroughs = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                if (WikiManager.RegexWikiField($"breakthrough{i + 1}", wikiContent, out string bt))
                    breakthroughs.Add(bt);
            }
            if (breakthroughs.Any())
                description.AppendLine($"**{string.Join(", ", breakthroughs)}**");
            else
                description.AppendLine("**Any**");

            // equipment
            description.Append($"Equipment: ");
            var equipment = new List<string>();
            var equipmentFields = new string[] { "weapon", "offhand", "helmet", "armor", "boots", "backpack" };
            foreach (var field in equipmentFields)
            {
                if (WikiManager.RegexWikiField(field, wikiContent, out string item))
                    equipment.Add(item);
            }
            if (equipment.Any())
                description.AppendLine($"**{string.Join(", ", equipment)}**");
            else
                description.AppendLine($"**Any**");

            // quickslots
            description.Append($"Quickslots: ");
            var quickslots = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                if (WikiManager.RegexWikiField($"quickslot{i + 1}", wikiContent, out string qs))
                    quickslots.Add(qs);
            }
            if (quickslots.Any())
                description.AppendLine($"**{string.Join(", ", quickslots)}**");
            else
                description.AppendLine($"**Any**");

            // DLC
            if (WikiManager.RegexWikiField("DLC", wikiContent, out string dlcReq))
                description.AppendLine($"DLC Required: **{dlcReq}**");

            // Finalize the description
            embed.Description = description.ToString();

            return embed;
        }

        private static void RegexFieldFromBuildPost(string fieldName, string content, out string result)
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

            var topChannel = BotManager.DiscordClient.GetChannel(TOP_BUILDS_CHANNELID) as SocketTextChannel;
            var topMessages = await topChannel.GetMessagesAsync(20).FlattenAsync();

            AlreadyPostedBuilds.Clear();

            var buildChannel = BotManager.DiscordClient.GetChannel(POST_YOUR_BUILDS_CHANNELID) as SocketTextChannel;

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
                    if (AlreadyPostedBuilds.Contains(url))
                        continue;
                    AlreadyPostedBuilds.Add(url);

                    if (count < 5)
                    {
                        count++;
                        string name = WikiManager.ExtractBuildName(url);
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

        /// <summary>
        /// Handle a reaction to a post
        /// </summary>
        public static async Task OnReaction(Cacheable<IUserMessage, ulong> _, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var sender = reaction.User;
            if (sender.Value.IsBot)
                return;

            if (channel.Id == POST_YOUR_BUILDS_CHANNELID)
            {
                try
                {
                    var message = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                    foreach (var otherReaction in message.Reactions)
                    {
                        if (otherReaction.Key.Name == reaction.Emote.Name)
                            continue;

                        try { await message.RemoveReactionAsync(otherReaction.Key, sender.Value); } catch { }
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
