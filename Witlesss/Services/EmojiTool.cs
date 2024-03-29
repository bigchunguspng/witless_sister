﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Drawing.StringAlignment;
using static System.Drawing.StringFormatFlags;
using static System.Drawing.StringTrimming;

namespace Witlesss.Services
{
    public class EmojiTool
    {
        public MemeType MemeType { get; init; }

        private bool Dg  => MemeType == MemeType.Dg;
        private bool Top => MemeType == MemeType.Top;

        private static readonly StringFormat[] Formats = new[]
        {
            new StringFormat(NoWrap) { Alignment = Near, Trimming = None },
            new StringFormat(NoWrap) { Alignment = Near, Trimming = Character },
            new StringFormat(      ) { Alignment = Near, Trimming = Character },
            new StringFormat(NoWrap) { Alignment = Near, Trimming = EllipsisCharacter }
        };

        public int DrawTextAndEmoji(Graphics g, string text, IList<Match> matches, TextParameters p, int m = 0, int m2 = 34, int off = 0)
        {
            var lines = 0;
            if (p.Lines > 1 && text.Contains('\n'))
            {
                var s = Dg ? text.Split('\n') : text.Split('\n', 2);
                var index1 = off + s[0].Length;
                var index2 = off + s[0].Length + 1 + s[1].Length;
                var matchesA = matches.Where(u => u.Index < index1).ToList();
                var matchesB = matches.Where(u => u.Index > index1 && u.Index < index2).ToList();
                lines += DrawTextAndEmoji(g, s[0], matchesA, p, m,              m2, off);
                lines += DrawTextAndEmoji(g, s[1], matchesB, p, m + m2 * lines, m2, index1 + 1);

                return lines;
            }

            var texts = EmojiRegex.Replace(text, "\t").Split('\t');
            var emoji = GetEmojiPngs(matches);
            var w = (int)p.Layout.Width;
            var h = (int)p.Layout.Height;

            using var textArea = new Bitmap(w, h);
            using var graphics = Graphics.FromImage(textArea);

            graphics.CompositingMode    = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode    = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint  = TextRenderingHint.AntiAlias;

            int x = 0, y = 0, max = 0;

            for (var i = 0; i < emoji.Count; i++)
            {
                DoText(texts[i]);

                for (var j = 0; j < emoji[i].Count; j++)
                {
                    var xd = emoji[i][j];
                    if (p.EmojiS + x > w)
                    {
                        if (Dg) break;
                        else     CR();
                    }

                    if (xd.EndsWith(".png"))
                    {
                        var image = new Bitmap(Image.FromFile(xd), p.EmojiSize);
#if DEBUG
                        graphics.FillRectangle(new SolidBrush(Color.Gold), new Rectangle(new Point(x, y), p.EmojiSize));
#endif
                        graphics.DrawImage(image, x, y);
                        MoveX(p.EmojiS);
                    }
                    else DoText(xd);
                }
            }
            DoText(texts[^1]);

            RenderLine();

            return lines + 1;

            void DoText(string s)
            {
                var rest = w - x;
                if (rest == 0)
                {
                    CR();
                    DoText(s);
                }
                else
                {
                    s = s.TrimEnd();

                    var ms = graphics.MeasureString(s, p.Font, p.Layout.Size, Formats[2], out _,  out var l);
                    var width = l > 1 ? rest : (int) Math.Min(ms.Width, rest);

                    if (width < rest) DrawSingleLineText(Formats[0]);
                    else if      (Dg) DrawSingleLineText(Formats[3]);
                    else
                    {
                        var format = Formats[1];
                        var layout = new RectangleF(x, y, rest, h);
                        _ = graphics.MeasureString(s, p.Font,   layout.Size, format, out var chars, out _); // w - x
                        _ = graphics.MeasureString(s, p.Font, p.Layout.Size, format, out var cw,    out _); // w
                        var start = (int)(Math.Max(0.66f - x / p.Layout.Width, 0) * cw);
                        var space = s[start..cw].Contains(' ');
                        var index = s[..chars].LastIndexOf(' ');
                        var cr = index < 0;
                        var trim = space ? cr ? "" : s[..index] : s[..chars];
                        ms = graphics.MeasureString(trim, p.Font, layout.Size, format);
                        layout.Width = ms.Width;
#if DEBUG
                        graphics.FillRectangle(new SolidBrush(Color.Crimson), layout);
#endif
                        graphics.DrawString(trim, p.Font, p.Color, layout, format);
                        MoveX((int)graphics.MeasureString(trim, p.Font).Width);
                        var next = space ? cr ? s : s[(index + 1)..] : s[chars..];
                        CR();
                        DoText(next);
                    }

                    void DrawSingleLineText(StringFormat format)
                    {
                        var layout = new RectangleF(x, y, width, h);
#if DEBUG
                        graphics.FillRectangle(new SolidBrush(Color.Chocolate), layout);
#endif
                        graphics.DrawString(s, p.Font, p.Color, layout, format);
                        MoveX(width);
                    }
                }
            }
            void MoveX(int o)
            {
                x += o;
                max = Math.Max(x, max);
            }
            void CR()
            {
                RenderLine();

                x = 0;
                max = 0;
                y += m2;
                lines++;
            }

            void RenderLine()
            {
                var offset = Top && IFunnyApp.UseLeftAlignment ? (int)p.Layout.X : (w - max) / 2;
                g.DrawImage(textArea, new Point(offset, (int)p.Layout.Y + m));
                graphics.Clear(Color.Transparent);
            }
        }

        public static string RemoveEmoji (string text) => ReplaceEmoji(text, "");
        public static string ReplaceEmoji(string text, string nn)
        {
            var matches = EmojiRegex.Matches(text);
            if (matches.Count == 0) return text;

            var emoji = GetEmojiPngs(matches);
            var m = 0;
            foreach (var cluster in emoji)
            {
                var replaced = cluster.Select(xd => xd.EndsWith(".png") ? nn : xd);
                text = text.Replace(matches[m++].Value, string.Join("", replaced));
            }

            return text;
        }

        private static List<List<string>> GetEmojiPngs(IList<Match> matches)
        {
            var emoji = new List<List<string>>(matches.Count);

            for (var n = 0; n < matches.Count; n++)
            {
                var match = matches[n];
                var xd = match.Value;
                var cluster = new List<string>(xd.Length / 2);
                for (var i = 0; i < xd.Length; i += char.IsSurrogatePair(xd, i) ? 2 : 1)
                {
                    var c = char.ConvertToUtf32(xd, i).ToString("x4");
                    cluster.Add(c);
                }

                emoji.Add(new List<string>(cluster.Count));

                for (var i = 0; i < cluster.Count; i++)
                {
                    var j = i;
                    var name = cluster[i];
                    string file = null;
                    bool repeat;
                    do
                    {
                        repeat = false;

                        var files = Directory.GetFiles(EMOJI_FOLDER, name + "*.png");
                        if (files.Length == 1) file = files[0];
                        else if (files.Length > 1)
                        {
                            file = files[^1];
                            if (cluster.Count > j + 1)
                            {
                                repeat = true;
                                j++;
                                name = name + "-" + cluster[j];
                            }
                        }
                    } while (repeat);

                    if (file != null)
                    {
                        emoji[n].Add(file);
                        var s = Path.GetFileNameWithoutExtension(file);
                        var split = s.Split('-');
                        for (var k = 1; k < split.Length && i + 1 < cluster.Count; k++)
                        {
                            if (split[k] == cluster[i + 1]) i++;
                        }
                    }
                    else
                    {
                        var character = char.ConvertFromUtf32(int.Parse(name, NumberStyles.HexNumber));
                        if (Regex.IsMatch(character, @"[\u231a-\u303d]")) emoji[n].Add(character);
                    }
                }
            }

            return emoji;
        }
    }
}