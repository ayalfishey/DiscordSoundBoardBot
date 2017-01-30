using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Audio;
using NAudio.Wave;
using System.IO;

namespace DiscordSoundBoard
{
    class SBBot
    {
        public static DiscordClient bot;
        public static CommandService commands;
        public static IAudioClient _vClient;
        private static bool playingSong = false;
        private string path;    //@"D:\SoundClips\"
        private string botToken;
        private IEnumerable<String> allfiles;


        public SBBot()
        {
            //Setting Up Logger
            bot = new DiscordClient(x =>
                {
                    x.LogLevel = LogSeverity.Info;
                    x.LogHandler = Log;
                });
            //Setting Up Audio
            bot.UsingAudio(x =>
           {
               x.Mode = AudioMode.Outgoing;
           });

            //Setting Up Commands
            bot.UsingCommands(x =>
           {
               x.PrefixChar = '!';
               x.AllowMentionPrefix = true;

           });
            commands = bot.GetService<CommandService>();

            //Command clears all messeges but the first one
            commands.CreateCommand("clear")
                .Do(async (e) =>
            {
                Message[] messagesToDelete;
                messagesToDelete = await e.Channel.DownloadMessages(100); //request 100 last posts
                var temp = new List<Message>(messagesToDelete); //convert thhe array of messeges to a list for easier managment
                temp.RemoveAt(messagesToDelete.Length - 1); //removes the first messege from the list
                messagesToDelete = temp.ToArray();  //convert it back to array
                await e.Channel.DeleteMessages(messagesToDelete); //delete messeges

            });

            path = System.Configuration.ConfigurationManager.AppSettings["SoundPath"]; //get path from config file
            botToken = System.Configuration.ConfigurationManager.AppSettings["DiscordToken"]; //get token from config file
            if (System.Configuration.ConfigurationManager.AppSettings["SoundPath"] == "NULL" || System.Configuration.ConfigurationManager.AppSettings["DiscordToken"] == "NULL") //if path or token were not changed make sure to edit them
            {
                Console.WriteLine("Please edit the config file before running");
                Environment.Exit(0);
            }

            // get all the files in the sound directory without the extention
            try
            {
                allfiles = Directory
                    .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Select(Path.GetFileNameWithoutExtension);
                //make a command for each of those files based on its name
                foreach (String com in allfiles)
                {
                    Console.WriteLine(com);
                    LoadCommand(com);
                }
            }
            catch
            {
                Console.WriteLine("Bad Folder Path");
                Environment.Exit(0);
            }

            commands.CreateCommand("help")
              .Do(async (e) =>
            {
                String list= "!Clear, ";
                foreach (String com in allfiles)
                {
                    list +="!"+com+", ";
                }
                await e.Channel.SendMessage(list); //delete messeges

            });



            //connect using bot token
            bot.ExecuteAndWait(async () =>
            {
                try
                {
                    await bot.Connect(botToken, TokenType.Bot);
                }
                catch
                {
                    Console.WriteLine("\nBad Bot Token");
                    Environment.Exit(0);
                }
            });
        }
        private void Log(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        //Create a single command
        private void LoadCommand(String command)
        {
            commands.CreateCommand(command)
                .Do(async (e) =>
            {
                //if file is not playing find users channel and play audio
                if (!playingSong)
                {
                    Channel voiceChannel = e.User.VoiceChannel;
                    await SendAudio(path + command + ".mp3", voiceChannel);
                }
            });
        }

        public static async Task SendAudio(string filepath, Channel voiceChannel)
        {
            // When we use the !play command, it'll start this method

            // The comment below is how you'd find the first voice channel on the server "Somewhere"
            //var voiceChannel = _client.FindServers("Somewhere").FirstOrDefault().VoiceChannels.FirstOrDefault();
            // Since we already know the voice channel, we don't need that.
            // So... join the voice channel:
            _vClient = await bot.GetService<AudioService>().Join(voiceChannel);

            // Simple try and catch.
            try
            {

                var channelCount = bot.GetService<AudioService>().Config.Channels; // Get the number of AudioChannels our AudioService has been configured to use.
                var OutFormat = new WaveFormat(48000, 16, channelCount); // Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.

                using (var MP3Reader = new Mp3FileReader(filepath)) // Create a new Disposable MP3FileReader, to read audio from the filePath parameter
                using (var resampler = new MediaFoundationResampler(MP3Reader, OutFormat)) // Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
                {
                    resampler.ResamplerQuality = 60; // Set the quality of the resampler to 60, the highest quality
                    int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
                    byte[] buffer = new byte[blockSize];
                    int byteCount;
                    // Add in the "&& playingSong" so that it only plays while true. For our cheesy skip command.
                    // AGAIN
                    // WARNING
                    // YOU NEED
                    // vvvvvvvvvvvvvvv
                    // opus.dll
                    // libsodium.dll
                    // ^^^^^^^^^^^^^^^
                    // If you do not have these, this will not work.
                    while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0) // Read audio into our buffer, and keep a loop open while data is present
                    {
                        if (byteCount < blockSize)
                        {
                            // Incomplete Frame
                            for (int i = byteCount; i < blockSize; i++)
                                buffer[i] = 0;
                        }

                        _vClient.Send(buffer, 0, blockSize); // Send the buffer to Discord
                    }
                    await _vClient.Disconnect();
                }
            }
            catch
            {
                System.Console.WriteLine("Something went wrong. :(");
            }
            await _vClient.Disconnect();
        }

    }

}
