﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Witlesss
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Witless
    {
        private const string Start = "_start", End = "_end", Dot = "_dot";
        
        private readonly Dictionary<string, Dictionary<string, int>> _words;
        private readonly Random _random = new Random();
        private readonly FileIO<Dictionary<string, Dictionary<string, int>>> _fileIO;
        private int _interval;

        [JsonProperty] private long Chat { get; set; }
        [JsonProperty] public int Interval
        {
            get => _interval;
            set
            {
                if (value < 1)
                    _interval = 1;
                else if (value > 62)
                    _interval = 62;
                else
                    _interval = value;
            }
        }

        public int Counter { get; private set; }
        
        public Witless(long chat, int interval = 7)
        {
            Chat = chat;
            Interval = interval;
            
            _fileIO = new FileIO<Dictionary<string, Dictionary<string, int>>>($@"{Environment.CurrentDirectory}\Telegram-WitlessDB-{Chat}.json");
            _words = _fileIO.LoadData();
        }
        
        public bool ReceiveSentence(string sentence)
        {
            if (!SentenceIsAcceptable(sentence)) return false;
            
            List<string> wordlist = new List<string> {Start};
            wordlist.AddRange(sentence.ToLower().Replace(". ", $" {Dot} {Start} ").Trim().Split().ToList());
            wordlist.Add(End);

            for (var i = 0; i < wordlist.Count - 1; i++)
            {
                string word = wordlist[i];
                if (!_words.ContainsKey(word))
                {
                    Dictionary<string, int> probOfNextWords = new Dictionary<string, int>();
                    _words.Add(word, probOfNextWords);
                }

                string nextWord = wordlist[i + 1];
                if (_words[word].ContainsKey(nextWord))
                {
                    _words[word][nextWord]++;
                }
                else
                {
                    _words[word].Add(nextWord, 1);
                }
            }

            return true;
        }

        private bool SentenceIsAcceptable(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;
            if (sentence.StartsWith('/'))
                return false;
            if (sentence.Contains("https://") || sentence.Contains("http://"))
                return false;
            /*if (sentence.Contains("@") && sentence.Contains(".") && !sentence.Contains(" "))
                return false;*/ // ок оставлю по приколу хд)00)
            
            return true;
        }

        public void Count()
        {
            Counter++;
            Counter %= Interval;
        }

        public string Generate()
        {
            string result = "";
            string currentWord = Start;

            while (currentWord != End)
            {
                result = result + " " + currentWord;
                currentWord = PickWord(_words[currentWord]);
            }

            result = result.Replace(Start, "").Replace($" {Dot} ", ".").TrimStart();
            
            Save();
            
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

            foreach (KeyValuePair<string,int> pair in dictionary)
            {
                if (pair.Value > r)
                {
                    return pair.Key;
                }
                else
                {
                    r -= pair.Value;
                }
            }

            return result;
        }

        private void Save() => _fileIO.SaveData(_words);
    }
}