﻿using System;
using static Witlesss.XD.SpeedMode;

namespace Witlesss.Commands.Editing
{
    public class ChangeSpeed : FileEditingCommand
    {
        private double _speed;
        private SpeedMode _mode;

        public ChangeSpeed SetMode(SpeedMode mode)
        {
            _mode = mode;
            return this;
        }

        protected override void Execute()
        {
            _speed = Text.HasDoubleArgument(out var x) ? _mode == Fast ? ClampFast(x) : ClampSlow(x) : 2D;

            if (_mode == Slow) _speed = 1 / _speed;

            Bot.Download(FileID, Chat, out var path, out var type);

            var result = Memes.ChangeSpeed(path, _speed);
            SendResult(result, type);
            Log($"{Title} >> {(_mode == Fast ? "FAST" : "SLOW" )} [>>]");

            double ClampFast(double v) => Math.Clamp(v, 0.5,   94);
            double ClampSlow(double v) => Math.Clamp(v, 0.0107, 2);
        }

        protected override string AudioFileName => SongNameOr($"Are you {Sender.Split()[0]} or something.mp3");
        protected override string VideoFileName => $"piece_fap_club-{_speed}.mp4";
    }
}