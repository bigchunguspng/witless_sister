﻿using System;
using System.Linq;

namespace Witlesss.Commands.Editing
{
    public class Sus : VideoCommand
    {
        protected override void Execute()
        {
            var argless = false;
            var x = Cut.ParseArgs(Text.Split().Skip(1).ToArray());
            if (x.failed)
            {
                if (Text.Contains(' '))
                {
                    Bot.SendMessage(Chat, SUS_MANUAL);
                    return;
                }
                argless = true;
            }

            Bot.Download(FileID, Chat, out var path, out var type);

            if (argless) x.length = TimeSpan.MinValue;

            var result = Memes.Sus(path, new CutSpan(x.start, x.length));
            SendResult(result, type);
            Log($"{Title} >> SUS [>_<]");
        }

        protected override string AudioFileName => SongNameOr($"Kid Named {WhenTheSenderIsSus()}.mp3");
        protected override string VideoFileName { get; } = "sus_fap_club.mp4";

        string WhenTheSenderIsSus() => Sender.Length > 2 ? Sender[..2] + Sender[0] : Sender;
    }
}