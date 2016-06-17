using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CloudFlareUtilities;
using WoWLauncher.Properties;

namespace WoWLauncher
{
    //public class HttpRequestException : Exception
    //{
    //    public string ReasonPhrase { get; }

    //    public HttpRequestException(string reasonPhrase)
    //    {
    //        ReasonPhrase = reasonPhrase;
    //    }
    //}

    public static class WoWUtility
    {
        public static async Task<string> GetStringTaskAsync(string url)
        {
            using (var httpClient = new HttpClient(new ClearanceHandler())/* { Timeout = Settings.Default.Timeout }*/)
            {
                using (var response = await httpClient.GetAsync(url))
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException(response.ReasonPhrase);
                    return content;
                }
            }
        }

        public static async Task CheckUpdates(IProgress<ActionProgress> progress)
        {
            progress.Report(new ActionProgress("Checking for updates..."));

            try
            {
                var data = Path.Combine(FindWoWFolder(Settings.Default.Patch), "Data");
                var updates = await GetStringTaskAsync(Settings.Default.UpdateUrl);
                if (string.IsNullOrEmpty(updates))
                {
                    progress.Report(new ActionProgress("No updates found", 100));
                    return;
                }

                var list = new List<string>(Regex.Split(updates, Environment.NewLine));

                foreach (var url in list)
                {
                    Uri uri;
                    var result = Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

                    if (!result)
                    {
                        progress.Report(new ActionProgress("Invalid update file"));
                        return;
                    }

                    var fileName = uri.Segments.Last();
                    var fileSize = CheckSize(url);
                    var localFileName = Path.Combine(data, fileName);
                    var fileInfo = new FileInfo(localFileName);
                    if (fileInfo.Exists)
                    {
                        if (fileInfo.Length != fileSize)
                            fileInfo.MoveTo(fileInfo.FullName + ".bak");
                    }

                    progress.Report(new ActionProgress($"Downloading file {fileName}"));
                    await DownloadFile(url, fileInfo.FullName, progress);
                }
                progress.Report(new ActionProgress("Up to date", 100));
            }
            catch (HttpRequestException ex)
            {
                progress.Report(new ActionProgress($"Failed to check for updates: {ex.Message}", 100));
            }
            catch (TaskCanceledException)
            {
                progress.Report(new ActionProgress("Failed to check for updates: Server timed out", 100));
            }
            catch (WebException)
            {
                progress.Report(new ActionProgress("Could not find specified update file to download", 100));
            }
        }

        private static async Task DownloadFile(string url, string path, IProgress<ActionProgress> progress)
        {
            using (var client = new WebClient())
            {
                client.DownloadProgressChanged += (s, e) =>
                    progress?.Report(new ActionProgress(string.Empty, e.ProgressPercentage));
                await client.DownloadFileTaskAsync(url, path);
            }
        }

        private static long CheckSize(string url)
        {
            var req = WebRequest.CreateHttp(url);
            req.Method = "HEAD";
            var resp = (HttpWebResponse)(req.GetResponse());
            return resp.ContentLength;
        }

        // TODO: Fix this `Settings` dependent code
        public static async Task GetWoWFolder(IProgress<ActionProgress> progress)
        {
            var folder = Settings.Default.WoWFolder == string.Empty ? await Task.Factory.StartNew(() => FindWoWFolder(Settings.Default.Patch)) : Settings.Default.WoWFolder;
            if (folder == null)
            {
                progress.Report(new ActionProgress("Cannot find WoW Directory. Please choose"));
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    folder = dialog.SelectedPath;
                }
            }
            Settings.Default.WoWFolder = folder;
            Settings.Default.Save();
        }

        public static string FindWoWFolder(string patchVersion)
        {
            var possibleFolders = new[]
            {
                @"World of Warcraft\",
                @"Program Files\World of Warcraft\",
                @"Program Files (x86)\World of Warcraft\",
                @"Games\World of Warcraft\"
            };
            var drives = DriveInfo.GetDrives();

            foreach (var drive in drives)
            {
                foreach (var folder in possibleFolders)
                {
                    var path = Path.Combine(drive.Name, folder);
                    if (!Directory.Exists(path)) continue;
                    var wowExecutable = FindWoWExecutable(path, patchVersion);
                    if (wowExecutable == null) continue;
                    return wowExecutable;
                }
            }

            return drives.Select(drive => FindWoWExecutable(drive.Name, patchVersion)).FirstOrDefault(wowPath => wowPath != null);
        }

        private static string FindWoWExecutable(string wowPath, string patchVersion)
        {
            try {
                var wowExes = Directory.GetFiles(wowPath, "Wow.exe", SearchOption.AllDirectories);

                return (from wowExe in wowExes
                        let versionInfo = FileVersionInfo.GetVersionInfo(wowExe)
                        where versionInfo.FileVersion.Replace(", ", ".") == patchVersion
                        select new FileInfo(wowExe).DirectoryName).FirstOrDefault();
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string GetLocaleFolder(string patchVersion)
        {
            var wowDirectory = FindWoWFolder(patchVersion);
            var locales = new[]
            {
                "US",
                "GB"
            };

            return (from locale in locales
                    select Path.Combine(wowDirectory, $@"Data\en{locale}\")
                    into folder
                    where Directory.Exists(folder)
                    select folder).FirstOrDefault();
        }

        public static void SetRealmlist(string realmList, string patchVersion)
        {
            var file = Path.Combine(GetLocaleFolder(patchVersion), "realmlist.wtf");
            if (File.Exists(file))
            {
                var contents = File.ReadAllText(file);
                if (contents.Contains(realmList)) return;
                File.Copy(file, file + ".backup", true);
            }
            File.WriteAllText(file, realmList);
        }
    }
}