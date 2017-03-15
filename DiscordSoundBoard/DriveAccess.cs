using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace DiscordSoundBoard
{
    class DriveAccess
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        static string[] Scopes = { DriveService.Scope.DriveReadonly, DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadataReadonly, DriveService.Scope.DriveAppdata, DriveService.Scope.DriveMetadata, DriveService.Scope.DrivePhotosReadonly };
        static string ApplicationName = "DiscordDrive";
        static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        static UserCredential credential;
        public static void getAuth()
        {


            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }
        }
        public static void DownloadFromDrive() {

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

        // Define parameters of request.
        string pageToken=null;
            FilesResource.ListRequest listRequest = service.Files.List();

            if (config.AppSettings.Settings["DriveFolder"].Value == "NULL")
            {
                Console.WriteLine("Please Enter Drive Folder Name");
                config.AppSettings.Settings["DriveFolder"].Value = Console.ReadLine();
            }

            var path = config.AppSettings.Settings["SoundPath"].Value; //get path from config file

            String folderName = config.AppSettings.Settings["DriveFolder"].Value; //get path from config file

            String folderID = null;
            try
            {
                do
                {
                    listRequest = service.Files.List();
                    listRequest.Q = "mimeType='application/vnd.google-apps.folder'";
                    listRequest.Spaces = "drive";
                    listRequest.Fields = "nextPageToken, files(id, name)";
                    listRequest.PageToken = pageToken;

                    // List files.
                    var files = listRequest.Execute();
                    Console.WriteLine("Files:");
                    foreach (var file in files.Files)
                    {
                        if (folderName == file.Name)
                        {
                            Console.WriteLine("{0} ({1})", file.Name, file.Id);
                            folderID = file.Id;
                        }
                    }
                    pageToken = files.NextPageToken;
                } while (pageToken != null);
                //If path config was not changed prompt user with a request for the path
                do
                {
                    listRequest = service.Files.List();
                    listRequest.Q = "'" + folderID + "'" + " in parents";
                    Console.WriteLine(listRequest.Q);
                    listRequest.Spaces = "drive";
                    listRequest.Fields = "nextPageToken, files(id, name)";
                    listRequest.PageToken = pageToken;

                    // List files.
                    var files = listRequest.Execute();
                    Console.WriteLine("Files:");
                    foreach (var file in files.Files)
                    {
                        Console.WriteLine("{0} ({1})", file.Name, file.Id);
                        var fileId = file.Id;
                        var request = service.Files.Get(fileId);
                        var stream = new System.IO.MemoryStream();
                        if (!System.IO.File.Exists(path + file.Name)) {
                            request.MediaDownloader.ProgressChanged +=
                            (IDownloadProgress progress) =>
                            {
                                switch (progress.Status)
                                {
                                    case DownloadStatus.Downloading:
                                        {
                                            Console.WriteLine(progress.BytesDownloaded);
                                            break;
                                        }
                                    case DownloadStatus.Completed:
                                        {
                                            Console.WriteLine("Download complete.");
                                            break;
                                        }
                                    case DownloadStatus.Failed:
                                        {
                                            Console.WriteLine("Download failed.");
                                            break;
                                        }
                                }
                            };
                            request.Download(stream);
                            if (!System.IO.Directory.Exists(path)){
                                System.IO.Directory.CreateDirectory(path);
                            }
                            var fileStream = System.IO.File.Create(path + file.Name);
                            stream.WriteTo(fileStream);
                        }
                    }
                    pageToken = files.NextPageToken;
                } while (pageToken != null);
            }
            catch (Exception e)
            {
                Console.WriteLine("there was a problem downloading the files");
            }

        }
    }
}
