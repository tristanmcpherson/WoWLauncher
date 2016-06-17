using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using HtmlAgilityPack;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.WPF;
using WoWLauncher.Properties;

namespace WoWLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Minimize_OnMouseEnter(object sender, MouseEventArgs e)
        {
           MinimizePath.Fill = Brushes.White;
        }

        private void Exit_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }
        private void Exit_OnMouseEnter(object sender, MouseEventArgs e)
        {
            ExitPath.Stroke = Brushes.White;
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowTitle.Content = Settings.Default.ServerTitle;

            IProgress<ActionProgress> status = new Progress<ActionProgress>(info =>
            {
                UpdateLabel.Content = info.Text;
                ProgressBar.Value = info.Percent;
                ProgressBar.IsIndeterminate = info.Percent == -1;
                if (info.Percent == 100)
                {
                    Launch.Content = "Launch";
                    Launch.IsEnabled = true;
                }
            });

            try
            {
                var news = await WoWUtility.GetStringTaskAsync(Settings.Default.NewsUrl);

                if (!string.IsNullOrWhiteSpace(Settings.Default.NewsParser))
                {
                    var parser = new NewsParser(html =>
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);
                        return from node in doc.DocumentNode.SelectNodes("//tr[@id='postRowId']")
                            let title = node.SelectSingleNode("./td[2]")
                            select new Post
                            {
                                Title = title.SelectSingleNode("./a/b").InnerText.Trim(),
                                Author = node.SelectSingleNode("./td[4]/font").InnerText.Trim(),
                                Date = DateTime.Parse((title.SelectSingleNode("./div[1]/div[2]").InnerText)),
                                Url = title.SelectSingleNode("./a").Attributes["href"].Value
                            };
                    }, $@"<p style=""text-align:left;""><a href=""{0}"" style=""color: white"">{1}</a><br><span style=""float:right;""><i style=""color: gray"">{2}</i></span></p>");

                    NewsTextBlock.Text = parser.Parse(news).Select(
                        x => string.Format(parser.PostFormat, x.Url, x.Title, x.Author, x.Date))
                        .Aggregate((current, next) => current + next);
                }
                else
                {
                    NewsTextBlock.Text = news ?? "Failed to fetch news";
                }
            }
            catch (HttpRequestException)
            {
                status.Report(new ActionProgress("Failed to fetch news", 100));
            }

            try
            {
                await WoWUtility.GetWoWFolder(status);
                await WoWUtility.CheckUpdates(status);
            }
            catch (HttpRequestException)
            {
                status.Report(new ActionProgress("Failed to check for updates", 100));
            }
            catch (ArgumentNullException)
            {
                //progress.Report(new ActionProgress("Cancelled finding wow folder", 100));
            }
        }

        private void Register_Click(object sender, RoutedEventArgs routedEventArgs)
        {
            Process.Start(Settings.Default.RegisterUrl);
        }

        private void Launch_Click(object sender, RoutedEventArgs routedEventArgs)
        {
            WoWUtility.SetRealmlist(Settings.Default.Realmlist, Settings.Default.Patch);
            Process.Start(Path.Combine(WoWUtility.FindWoWFolder(Settings.Default.Patch), "Wow.exe"));
        }

        private void SetWoWFolder_OnClick(object sender, RoutedEventArgs e)
        {
            
        }

        private void NewsTextBlock_OnLinkClicked(object sender, RoutedEvenArgs<HtmlLinkClickedEventArgs> args)
        {
            Process.Start(args.Data.Link);
        }
    }
}
