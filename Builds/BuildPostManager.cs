﻿using Discord;
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
        public static HashSet<string> AlreadyPostedBuildURLs { get; } = new();

        /// <summary>
        /// Handle a user's build url message and post an embed
        /// </summary>
        public static async Task HandleBuildPost(IMessage message)
        {
            // ignore the "no-embed" brackets if they were used
            string wikiLink = message.Content.Replace("<", "").Replace(">", "");

            if (AlreadyPostedBuildURLs.Contains(wikiLink))
            {
                Program.Log($"Linked build is a duplicate of an already posted build.");

                await message.DeleteAsync();
                await DiscordUtils.SendDirectMessage(
                    "Your build was removed because it was a duplicate of a build which was already posted!", 
                    message.Author);
                return;
            }

            try
            {
                Program.Log($"Attempting to process a build post from {message.Author}: {message.CleanContent}");

                var buildPageUri = new Uri(wikiLink);

                // query the wiki for the source of the page.
                StringBuilder category = new();
                EmbedBuilder embed = await GenerateBuildEmbedContent(buildPageUri, category);

                // remove the original message
                await message.DeleteAsync(new RequestOptions() { AuditLogReason = "Removing to replace with bot post" });

                // send new message
                // post our new message
                var newMessage = await message.Channel.SendMessageAsync(embed: embed.Build());

                // add reactions
                await newMessage.AddReactionAsync(DiscordUtils.ThumbsUp);
                await newMessage.AddReactionAsync(DiscordUtils.Gold);
                await newMessage.AddReactionAsync(DiscordUtils.Tsar);

                Program.Log($"Handled successfully.");

                //// identify correct thread (NOT USING)
                //string ctg = category.ToString();
                //if (DiscordUtils.BUILD_THREADS.TryGetValue(ctg, out IThreadChannel thread))
                //{
                //    // post our new message
                //    var newMessage = await thread.SendMessageAsync(embed: embed.Build());
                //
                //    // add reactions
                //    await newMessage.AddReactionAsync(DiscordUtils.ThumbsUp);
                //    await newMessage.AddReactionAsync(DiscordUtils.Gold);
                //    await newMessage.AddReactionAsync(DiscordUtils.Tsar);
                //}
                //else
                //{
                //    await DiscordUtils.SendDirectMessage(
                //        "I could not find a valid category for your Wiki build! " +
                //        "Please make sure you use one of the available category names and that there are no typos.",
                //        message.Author);
                //}
            }
            catch (Exception ex)
            {
                Program.Log($"Exception handling build post:");
                Program.Log(ex);

                await message.DeleteAsync();
                await DiscordUtils.SendExceptionMessage(message);
            }
        }

        /// <summary>
        /// Update a build post by the bot
        /// </summary>
        public static async Task<string> UpdateBuildMessage(string messageUrl)
        {
            try
            {
                Program.Log($"Attempting to update a build post: {messageUrl}");

                var message = await GetBuildMessage(messageUrl);
                if (message == null)
                    return $"Could not find the message linked! :(";

                var ret = await UpdateBuildPost(message, message.Embeds.First().Url, true);
                Program.Log($"Handled successfully.");
                return ret;
            }
            catch (Exception ex)
            {
                Program.Log($"Encountered unexpected error updating a build post: {ex}");
                return $"This worries me! An unexpected error occured.";
            }
        }

        /// <summary>
        /// Updates the url in a build post to a new wiki page, then updates the post.
        /// </summary>
        public static async Task<string> RelinkBuildPost(string messageLink, string newWikiLink)
        {
            try
            {
                Program.Log($"Attempting to relink a build post to a new link: {messageLink} -> {newWikiLink}");

                newWikiLink = newWikiLink.Replace("<", "").Replace(">", "");

                if (AlreadyPostedBuildURLs.Contains(newWikiLink))
                    return "A build with that wiki link has already been posted before!";

                var message = await GetBuildMessage(messageLink);

                var ret = await UpdateBuildPost(message, newWikiLink, true);
                Program.Log($"Handled successfully.");
                return ret;
            }
            catch (Exception ex)
            {
                Program.Log($"Encountered unexpected error relinking a build post: {ex}");
                return $"This worries me! An unexpected error occured.";
            }
        }

        private static async Task<RestUserMessage> GetBuildMessage(string messageLink)
        {
            DiscordUtils.ExtractIdsFromMessageLink(messageLink, out ulong channelID, out ulong messageID);

            if (channelID != DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
                throw new Exception("This message is not in the 'post-your-builds' channel!");

            var channel = BotManager.DiscordClient.GetGuild(DiscordUtils.OUTWARD_DISCORD_ID).GetChannel(channelID) as IMessageChannel;

            if (await channel.GetMessageAsync(messageID) is not RestUserMessage message)
                throw new Exception("Could not find a valid post from the provided message link!");

            return message;
        }

        private static async Task<string> GetWikiBuildContent(string buildName)
        {
            Program.Log($"Querying wiki for {buildName}...");

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
                var embed = await GenerateBuildEmbedContent(buildUrl, new StringBuilder());

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
                Program.Log($"Unexpected error updating build post: {ex}");
                return $"This worries me! An unexpected error occured.";
            }
        }

        /// <summary>
        /// Generate the embed for a build post
        /// </summary>
        public static async Task<EmbedBuilder> GenerateBuildEmbedContent(Uri buildPageUri, StringBuilder category)
        {
            Program.Log($"Generating build post embed content...");

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

            //// build name. if no match, default to page name.
            //WikiUtils.RegexWikiFieldRef("name", wikiContent, ref buildName);
            //embed.Title = buildName;
            embed.Title = WikiUtils.ExtractBuildNameFromWikiLink(embed.Url).Replace("_", " ");

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
            WikiUtils.RegexWikiField("type", wikiContent, out string ctg);
            category.Append(ctg);
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
                {
                    if (field != "backpack" && WikiUtils.RegexWikiField($"{field}ench", wikiContent, out string ench))
                        item += $" ({ench})";
                    equipment.Add(item);
                }
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
