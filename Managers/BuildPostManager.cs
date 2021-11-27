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
    public static class BuildPostManager
    {
        public static HashSet<string> AlreadyPostedBuildURLs { get; set; } = new();

        public static async Task HandleMessageAsync(SocketUserMessage message)
        {
            // Handle messages posted in the "post your build" channel
            if (message.Channel.Id == DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
                await HandleBuildPost(message);
        }

        /// <summary>
        /// Handle a user's build url message and post an embed
        /// </summary>
        public static async Task HandleBuildPost(IMessage message)
        {
            // ignore the "no-embed" brackets if they were used
            string wikiLink = message.Content.Replace("<", "").Replace(">", "");

            if (AlreadyPostedBuildURLs.Contains(wikiLink))
            {
                await message.DeleteAsync();
                await DiscordUtils.SendDirectMessage(
                    "Your build was removed because it was a duplicate of a build which was already posted!", 
                    message.Author);
                return;
            }

            try
            {
                var buildPageUri = new Uri(wikiLink);

                // query the wiki for the source of the page.
                EmbedBuilder embed = await GenerateBuildEmbedContent(buildPageUri);

                // remove the original message
                await message.DeleteAsync(new RequestOptions() { AuditLogReason = "Removing to replace with bot post" });

                // post our new message
                var newMessage = await message.Channel.SendMessageAsync(embed: embed.Build());
                
                // add reactions
                await newMessage.AddReactionAsync(DiscordUtils.ThumbsUp);
                await newMessage.AddReactionAsync(DiscordUtils.Gold);
                await newMessage.AddReactionAsync(DiscordUtils.Tsar);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception loading/parsing wiki page!");
                Console.WriteLine(ex);

                await message.DeleteAsync();
                await DiscordUtils.SendExceptionMessage(message, ex);
            }
        }

        /// <summary>
        /// Update a build post by the bot
        /// </summary>
        public static async Task<string> UpdateBuildMessage(string messageUrl)
        {
            try
            {
                var message = await GetBuildMessage(messageUrl);
                return await UpdateBuildPost(message, message.Embeds.First().Url, true);
            }
            catch (Exception ex)
            {
                return $"An error occured! {ex.Message}";
            }
        }

        /// <summary>
        /// Updates the url in a build post to a new wiki page, then updates the post.
        /// </summary>
        public static async Task<string> RelinkBuildPost(string messageLink, string newWikiLink)
        {
            try
            {
                newWikiLink = newWikiLink.Replace("<", "").Replace(">", "");

                if (AlreadyPostedBuildURLs.Contains(newWikiLink))
                    return "A build with that wiki link has already been posted before!";

                var message = await GetBuildMessage(messageLink);

                return await UpdateBuildPost(message, newWikiLink, true);
            }
            catch (Exception ex)
            {
                return $"An error occured! {ex.Message}";
            }
        }

        private static async Task<RestUserMessage> GetBuildMessage(string messageLink)
        {
            DiscordUtils.ExtractIdsFromMessageLink(messageLink, out ulong channelID, out ulong messageID);

            if (channelID != DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
                throw new Exception("This message is not in the 'post-your-builds' channel!");

            var channel = BotManager.DiscordClient.GetGuild(DiscordUtils.OUTWARD_BUILDS_GUILDID).GetChannel(channelID) as IMessageChannel;

            if (await channel.GetMessageAsync(messageID) is not RestUserMessage message)
                throw new Exception("Could not find a valid post from the provided message link!");

            return message;
        }

        private static async Task<string> GetWikiBuildContent(string buildName)
        {
            // query that page
            string wikiResponse = await WikiUtils.WikiQuery($"Build:{buildName}");

            // parse the json to get to the actual source
            var result = JsonConvert.DeserializeObject<JToken>(wikiResponse);
            string wikiContent = result["query"]["pages"].First.First["revisions"].First["*"].ToString();

            return wikiContent;
        }

        private static async Task<string> UpdateBuildPost(RestUserMessage message, string wikiLink, bool deleteIfWikiPageNotFound)
        {
            try
            {
                var buildUrl = new Uri(wikiLink);
                var embed = await GenerateBuildEmbedContent(buildUrl);

                await message.ModifyAsync((MessageProperties msg) =>
                {
                    msg.Embed = embed.Build();
                });

                return "Updated the build post successfully!";
            }
            catch (WikiPageNotFoundException)
            {
                if (deleteIfWikiPageNotFound)
                {
                    await message.DeleteAsync();
                    return "The wiki page linked to that post no longer exists, the post has been removed.";
                }
                else
                    return "Could not find a valid Wiki page with the new link!";
            }
            catch (WikiQueryException)
            {
                return "Unexpected Wiki query result data, unable to process.";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occured! {ex.GetType().Name}: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate the embed for a build post
        /// </summary>
        public static async Task<EmbedBuilder> GenerateBuildEmbedContent(Uri buildPageUri)
        {
            // get the page name by taking a substring from the end of the url
            var buildName = buildPageUri.ToString()[WikiUtils.VALID_BUILD_LINK.Length..];

            string wikiContent = await GetWikiBuildContent(buildName);

            // Start creating our embed message
            var embed = new EmbedBuilder
            {
                Url = buildPageUri.ToString(),
                Color = Color.DarkGrey
            };

            // start building our actual message (we'll use regex to extract the metadata and build a description)
            var description = new StringBuilder();

            // build name. if no match, default to page name.
            WikiUtils.RegexWikiFieldRef("name", wikiContent, ref buildName);
            embed.Title = buildName;

            // author and discord user
            string authorName = "Unknown";
            WikiUtils.RegexWikiFieldRef("author", wikiContent, ref authorName);
            description.AppendLine($"by **{authorName}**");

            // build description
            if (WikiUtils.RegexWikiField("description", wikiContent, out string desc))
            {
                desc = desc.Replace("[[", "").Replace("]]", "");
                description.AppendLine($"*\"{desc}\"*");
            }
            description.AppendLine();

            // build category
            WikiUtils.RegexWikiField("type", wikiContent, out string category);
            description.AppendLine($"Type: **{category}**");

            // faction
            string faction = "Any";
            WikiUtils.RegexWikiFieldRef("faction", wikiContent, ref faction);
            description.AppendLine($"Faction: **{faction}**");

            // breakthroughs
            description.Append($"Breakthroughs: ");
            var breakthroughs = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                if (WikiUtils.RegexWikiField($"breakthrough{i + 1}", wikiContent, out string bt))
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
                if (WikiUtils.RegexWikiField(field, wikiContent, out string item))
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
                if (WikiUtils.RegexWikiField($"quickslot{i + 1}", wikiContent, out string qs))
                    quickslots.Add(qs);
            }
            if (quickslots.Any())
                description.AppendLine($"**{string.Join(", ", quickslots)}**");
            else
                description.AppendLine($"**Any**");

            // DLC
            if (WikiUtils.RegexWikiField("DLC", wikiContent, out string dlcReq))
                description.AppendLine($"DLC Required: **{dlcReq}**");

            // Finalize the description
            embed.Description = description.ToString();

            return embed;
        }
    }
}
