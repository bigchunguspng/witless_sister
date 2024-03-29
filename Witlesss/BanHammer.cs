﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Witlesss
{
    public class BanHammer
    {
        private readonly FileIO<Dictionary<long, DateTime>>   BansIO;
        private readonly        Dictionary<long, DateTime>    BannedChats;
        private readonly        Dictionary<long, ChatBotUsage> SussyChats;


        public BanHammer(Bot bot)
        {
            Bot = bot;

            BansIO =  new FileIO<Dictionary<long, DateTime>>($@"{DBS_FOLDER}\bans.json");
            BannedChats = BansIO.LoadData();
            SussyChats = new();
        }

        private Bot Bot { get; }

        public void BanChat(long chat, double minutes = 30)
        {
            BannedChats.TryAdd(chat, DateTime.Now + TimeSpan.FromMinutes(minutes));
            SussyChats.Remove(chat);
            if (ChatIsBaka(chat)) BakaFrom(chat).Banned = true;
            SaveBanList();
            Log($"{chat} >> BANNED", ConsoleColor.Magenta);
        }

        public void UnbanChat(long chat)
        {
            var s = BannedChats.Remove(chat);
            if (ChatIsBaka(chat)) BakaFrom(chat).Banned = false;
            SaveBanList();
            Log($"{chat} >> {(s ? "UNBANNED" : "WAS NOT BANNED")}", ConsoleColor.Magenta);
        }
        private void SaveBanList() => BansIO.SaveData(BannedChats);

        private bool  ChatIsBaka(long chat) => Bot.SussyBakas.ContainsKey(chat);
        private Witless BakaFrom(long chat) => Bot.SussyBakas[chat];


        public  void GiveBans() => BannedChats.Keys.Where(ChatIsBaka).ForEach(chat => BakaFrom(chat).Banned = true);

        public  void PullBanStatus (long chat) => BakaFrom(chat).Banned = BannedChats.ContainsKey(chat);
        public  bool ChatIsBanned  (Witless w) => CheckBan(w.Chat, w.Banned);
        public  bool ChatIsBanned  (long chat) => CheckBan(chat, BannedChats.ContainsKey(chat));

        private bool CheckBan  (long chat, bool banned) => banned && !BanIsOver(chat);
        private bool BanIsOver (long chat)
        {
            var date = BannedChats[chat];
            var over = DateTime.Now > date;
            if (over) UnbanChat(chat);
            else Bot.SendMessage(chat, U_ARE_BANNED_LOL(chat));
            return over;
        }

        private string U_ARE_BANNED_LOL(long chat)
        {
            var sb = new StringBuilder();
            sb.Append(Pick(FAIL_EMOJI_1)).Append(" Ваш чат был временно забанен ").Append(Pick(FAIL_EMOJI_1));
            sb.Append("\n\n");
            sb.Append(Pick(RANDOM_EMOJI)).Append(" Эта команда станет доступной через ");
            sb.Append(TimeLeft(BannedChats[chat]));
            return sb.ToString();
        }

        private static string TimeLeft(DateTime date)
        {
            var time = date - DateTime.Now;
            var h = time.Hours + time.Days * 24;
            var m = time.Minutes;
            var s = time.Seconds;

            var sb = new StringBuilder();
            if (h > 0)          sb.Append(h).Append(" час"   ).Append(HOURS_ED(h)).Append(' ');
            if (m > 0)          sb.Append(m).Append(" минут" ).Append( MINS_ED(m)).Append(' ');
            if (h < 1 && m < 2) sb.Append(s).Append(" секунд").Append( MINS_ED(s));

            return sb.ToString().Trim();
        }

        public void Suspect(long chat, TimeSpan time)
        {
            if (SussyChats.TryGetValue(chat, out var x))
            {
                if (x.ForgiveDate < DateTime.Now)
                {
                    x.HangingTime = TimeSpan.Zero;
                    x.ForgiveDate = DateTime.Now;
                }
                x.HangingTime += time;
                x.ForgiveDate += time;
            }
            else
            {
                x = new ChatBotUsage(time, DateTime.Now + TimeSpan.FromMinutes(5));
                SussyChats.Add(chat, x);
            }
            Log($@"{chat} >> {FormatTime(x.HangingTime)} by {x.ForgiveDate:T}", ConsoleColor.DarkGray);

            if (x.HangingTime > TimeSpan.FromMinutes(2) && x.ForgiveDate > DateTime.Now)
            {
                BanChat(chat, x.HangingTime.Minutes);
                Log($"{chat} >> GET BANNED LMAO", ConsoleColor.Yellow);
            }
        }
    }

    public class ChatBotUsage
    {
        public ChatBotUsage(TimeSpan hangingTime, DateTime forgiveDate)
        {
            HangingTime = hangingTime;
            ForgiveDate = forgiveDate;
        }

        public TimeSpan HangingTime { get; set; }
        public DateTime ForgiveDate { get; set; }
    }
}