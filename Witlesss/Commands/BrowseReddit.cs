﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Reddit.Controllers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using static Witlesss.XD.SortingMode;

#pragma warning disable CS8509
#pragma warning disable SYSLIB0014

namespace Witlesss.Commands // ReSharper disable InconsistentNaming
{
    public class BrowseReddit : Command
    {
        private readonly Regex _arg = new(@"^\/w\S*\s((?:(?:.*)(?=\s[a-z0-9_]+\*))|(?:(?:.*)(?=\s-\S+))|(?:.*))");
        private readonly Regex _sub = new(@"\s([a-z0-9_]+)");
        private readonly Regex sub_ = new(@"([a-z0-9_]+)\*");
        private readonly Regex _ops = new(@"(?<=-)([hntrc][hdwmya]?)\S*$");
        private readonly Regex _wtf = new(@"^\/w[^\ss_@]");

        private static readonly Regex _ul = new(@"<ul.*ul>"), _li = new(@"<li.*?href=""(.*?)"".*?li>");

        private static readonly RedditTool Reddit = RedditTool.Instance;

        // input: /w {reply message}
        // input: /ww
        // input: /wss subreddit
        // input: /ws [subreddit [-ops]]
        // input: /w search query [subreddit*] [-ops]   (ops: -h/-n/-t/-c/-ta/...)
        public override void Run()
        {
            var input = TextWithoutBotUsername;

            if (Message.ReplyToMessage is { Text: { } t } message && IsCommand(t, "/w"))
            {
                Pass(message);
                Run(); // RECURSIVE
            }
            else if (_wtf.IsMatch(input))
            {
                Log("LAST QUERY");
                SendPost(Reddit.GetLastOrRandomQuery(Chat));
            }
            else if (input.StartsWith("/wss")) // subreddit
            {
                if (input.Contains(' '))
                {
                    var text = input.Split(' ', 2)[1];
                    var subs = Reddit.FindSubreddits(text);
                    var b = subs.Count > 0;

                    Bot.SendMessage(Chat, b ? SubredditList(text, subs) : "<b>*пусто*</b>");
                }
                else
                {
                    Bot.SendMessage(Chat, REDDIT_SUBS_MANUAL);
                }
            }
            else if (input.StartsWith("/ws")) // [subreddit [-ops]]
            {
                var sub = _sub.Match(input);
                if (sub.Success)
                {
                    var subreddit = sub.Groups[1].Value;

                    var options = GetOptions("ha");

                    var sort = (SortingMode)options[0];
                    var time = GetTime(options, TimeMatters(sort));

                    Log("SUBREDDIT");
                    SendPost(new ScQuery(subreddit, sort, time));
                }
                else
                {
                    Bot.SendMessage(Chat, REDDIT_MANUAL, preview: false);
                }
            }
            else // /w search query [subreddit*] [-ops]
            {
                var arg = _arg.Match(input);
                if (arg.Success)
                {
                    var q = arg.Groups[1].Value;

                    var sub = sub_.Match(input);
                    var s = sub.Success;
                    var subreddit = s ? sub.Groups[1].Value : null;

                    var options = GetOptions("ra");

                    var sort = Sorts  [options[0]];
                    var time = GetTime(options, TimeMatters(options[0]));

                    Log("SEARCH");
                    SendPost(s ? new SsQuery(subreddit, q, sort, time) : new SrQuery(q, sort, time));
                }
                else
                {
                    Log("DEFAULT (RANDOM)");
                    SendPost(Reddit.RandomSubredditQuery);
                }
            }

            bool IsCommand(string a, string b) => a.ToLower().StartsWith(b);

            string GetOptions(string alt)
            {
                var ops = _ops.Match(input);
                return ops.Success ? ops.Value : alt;
            }
        }

        public  static string GetTime(string o, bool b) => o.Length > 1 && b ? Times[o[1]] : Times['a'];

        public  static readonly Dictionary<char, string> Sorts = new()
        {
            { 'r', "relevance" }, { 'h', "hot" }, { 't', "top" }, { 'n', "new" }, { 'c', "comments" }
        };
        private static readonly Dictionary<char, string> Times = new()
        {
            { 'a', "all" }, { 'h', "hour" }, { 'd', "day" }, { 'w', "week" }, { 'm', "month" }, { 'y', "year" }
        };
        
        public  static bool TimeMatters(SortingMode s) => s is Top or Controversial;
        public  static bool TimeMatters(char        c) => c is not 'h' and not 'n';

        #region SENDING MEMES

        private static void SendPost(RedditQuery query)
        {
            var post = GetPostOrBust(query);
            if (post == null) return;

            var a = post.URL.Contains("/gallery/");
            if (a)  SendGalleryPost(post);
            else SendSingleFilePost(post);

            if (Bot.WitlessExist(Chat))
            {
                Bot.SussyBakas[Chat].Eat(post.Title);
            }

            Log($"{Title} >> r/{post.Subreddit} (Q:{Reddit.QueriesCached} P:{Reddit.PostsCached})");
        }

        private static void SendGalleryPost(PostData post) => Bot.SendAlbum(Chat, AlbumFromGallery(post));

        private static IEnumerable<InputMediaPhoto> AlbumFromGallery(PostData post)
        {
            using var client = new WebClient();
            string html = client.DownloadString(post.URL);

            var list = _li.Matches(_ul.Match(html).Value);

            var captioned = false;
            return list.Select(GetInputMedia).Take(10);

            InputMediaPhoto GetInputMedia(Match match)
            {
                var cap = captioned ? null : post.Title;
                captioned = true;

                var url = match.Groups[1].Value.Replace("&amp;", "&");
                return new InputMediaPhoto(new InputMedia(url)) { Caption = cap };
            }
        }

        private static void SendSingleFilePost(PostData post)
        {
            var g = post.URL.EndsWith(".gif");
            try
            {
                SendPicOrAnimation(new InputOnlineFile(post.URL));
            }
            catch
            {
                var meme = DownloadMeme(post, g ? ".gif" : ".png");
                var path = g ? Memes.CompressGIF(meme) : Memes.Compress(meme);
                
                using var stream = File.OpenRead(path);
                SendPicOrAnimation(new InputOnlineFile(stream, $"r-{post.Subreddit}.mp4"));
            }

            void SendPicOrAnimation(InputOnlineFile file)
            {
                if (g) Bot.SendAnimaXD(Chat, file, post.Title);
                else   Bot.SendPhotoXD(Chat, file, post.Title);
            }
        }

        private static PostData GetPostOrBust(RedditQuery query)
        {
            try
            {
                return Reddit.PullPost(query, Chat);
            }
            catch
            {
                Bot.SendMessage(Chat, "💀");

                //                   He sends
                // awesome fucking evil blue flaming skull next to
                //  a keyboard with the "g" key being highlighted
                //                 to your chat
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⢀⣶⣿⣿⣿⣿⣿⣿⣶⣆⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⣸⣿⣿⠉⠉⠉⠄⠉⢹⣿⣦⡀⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⢿⣿⣿⣁⠄⠄⠤⠤⡀⠻⣿⠃⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠘⣿⣿⣿⡗⠖⡶⢾⣶⠊⡏⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⢻⣿⣿⣅⣈⠂⠐⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠘⢿⣾⣇⣂⣠⠄⠄⠄⠁⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⢘⣿⣗⠒⠄⢨⠶⢁⣄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠄⠨⣿⣿⡿⠋⠁⣴⣿⣿⣷⣦⣄⡀⠄⠄⠄⠄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⠄⠄⢀⣠⣄⣶⣎⢱⢄⢀⣾⣿⣿⣿⣿⣿⣿⣿⣶⣦⣤⣄⠄⠄⠄⠄
                // ⠄⠄⠄⠄⠄⠄⠄⢠⣾⣿⣿⡞⢝⡟⠃⣠⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣯⣿⣿⣇⠄⠄⠄
                // ⠄⠄⠄⠄⠆⢄⠄⢛⡫⠝⢿⡥⠟⡃⣴⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣼⣭⣻⣿⣿⡀⠄⠄
                // ⠄⠄⠄⣴⣆⠄⢋⠄⠐⣡⣿⣆⣴⣼⣿⣿⣿⣿⣿⣿⣿⣿⠏⢈⣿⣿⣿⣿⣿⣿⣷⡄⠄
                // ⠄⠄⣼⣿⣷⠄⠉⠒⣪⣹⣟⣹⣿⣿⣿⣿⣿⣟⣿⣿⣿⡇⢀⣸⣿⣿⣿⢟⣽⣿⣿⣇⠄
                // WHOLESEOME 0 DESTRUCTION 100 QUAGMIRE TOILET 62
                return null;
            }
        }

        private static string DownloadMeme(PostData post, string extension)
        {
            var name = UniquePath($@"{PICTURES_FOLDER}\{post.Fullname}{extension}");
            using var web = new WebClient();
            web.DownloadFile(post.URL, name);

            return name;
        }

        #endregion

        #region LOOKING FOR SUBREDDITS

        private static string SubredditList(string q, List<Subreddit> subs)
        {
            var sb = new StringBuilder(string.Format(SEARCH_HEADER, q, subs.Count, Ending(subs.Count)));
            foreach (var s in subs) sb.Append(string.Format(SUBS_LI, s.Name, FormatSubs(s.Subscribers ?? 0)));
            return sb.Append(string.Format(SEARCH_FOOTER, Bot.Me.FirstName)).ToString();
        }

        private const string SEARCH_HEADER = "По запросу <b>{0}</b> найдено <b>{1}</b> сообществ{2}:\n";
        private const string SUBS_LI       = "\n<code>{0}</code> - <i>{1}</i>";
        public  const string SEARCH_FOOTER = "\n\nБлагодарим за использование поисковика {0}";

        public  static string FormatSubs(int x, string bruh = "💀") => x switch
        {
            < 1000      =>  x + bruh,
            < 100_000   => (x / 1000D).ToString("0.#") + "k👌",
            < 1_000_000 =>  x / 1000      + "k👌",
            _           =>  x / 1_000_000 + "M 🤯"
        };
        private static string Ending(int x)
        {
            if (x is > 4 and < 21) return "";
            return (x % 10) switch { 1 => "o", 2 or 3 or 4 => "а", _ => ""};
        }

        #endregion
    }

    public class GetRedditLink : Command
    {
        public override void Run()
        {
            if (Message.ReplyToMessage is { } message)
            {
                Pass(message);
                if (RedditTool.Instance.Recognize(Text) is { } post)
                {
                    Bot.SendMessage(Chat, $"<b><a href='{post.Permalink}'>r/{post.Subreddit}</a></b>", preview: false);
                }
                else
                {
                    Bot.SendMessage(Chat, $"{Pick(I_FORGOR_RESPONSE)} {Pick(FAIL_EMOJI_1)}");
                }
            }
            else Bot.SendMessage(Chat,  string.Format(LINK_MANUAL, RedditTool.KEEP_POSTS));
        }
    }
}