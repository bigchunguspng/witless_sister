﻿using System;
using System.Collections.Generic;
using static Witlesss.Extension;

namespace Witlesss.MediaTools
{
    // ffmpeg -i "input.mp4" -filter_complex "[0:v]setpts=0.5*PTS,fps=60;[0:a]atempo=2.0" output.mp4
    // ffmpeg -i "input.mp4" -filter:v       "setpts=0.5*PTS,fps=60"                      output.mp4
    // ffmpeg -i "input.mp3" -filter:a       "atempo=2.0"                                 output.mp3
    public class F_Speed : F_Base
    {
        private readonly string _input, _output;
        private readonly double _speed, _fps;
        private readonly MediaType _type;

        public F_Speed(string input, string output, double speed, MediaType type, double fps) : this(input, output, speed, type) => _fps = fps;
        public F_Speed(string input, string output, double speed, MediaType type)
        {
            _input = input;
            _output = output;
            _speed = speed;
            _type = type;
        }

        public override IList<string> CreateArguments() => new[]
        {
            "-i", _input, FiltersNames[_type], Filter(), _output
        };

        private static readonly Dictionary<MediaType, string> FiltersNames = new Dictionary<MediaType, string>()
        {
            {MediaType.Audio,      "-filter:a"},
            {MediaType.Video,      "-filter:v"},
            {MediaType.AudioVideo, "-filter_complex"}
        };

        private string FilterAudio() => $"atempo={FormatDouble(_speed)}";
        private string FilterVideo() => $"setpts={FormatDouble(1 / _speed)}*PTS,fps={FormatDouble(_fps)}";
        private string Filter() => _type switch
        {
            MediaType.Audio      => FilterAudio(),
            MediaType.Video      => FilterVideo(),
            MediaType.AudioVideo => $"[0:v]{FilterVideo()};[0:a]{FilterAudio()}",
            _                    => throw new ArgumentOutOfRangeException()
        };
    }
}