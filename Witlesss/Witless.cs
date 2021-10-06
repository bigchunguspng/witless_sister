﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static Witlesss.Logger;

namespace Witlesss
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Witless
    {
        private const string Start = "_start", End = "_end", Dot = "_dot", Link = "[ссылка удалена]";
        
        private readonly Random _random = new Random();
        private readonly FileIO<Dictionary<string, Dictionary<string, int>>> _fileIO;
        private Counter _generation;
        public bool HasUnsavedStuff;
        
        public Dictionary<string, Dictionary<string, int>> Words { get; set; }

        [JsonProperty] private long Chat { get; set; }
        [JsonProperty] public int Interval
        {
            get => _generation.Interval;
            set => _generation.Interval = value;
        }
        
        public Witless(long chat, int interval = 7)
        {
            Chat = chat;
            _generation = new Counter(interval);
            _fileIO = new FileIO<Dictionary<string, Dictionary<string, int>>>($@"{Environment.CurrentDirectory}\Telegram-WitlessDB-{Chat}.json");
            Load();
            WaitOnStartup();
        }
        
        public bool ReceiveSentence(string sentence)
        {
            if (!SentenceIsAcceptable(sentence)) return false;
            
            List<string> wordlist = new List<string> {Start};
            wordlist.AddRange(sentence.ToLower().Replace(". ", $" {Dot} {Start} ")
                .Trim().Split(new[]{ ' ', '\t', '\n'}, StringSplitOptions.RemoveEmptyEntries).ToList());
            wordlist.Add(End);
            
            for (var i = 0; i < wordlist.Count; i++)
            {
                if (WordIsLink(wordlist[i]))
                    wordlist[i] = Link;
            }

            for (var i = 0; i < wordlist.Count - 1; i++)
            {
                string word = wordlist[i];
                if (!Words.ContainsKey(word))
                {
                    Words.Add(word, new Dictionary<string, int>());
                }

                string nextWord = wordlist[i + 1];
                if (Words[word].ContainsKey(nextWord))
                {
                    Words[word][nextWord]++;
                }
                else
                {
                    Words[word].Add(nextWord, 1);
                }
            }

            HasUnsavedStuff = true;
            return true;
        }
        private bool SentenceIsAcceptable(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;
            if (sentence.StartsWith('/'))
                return false;
            if (sentence.StartsWith("http") && !sentence.Contains(" "))
                return false;
            return true;
        }
        private bool WordIsLink(string word) => (word.Contains(".com") || word.Contains(".ru")) && word.Length > 20 || word.StartsWith("http") && word.Length > 7;

        public string TryToGenerate()
        {
            try
            {
                return Generate();
            }
            catch (Exception e)
            {
                Log(e.Message, ConsoleColor.Red);
                return "";
            }
        }
        private string Generate()
        {
            string result = "";
            string currentWord = Start;

            while (currentWord != End)
            {
                result = result + " " + currentWord;
                currentWord = PickWord(Words[currentWord]);
            }

            result = result.Replace(Start, "").Replace($" {Dot} ", ".").TrimStart();
            
            return result;
        }
        private string PickWord(Dictionary<string, int> dictionary)
        {
            int totalProbability = 0;
            foreach (KeyValuePair<string, int> chance in dictionary)
            {
                totalProbability += chance.Value;
            }
            
            int r = _random.Next(totalProbability);
            string result = End;

            foreach (KeyValuePair<string,int> chance in dictionary)
            {
                if (chance.Value > r)
                {
                    return chance.Key;
                }
                else
                {
                    r -= chance.Value;
                }
            }

            return result;
        }
        
        private async void WaitOnStartup()
        {
            await Task.Run(() =>
            {
                _generation.Stop();
                Thread.Sleep(28000);
                
                Save();
                _generation.Resume();
            });
        }
        
        public void Count() => _generation.Count();
        public bool ReadyToGen() => _generation.Ready();

        public void Save()
        {
            if (HasUnsavedStuff)
            {
                _fileIO.SaveData(Words);
                HasUnsavedStuff = false;
                Log($"Словарь для чата {Chat} сохранён!", ConsoleColor.Green);
            }
        }

        public void Load()
        {
            Words = _fileIO.LoadData();
            HasUnsavedStuff = false;
            Log($"Словарь для чата {Chat} загружен!");
        }

        public void Backup()
        {
            Save();
            Directory.CreateDirectory($@"{Environment.CurrentDirectory}\Backup");
            FileInfo file = new FileInfo($@"{Environment.CurrentDirectory}\Telegram-WitlessDB-{Chat}.json");
            file.CopyTo($@"{Environment.CurrentDirectory}\Backup\Telegram-WitlessDB-{Chat}-{DateTime.Now:dd.MM.yyyy_(HH-mm-ss)}.json");
        }
    }
}