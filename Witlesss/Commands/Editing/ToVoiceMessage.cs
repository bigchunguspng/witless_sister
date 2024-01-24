﻿using Telegram.Bot.Types.InputFiles;

namespace Witlesss.Commands.Editing;

public class ToVoiceMessage : FileEditingCommand
{
    public override void Run()
    {
        if (NothingToProcess()) return;

        Bot.Download(FileID, Chat, out var path);

        string result;
        try
        {
            result = Memes.ToVoice(path);
        }
        catch
        {
            result = "voice.ogg";
        }

        using var stream = File.OpenRead(result);
        Bot.SendVoice(Chat, new InputOnlineFile(stream, "balls.ogg"));
        Log($"{Title} >> VOICE ~|||~");
    }
}