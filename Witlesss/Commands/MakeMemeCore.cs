﻿using System;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using StrInt     = System.Func<int, string>;
using MemeMaker  = System.Func<string, Witlesss.XD.Extension.DgText, string>;
using MemeMakerX = System.Func<string, Witlesss.XD.Extension.DgText, string, string>;

namespace Witlesss.Commands
{
    public abstract class MakeMemeCore : WitlessCommand
    {
        private string    _path;
        private MediaType _type;
        private readonly StopWatch _watch = new();

        private readonly Regex _cmd;

        protected MakeMemeCore(Regex cmd) => _cmd = cmd;

        protected Memes M => Bot.MemeService;

        protected void Run(Func<Message, bool> process, string type)
        {
            JpegCoder.PassQuality(Baka);

            var x = Message.ReplyToMessage;
            if (process(Message) || process(x)) return;

            Bot.SendMessage(Chat, string.Format(MEME_MANUAL, type));
        }

        protected void DoPhoto(string fileID, StrInt log, MemeMaker produce, bool regex)
        {
            Download(fileID);

            var repeats = GetRepeats(regex);
            for (int i = 0; i < repeats; i++)
            {
                using var stream = File.OpenRead(produce(_path, Texts()));
                Bot.SendPhoto(Chat, new InputOnlineFile(stream));
            }
            Log($"{Title} >> {log(repeats)}");
        }

        protected void DoStick(string fileID, string log, MemeMakerX produce)
        {
            Download(fileID);

            using var stream = File.OpenRead(produce(_path, Texts(), GetStickerExtension()));
            Bot.SendPhoto(Chat, new InputOnlineFile(stream));
            Log($"{Title} >> {log}");
        }

        protected void DoVideo(string fileID, string log, MemeMaker produce)
        {
            if (Bot.ThorRagnarok.ChatIsBanned(Baka)) return;

            _watch.WriteTime();
            Download(fileID);

            if (_type == MediaType.Round) _path = Memes.CropVideoNote(_path);

            using var stream = File.OpenRead(produce(_path, Texts()));
            Bot.SendAnimation(Chat, new InputOnlineFile(stream, "piece_fap_club.mp4"));
            Log($@"{Title} >> {log} >> TIME: {_watch.CheckStopWatch()}");
        }

        protected abstract DgText GetMemeText(string text);

        private DgText Texts() => GetMemeText(RemoveCommand(Text));
        private string RemoveCommand(string text) => text == null ? null : _cmd.Replace(text, "");

        private string GetStickerExtension() => Text != null && Text.Contains('x') ? ".jpg" : ".png";
        
        private void Download(string fileID) => Bot.Download(fileID, Chat, out _path, out _type);
        
        public static int GetRepeats(bool regex)
        {
            var repeats = 1;
            if (regex)
            {
                var match = Regex.Match(Text, @"\d");
                if (match.Success && int.TryParse(match.Value, out int x)) repeats = x;
            }
            return repeats;
        }

    }
}