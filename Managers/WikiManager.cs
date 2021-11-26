using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GepBot
{
    public static class WikiManager
    {
        public const string VALID_BUILD_LINK = @"https://outward.fandom.com/wiki/Build:";

        public static async Task<string> WikiQuery(string pageName)
        {
            string url = @$"https://outward.fandom.com/api.php?action=query&titles={pageName}&prop=revisions&rvprop=content&format=json"; ;
            string result = await BotManager.HttpClient.GetStringAsync(url);
            return result;
        }

        /// <summary>
        /// Extract the build name from a build page link
        /// </summary>
        public static string ExtractBuildName(string url) => url[VALID_BUILD_LINK.Length..].Replace('_', ' ');

        /// <summary>
        /// Regex match the value from '|fieldname = <value>'
        /// For example, given the input '|someField = 5', this would return 5.
        /// </summary>
        public static bool RegexWikiFieldRef(string fieldName, string content, ref string result)
        {
            Regex regex = new(@$"(?:\|{fieldName})(?: *= *)[^\n\r\|]*");
            Match match = regex.Match(content);
            if (match.Success)
            {
                string matchResult = match.Value;
                // remove the field name (for some reason Regex.Replace isn't working, idk, just do this for now)
                regex = new(@$"(\|{fieldName})( *= *)");
                match = regex.Match(matchResult);
                if (match.Success)
                {
                    matchResult = matchResult[match.Value.Length..];
                    if (!string.IsNullOrEmpty(matchResult))
                    {
                        result = matchResult;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Regex match a field value using out instead of ref.
        /// </summary>
        public static bool RegexWikiField(string fieldName, string content, out string result)
        {
            result = string.Empty;
            return RegexWikiFieldRef(fieldName, content, ref result);
        }
    }
}
