using AI_Actions;
using Microsoft.Extensions.Configuration;
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

        private void AddNewSceneTab(string initialText = "")
        {
            if (GetSceneTabCount() >= 10)
                return;

            int sceneIndex = ScenesTabControl.Items.Count;

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

            generateButton.Click += async (s, e) =>
            {
                string prompt = sceneTextBox.Text;
                string filename = "scene_video_";
                int videoLength = int.TryParse(videoLengthBox.Text, out var len) ? len : 10;

                generateButton.IsEnabled = false;
                generateButton.Content = "Generating...";
                downloadButton.IsEnabled = false;

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

            TabItem tabItem = new TabItem
            {
                Header = headerPanel,
                Content = content
            };

            closeButton.Click += (s, e) =>
            {
                ScenesTabControl.Items.Remove(tabItem);
                RenumberSceneTabs();
                RefreshPlusButtonVisibility();
            };

            var plusTab = ScenesTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(tab => tab.Header is Button btn && btn.Content.ToString() == "+");

            if (plusTab != null)
            {
                int insertIndex = ScenesTabControl.Items.IndexOf(plusTab);
                ScenesTabControl.Items.Insert(insertIndex, tabItem);
            }
            else
            {
                ScenesTabControl.Items.Add(tabItem);
            }
            ScenesTabControl.SelectedItem = tabItem;

            RenumberSceneTabs();
            RefreshPlusButtonVisibility();
        }

        private void RenumberSceneTabs()
        {
            int count = 1;
            foreach (TabItem tabItem in ScenesTabControl.Items)
            {
                if (tabItem.Header is DockPanel panel && panel.Children.OfType<Label>().FirstOrDefault() is Label label)
                {
                    label.Content = $"Scene {count++}";
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

        private void ScenesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenesTabControl.SelectedItem is TabItem selectedTab &&
                selectedTab.Header is Button btn &&
                btn.Content.ToString() == "+")
            {
                ScenesTabControl.SelectedIndex = Math.Max(0, ScenesTabControl.Items.Count - 2);
            }
        }
    }
}