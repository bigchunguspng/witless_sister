﻿namespace Witlesss.Commands.Editing
{
    public class Reverse : FileEditingCommand
    {
        protected override void Execute()
        {
            Bot.Download(FileID, Chat, out var path, out var type);
            
            SendResult(Memes.Reverse(path), type);
            Log($"{Title} >> REVERSED [<<]");
        }
        
        protected override string AudioFileName => SongNameOr($"Kid Named {Sender}.mp3");
        protected override string VideoFileName { get; } = "piece_fap_reverse.mp4";
    }
}