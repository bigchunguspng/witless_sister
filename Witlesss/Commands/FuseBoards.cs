﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.StringSplitOptions;

#pragma warning disable CS4014

namespace Witlesss.Commands
{
    public class FuseBoards : Fuse
    {
        private readonly BoardService _chan = new();
        private List<BoardService.BoardGroup> _boards;
        private FileInfo[] _files;

        private static readonly SyncronizedDictionary<long, string> _names = new();

        private List<BoardService.BoardGroup> Boards => _boards ??= _chan.GetBoardList("https://www.4chan.org/index.php");

        // /boards
        // /boards info
        // /board a.b.c - Y-M-D.json
        // /board [thread/archive/archive link]
        protected override void ExecuteAuthorized()
        {
            if (Text.StartsWith("/boards"))
            {
                if (Text.EndsWith(" info")) SendSavedList(Chat, 0, 25);
                else                        SendBoardList(Chat, 0,  2);

                return;
            }

            if (Text.Contains(' '))
            {
                Baka.Save();
                Size = SizeInBytes(Baka.Path);

                GetWordsPerLineLimit();
                
                var args = Text.Split(' ', 2, RemoveEmptyEntries);

                var url = args[1];

                if (url.Contains(' ')) // FUSE WITH JSON FILE
                {
                    var files = GetFiles(CHAN_FOLDER, $"{url}.json");
                    if (files.Length > 0)
                    {
                        EatFromJsonFile(files[0]);
                        GoodEnding();
                    }
                    else
                        Bot.SendMessage(Chat, FUSE_FAIL_BOARD);
                    
                    return;
                }

                var uri = UrlOrBust(ref url);

                var host = uri.Host;
                var name = string.Join('.', url.Split(new[] { host }, None)[1].Split('/', RemoveEmptyEntries).Take(3));
                _names[Chat] = name;

                if (url.EndsWith("/archive"))
                {
                    var threads = _chan.GetArchivedThreads(url);
                    var tasks = threads.Select(x => GetDiscussionAsync("https://" + host + x)).ToList();

                    RespondAndStartEating(tasks);
                }
                else if (url.Contains("/thread/"))
                {
                    var replies = _chan.GetThreadDiscussion(url).ToList();

                    EatMany(replies, Baka, Size, Chat, Title, Limit);
                }
                else // BOARD
                {
                    var threads = _chan.GetThreads(url);
                    var tasks = threads.Select(x => GetDiscussionAsync(url + x)).ToList();

                    RespondAndStartEating(tasks);
                }
            }
            else
                Bot.SendMessage(Chat, BOARD_MANUAL);
        }

        private Task<List<string>> GetDiscussionAsync(string url)
        {
            // Use .ToList() if u want the Task to start right at this point!
            // Otherwise enumeration will be triggered later (slower).
            return Task.Run(() => _chan.GetThreadDiscussion(url).ToList());
        }

        private void RespondAndStartEating(List<Task<List<string>>> tasks)
        {
            var less_go = tasks.Count > 60 ? "Начинаю поглощение интернета 😈" : "頂きます！😋🍽";
            var text = string.Format(BOARD_START, tasks.Count, less_go);
            if (tasks.Count > 200) text += $"\n\n\n{MAY_TAKE_A_WHILE}";
            var message = Bot.PingChat(Chat, text);
            Bot.RunSafelyAsync(EatBoardDiscussion(SnapshotMessageData(), tasks, Limit), Chat, message);
        }


        private static async Task EatBoardDiscussion(WitlessMessageData x, List<Task<List<string>>> tasks, int limit)
        {
            await Task.WhenAll(tasks);

            var size = SizeInBytes(x.Baka.Path);

            var lines = tasks.Select(task => task.Result).SelectMany(s => s).ToList();

            EatMany(lines, x.Baka, size, x.Chat, x.Title, limit);
        }

        private static void EatMany(List<string> lines, Witless baka, long size, long chat, string title, int limit)
        {
            EatAllLines(lines, baka, limit, out var eated);
            SaveChanges(baka, title);

            Directory.CreateDirectory(CHAN_FOLDER);
            var path = $@"{CHAN_FOLDER}\{_names[chat]} - {DateTime.Now:yyyy'-'MM'-'dd' 'HH'.'mm}.json";
            new FileIO<List<string>>(path).SaveData(lines);
            _names.Remove(chat);

            var report = FUSION_SUCCESS_REPORT(baka, size, title);
            var detais = $"\n\n<b>Новых строк:</b> {BrowseReddit.FormatSubs(eated, "😏")}";
            Bot.SendMessage(chat, report + detais);
        }


        public void SendBoardList(long chat, int page, int perPage, int messageId = -1)
        {
            var boards = Boards.Skip(page * perPage).Take(perPage);
            var last = (int)Math.Ceiling(Boards.Count / (double)perPage) - 1;
                
            var sb = new StringBuilder("🍀🍀🍀 <b>4CHAN BOARDS</b> 🍀🍀🍀");
            sb.Append(" [PAGE: ").Append(page + 1).Append("/").Append(last + 1).Append("]");
            foreach (var group in boards)
            {
                sb.Append($"\n\n<b><u>{group.Title}</u></b>");
                if (group.IsNSFW) sb.Append(" (NSFW🥵)");
                sb.Append("\n");
                foreach (var board in group.Boards)
                {
                    sb.Append($"\n<i>{board.Title}</i> - <code>{board.URL}</code>");
                }
            }
            sb.Append(string.Format(BrowseReddit.SEARCH_FOOTER, Bot.Me.FirstName));
            sb.Append(USE_ARROWS);

            var text = sb.ToString();
            var buttons = GetPaginationKeyboard(page, perPage, last, "b");

            SendOrEditMessage(chat, text, messageId, buttons);
        }

        public void SendSavedList(long chat, int page, int perPage, int messageId = -1)
        {
            var files = GetFilesInfo(CHAN_FOLDER);
            if (_files is null || _files.Length != files.Length) _files = files;

            var single = _files.Length <= perPage;

            var lastPage = (int)Math.Ceiling(_files.Length / (double)perPage) - 1;
            var sb = new StringBuilder("<b>Доступные доскиъ/трѣды:</b> ");
            if (!single) sb.Append("📄[").Append(page + 1).Append('/').Append(lastPage + 1).Append(']');
            sb.Append('\n').Append(JsonList(_files, page, perPage));
            if (!single) sb.Append(USE_ARROWS);

            var text = sb.ToString();

            if (single) Bot.SendMessage(chat, text);
            else SendOrEditMessage(chat, text, messageId, GetPaginationKeyboard(page, perPage, lastPage, "bi"));
        }


        private Uri UrlOrBust(ref string url)
        {
            try
            {
                if (!url.Contains('/'))
                {
                    var ending = $"/{url}/";
                    var urls = Boards.SelectMany(x => x.Boards.Select(b => b.URL)).ToList();
                    var match = urls.FirstOrDefault(x => x.EndsWith(ending));
                    if (match != null)
                    {
                        url = match;
                    }
                }

                return new Uri(url);
            }
            catch
            {
                Bot.SendMessage(Chat, "Dude, wrong URL 👉😄");
                throw;
            }
        }
    }
}