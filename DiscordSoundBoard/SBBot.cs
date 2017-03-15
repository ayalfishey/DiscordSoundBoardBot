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
using System.Configuration;

namespace DiscordSoundBoard
{
    class SBBot
    {
        public static DiscordClient bot;
        public static CommandService commands;
        public static IAudioClient _vClient;
        private static bool playingSong = false;
        private string path;
        private string botToken;
        private IEnumerable<String> allfiles;
        private Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);


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


            commands.CreateCommand("reload")
             .Do(async (e) =>
             {
                 await e.Channel.SendMessage("Reloading Commands");
                 LoadAllCommands();
                 await e.Channel.SendMessage("Done Reloading");

             });

            //Command clears all messeges but the first one
            commands.CreateCommand("clean")
                .Do(async (e) =>
            {
                Message[] messagesToDelete;
                messagesToDelete = await e.Channel.DownloadMessages(100); //request 100 last posts
                await e.Channel.DeleteMessages(messagesToDelete); //delete messeges
            });

            //If path config was not changed prompt user with a request for the path
            if (config.AppSettings.Settings["SoundPath"].Value == "NULL")
            {
                Console.WriteLine("Please Enter Sound Clips Path");
                config.AppSettings.Settings["SoundPath"].Value = Console.ReadLine();
            }

            path = config.AppSettings.Settings["SoundPath"].Value; //get path from config file

            // get all the files in the sound directory without the extention
            LoadAllCommands();

            // make the command to list commands
            commands.CreateCommand("list")
              .Do(async (e) =>
            {
                //manualy added Help and Clear
                String list= "!List, !Clean,!Reload ";
                //go through each of the files and concatanate them with the output string
                foreach (String com in allfiles)
                {
                    list +="!"+com+", ";
                }
                //send output to the channel
                await e.Channel.SendMessage(list); 

            });

            //If token config was not changed prompt user with a request for the token
            if (config.AppSettings.Settings["DiscordToken"].Value == "NULL")
            {
                Console.WriteLine("\nPlease Enter Discord Bot Token");
                config.AppSettings.Settings["DiscordToken"].Value = Console.ReadLine();
            }

            botToken = config.AppSettings.Settings["DiscordToken"].Value; //get token from config file

            //connect using bot token
            bot.ExecuteAndWait(async () =>
            {
                try
                {
                    await bot.Connect(botToken, TokenType.Bot);

                    //Save Token
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
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

        private void LoadAllCommands()
        {
            try
            {
                DriveAccess.DownloadFromDrive();
                allfiles = Directory
                    .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Select(Path.GetFileNameWithoutExtension);
                //make a command for each of those files based on its name
                foreach (String com in allfiles)
                {
                    Console.WriteLine(com);
                    LoadCommand(com);
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
            }
            catch
            {
                Console.WriteLine("Bad Folder Path");
                Environment.Exit(0);
            }
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


        // Thank you Rand from the "Discord API" Discord for this peace of code ajusted by me to fit this project
        //(https://github.com/DjRand/)

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
