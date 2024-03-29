﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Witlesss.Commands;

namespace Witlesss
{
    public class ConsoleUI
    {
        private long  _active;
        private string _input;

        public ConsoleUI(Bot bot) => Bot = bot;

        private Bot Bot { get; }

        private BanHammer Thor => Bot.ThorRagnarok;
        private ChatList SussyBakas => Bot.SussyBakas;
        private Witless Active => SussyBakas[_active];

        private IEnumerable<Witless> Bakas => SussyBakas.Values;

        public static bool LoggedIntoReddit = false;


        public void EnterConsoleLoop()
        {
            do
            {
                _input = Console.ReadLine();
                try
                {
                    if (_input != null && !_input.EndsWith("_"))
                    {
                        if      (_input.StartsWith("+") && _input.Length > 1) SetActiveChat();
                        else if (_input.StartsWith("/")                     ) DoConsoleCommands();
                    }
                }
                catch
                {
                    Log(">:^< u/stupid >:^<", ConsoleColor.Yellow);
                }
            } while (_input != "s");
            Bot.SaveDics();
            if (LoggedIntoReddit) RedditTool.Instance.SaveExcluded();
        }

        private void DoConsoleCommands()
        {
            if      (BotWannaSpeak()) BreakFourthWall();
            else if (_input == "/"  ) Log(CONSOLE_MANUAL, ConsoleColor.Yellow);
            else if (_input == "/s" ) Bot.SaveBakas();
            else if (_input == "/sd") SyncDics();
            else if (_input == "/sp") Spam.SendSpam();
            else if (_input == "/db") DeleteBlockers();
            else if (_input == "/DB") DeleteBlocker();
            else if (_input == "/ds") DeleteBySize();
            else if (_input == "/cc") ClearTempFiles();
            else if (_input == "/oo") ClearDics();
            else if (_input == "/Oo") ClearDic(Active);
            else if (_input == "/xx") FixDBs();
            else if (_input == "/Xx") FixDB(Active);
            else if (_input == "/l" ) ActivateLastChat();
            else if (_input == "/b" ) Thor.  BanChat(_active);
            else if (_input == "/ub") Thor.UnbanChat(_active);
            else if (_input.StartsWith("/sp") && _input.HasIntArgument(out int a)) Spam.SendSpam(a);
            else if (_input.StartsWith("/ds") && _input.HasIntArgument(out int b)) DeleteBySize(b);
            else if (_input.StartsWith("/b" ) && _input.HasIntArgument(out int c)) Thor.BanChat(_active, c);
        }

        private bool BotWannaSpeak() => Regex.IsMatch(_input, @"^\/[aw] ");

        private void SetActiveChat()
        {
            string shit = _input[1..];
            foreach (long chat in SussyBakas.Keys)
            {
                if (chat.ToString().EndsWith(shit))
                {
                    _active = chat;
                    Log($"ACTIVE CHAT >> {_active}");
                    break;
                }
            }
        }

        private void BreakFourthWall()
        {
            string text = _input.Split (' ', 2)[1];
            if (!Bot.WitlessExist(_active)) return;

            if      (_input.StartsWith("/a ") && Active.Eat(text, out text)) // add
            {
                Log($@"{_active} >> XD << ""{text}""", ConsoleColor.Yellow);
            }
            else if (_input.StartsWith("/w "))                               // write
            {
                Bot.SendMessage(_active, text);
                Active.Eat(text);
                Log($@"{_active} >> {text}", ConsoleColor.Yellow);
            }
        }

        private void ActivateLastChat()
        {
            (_active, var title) = Command.LastChat;
            Log($"ACTIVE CHAT >> {_active} ({title})");
        }

        private        void ClearDics() => Bakas.ForEach(ClearDic);
        private static void ClearDic(Witless witless)
        {
            witless.Delete();
            witless.Load();
        }

        private void DeleteBlockers()
        {
            foreach (var w in Bakas) if (DeleteBlocker(w) == -1) Bot.RemoveChat(w.Chat);
            Bot.SaveChatList();
        }
        private void DeleteBlocker()
        {
            if (DeleteBlocker(Active) == -1)
            {
                Bot.RemoveChat(_active);
                Bot.SaveChatList();
            }
        }
        private int DeleteBlocker(Witless witless)
        {
            var x = Bot.PingChat(witless.Chat, notify: false);
            if (x == -1) witless.Delete();
            else Bot.Client.DeleteMessageAsync(witless.Chat, x);

            return x;
        }

        private void DeleteBySize(int size = 2)
        {
            foreach (var witless in Bakas)
            {
                if (SizeInBytes(witless.Path) > size) continue;

                witless.Delete();
                Bot.RemoveChat(witless.Chat);
            }
            Bot.SaveChatList();
        }

        private void SyncDics()
        {
            foreach (var witless in Bakas)
            {
                var path = $@"{COPIES_FOLDER}\{DB_FILE_PREFIX}-{witless.Chat}.json";
                if (File.Exists(path) && Bot.WitlessExist(witless.Chat))
                {
                    new FusionCollab(witless, new FileIO<WitlessDB>(path).LoadData()).Fuse();
                    Log($"{LOG_FUSION_DONE} << {witless.Chat}", ConsoleColor.Magenta);
                    witless.SaveNoMatterWhat();
                }
            }
        }
        
        private void FixDBs() => Bakas.ForEach(FixDB);
        private void FixDB(Witless witless)
        {
            if (Bot.WitlessExist(witless.Chat))
            {
                witless.Baka.FixWitlessDB();
                witless.SaveNoMatterWhat();
            }
        }
    }
}