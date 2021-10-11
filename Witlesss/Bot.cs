﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using static System.Environment;
using static Witlesss.Logger;
using static Witlesss.Memes;
using static Witlesss.Strings;
using File = System.IO.File;
using WitlessDB = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, int>>;

namespace Witlesss
{
    public class Bot
    {
        private readonly Random _random = new Random();
        private readonly TelegramBotClient _client;
        private readonly ConcurrentDictionary<long, Witless> _sussyBakas;
        private readonly FileIO<ConcurrentDictionary<long, Witless>> _fileIO;
        private readonly Memes _memes;
        private long _activeChat;

        public Bot()
        {
            string token = File.ReadAllText($@"{CurrentDirectory}\.token");
            
            _memes = new Memes();
            _fileIO = new FileIO<ConcurrentDictionary<long, Witless>>($@"{CurrentDirectory}\{CHATLIST_FILENAME}.json");
            _sussyBakas = _fileIO.LoadData();
            _client = new TelegramBotClient(token);
        }

        public void Run()
        {
            _client.StartReceiving();
            _client.OnMessage += OnMessageHandler;

            StartSaveLoop(2);
            ProcessConsoleInput();
        }

        private void OnMessageHandler(object sender, MessageEventArgs e)
        {
            var message = e.Message;
            string text = message.Caption ?? message.Text;
            long chat = message.Chat.Id;
            string title = TitleOrUsername();

            if (WitlessExist(chat))
            {
                var witless = _sussyBakas[chat];
                
                if (text != null && text.StartsWith('/'))
                {
                    if (TextAsCommand().StartsWith("/dg"))
                    {
                        ChatDemotivate(chat, title, witless, message, text);
                        return;
                    }
                    if (TextAsCommand().StartsWith("/set_frequency"))
                    {
                        ChatSetFrequency(chat, title, witless, text);
                        return;
                    }
                    if (TextAsCommand() == "/chat_id")
                    {
                        SendMessage(chat, chat.ToString());
                        return;
                    }
                    if (TextAsCommand().StartsWith("/fuse"))
                    {
                        ChatFuse(chat, title, witless, text);
                        return;
                    }
                    if (TextAsCommand().StartsWith("/move"))
                    {
                        ChatMove(chat, title, witless, text);
                        return;
                    }
                }
                
                if (witless.ReceiveSentence(ref text))
                    Log($@"""{title}"": получено сообщение ""{text}""", ConsoleColor.Blue);

                witless.Count();
                
                if (witless.ReadyToGen())
                {
                    if (message.Photo != null && _random.Next(witless.Interval) == 0)
                    {
                        string fileID = message.Photo[^1].FileId;
                        SendDemotivator(chat, title, witless, fileID, text);
                    }
                    else
                    {
                        SendMessage(chat, witless.TryToGenerate());
                        Log($@"""{title}"": сгенерировано прикол");
                    }
                }
            }
            else if (text != null && TextAsCommand() == "/start")
            {
                ChatStart(chat, title);
            }

            string TitleOrUsername() => chat < 0 ? message.Chat.Title : message.From.FirstName;
            string TextAsCommand() => text.ToLower().Replace(BOT_USERNAME, "");
        }
        private void ChatStart(long chat, string title)
        {
            if (!_sussyBakas.TryAdd(chat, new Witless(chat))) 
                return;
            SaveChatList();
            Log($@"Создано базу для чата {chat} ({title})");
            SendMessage(chat, START_RESPONSE);
        }
        private void ChatSetFrequency(long chat, string title, Witless witless, string text)
        {
            if (text.Split().Length > 1 && int.TryParse(text.Split()[1], out int value))
            {
                witless.Interval = value;
                SaveChatList();
                SendMessage(chat, $"{SET_FREQUENCY_RESPONSE_A} {witless.Interval} {SET_FREQUENCY_RESPONSE_B}");
                Log($@"""{title}"": интервал генерации изменен на {witless.Interval}");
            }
            else
                SendMessage(chat, SET_FREQUENCY_MANUAL);
        }
        private void ChatFuse(long chat, string title, Witless witless, string text)
        {
            string[] a = text.Split();
            if (a.Length > 1)
            {
                string name = a[1];
                bool passedID = long.TryParse(name, out long key);
                bool thisChatID = key == chat;
                if (thisChatID)
                {
                    SendMessage(chat, FUSE_FAIL_SELF);
                    return;
                }

                bool chatExist = passedID && WitlessExist(key);
                bool baseExist = BaseExists(name);
                if (chatExist || baseExist)
                {
                    witless.Backup();
                    var fusion = new FusionCollab(witless.Words, chatExist ? _sussyBakas[key].Words : FromFile());
                    fusion.Fuse();
                    witless.HasUnsavedStuff = true;
                    witless.Save();
                    SendMessage(chat, $@"{FUSE_SUCCESS_RESPOND_A} ""{title}"" {FUSE_SUCCESS_RESPOND_B}");
                }
                else
                    SendMessage(chat, passedID ? FUSE_FAIL_CHAT : FUSE_FAIL_BASE);

                WitlessDB FromFile() => new FileIO<WitlessDB>($@"{CurrentDirectory}\{EXTRA_DBS_FOLDER}\{name}.json").LoadData();
            }
            else
                SendMessage(chat, FUSE_MANUAL, ParseMode.Html);
        }
        private void ChatDemotivate(long chat, string title, Witless witless, Message message, string text)
        {
            string fileID;
            if (message.Photo != null)
                fileID = message.Photo[^1].FileId;
            else if (message.ReplyToMessage?.Photo != null)
                fileID = message.ReplyToMessage.Photo[^1].FileId;
            else
            {
                SendMessage(chat, DG_MANUAL);
                return;
            }
            SendDemotivator(chat, title, witless, fileID, text);
        }
        private void SendDemotivator(long chat, string title, Witless witless, string fileID, string text)
        {
            string a, b = witless.TryToGenerate();
            if (text != null && text.Contains(' ')) // custom upper text
            {
                a = text.Substring(text.IndexOf(' ') + 1);
                if (a.Contains('\n')) // custom bottom text
                {
                    b = a.Substring(a.IndexOf('\n') + 1);
                    a = a.Remove(a.IndexOf('\n'));
                }
            }
            else
                a = witless.TryToGenerate();
            
            var path = $@"{CurrentDirectory}\{PICTURES_FOLDER}\{chat}-{fileID.Remove(62)}.jpg";
            DownloadFile(fileID, path).Wait();
            using (var stream = File.OpenRead(_memes.MakeDemotivator(path, a, b)))
                _client.SendPhotoAsync(chat, new InputOnlineFile(stream)).Wait();
            Log($@"""{title}"": сгенерировано демотиватор [_]");
        }
        private async Task DownloadFile(string fileId, string path)
        {
            Directory.CreateDirectory($@"{CurrentDirectory}\{PICTURES_FOLDER}");
            try
            {
                var file = await _client.GetFileAsync(fileId);
                var stream = new FileStream(path, FileMode.Create);
                _client.DownloadFileAsync(file.FilePath, stream).Wait();
                await stream.DisposeAsync();
            }
            catch (Exception e)
            {
                Log(e.Message, ConsoleColor.Red);
            }
        }
        private void ChatMove(long chat, string title, Witless witless, string text)
        {
            string[] a = text.Split();
            if (a.Length > 1)
            {
                string name = a[1];
                string path = BaseExists(name) ? UniquePath(ExtraDBPath(name), ".json") : ExtraDBPath(name);
                witless.Save();
                File.Copy(witless.Path, path);
                
                witless.Words.Clear();
                Log($@"""{title}"": словарь беседы очищен!", ConsoleColor.Magenta);
                witless.HasUnsavedStuff = true;
                witless.Save();
                
                string result = path.Substring(path.LastIndexOf('\\') + 1).Replace(".json", "");
                SendMessage(chat, $"{MOVE_DONE_AS} \"{result}\"\n\n{MOVE_DONE_CLEARED}");
                Log($@"""{title}"": словарь сохранён как ""{result}""", ConsoleColor.Magenta);
            }
            else
                SendMessage(chat, MOVE_MANUAL, ParseMode.Html);

            string ExtraDBPath(string name) => $@"{CurrentDirectory}\{EXTRA_DBS_FOLDER}\{name}.json";
        }

        private void ProcessConsoleInput()
        {
            string input;
            do
            {
                input = Console.ReadLine();
                
                if (input != null)
                {
                    if (input.StartsWith("+") && input.Length > 1)
                    {
                        string shit = input.Substring(1);
                        foreach (long chat in _sussyBakas.Keys)
                        {
                            if (chat.ToString().EndsWith(shit))
                            {
                                _activeChat = chat;
                                Log($"Выбрано чат {_activeChat}");
                                break;
                            }
                        }
                    }
                    else if (WitlessExist(_activeChat) && input.Length > 3)
                    {
                        string text = input.Substring(3).Trim();
                        var witless = _sussyBakas[_activeChat];
                        
                        if (input.StartsWith("/a ") ) //add
                        {
                            if (witless.ReceiveSentence(ref text))
                                Log($@"{_activeChat}: в словарь добавлено ""{text}""", ConsoleColor.Yellow);
                        }
                        else if (input.StartsWith("/w ")) //write
                        {
                            SendMessage(_activeChat, text);
                            bool accepted = witless.ReceiveSentence(ref text);
                            Log($@"{_activeChat}: отправлено {(accepted ? "и добавлено в словарь " : "")}""{text}""", ConsoleColor.Yellow);
                        }
                    }
                    else if (input == "/s") SaveDics();
                    else if (input == "/u") ReloadDics();
                }
            } while (input != "s");
            _client.StopReceiving();
            SaveDics();
        }

        private async void SendMessage(long chat, string text, ParseMode mode = ParseMode.Default)
        {
            try
            {
                await _client.SendTextMessageAsync(chat, text, mode, disableNotification: true)
                    .ContinueWith(task =>
                    {
                        Log(chat + ": " + task.Exception?.Message, ConsoleColor.Red);
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch
            {
                // u/stupid
            }
        }

        private bool WitlessExist(long chat) => _sussyBakas.ContainsKey(chat);
        private bool BaseExists(string name)
        {
            var path = $@"{CurrentDirectory}\{EXTRA_DBS_FOLDER}";
            Directory.CreateDirectory(path);
            return Directory.GetFiles(path).Contains($@"{path}\{name}.json");
        }
        
        private void SaveChatList()
        {
            _fileIO.SaveData(_sussyBakas);
            Log(LOG_CHATLIST_SAVED, ConsoleColor.Green);
        }

        private async void StartSaveLoop(int minutes)
        {
            var saving = new Counter(minutes);
            await Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(60000);
                    saving.Count();
                    if (saving.Ready())
                    {
                        SaveDics();
                    }
                }
            });
        }

        private void SaveDics()
        {
            foreach (var witless in _sussyBakas.Values) witless.Save();
        }
        
        private void ReloadDics()
        {
            foreach (var witless in _sussyBakas.Values) witless.Load();
        }
    }
}