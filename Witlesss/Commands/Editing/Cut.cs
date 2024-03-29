﻿using System;
using System.Linq;
using static System.TimeSpan;

#pragma warning disable CS4014

namespace Witlesss.Commands.Editing
{
    public class Cut : Slice
    {
        protected override void Execute()
        {
            var args = Text.Split().SkipWhile(x => x.StartsWith('/') || x.StartsWith("http")).ToArray();

            var x = ParseArgs(args);
            if (x.failed)
            {
                Bot.SendMessage(Chat, CUT_MANUAL);
                return;
            }

            var span = new CutSpan(x.start, x.length);

            Bot.RunSafelyAsync(new CutAsync(SnapshotMessageData(), FileID, span).RunAsync(), Chat, -1);
        }

        public static (bool failed, TimeSpan start, TimeSpan length) ParseArgs(string[] s)
        {
            var len = s.Length;
            if     (len == 1 && s[0].IsTimeSpan(out var length)) return (false, Zero,  length);      // [++]----]
            if     (len >= 2 && s[0].IsTimeSpan(out var  start))
            {
                if (len == 3 && s[2].IsTimeSpan(out var    end)) return (false, start, end - start); // [-[++]--]
                if             (s[1].IsTimeSpan(out     length)) return (false, start, length);      // [-[++]--]
                else                                             return (false, start, Zero);        // [-[+++++]
            }
            else                                                 return (true,  Zero,  Zero);        // [-------]
        }
    }
}