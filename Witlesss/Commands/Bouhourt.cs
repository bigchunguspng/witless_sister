﻿using System.Collections.Generic;
using System.Linq;
using static Witlesss.Copypaster;

namespace Witlesss.Commands
{
    public class Bouhourt : WitlessCommand
    {
        private readonly WitlessDB _baguette = new FileIO<WitlessDB>("BT.json").LoadData();

        public override void Run()
        {
            var length = 3;
            var lengthSpecified = false;
            if (Text.HasIntArgument(out int value))
            {
                length = System.Math.Clamp(value, 2, 16);
                lengthSpecified = true;
            }

            string start = null;
            var split = Text.Split(' ', lengthSpecified ? 3 : 2);
            if (split.Length > (lengthSpecified ? 2 : 1))
            {
                start = split[^1].ToUpper();
                length -= 1;
            }

            var lines = new List<string>(length);
            var words = _baguette[START];
            var word = PickWord(words);

            if (start != null) lines.Add(start);

            AddTextLine();

            words = _baguette["_mid"];
            for (int i = 1; i < length; i++)
            {
                word = PickWord(words);
                if (word == END) break;
                AddTextLine();
            }

            string result = string.Join("\n@\n", lines.Where(x => x != "")).Replace(" @ ", "\n@\n").ToUpper();
            Bot.SendMessage(Chat, result);
            Log($"{Title} >> BUGURT #@#");

            void AddTextLine() => lines.Add(Baka.GenerateByWord(PullWord(word)).Trim('@').TrimStart());
        }

        private static string PullWord(string word)
        {
            string[] xs;

            if      (word.StartsWith("..")) xs = Baka.Words.Keys.Where(x => x.EndsWith(word[2..] )).ToArray();
            else if (word.EndsWith  ("..")) xs = Baka.Words.Keys.Where(x => x.EndsWith(word[..^2])).ToArray();
            else if (word.Contains  (' ') ) return word.Split()[0] + ' ' + Baka.GenerateByWord(PullWord(word.Split()[1]));
            else
                return Baka.Words.ContainsKey(word) ? word : START;
            return xs.Length > 0 ? xs.ElementAt(Random.Next(xs.Length)) : START;
        }
    }
}