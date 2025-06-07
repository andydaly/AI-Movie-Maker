using AI_Actions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AI_Movie_Maker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        AITextGenerator AITextGenerator;
        AIVideoGenerator AIVideoGenerator;
        AIPrompts AIPrompts;
        private Dictionary<int, bool> tabHasVideo = new();
        private Dictionary<TabItem, string> tabToVideoPath = new(); // Map tab to its video file
        private TabItem fullVideoTab = null;
        private int nextSceneIndex = 1;

        public MainWindow()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            IConfiguration configuration = builder.Build();
            string endpoint = configuration["ApiSettings:Endpoint"];
            string apiKey = configuration["ApiSettings:ApiKey"];
            string deploymentName = configuration["ApiSettings:DeploymentName"];
            AITextGenerator = new AITextGenerator(
                endpoint,
                apiKey,
                deploymentName);
            AIVideoGenerator = new AIVideoGenerator(
                endpoint,
                apiKey,
                "preview");
            AIPrompts = new AIPrompts(AITextGenerator);


            InitializeComponent();
            CreateAddTabButton();
            ScenesTabControl.SelectionChanged += ScenesTabControl_SelectionChanged;
        }

        private void CreateAddTabButton()
        {
            var existingPlusTab = ScenesTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(tab => tab.Header is Button btn && btn.Content.ToString() == "+");

            if (existingPlusTab != null)
                ScenesTabControl.Items.Remove(existingPlusTab);

            var addButton = new Button
            {
                Content = "+",
                Width = 25,
                Height = 25,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Add Scene Tab"
            };

            addButton.Click += (s, e) =>
            {
                AddNewSceneTab();
            };

            var plusTab = new TabItem
            {
                Header = addButton,
                Content = null,
                IsEnabled = true
            };

            ScenesTabControl.Items.Add(plusTab);
        }

        private void PromptBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PromptBox.Text == "Enter video prompt")
            {
                PromptBox.Text = "";
                PromptBox.Foreground = Brushes.Black;
            }
        }

        private void PromptBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptBox.Text))
            {
                PromptBox.Text = "Enter video prompt";
                PromptBox.Foreground = Brushes.Gray;
            }
        }

        private void Increase_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(NumberBox.Text, out int current) && current < 10)
            {
                NumberBox.Text = (current + 1).ToString();
            }
        }

        private void Decrease_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(NumberBox.Text, out int current) && current > 1)
            {
                NumberBox.Text = (current - 1).ToString();
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            string textPrompt = PromptBox.Text == "Enter video prompt" ? "" : PromptBox.Text.Trim();

            if (!int.TryParse(NumberBox.Text, out int sceneCount) || string.IsNullOrWhiteSpace(textPrompt))
            {
                MessageBox.Show("Please enter a valid prompt and scene count.");
                return;
            }

            ScenesTabControl.Items.Clear();

            List<string> scenes = AIPrompts.GenerateScenes(textPrompt, sceneCount);
            for (int i = 0; i < sceneCount; i++)
            {
                string sceneText = scenes != null && i < scenes.Count ? scenes[i] : $"[Scene {i + 1} text missing]";
                AddNewSceneTab(sceneText);
            }
            if (sceneCount < 10)
            {
                CreateAddTabButton();
            }

            ScenesTabControl.SelectedIndex = 0;
            CreateAddTabButton();
            RefreshPlusButtonVisibility();
        }

        private async void PreviewFullVideoButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewFullVideoButton.IsEnabled = false;

            // Check if Full Video tab is already open
            var existingTab = ScenesTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => t.Header is DockPanel dp &&
                                     dp.Children.OfType<Label>().FirstOrDefault()?.Content?.ToString() == "Full Video");

            if (existingTab != null)
            {
                ScenesTabControl.SelectedItem = existingTab;
                return;
            }

            // Get all video paths and ensure resolutions match
            var videoPaths = new List<string>();
            int? referenceWidth = null, referenceHeight = null;
            string selectedRes = ((RadioButton)ResolutionPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true))?.Tag?.ToString() ?? "1280x720";

            (referenceWidth, referenceHeight) = selectedRes == "720x1280" ? (280, 700) : (960, 540);

            foreach (var kv in tabToVideoPath)
            {
                if (!File.Exists(kv.Value))
                {
                    MessageBox.Show("One or more video files are missing.");
                    return;
                }

                videoPaths.Add(kv.Value);
            }

            if (videoPaths.Count < 2)
            {
                MessageBox.Show("You need at least two videos to preview the full video.");
                return;
            }

            string outputFilePath = Path.Combine(Path.GetTempPath(), "combined_full_video.mp4");

            try
            {
                await CombineVideosAsync(videoPaths, outputFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error combining videos: {ex.Message}");
                return;
            }

            var mediaPlayer = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Source = new Uri(outputFilePath, UriKind.Absolute),
                Width = referenceWidth.Value,
                Height = referenceHeight.Value,
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Top
            };

            mediaPlayer.Loaded += (s, ev) => { mediaPlayer.Play(); };
            mediaPlayer.MediaEnded += (s, ev) => { mediaPlayer.Position = TimeSpan.Zero; mediaPlayer.Play(); };

            var downloadButton = new Button
            {
                Content = "Download Full Video",
                Margin = new Thickness(5),
                Width = 160
            };

            downloadButton.Click += (s, ev) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "full_video.mp4",
                    Filter = "MP4 files (*.mp4)|*.mp4"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.Copy(outputFilePath, dlg.FileName, overwrite: true);
                    MessageBox.Show("Full video downloaded successfully.");
                }
            };

            var content = new StackPanel();
            content.Children.Add(mediaPlayer);
            content.Children.Add(downloadButton);

            var headerPanel = new DockPanel();
            headerPanel.Children.Add(new Label { Content = "Full Video" });

            var closeButton = new Button
            {
                Content = "✖",
                Padding = new Thickness(3, 0, 3, 0),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(closeButton);

            var fullVideoTab = new TabItem
            {
                Header = headerPanel,
                Content = content
            };

            closeButton.Click += (s, ev) =>
            {
                ScenesTabControl.Items.Remove(fullVideoTab);
                RefreshPlusButtonVisibility();
                CheckPreviewButtonStatus();
            };

            int insertIndex = Math.Max(ScenesTabControl.Items.Count - 1, 0);
            ScenesTabControl.Items.Insert(insertIndex, fullVideoTab);
            ScenesTabControl.SelectedItem = fullVideoTab;

            RefreshPlusButtonVisibility();
        }

        private void AddNewSceneTab(string initialText = "")
        {
            if (GetSceneTabCount() >= 10)
                return;

            int sceneIndex = GetNextSceneIndex();

            TextBox sceneTextBox = new TextBox
            {
                Text = initialText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(5),
                MinHeight = 100,
                MaxHeight = 200,
                Width = 600
            };

            MediaElement mediaPlayer = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Margin = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Top
            };
            mediaPlayer.MediaEnded += (s, e) => { mediaPlayer.Position = TimeSpan.Zero; mediaPlayer.Play(); };

            string selectedRes = ((RadioButton)ResolutionPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true))?.Tag?.ToString() ?? "1280x720";

            (mediaPlayer.Width, mediaPlayer.Height) = selectedRes == "720x1280" ? (280, 700) : (960, 540);

            Button generateButton = new Button { Content = "Generate Video", Margin = new Thickness(5) };
            Button downloadButton = new Button { Content = "Download Video", Margin = new Thickness(5), IsEnabled = false };

            int videoLength = 10;

            TextBox videoLengthBox = new TextBox
            {
                Text = videoLength.ToString(),
                Width = 40,
                Margin = new Thickness(5),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsReadOnly = true
            };

            Button decreaseButton = new Button
            {
                Content = "➖",
                Width = 25,
                Margin = new Thickness(2, 5, 0, 5)
            };

            Button increaseButton = new Button
            {
                Content = "➕",
                Width = 25,
                Margin = new Thickness(0, 5, 5, 5)
            };

            // Increase/Decrease handlers
            decreaseButton.Click += (s, e) =>
            {
                int current = int.Parse(videoLengthBox.Text);
                if (current > 1)
                    videoLengthBox.Text = (--current).ToString();
            };

            increaseButton.Click += (s, e) =>
            {
                int current = int.Parse(videoLengthBox.Text);
                if (current < 20)
                    videoLengthBox.Text = (++current).ToString();
            };


            string videoPath = null;
            TabItem tabItem = new TabItem();

            generateButton.Click += async (s, e) =>
            {
                string prompt = sceneTextBox.Text;
                string filename = "scene_video_";
                int videoLength = int.TryParse(videoLengthBox.Text, out var len) ? len : 10;

                generateButton.IsEnabled = false;
                generateButton.Content = "Generating...";
                downloadButton.IsEnabled = false;

                tabHasVideo[sceneIndex] = false;
                CheckPreviewButtonStatus();

                try
                {
                    string loadingPath = System.IO.Path.GetFullPath(selectedRes == "720x1280" ? "media/loadingicon2.mp4" : "media/loadingicon.mp4");

                    if (File.Exists(loadingPath))
                    {
                        mediaPlayer.Source = new Uri(loadingPath, UriKind.Absolute);
                        mediaPlayer.Play();
                    }

                    int width = selectedRes == "720x1280" ? 720 : 1280;
                    int height = selectedRes == "720x1280" ? 1280 : 720;

                    videoPath = await AIVideoGenerator.GenerateVideoAsync(prompt, filename + (sceneIndex + 1), videoLength, width, height);

                    if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                    {
                        mediaPlayer.Source = new Uri(videoPath, UriKind.Absolute);
                        mediaPlayer.Play();
                        downloadButton.IsEnabled = true;
                        tabHasVideo[sceneIndex] = true;
                        tabToVideoPath[tabItem] = videoPath;
                        CheckPreviewButtonStatus();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating video: {ex.Message}");
                }
                finally
                {
                    generateButton.IsEnabled = true;
                    generateButton.Content = "Generate Video";
                }
            };

            downloadButton.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = System.IO.Path.GetFileName(videoPath),
                        Filter = "MP4 files (*.mp4)|*.mp4|All files (*.*)|*.*"
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        File.Copy(videoPath, dlg.FileName, overwrite: true);
                        MessageBox.Show("Video downloaded successfully.");
                    }
                }
            };

            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal };
            controls.Children.Add(sceneTextBox);
            controls.Children.Add(decreaseButton);
            controls.Children.Add(videoLengthBox);
            controls.Children.Add(increaseButton);
            controls.Children.Add(generateButton);
            controls.Children.Add(downloadButton);

            StackPanel content = new StackPanel();
            content.Children.Add(controls);
            content.Children.Add(mediaPlayer);

            DockPanel headerPanel = new DockPanel();
            headerPanel.Children.Add(new Label { Content = $"Scene {sceneIndex + 1}" });

            Button closeButton = new Button
            {
                Content = "✖",
                Padding = new Thickness(3, 0, 3, 0),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(closeButton);

            tabItem.Header = headerPanel;
            tabItem.Content = content;
            tabItem.Tag = sceneIndex; // Store the scene index in the tag for easy retrieval

            closeButton.Click += (s, e) =>
            {
                if (tabItem.Tag is int index)
                {
                    tabHasVideo.Remove(index);
                }
                ScenesTabControl.Items.Remove(tabItem);
                RenumberSceneTabs();
                RefreshPlusButtonVisibility();
                CheckPreviewButtonStatus();
            };

            ScenesTabControl.Items.Add(tabItem);

            // Handle sorting tabs
            var plusTab = ScenesTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(tab => tab.Header is Button btn && btn.Content.ToString() == "+");

            if (plusTab != null)
                ScenesTabControl.Items.Remove(plusTab);

            var sortedSceneTabs = ScenesTabControl.Items
                .OfType<TabItem>()
                .Where(t => t.Tag is int)
                .OrderBy(t => (int)t.Tag)
                .ToList();

            ScenesTabControl.Items.Clear();

            foreach (var tab in sortedSceneTabs)
                ScenesTabControl.Items.Add(tab);

            if (plusTab != null)
                ScenesTabControl.Items.Add(plusTab);

            ScenesTabControl.SelectedItem = tabItem;

            tabHasVideo[sceneIndex] = false;

            RenumberSceneTabs();
            RefreshPlusButtonVisibility();
            CheckPreviewButtonStatus();
        }

        public static async Task<string> CombineVideosAsync(List<string> videoPaths, string outputFilePath)
        {
            if (videoPaths == null || videoPaths.Count < 2)
                throw new ArgumentException("At least two videos are required to combine.");

            // Path to bundled ffmpeg.exe
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
                throw new FileNotFoundException("ffmpeg.exe not found at: " + ffmpegPath);

            // Ensure the output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

            // Prepare the temporary file list
            string tempFileList = Path.GetTempFileName();
            using (StreamWriter writer = new StreamWriter(tempFileList))
            {
                foreach (var path in videoPaths)
                {
                    // Escape backslashes and wrap paths in single quotes
                    writer.WriteLine($"file '{path.Replace("\\", "/")}'");
                }
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();

                    string errorOutput = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                        throw new Exception($"ffmpeg exited with code {process.ExitCode}:\n{errorOutput}");

                    return outputFilePath;
                }
            }
            finally
            {
                // Clean up the temp file list
                if (File.Exists(tempFileList))
                    File.Delete(tempFileList);
            }
        }


        private void RenumberSceneTabs()
        {
            foreach (var tab in ScenesTabControl.Items.OfType<TabItem>())
            {
                if (tab.Tag is int sceneIndex && tab.Header is DockPanel headerPanel)
                {
                    var label = headerPanel.Children.OfType<Label>().FirstOrDefault();
                    if (label != null)
                        label.Content = $"Scene {sceneIndex + 1}";
                }
            }
        }

        private int GetSceneTabCount()
        {
            return ScenesTabControl.Items
                .OfType<TabItem>()
                .Count(tab => !(tab.Header is Button));
        }

        private void RefreshPlusButtonVisibility()
        {
            var plusTab = ScenesTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(tab => tab.Header is Button btn && btn.Content.ToString() == "+");

            if (plusTab != null && plusTab.Header is Button plusButton)
            {
                plusButton.IsEnabled = GetSceneTabCount() < 10;
            }
        }

        private void CheckPreviewButtonStatus()
        {
            var sceneTabs = ScenesTabControl.Items
        .OfType<TabItem>()
        .Where(tab => tab.Tag is int);

            var sceneIndices = sceneTabs
                .Select(tab => (int)tab.Tag)
                .ToList();

            int totalScenes = sceneIndices.Count;
            int completedScenes = sceneIndices.Count(index =>
                tabHasVideo.TryGetValue(index, out bool hasVideo) && hasVideo);

            PreviewFullVideoButton.IsEnabled = totalScenes > 1 && completedScenes == totalScenes;
        }

        private void ScenesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenesTabControl.SelectedItem is TabItem selectedTab &&
                selectedTab.Header is Button btn &&
                btn.Content.ToString() == "+")
            {
                ScenesTabControl.SelectedIndex = Math.Max(0, ScenesTabControl.Items.Count - 2);
            }
        }

        private int GetNextSceneIndex()
        {
            var usedIndices = ScenesTabControl.Items
                .OfType<TabItem>()
                .Where(tab =>
                    tab.Tag is int &&
                    !(tab.Header is DockPanel dp &&
                      dp.Children.OfType<Label>().FirstOrDefault()?.Content?.ToString() == "Full Video") &&
                    !(tab.Header is Button btn && btn.Content?.ToString() == "+"))
                .Select(tab => (int)tab.Tag)
                .ToHashSet();

            for (int i = 0; i < 100; i++)
            {
                if (!usedIndices.Contains(i))
                    return i;
            }

            throw new InvalidOperationException("Too many scene tabs.");
        }
    }
}