using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TwitchLib.Api;
using System.Net.Http;
using Newtonsoft.Json;

namespace DiscordBot
{
    public class Program
    {
        #region TempConstants The bot doesn't have any commands yet so we're using constants for now...
        //discord channel ids
        const ulong CNCNET_STREAMS_CHANNEL = 266329809120919554;
        const ulong DUNE2000_STREAMS_CHANNEL = 586252746294820864;
        const ulong TIBERIANSUN_STREAMS_CHANNEL = 625864665208979469;
        const ulong YURISREVENGE_STREAMS_CHANNEL = 437000675214491658;
        const ulong DUNE2000_STAFF_CHANNEL = 599560934415138816;

        //twitch game ids
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

        //mixer type ids
        const string REDALERT_TYPEID = "73617";
        const string REDALERT_CS_TYPEID = "124383";
        const string REDALERT_AM_TYPEID = "103176";
        const string DUNE2000_TYPEID = "66027";
        const string TIBERIANDAWN_TYPEID = "1971";
        const string TIBERIANDAWN_CO_TYPEID = "73608";
        const string TIBERIANSUN_TYPEID = "73632";
        const string TIBERIANSUN_FS_TYPEID = "73602";
        const string REDALERT2_TYPEID = "73620";
        const string REDALERT2_YR_TYPEID = "73623";

        private List<string> GameIds =
            new List<string>() {
                REDALERT_GAMEID, REDALERT_CS_GAMEID, REDALERT_AM_GAMEID, DUNE2000_GAMEID, TIBERIANDAWN_GAMEID,
                TIBERIANDAWN_CO_GAMEID, TIBERIANSUN_GAMEID, TIBERIANSUN_FS_GAMEID, REDALERT2_GAMEID,
                REDALERT2_YR_GAMEID
            };

        private List<string> TypeIds =
            new List<string>() {
                REDALERT_TYPEID, REDALERT_CS_TYPEID, REDALERT_AM_TYPEID, DUNE2000_TYPEID, TIBERIANDAWN_TYPEID,
                TIBERIANDAWN_CO_TYPEID, TIBERIANSUN_TYPEID, TIBERIANSUN_FS_TYPEID, REDALERT2_TYPEID,
                REDALERT2_YR_TYPEID
            };
        #endregion

        private DiscordSocketClient DiscordClient;
        private TwitchAPI TwitchApi;
        private Dictionary<string, Streamer> Streamers = new Dictionary<string, Streamer>(100);

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

                try
                {
                    //twitch
                    var res = await TwitchApi.Helix.Streams.GetStreamsAsync(null, null, 20, GameIds);

                    foreach (var s in res.Streams.Where(x => x.Type == "live"))
                        await Announce(
                            s.UserId,
                            s.UserName,
                            s.GameId,
                            s.Title,
                            "https://www.twitch.tv/" + s.UserName);


                    //mixer.com
                    string url = 
                        string.Format(
                            "https://mixer.com/api/v1/channels?fields={0}&where={1}", 
                            "userId,token,online,name,typeId", 
                            "typeId:in:" + string.Join(";", TypeIds));

                    using (var client = new HttpClient())
                    using (var response = await client.GetAsync(url))
                    using (var content = response.Content)
                    {
                        var json = await content.ReadAsStringAsync();

                        var streams = JsonConvert.DeserializeObject<List<Stream>>(json);

                        foreach (var s in streams.Where(x => x.online))
                            await Announce(
                                "m_" + s.userId.ToString(),
                                s.token,
                                s.typeId.ToString(),
                                s.name,
                                "https://mixer.com/" + s.token);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private async Task Announce(string userId, string userName, string gameId, string title, string url)
        {
            Streamer streamer;

            if (Streamers.TryGetValue(userId, out streamer))
            {
                streamer.SetLastStreamTick();
            }
            else
            {
                string message =
                    string.Format(
                        "**{0} is now live!** `{1}`\n{2}\n{3}",
                        userName,
                        title.Replace("`", ""),
                        GetGameName(gameId),
                        url);

                ulong chanId = 0;

                switch (gameId)
                {
                    case DUNE2000_GAMEID:
                    case DUNE2000_TYPEID:
                        chanId = DUNE2000_STREAMS_CHANNEL; break;
                    case TIBERIANSUN_GAMEID:
                    case TIBERIANSUN_FS_GAMEID:
                    case TIBERIANSUN_TYPEID:
                    case TIBERIANSUN_FS_TYPEID:
                        chanId = TIBERIANSUN_STREAMS_CHANNEL; break;
                    case REDALERT2_GAMEID:
                    case REDALERT2_YR_GAMEID:
                    case REDALERT2_TYPEID:
                    case REDALERT2_YR_TYPEID:
                        chanId = YURISREVENGE_STREAMS_CHANNEL; break;
                }

                if (chanId != 0)
                {
                    var c = DiscordClient.GetChannel(chanId) as IMessageChannel;
                    await c.SendMessageAsync(message);
                }

                var chan = DiscordClient.GetChannel(CNCNET_STREAMS_CHANNEL) as IMessageChannel;
                await chan.SendMessageAsync(message);

                streamer = new Streamer();
                streamer.Username = userName;
                streamer.SetLastStreamTick();

                Streamers.Add(userId, streamer);
            }
        }

        private string GetGameName(string id)
        {
            switch(id)
            {
                case REDALERT_GAMEID:
                case REDALERT_CS_GAMEID:
                case REDALERT_AM_GAMEID:
                case REDALERT_TYPEID:
                case REDALERT_CS_TYPEID:
                case REDALERT_AM_TYPEID:
                    return "Command & Conquer: Red Alert";
                case TIBERIANDAWN_GAMEID:
                case TIBERIANDAWN_CO_GAMEID:
                case TIBERIANDAWN_TYPEID:
                case TIBERIANDAWN_CO_TYPEID:
                    return "Command & Conquer";
                case DUNE2000_GAMEID:
                case DUNE2000_TYPEID:
                    return "Dune 2000";
                case TIBERIANSUN_GAMEID:
                case TIBERIANSUN_FS_GAMEID:
                case TIBERIANSUN_TYPEID:
                case TIBERIANSUN_FS_TYPEID:
                    return "Command & Conquer: Tiberian Sun";
                case REDALERT2_GAMEID:
                case REDALERT2_TYPEID:
                    return "Command & Conquer: Red Alert 2";
                case REDALERT2_YR_GAMEID:
                case REDALERT2_YR_TYPEID:
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

    class Stream
    {
        public uint userId { get; set; }
        public string token { get; set; }
        public bool online { get; set; }
        public string name { get; set; }
        public uint typeId { get; set; }
    }
}
