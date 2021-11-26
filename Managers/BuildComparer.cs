using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot
{
    public class BuildComparer : IComparer<IMessage>
    {
        public static readonly BuildComparer Instance = new();

        public int Compare(IMessage x, IMessage y) => GetVoteTally(y).CompareTo(GetVoteTally(x));

        public static int GetVoteTally(IMessage message)
        {
            int votes = 0;
            foreach (var reaction in message.Reactions)
            {
                if (reaction.Key.Name == BuildsManager.ThumbsUp.Name)
                    votes += reaction.Value.ReactionCount - 1;
                else if (reaction.Key.Name == BuildsManager.Gold.Name)
                    votes += (reaction.Value.ReactionCount - 1) * 2;
                else if (reaction.Key.Name == BuildsManager.Tsar.Name)
                    votes += (reaction.Value.ReactionCount - 1) * 3;
            }
            return votes;
        }
    }
}
