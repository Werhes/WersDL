using iNKORE.UI.WPF.TrayIcons;
using MicaWPF.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NewtonsoftJson = Newtonsoft.Json;

namespace YT_DLP_GUI
{
    public partial class MainWindow : MicaWindow
    {
        private bool isDownloading = false;
        private Process currentProcess = null;
        private DispatcherTimer clipboardTimer;
        private int currentMaxHeight = 2160;
        private Dictionary<string, string> locMain = new Dictionary<string, string>();
        private Dictionary<string, string> locGui = new Dictionary<string, string>();

        public static class Localization
        {
            public static Dictionary<string, string> LoadLocalization(string language, string category)
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"YT_DLP_GUI.loc.{language}.json";

                using Stream stream = assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    Properties.Settings.Default.lang = "en";
                    throw new FileNotFoundException($"Cannot find embedded localization {resourceName}.");
                }

                using StreamReader reader = new StreamReader(stream);
                var jsonContent = reader.ReadToEnd();
                var localizationData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>(jsonContent);
                if (localizationData != null &&
                    localizationData.ContainsKey("categories") &&
                    localizationData["categories"].ContainsKey("loc") &&
                    localizationData["categories"]["loc"].ContainsKey(category))
                {
                    return localizationData["categories"]["loc"][category];
                }

                Properties.Settings.Default.lang = "en";
                throw new KeyNotFoundException($"Cannot find category '{category}' in localization {resourceName}");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            UpdateComboBoxItems();
            CheckForUpd();

            if (string.IsNullOrEmpty(Properties.Settings.Default.lang))
            {
                string systemLang = CultureInfo.CurrentUICulture.Name.ToLower();
                string isoLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();

                Properties.Settings.Default.lang = systemLang switch
                {
                    "zh-tw" or "zh-hk" or "zh-mo" => "tw",
                    "zh-cn" or "zh-sg" or "zh-chs" => "zh",

                    _ => isoLang switch
                    {
                        "uk" => "uk",
                        "be" => "be",
                        "kk" => "kk",
                        "uz" => "uz",
                        "tk" => "tk",
                        "ky" => "ky",
                        "tg" => "tg",
                        "az" => "az",
                        "hy" => "hy",
                        "ka" => "ka",
                        "bg" => "bg",
                        "ro" => "ro",
                        "pl" => "pl",
                        "cs" => "cs",
                        "sk" => "sk",
                        "hu" => "hu",
                        "el" => "el",
                        "lv" => "lv",
                        "lt" => "lt",
                        "et" => "et",
                        "fi" => "fi",
                        "de" => "de",
                        "fr" => "fr",
                        "es" => "es",
                        "it" => "it",
                        "pt" => "pt",
                        "tr" => "tr",
                        "ja" => "ja",
                        "ko" => "ko",
                        "vi" => "vi",
                        "id" => "id",
                        "tl" => "tl",
                        "fil" => "tl",
                        "hi" => "hi",
                        "th" => "th",
                        "ru" => "ru",
                        _ => "en"
                    }
                };
                Properties.Settings.Default.Save();
            }

            LoadLang(Properties.Settings.Default.lang);

            if (string.IsNullOrEmpty(Properties.Settings.Default.savedFolder))
            {
                string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "YouTube");
                Properties.Settings.Default.savedFolder = defaultPath;
                Properties.Settings.Default.Save();
            }
            SavePathTextBox.Text = Properties.Settings.Default.savedFolder;
            clipboardTimer = new DispatcherTimer();
            clipboardTimer.Interval = TimeSpan.FromSeconds(1);
            clipboardTimer.Tick += CheckClipboardText;
            clipboardTimer.Start();
        }

        public void LoadLang(string langCode)
        {
            try
            {
                locMain = Localization.LoadLocalization(langCode, "main");
                locGui = Localization.LoadLocalization(langCode, "gui");

                UrlLabel.Text = locGui["videolink"];
                SaveLabel.Text = locGui["savelocation"];
                ModeLabel.Text = locGui["savemode"];
                OptionsLabel.Text = locGui["options"];

                RadioVideo.Content = locGui["video"];
                RadioAudio.Content = locGui["audio"];

                VideoOnlyCheckBox.Content = locGui["onlyvideo"];
                BrowseButton.Content = locGui["browse"];
                OpenFolderButton.Content = locGui["open"];
                settingsButton.Label = locGui["set"];

                if (!isDownloading)
                {
                    StatusTextBlock.Text = locMain["status_main"];
                    DownloadButton.Content = locGui["button_start"];
                }
                else
                {
                    DownloadButton.Content = locGui["button_cancel"];
                }

                UpdateComboBoxItems();
            }
            catch { }
        }

        private bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            url = url.ToLower().Trim();
            return url.StartsWith("https://www.youtube.com/") ||
                   url.StartsWith("https://youtube.com/") ||
                   url.StartsWith("https://youtu.be/") ||
                   url.StartsWith("http://www.youtube.com/") ||
                   url.StartsWith("http://youtube.com/") ||
                   url.StartsWith("http://youtu.be/");
        }

        private void CheckClipboardText(object sender, EventArgs e)
        {
            bool showPasteButton = false;

            try
            {
                if (Clipboard.ContainsText())
                {
                    string txt = Clipboard.GetText().Trim();

                    if (IsYouTubeUrl(txt))
                    {
                        if (UrlTextBox.Text.Trim() != txt)
                        {
                            showPasteButton = true;
                        }
                    }
                }
            }
            catch
            {
            }

            PasteButton.Visibility = showPasteButton ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                UrlTextBox.Text = Clipboard.GetText();
                UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
                PasteButton.Visibility = Visibility.Collapsed;
            }
        }

        private async void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StatusTextBlock.Text = locMain.ContainsKey("status_main") ? locMain["status_main"] : "Готов к работе.";
            OperationProgressBar.Value = 0;
            OperationProgressBar.IsIndeterminate = false;

            string url = UrlTextBox.Text.Trim();

            if (IsYouTubeUrl(url))
            {
                PreviewPanel.Visibility = Visibility.Visible;
                PreviewTitle.Text = locMain.ContainsKey("status_info") ? locMain["status_info"] : "Загрузка информации о видео...";
                PreviewImage.Source = null;

                currentMaxHeight = 2160;
                UpdateComboBoxItems();

                await LoadVideoInfoAsync(url);
            }
            else
            {
                PreviewPanel.Visibility = Visibility.Collapsed;
            }
        }

        private Task LoadVideoInfoAsync(string url)
        {
            return Task.Run(() =>
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = $"--dump-json --no-playlist \"{url}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                info.EnvironmentVariables["PYTHONUTF8"] = "1";

                try
                {
                    using (Process p = Process.Start(info))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            using (JsonDocument doc = JsonDocument.Parse(output))
                            {
                                string title = doc.RootElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : (locMain.ContainsKey("status_noname") ? locMain["status_noname"] : "Неизвестное название");
                                string channel = doc.RootElement.TryGetProperty("uploader", out var uploaderElement) ? uploaderElement.GetString() : "";
                                string thumbUrl = doc.RootElement.TryGetProperty("thumbnail", out var thumbElement) ? thumbElement.GetString() : "";
                                int maxRes = 0;

                                if (doc.RootElement.TryGetProperty("formats", out var formats))
                                {
                                    foreach (var format in formats.EnumerateArray())
                                    {
                                        if (format.TryGetProperty("vcodec", out var vcodec) && vcodec.GetString() != "none")
                                        {
                                            if (format.TryGetProperty("height", out var hProp) && hProp.ValueKind == JsonValueKind.Number)
                                            {
                                                int h = hProp.GetInt32();
                                                if (h > maxRes) maxRes = h;
                                            }
                                        }
                                    }
                                }

                                Dispatcher.Invoke(() =>
                                {
                                    PreviewTitle.Inlines.Clear();
                                    PreviewTitle.Inlines.Add(new System.Windows.Documents.Run(title));

                                    if (!string.IsNullOrEmpty(channel))
                                    {
                                        PreviewTitle.Inlines.Add(new System.Windows.Documents.LineBreak());
                                        var channelRun = new System.Windows.Documents.Run(channel);
                                        channelRun.FontSize = PreviewTitle.FontSize + 4;
                                        channelRun.FontWeight = FontWeights.SemiBold;
                                        PreviewTitle.Inlines.Add(channelRun);
                                    }

                                    if (maxRes > 0)
                                    {
                                        currentMaxHeight = maxRes;
                                        UpdateComboBoxItems();
                                    }

                                    if (!string.IsNullOrEmpty(thumbUrl))
                                    {
                                        try
                                        {
                                            BitmapImage bitmap = new BitmapImage();
                                            bitmap.BeginInit();
                                            bitmap.UriSource = new Uri(thumbUrl, UriKind.Absolute);
                                            bitmap.EndInit();
                                            PreviewImage.Source = bitmap;
                                        }
                                        catch { }
                                    }
                                });
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() => PreviewTitle.Text = locMain.ContainsKey("error1") ? locMain["error1"] : "Не удалось загрузить данные");
                        }
                    }
                }
                catch
                {
                    Dispatcher.Invoke(() => PreviewTitle.Text = locMain.ContainsKey("error2") ? locMain["error2"] : "Ошибка при чтении данных");
                }
            });
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = locMain.ContainsKey("openfoldertitle") ? locMain["openfoldertitle"] : "Выберите папку для сохранения файлов",
                InitialDirectory = Directory.Exists(SavePathTextBox.Text) ? SavePathTextBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() == true)
            {
                SavePathTextBox.Text = dialog.FolderName;
                Properties.Settings.Default.savedFolder = dialog.FolderName;
                Properties.Settings.Default.Save();
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = SavePathTextBox.Text.Trim();
            if (Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true, Verb = "open" });
            }
            else
            {
                string errTitle = locMain.ContainsKey("error") ? locMain["error"] : "Ошибка";
                string errMsg = locMain.ContainsKey("error3") ? locMain["error3"] : "Указанная папка не существует.";
                MessageBox.Show(errMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SavePathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.savedFolder = SavePathTextBox.Text.Trim();
            Properties.Settings.Default.Save();
        }

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxItems();
        }

        private void UpdateComboBoxItems()
        {
            if (SettingsComboBox == null || ComboLabel == null || OptionsPanel == null) return;
            if (locGui.Count == 0) return;

            SettingsComboBox.Items.Clear();

            if (RadioVideo.IsChecked == true)
            {
                ComboLabel.Text = locGui.ContainsKey("quality") ? locGui["quality"] : "Качество:";
                SettingsComboBox.Items.Add(new ComboBoxItem { Content = locGui.ContainsKey("max") ? locGui["max"] : "Максимальное", Tag = "max" });

                if (currentMaxHeight >= 2160)
                    SettingsComboBox.Items.Add(new ComboBoxItem { Content = "4K", Tag = "4k" });

                if (currentMaxHeight >= 1080)
                    SettingsComboBox.Items.Add(new ComboBoxItem { Content = "1080p", Tag = "1080" });

                if (currentMaxHeight >= 720)
                    SettingsComboBox.Items.Add(new ComboBoxItem { Content = "720p", Tag = "720" });

                SettingsComboBox.SelectedIndex = 0;
                OptionsPanel.Visibility = Visibility.Visible;
            }
            else if (RadioAudio.IsChecked == true)
            {
                ComboLabel.Text = locGui.ContainsKey("extension") ? locGui["extension"] : "Формат:";
                SettingsComboBox.Items.Add(new ComboBoxItem { Content = "MP3", Tag = "mp3" });
                SettingsComboBox.Items.Add(new ComboBoxItem { Content = "M4A", Tag = "m4a" });
                SettingsComboBox.SelectedIndex = 0;
                OptionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDownloading)
            {
                try
                {
                    if (currentProcess != null && !currentProcess.HasExited)
                    {
                        currentProcess.Kill();
                        StatusTextBlock.Text = locMain.ContainsKey("status_canceled") ? locMain["status_canceled"] : "Загрузка отменена пользователем.";
                    }
                }
                catch { }

                ResetUiState();
                return;
            }

            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                string errTitle = locMain.ContainsKey("error") ? locMain["error"] : "Ошибка";
                string errPaste = locMain.ContainsKey("pastefirst") ? locMain["pastefirst"] : "Пожалуйста, вставьте ссылку на YouTube.";
                MessageBox.Show(errPaste, errTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string saveDir = SavePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(saveDir)) return;

            if (!Directory.Exists(saveDir))
            {
                try { Directory.CreateDirectory(saveDir); }
                catch { return; }
            }

            isDownloading = true;
            DownloadButton.Content = locGui.ContainsKey("button_cancel") ? locGui["button_cancel"] : "Отменить загрузку";
            OperationProgressBar.IsIndeterminate = true;
            OperationProgressBar.Value = 0;
            StatusTextBlock.Text = locMain.ContainsKey("status_preparing") ? locMain["status_preparing"] : "Подготовка к скачиванию...";

            string dlFormat = "";
            string mergeArg = "";
            var selectedSetting = (ComboBoxItem)SettingsComboBox.SelectedItem;
            string tag = selectedSetting.Tag.ToString();
            bool isVideoOnly = VideoOnlyCheckBox.IsChecked == true;
            string audioPart = isVideoOnly ? "" : "+bestaudio[ext=m4a]";
            string bestFallback = isVideoOnly ? "/bestvideo" : "/best[ext=mp4]";

            if (RadioVideo.IsChecked == true)
            {
                if (tag == "max")
                {
                    dlFormat = $"bestvideo{audioPart}/best";
                    mergeArg = isVideoOnly ? "" : "--merge-output-format mp4";
                }
                else if (tag == "4k")
                {
                    dlFormat = $"bestvideo[height<=2160]{audioPart}{bestFallback}";
                    mergeArg = isVideoOnly ? "" : "--merge-output-format mp4";
                }
                else if (tag == "1080")
                {
                    dlFormat = $"bestvideo[ext=mp4][height<=1080]{audioPart}{bestFallback}";
                    mergeArg = isVideoOnly ? "" : "--merge-output-format mp4";
                }
                else if (tag == "720")
                {
                    dlFormat = $"bestvideo[ext=mp4][height<=720]{audioPart}{bestFallback}";
                    mergeArg = isVideoOnly ? "" : "--merge-output-format mp4";
                }
            }
            else
            {
                if (tag == "mp3")
                {
                    dlFormat = "bestaudio";
                    mergeArg = "-x --audio-format mp3";
                }
                else if (tag == "m4a")
                {
                    dlFormat = "bestaudio[ext=m4a]";
                    mergeArg = "-x";
                }
            }

            string outputPath = Path.Combine(saveDir, "%(title)s.%(ext)s");
            string arguments = $"--no-playlist -f \"{dlFormat}\" {mergeArg} -o \"{outputPath}\" \"{url}\"";

            await RunYtDlpAsync(arguments);

            if (isDownloading)
            {
                StatusTextBlock.Text = locMain.ContainsKey("status_done") ? locMain["status_done"] : "Готово! Файл успешно сохранён.";
                OperationProgressBar.Value = 100;
                ResetUiState();
            }
        }

        private Task RunYtDlpAsync(string arguments)
        {
            return Task.Run(() =>
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

                currentProcess = new Process { StartInfo = startInfo };
                currentProcess.OutputDataReceived += Process_OutputDataReceived;

                try
                {
                    currentProcess.Start();
                    currentProcess.BeginOutputReadLine();
                    currentProcess.WaitForExit();
                }
                catch { }
                finally
                {
                    currentProcess?.Dispose();
                    currentProcess = null;
                }
            });
        }

        private void ResetUiState()
        {
            isDownloading = false;
            DownloadButton.Content = locGui.ContainsKey("button_start") ? locGui["button_start"] : "Начать загрузку";
            OperationProgressBar.IsIndeterminate = false;
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.Data.Contains("[download]"))
                    {
                        OperationProgressBar.IsIndeterminate = false;

                        Match match = Regex.Match(e.Data, @"(\d+\.\d+)%");
                        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double percent))
                        {
                            OperationProgressBar.Value = percent;
                            string fetchPrefix = locMain.ContainsKey("status_fetching") ? locMain["status_fetching"] : "Загрузка: ";
                            StatusTextBlock.Text = $"{fetchPrefix}{percent}%";
                        }
                        else if (e.Data.Contains("Destination:"))
                        {
                            StatusTextBlock.Text = locMain.ContainsKey("status_downloading") ? locMain["status_downloading"] : "Скачивание...";
                        }
                    }
                    else if (e.Data.Contains("[Merger]") || e.Data.Contains("[ExtractAudio]"))
                    {
                        OperationProgressBar.IsIndeterminate = true;
                        StatusTextBlock.Text = locMain.ContainsKey("status_ffmpeg") ? locMain["status_ffmpeg"] : "Склейка / Конвертация аудио (FFmpeg)...";
                    }
                });
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var slideOut = new DoubleAnimation(-500, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };

            MainTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
            SettingsTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        public void CloseSettingsAnimation()
        {
            var slideOut = new DoubleAnimation(500, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
            var slideIn = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };

            SettingsTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
            MainTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private async Task CheckForUpd()
        {
            int ThisBuild = 0;
            try
            {
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("YT_DLP_GUI.BuildNumber.txt");
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                        ThisBuild = int.Parse(reader.ReadToEnd().Trim());
                }
            }
            catch { ThisBuild = 1; }

            string url = "https://raw.githubusercontent.com/MarkAdderly/YouTube-Downloader-GUI/refs/heads/main/ver.json";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string jsonString = await response.Content.ReadAsStringAsync();
                    var jsonData = NewtonsoftJson.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

                    if (jsonData != null && jsonData.ContainsKey("build"))
                    {
                        int latestBuild = int.Parse(jsonData["build"]);

                        if (latestBuild > ThisBuild)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                string title = "Update Available";
                                string msg = locMain.ContainsKey("updatedialog") ? locMain["updatedialog"] : "New version available";
                                string btnUpdate = locMain.ContainsKey("updatebutton") ? locMain["updatebutton"] : "Update";
                                string btnCancel = locMain.ContainsKey("updatecancel") ? locMain["updatecancel"] : "Cancel";

                                var content = new StackPanel();
                                content.Children.Add(new TextBlock
                                {
                                    Text = msg,
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 5, 0, 0),
                                    FontSize = 14,
                                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display")
                                });

                                var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
                                {
                                    Title = title,
                                    Content = content,
                                    PrimaryButtonText = btnUpdate,
                                    CloseButtonText = btnCancel,
                                    DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Primary
                                };

                                _ = dialog.ShowAsync().ContinueWith(t => {
                                    if (t.Result == iNKORE.UI.WPF.Modern.Controls.ContentDialogResult.Primary)
                                    {
                                        Process.Start(new ProcessStartInfo("https://adderly.top/YouTubeDownloader") { UseShellExecute = true });
                                    }
                                });
                            });
                        }
                    }
                }
                catch { }
            }
        }
    }
}