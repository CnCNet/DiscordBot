﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TwitchLib.Api;

namespace DiscordBot
{
    public class Program
    {
        const ulong CNCNET_STREAMS_CHANNEL = 266329809120919554;
        const ulong DUNE2000_STREAMS_CHANNEL = 586252746294820864;
        const ulong DUNE2000_STAFF_CHANNEL = 599560934415138816;

        const string REDALERT_GAMEID = "235";
        const string REDALERT_CS_GAMEID = "10393";
        const string REDALERT_AM_GAMEID = "14999";
        const string DUNE2000_GAMEID = "1421";
        const string TIBERIANDAWN_GAMEID = "4012";
        const string TIBERIANDAWN_CO_GAMEID = "10300";
        const string TIBERIANSUN_GAMEID = "1900";
        const string TIBERIANSUN_FS_GAMEID = "20015";
        const string REDALERT2_GAMEID = "16580";
        const string REDALERT2_YR_GAMEID = "5090";

        private DiscordSocketClient DiscordClient;
        private TwitchAPI TwitchApi;
        private Dictionary<string, Streamer> Streamers = new Dictionary<string, Streamer>(100);

        private List<string> GameIds =
            new List<string>() {
                REDALERT_GAMEID, REDALERT_CS_GAMEID, REDALERT_AM_GAMEID, DUNE2000_GAMEID, TIBERIANDAWN_GAMEID,
                TIBERIANDAWN_CO_GAMEID, TIBERIANSUN_GAMEID, TIBERIANSUN_FS_GAMEID, REDALERT2_GAMEID,
                REDALERT2_YR_GAMEID
            };

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            TwitchApi = new TwitchAPI();
            TwitchApi.Settings.ClientId = File.ReadAllText("DiscordBot_ClientId.txt");

            DiscordClient = new DiscordSocketClient();
            DiscordClient.Log += Log;

            await DiscordClient.LoginAsync(TokenType.Bot, File.ReadAllText("DiscordBot_Token.txt"));
            await DiscordClient.StartAsync();

            await GetLiveStreams();
        }

        private async Task GetLiveStreams()
        {
            while (true)
            {
                await Task.Delay(60 * 1000);

                foreach (var item in Streamers.Where(x => x.Value.TimedOut).ToList())
                    Streamers.Remove(item.Key);

                var res = await TwitchApi.Helix.Streams.GetStreamsAsync(null, null, 20, GameIds);

                foreach (var stream in res.Streams.Where(x => x.Type == "live"))
                {
                    Streamer streamer;

                    if (Streamers.TryGetValue(stream.UserId, out streamer))
                    {
                        streamer.SetLastStreamTick();
                    }
                    else
                    {
                        string message =
                            string.Format(
                                "**{0} is now live!** `{1}`\n{2}\n{3}{4}", 
                                stream.UserName,
                                stream.Title.Replace("`", ""),
                                GetGameName(stream.GameId),
                                "https://www.twitch.tv/",
                                stream.UserName);

                        if (stream.GameId == DUNE2000_GAMEID)
                        {
                            var c = DiscordClient.GetChannel(DUNE2000_STREAMS_CHANNEL) as IMessageChannel;
                            await c.SendMessageAsync(message);
                        }

                        var chan = DiscordClient.GetChannel(CNCNET_STREAMS_CHANNEL) as IMessageChannel;
                        await chan.SendMessageAsync(message);

                        streamer = new Streamer();
                        streamer.Username = stream.UserName;
                        streamer.SetLastStreamTick();

                        Streamers.Add(stream.UserId, streamer);
                    }
                }
            }
        }

        private string GetGameName(string id)
        {
            switch(id)
            {
                case REDALERT_GAMEID:
                case REDALERT_CS_GAMEID:
                case REDALERT_AM_GAMEID:
                    return "Command & Conquer: Red Alert";
                case TIBERIANDAWN_GAMEID:
                case TIBERIANDAWN_CO_GAMEID:
                    return "Command & Conquer";
                case DUNE2000_GAMEID:
                    return "Dune 2000";
                case TIBERIANSUN_GAMEID:
                case TIBERIANSUN_FS_GAMEID:
                    return "Command & Conquer: Tiberian Sun";
                case REDALERT2_GAMEID:
                    return "Command & Conquer: Red Alert 2";
                case REDALERT2_YR_GAMEID:
                    return "Command & Conquer: Yuri's Revenge";
                default:
                    return "";
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }

    class Streamer
    {
        const int TIMEOUT = 300; //seconds

        public string Username { get; set; }

        public bool TimedOut
        {
            get { return TimeSpan.FromTicks(DateTime.UtcNow.Ticks - LastStreamTick).TotalSeconds >= TIMEOUT; }
        }

        long LastStreamTick;

        public void SetLastStreamTick()
        {
            LastStreamTick = DateTime.UtcNow.Ticks;
        }
    }
}
