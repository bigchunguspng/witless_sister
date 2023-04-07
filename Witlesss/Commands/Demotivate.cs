﻿using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using static Witlesss.XD.DgMode;

namespace Witlesss.Commands
{
    public class Demotivate : MakeMemeCore, ImageProcessor
    {
        public Demotivate() : base(new Regex(@"^\/d[vg]\S* *", RegexOptions.IgnoreCase)) { }

        private static bool REPEAT_RX() => Text is not null && Regex.IsMatch(Text, @"^\/d[vg]\S*\d+\S*");
        private static string D_PHOTO(int x) => $"DEMOTIVATOR [{(x == 1 ? "_" : x)}]";

        private const string D_VIDEO = "DEMOTIVATOR [^] VID";
        private const string D_STICK = "DEMOTIVATOR [#] STICKER";

        public ImageProcessor SetUp(int w, int h)
        {
            SelectModeAuto(w, h);
            JpegCoder.PassQuality(Baka);

            return this;
        }

        public override void Run() => Run(ProcessMessage, "Демотиваторы");

        private bool ProcessMessage(Message mess)
        {
            if (mess is null) return false;
            
            if      (mess.Photo     is not null)              ProcessPhoto(mess.Photo[^1].FileId);
            else if (mess.Animation is not null)              ProcessVideo(mess.Animation.FileId);
            else if (mess.Sticker   is { IsVideo: true })     ProcessVideo(mess.Sticker  .FileId);
            else if (mess.Video     is not null)              ProcessVideo(mess.Video    .FileId);
            else if (mess.VideoNote is not null)              ProcessVideo(mess.VideoNote.FileId);
            else if (mess.Sticker   is { IsAnimated: false }) ProcessStick(mess.Sticker  .FileId);
            else return false;
            
            return true;
        }

        public  void ProcessPhoto(string fileID) => DoPhoto(fileID, D_PHOTO, M.MakeDemotivator, REPEAT_RX());
        public  void ProcessStick(string fileID) => DoStick(fileID, D_STICK, M.MakeStickerDemotivator);
        private void ProcessVideo(string fileID) => DoVideo(fileID, D_VIDEO, M.MakeVideoDemotivator);

        protected override DgText GetMemeText(string text)
        {
            string a, b = Baka.Generate();
            if (b.Length > 1) b = b[0] + b[1..].ToLower(); // lower text can't be UPPERCASE
            if (string.IsNullOrEmpty(text)) a = Baka.Generate();
            else
            {
                var s = text.Split('\n', 2);
                a = s[0];
                if (s.Length > 1) b = s[1];
            }
            return new DgText(a, b);
        }

        public Demotivate SetUp(DgMode mode)
        {
            SetMode(mode);

            return this;
        }

        private static void SelectModeAuto(float w, float h) => SetMode(w / h > 1.6 ? Wide : Square);
        private static void SetMode(DgMode mode = Square) => Bot.MemeService.Mode = mode;
    }
}