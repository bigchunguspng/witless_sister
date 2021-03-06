using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Witlesss.Commands;
using static System.Environment;
using static Witlesss.Also.Extension;
using static Witlesss.Logger;
using static Witlesss.Also.Strings;
using File = System.IO.File;
using ChatList = System.Collections.Concurrent.ConcurrentDictionary<long, Witlesss.Witless>;
using WitlessDB = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, int>>;

namespace Witlesss
{
    public class Bot : BotCore
    {
        public readonly ChatList SussyBakas;
        private readonly FileIO<ChatList> _fileIO;
        public readonly Memes MemeService;
        private long _activeChat;

        private readonly MainJunction _junction;

        public Bot()
        {
            MemeService = new Memes();
            _fileIO = new FileIO<ChatList>($@"{CurrentDirectory}\{DBS_FOLDER}\{CHATLIST_FILENAME}.json");
            SussyBakas = _fileIO.LoadData();
            _junction = new MainJunction();
        }

        public void Run()
        {
            ClearTempFiles();

            Command.Bot = this;
            var options = new ReceiverOptions() {AllowedUpdates = new[] {UpdateType.Message, UpdateType.EditedMessage}};
            Client.StartReceiving(new Handler(this), options);

            StartSaveLoop(2);
            ProcessConsoleInput();
        }
        
        public void TryHandleMessage(Message message)
        {
            try
            {
                _junction.Pass(message);
                _junction.Run();
            }
            catch (Exception exception)
            {
                LogError(TitleOrUsername(message) + " >> CAN'T HANDLE MESSAGE: " + exception.Message);
            }
        }
        
        
        public string GetVideoOrAudioID(Message message, long chat)
        {
            var fileID = "";
            var mess = message.ReplyToMessage ?? message;
            for (int cycle = message.ReplyToMessage != null ? 0 : 1; cycle < 2; cycle++)
            {
                if (mess.Animation != null)
                    fileID = mess.Animation.FileId;
                else if (mess.Video != null)
                    fileID = mess.Video.FileId;
                else if (mess.Audio != null)
                    fileID = mess.Audio.FileId;
                else if (mess.Document?.MimeType != null && mess.Document.MimeType.StartsWith("audio"))
                    fileID = mess.Document.FileId;
                else if (mess.Voice != null)
                    fileID = mess.Voice.FileId;
                if (fileID.Length > 0)
                    break;
                else if (cycle == 1)
                {
                    SendMessage(chat, DAMN_MANUAL);
                    return null;
                }
                else mess = message;
            }
            return fileID;
        }

        private void ProcessConsoleInput()
        {
            string input;
            do
            {
                input = Console.ReadLine();
                
                if (input != null && !input.EndsWith("_"))
                {
                    if (input.StartsWith("+") && input.Length > 1)
                    {
                        string shit = input.Substring(1);
                        foreach (long chat in SussyBakas.Keys)
                        {
                            if (chat.ToString().EndsWith(shit))
                            {
                                _activeChat = chat;
                                Log($"{_activeChat} >> ACTIVE CHAT");
                                break;
                            }
                        }
                    }
                    else if (WitlessExist(_activeChat) && input.Length > 3)
                    {
                        string text = input.Substring(3).Trim();
                        var witless = SussyBakas[_activeChat];
                        
                        if (input.StartsWith("/a ") && witless.ReceiveSentence(ref text)) //add
                        {
                            Log($@"{_activeChat} >> ADDED TO DIC ""{text}""", ConsoleColor.Yellow);
                        }
                        else if (input.StartsWith("/w ")) //write
                        {
                            SendMessage(_activeChat, text);
                            bool accepted = witless.ReceiveSentence(ref text);
                            Log($@"{_activeChat} >> SENT {(accepted ? "AND ADDED TO DIC " : "")}""{text}""", ConsoleColor.Yellow);
                        }
                    }
                    else if (input == "/s") SaveDics();
                    else if (input == "/u") ReloadDics();
                    else if (input == "/r") ClearTempFiles();
                    else if (input == "/f") FuseAllDics();
                }
            } while (input != "s");
            SaveDics();
        }

        private string TitleOrUsername(Message message) => message.Chat.Id < 0 ? message.Chat.Title : message.From?.FirstName;

        public bool WitlessExist(long chat) => SussyBakas.ContainsKey(chat);

        public bool BaseExists(string name)
        {
            var path = $@"{CurrentDirectory}\{EXTRA_DBS_FOLDER}";
            Directory.CreateDirectory(path);
            return Directory.GetFiles(path).Contains($@"{path}\{name}.json");
        }

        public void SaveChatList()
        {
            _fileIO.SaveData(SussyBakas);
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
        
        private void FuseAllDics()
        {
            foreach (var witless in SussyBakas.Values)
            {
                var path = $@"{CurrentDirectory}\A\{DB_FILE_PREFIX}-{witless.Chat}.json";
                if (File.Exists(path))
                {
                    witless.Backup();
                    var fusion = new FusionCollab(witless.Words, new FileIO<WitlessDB>(path).LoadData());
                    fusion.Fuse();
                    witless.HasUnsavedStuff = true;
                    witless.Save();
                }
            }
        }

        private void SaveDics()
        {
            foreach (var witless in SussyBakas.Values) witless.Save();
        }
        
        private void ReloadDics()
        {
            foreach (var witless in SussyBakas.Values) witless.Load();
        }
    }
}