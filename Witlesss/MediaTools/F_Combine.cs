﻿using System.Drawing;
using System.IO;
using System.Text;
using FFMpegCore;
using Drawer = Witlesss.Services.Memes.DemotivatorDrawer;
using FFO = FFMpegCore.FFMpegArgumentOptions;
using FAP = FFMpegCore.FFMpegArgumentProcessor;

namespace Witlesss.MediaTools
{
    public class F_Combine : F_Action
    {
        private readonly string _video, _image;

        public F_Combine(string video, string image)
        {
            _video = video;
            _image = image;
        }


        // -i video -i image -filter_complex "[0:v]scale=620:420[vid];[vid][1:v]overlay=0:0"
        public F_Action Meme(int loss, Size size) => ApplyEffects(o =>
        {
            BuildAndCompress(o, loss, Filter.Null.Scale("0:v", size, "vid").Overlay("vid", "1:v", Point.Empty));
        });

        // -i video -i image -filter_complex "[0:v]scale=620:530[vid];[1:v][vid]overlay=50:50"
        public F_Action Demo(int loss, Drawer drawer) => ApplyEffects(o =>
        {
            ArgsDemo(o, loss, drawer.Size, drawer.Pic);
        });

        public F_Action When(int loss, Size size, Rectangle crop, Point point, bool blur) => ApplyEffects(o =>
        {
            var filter = FixPicFps().Scale("0:v", size, "v0").Crop("v0", crop, "vid").Overlay("pic", "vid", point, blur ? "ova" : null);
            if (blur)
                filter = filter.Blur("ova", 1);
            BuildAndCompress(o, loss, filter);
        });

        public F_Action D300(int loss, Size image, Point point, Size frame) => ApplyEffects(o =>
        {
            ArgsDemo(o, loss, image, point);
            o.Resize(frame);
        });


        private void ArgsDemo(FFO o, int f, Size s, Point p)
        {
            BuildAndCompress(o, f, FixPicFps().Scale("0:v", s, "vid").Overlay("pic", "vid", p));
        }

        private Filter FixPicFps() => Filter.Null.Fps("1:v", new F_Process(_video).GetFramerate(), "pic");

        private static void BuildAndCompress(FFO o, int factor, Filter filter)
        {
            o.WithComplexFilter(filter);

            if (factor >  0) o.WithCompression(factor);
            if (factor > 23) o.WithAudioBitrate(154 - 3 * factor);
        }


        public string AddTrackMetadata(string artist, string title)
        {
            var name = $"{(artist is null ? "" : $"{artist} - ")}{title}";
            var path = $"{Path.GetDirectoryName(_video)}/{ValidFileName(name, '#')}.mp3";
            return ApplyEffects(o => MetadataArgs(o, artist, title)).OutputAs(path);
        }

        private static void MetadataArgs(FFO o, string artist, string title)
        {
            var sb = new StringBuilder();
            sb.Append("-map 0:0 -map 1:0 -c copy -id3v2_version 3 ");
            sb.Append("-metadata:s:v title=\"Album cover\" ");
            sb.Append("-metadata:s:v comment=\"Cover (front)\" ");
            if (artist is not null) sb.Append("-metadata artist=\"").Append(artist).Append("\" ");
            sb.Append                        ("-metadata title=\"" ).Append(title ).Append("\" ");

            o.WithCustomArgument(sb.ToString());
        }


        protected override string NameSource => _video;

        protected override string Cook(string output)
        {
            Run(FFMpegArguments.FromFileInput(_video).AddFileInput(_image).OutputToFile(output, addArguments: Action));
            return output;
        }
    }
}