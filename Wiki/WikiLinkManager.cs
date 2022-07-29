﻿using Discord;
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
    public static class WikiLinkManager
    {
        private static readonly Regex LinkRegex = new(@"(?:\[\[)[^\]]*(?=\]\])");

        public static async Task<string> GetBestWikiPage(string search)
        {
            Program.Log($"Querying wiki for term: {search}");

            search = search.Replace(" ", "_");
            string wikiResponse = await WikiUtils.WikiSearch(search);
            JToken result = JsonConvert.DeserializeObject<JToken>(wikiResponse);
            return result.Children().ToArray()[3][0].ToString();
        }

        public static async Task CheckMessage(SocketUserMessage message)
        {
            MatchCollection matches = LinkRegex.Matches(message.Content);

            List<string> wikiLinks = new();
            List<string> invalidWikiLinks = new();

            foreach (Match match in matches)
            {
                if (!match.Success)
                    continue;

                string value = match.Value.Replace("[[", "").Trim();

                string wikiPageURL;
                try
                {
                    wikiPageURL = (await GetBestWikiPage(value)).Trim().Replace(" ", "_");
                }
                catch
                {
                    invalidWikiLinks.Add(value);
                    continue;
                }

                if (!match.Value.StartsWith("[[build:", StringComparison.OrdinalIgnoreCase))
                    wikiLinks.Add(wikiPageURL);
                else
                {
                    try
                    {
                        Uri uri = new Uri(wikiPageURL);
                        EmbedBuilder embed = await BuildPostManager.GenerateBuildEmbedContent(uri, new StringBuilder());
                        Discord.Rest.RestUserMessage newMessage = await message.Channel.SendMessageAsync(embed: embed.Build());
                        // only post one build at a time, dont wanna spam.
                        return;
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"Exception processing message with build link: {ex}");
                    }
                }
            }

            if (wikiLinks.Any() || invalidWikiLinks.Any())
            {
                Program.Log($"Processing wiki links for message by {message.Author.Username}...");

                StringBuilder sb = new StringBuilder();
                if (wikiLinks.Any())
                {
                    foreach (string link in wikiLinks)
                        sb.AppendLine($"* <{link}>".Replace("%27", "'").Replace("%26", "&"));
                }

                if (wikiLinks.Any() && invalidWikiLinks.Any())
                    sb.AppendLine();

                if (invalidWikiLinks.Any())
                {
                    sb.Append("This worries me:");
                    foreach (string link in invalidWikiLinks)
                        sb.Append($" '{link}'");
                    sb.AppendLine($" <:trog:{DiscordUtils.Trog.Id}>");
                }

                await message.Channel.SendMessageAsync(
                    sb.ToString(),
                    messageReference: new MessageReference(message.Id, message.Channel.Id, (message.Channel as SocketGuildChannel).Guild.Id));
            }
        }
    }
}
