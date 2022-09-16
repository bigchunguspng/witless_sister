﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Environment;
using static Witlesss.Extension;
using static Witlesss.Logger;
using static Witlesss.Strings;
using WitlessDB = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, float>>;

namespace Witlesss
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Witless
    {
        public const string START = "_start", END = "_end", LINK = "[ссылка удалена]", LF = "_LF", LF_Spaced = " " + LF + " ";

        private readonly Random _random;
        private readonly FileIO<WitlessDB> _fileIO;
        private Counter _generation;
        private int _probability;
        
        public bool HasUnsavedStuff;
        
        public Witless(long chat, int interval = 7, int probability = 20)
        {
            Chat = chat;
            _random = new Random();
            _generation = new Counter(interval);
            _fileIO = new FileIO<WitlessDB>(Path);
            DgProbability = probability;
            Load();
            PauseGeneration(30);
        }

        [JsonProperty] public long Chat { get; set; }
        [JsonProperty] public int Interval
        {
            get => _generation.Interval;
            set => _generation.Interval = value;
        }
        [JsonProperty] public int DgProbability
        {
            get => _probability;
            set => _probability = Math.Clamp(value, 0, 100);
        }
        [JsonProperty] public bool DemotivateStickers { get; set; }
        
        public WitlessDB Words { get; set; }
        public string Path => $@"{CurrentDirectory}\{DBS_FOLDER}\{DB_FILE_PREFIX}-{Chat}.json";

        public bool Eat(string text, out string eaten)
        {
            eaten = null;
            if (!SentenceIsAcceptable(text)) return false;
            
            var words = Tokenize(text);
            
            if (text.Contains('/') && text.Contains('.'))
            {
                for (var i = 0; i < words.Length; i++) if (WordIsLink(words[i])) words[i] = LINK;
            }

            int count = TokenCount();
            if (count < 14)
            {
                float weight = MathF.Round(1.4F - 0.1F * count, 1); // 1 => 1.3  |  5 => 0.9  |  13 => 0.1
                eaten = EatSimple(words, weight);
            }
            if (count > 4)
            {
                eaten = EatAdvanced(words);
            }
            
            HasUnsavedStuff = true;
            return true;

            int TokenCount() => words.Length - words.Count(x => x == LF);
        }

        private string EatAdvanced(string[] words)
        {
            words = Advance(words);
            EatSimple(words);
            return string.Join(' ', words.Select(x => x.Replace(' ', '_')));
        }
        private string EatSimple(string[] words, float weight = 1F)
        {
            var list = new List<string>(words.Length + 2) {START};
            
            list.AddRange(words);
            list.Add(END);
            list.RemoveAll(x => x == LF);
            
            for (var i = 0; i < list.Count - 1; i++)
            {
                string word = list[i];
                if (!Words.ContainsKey(word)) Words.TryAdd(word, new ConcurrentDictionary<string, float>());

                string next = list[i + 1];
                if (Words[word].ContainsKey(next))
                    Words[word][next] = MathF.Round(Words[word][next] + weight, 1);
                else
                    Words[word].TryAdd(next, weight);
            }
            return string.Join(' ', list.GetRange(1, list.Count - 2));
        }
        private string[] Advance(string[] words)
        {
            var tokens = new LinkedList<string>(words);

            if (tokens.Contains(LF))
            {
                var indexes = tokens.Select((t, i) => new {t, i}).Where(x => x.t == LF).Select(x => x.i).ToArray();
                var list = new List<string[]>(indexes.Length + 1);
                var toks = tokens.ToArray();
                var a = 0;
                foreach (int index in indexes)
                {
                    if (a == index)
                    {
                        a++;
                        continue;
                    }
                    list.Add(toks[a..index]);
                    a = index + 1;
                }
                list.Add(toks[a..tokens.Count]);
                tokens.Clear();
                for (var i = 0; i < list.Count; i++)
                {
                    list[i] = Advance(list[i]);
                    foreach (string token in list[i])
                    {
                        tokens.AddLast(token);
                    }
                }
                return tokens.ToArray();
            }
            
            UniteTokensToRight(1, 3, 20);
            UniteTokensToRight(2, 2, 20);
            UniteTokensToLeft (2, 2, 5);
            UniteTokensToRight(3, 3, 4);

            return tokens.ToArray();

            IEnumerable<string> SmallWordsSkipLast (int length) => tokens.SkipLast(1).Where(x => UnitableToken(x, length)).Reverse();
            IEnumerable<string> SmallWordsSkipFirst(int length) => tokens.Skip    (1).Where(x => UnitableToken(x, length)).Reverse();
            
            bool UnitableToken(string x, int length) => x.Length == length && !ContainsDigit(x);
            bool ContainsDigit(string x) => Regex.IsMatch(x, @"\S*\d+\S*");
            bool IsSimpleToken(string x) => !(x.Contains(' ') || ContainsDigit(x));

            void UniteTokensToRight(int length, int min, int max)
            {
                var small = SmallWordsSkipLast(length).ToArray();
                if (small.Length == 0) return;
                    
                foreach (string word in small)
                {
                    var x = tokens.Last;
                    tokens.RemoveLast();
                    var a = tokens.FindLast(word);
                    tokens.AddLast(x!);
                    var n = a?.Next;
                    var l = n?.Value.Length;
                    if (l >= min && l <= max && IsSimpleToken(n.Value))
                    {
                        a!.Value = a.Value + " " + n.Value;
                        tokens.Remove(n);
                    }
                }
            }
            void UniteTokensToLeft (int length, int min, int max)
            {
                var small = SmallWordsSkipFirst(length).ToArray();
                if (small.Length == 0) return;

                foreach (string word in small)
                {
                    var x = tokens.First;
                    tokens.RemoveFirst();
                    var a = tokens.FindLast(word);
                    tokens.AddFirst(x!);
                    var p = a?.Previous;
                    var l = p?.Value.Length;
                    if (l >= min && l <= max && IsSimpleToken(p.Value))
                    {
                        a!.Value = p.Value + " " + a.Value;
                        tokens.Remove(p);
                    }
                }
            }
        }
        private string[] Tokenize(string s) => s.ToLower().Replace("\n", LF_Spaced).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        private bool SentenceIsAcceptable(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;
            if (sentence.StartsWith('/'))
                return false;
            if (sentence.StartsWith('.'))
                return false;
            if (sentence.StartsWith("http") && !sentence.Contains(" ")) // todo regex
                return false;
            return true;
        }
        private bool WordIsLink(string word) => (word.Contains(".com") || word.Contains(".ru")) && word.Length > 20 || word.StartsWith("http") && word.Length > 7; // todo regex

        public string TryToGenerate(string word = START)
        {
            try
            {
                return Generate(word);
            }
            catch (Exception e)
            {
                LogError(e.Message);
                return "";
            }
        }
        public string TryToGenerateBackwards(string word)
        {
            try
            {
                return GenerateBackwards(word);
            }
            catch (Exception e)
            {
                LogError(e.Message);
                return "";
            }
        }

        public string GenerateByWord(string word)
        {
            word = FindMatch(word, START);
            return TryToGenerate(word);
        }
        public string GenerateByWordBackwards(string word)
        {
            word = FindMatch(word, END);
            return TryToGenerateBackwards(word);
        }
        
        private string FindMatch(string word, string alt)
        {
            if (!Words.ContainsKey(word))
            {
                var words = new List<string>();
                foreach (string key in Words.Keys)
                {
                    if (key.StartsWith(word)) words.Add(key);
                }
                if (words.Count > 0)
                    return words[_random.Next(words.Count)];

                foreach (string key in Words.Keys)
                {
                    if (word.StartsWith(key, StringComparison.Ordinal)) words.Add(key);
                }
                if (words.Count > 0)
                {
                    words.Sort(Comparison);
                    return words[0];
                }
                return alt;
            }
            return word;

            int Comparison(string x, string y) => y.Length - x.Length;
        }
        
        private string Generate(string word)
        {
            string result = "";
            string currentWord = word == START || Words.ContainsKey(word) ? word : START;

            while (currentWord != END)
            {
                result = result + " " + currentWord;
                currentWord = PickWord(Words[currentWord]);
            }

            result = result.Replace(START, "").TrimStart();
            
            return TextInRandomLetterCase(result);
        }
        private string GenerateBackwards(string word)
        {
            string result = "";
            string currentWord = word == END || Words.ContainsKey(word) ? word : END;
            
            while (currentWord != START)
            {
                result = currentWord + " " + result;
                
                var words = new ConcurrentDictionary<string, float>();
                foreach (var bunch in Words)
                {
                    if (bunch.Value.ContainsKey(currentWord) && !words.TryAdd(bunch.Key, 1)) words[bunch.Key]++;
                }
                currentWord = PickWord(words);
            }
            
            result = result.Replace(END, "").TrimEnd();
            
            return TextInRandomLetterCase(result);
        }
        private string PickWord(ConcurrentDictionary<string, float> dictionary)
        {
            var chanceTotal = 0F;
            foreach (var chance in dictionary)
            {
                chanceTotal += chance.Value;
            }
            
            float r = (float)_random.NextDouble() * chanceTotal;
            string result = END;

            foreach (var chance in dictionary)
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
        
        private async void PauseGeneration(int seconds)
        {
            _generation.Stop();
            await Task.Delay(1000 * seconds);
            Save();
            _generation.Resume();
        }
        
        public void Count()      => _generation.Count();
        public bool ReadyToGen() => _generation.Ready();

        public void Save()
        {
            if (HasUnsavedStuff) SaveNoMatterWhat();
        }

        public void SaveNoMatterWhat()
        {
            _fileIO.SaveData(Words);
            HasUnsavedStuff = false;
            Log($"DIC SAVED << {Chat}", ConsoleColor.Green);
        }

        public void Load()
        {
            Words = _fileIO.LoadData();
            HasUnsavedStuff = false;
            Log($"DIC LOADED << {Chat}");
        }

        public void Backup()
        {
            Save();
            var file = new FileInfo(Path);
            var path = $@"{CurrentDirectory}\{BACKUP_FOLDER}\{DateTime.Now:yyyy-MM-dd}";
            Directory.CreateDirectory(path);
            file.CopyTo(UniquePath($@"{path}\{DB_FILE_PREFIX}-{Chat}.json", ".json"));
        }
    }
}