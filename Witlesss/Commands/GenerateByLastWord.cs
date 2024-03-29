﻿using System.Text.RegularExpressions;

namespace Witlesss.Commands
{
    public class GenerateByLastWord : GenerateByFirstWord
    {
        private readonly Regex _repeat = new(@"^\/zz\S*([2-9])\S*");

        public override void Run()
        {
            if (Text.Contains(' '))
            {
                var words = Text.Split();
                var word = words[1];
                var mode = GetMode(word);
                if (words.Length > 2)
                {
                    word = string.Join(' ', words[1..3]); // take first two words
                }
                
                word = word.ToLower();

                var text = RemoveCommand(words[0]);
                var ending = text[word.Length..];
                var repeats = GetRepeats(_repeat.Match(Text));
                for (int i = 0; i < repeats; i++)
                {
                    text = Baka.GenerateByLast(word.ToLower()) + ending;
                    Bot.SendMessage(Chat, text.ToLetterCase(mode));
                }

                LogXD(repeats, "FUNNY BY LAST WORD");
            }
            else
                Bot.SendMessage(Chat, ZZ_MANUAL);
        }
    }
}