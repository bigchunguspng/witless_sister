﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;


namespace Witlesss.Services.Internet
{
    public class TelegramFileDownloader
    {
        private readonly BotCore _bot;
        private readonly DownloadCache _recent = new(32);
        private readonly DownloadCache  _large = new(32);

        public TelegramFileDownloader(BotCore bot)
        {
            _bot = bot;
        }

        public void Download(string fileID, long chat, out string path, out MediaType type)
        {
            string shortID = ShortID(fileID);
            string extension = ExtensionFromID(shortID);
            type = MediaTypeFromID(shortID);
            Witlesss.Memes.Sticker = extension == ".webm";

            if (_recent.Contains(shortID, out path) || _large.Contains(shortID, out path)) return;

            path = UniquePath($@"{PICTURES_FOLDER}\{chat}\{shortID}{extension}");

            DownloadFile(fileID, path, chat).Wait();
            
            (new FileInfo(path).Length > 2_000_000 ? _large : _recent).Add(shortID, path);
        }

        public async Task DownloadFile(string fileId, string path, long chat = default)
        {
            Directory.CreateDirectory($@"{PICTURES_FOLDER}\{chat}");
            try
            {
                var file = await _bot.Client.GetFileAsync(fileId);
                var stream = new FileStream(path, FileMode.Create);
                _bot.Client.DownloadFileAsync(file.FilePath!, stream).Wait();
                await stream.DisposeAsync();
            }
            catch (Exception e)
            {
                _bot.SendMessage(chat, e.Message.Contains("file is too big") ? Pick(FILE_TOO_BIG_RESPONSE) : XDDD(e.Message));
                throw;
            }
        }
    }

    public class DownloadCache
    {
        private readonly int _limit;

        private readonly Queue<string> _keys;
        private readonly Dictionary<string, string> _paths;

        public DownloadCache(int limit)
        {
            _limit = limit;
            _keys = new Queue<string>(_limit);
            _paths = new Dictionary<string, string>(_limit);
        }

        public void Add(string id, string path)
        {
            if (_keys.Count == _limit)
            {
                var key = _keys.Dequeue();
                _paths.Remove(key);
            }
            _keys.Enqueue(id);
            _paths.Add(id, path);
        }

        public bool Contains(string id, out string path)
        {
            return _paths.TryGetValue(id, out path);
        }
    }
}