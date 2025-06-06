using AI_Actions;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.IO;
using System.Text;
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
            if (int.TryParse(NumberBox.Text, out int current))
            {
                if (current < 10)
                    NumberBox.Text = (current + 1).ToString();
            }
        }

        private void Decrease_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(NumberBox.Text, out int current))
            {
                if (current > 1)
                    NumberBox.Text = (current - 1).ToString();
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            string textPrompt = PromptBox.Text == "Enter video prompt" ? "" : PromptBox.Text.Trim();

            if (int.TryParse(NumberBox.Text, out int sceneCount) && !string.IsNullOrWhiteSpace(textPrompt))
            {
                ScenesTabControl.Items.Clear();

                List<string> AIScenes = AIPrompts.GenerateScenes(textPrompt, sceneCount);

                for (int i = 0; i < sceneCount; i++)
                {
                    string sceneText = (AIScenes != null && i < AIScenes.Count)
                        ? AIScenes[i]
                        : $"[Scene {i + 1} text missing]";

                    int sceneIndex = i;

                    var sceneTextBox = new TextBox
                    {
                        Text = sceneText,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Margin = new Thickness(5),
                        MinHeight = 100,
                        MaxHeight = 200
                    };

                    var generateButton = new Button
                    {
                        Content = "Generate Video",
                        Margin = new Thickness(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    var downloadButton = new Button
                    {
                        Content = "Download Video",
                        Margin = new Thickness(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        IsEnabled = false
                    };

                    var mediaPlayer = new MediaElement
                    {
                        Width = 960,
                        Height = 540,
                        LoadedBehavior = MediaState.Manual,
                        UnloadedBehavior = MediaState.Stop,
                        Margin = new Thickness(1),
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    mediaPlayer.MediaEnded += (s, eArgs) =>
                    {
                        mediaPlayer.Position = TimeSpan.Zero;
                        mediaPlayer.Play();
                    };

                    string videoPath = null;

                    generateButton.Click += async (s, eArgs) =>
                    {
                        string prompt = sceneTextBox.Text;
                        string filename = "scene_video_";
                        int videoLength = 10;

                        string selectedRes = ((RadioButton)ResolutionPanel.Children
                            .OfType<RadioButton>()
                            .FirstOrDefault(rb => rb.IsChecked == true))?.Tag?.ToString() ?? "1280x720";

                        int outputWidth = 1280, outputHeight = 720;
                        int mediaWidth = 960, mediaHeight = 540;
                        string loadingPath = System.IO.Path.GetFullPath("media/loadingicon.mp4");

                        if (selectedRes == "720x1280")
                        {
                            outputWidth = 720;
                            outputHeight = 1280;
                            mediaWidth = 280;
                            mediaHeight = 700;
                            loadingPath = System.IO.Path.GetFullPath("media/loadingicon2.mp4");
                        }

                        mediaPlayer.Width = mediaWidth;
                        mediaPlayer.Height = mediaHeight;

                        generateButton.IsEnabled = false;
                        generateButton.Content = "Generating...";
                        downloadButton.IsEnabled = false;

                        try
                        {
                            if (System.IO.File.Exists(loadingPath))
                            {
                                mediaPlayer.Source = new Uri(loadingPath, UriKind.Absolute);
                                mediaPlayer.Play();
                            }

                            videoPath = await AIVideoGenerator.GenerateVideoAsync(
                                prompt,
                                filename + (sceneIndex + 1),
                                videoLength,
                                outputWidth,
                                outputHeight
                            );

                            if (!string.IsNullOrEmpty(videoPath) && System.IO.File.Exists(videoPath))
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

                    downloadButton.Click += (s, eArgs) =>
                    {
                        if (!string.IsNullOrEmpty(videoPath) && System.IO.File.Exists(videoPath))
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

                    var topGrid = new Grid
                    {
                        Margin = new Thickness(5)
                    };

                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    Grid.SetColumn(sceneTextBox, 0);
                    topGrid.Children.Add(sceneTextBox);

                    Grid.SetColumn(generateButton, 1);
                    topGrid.Children.Add(generateButton);

                    Grid.SetColumn(downloadButton, 2);
                    topGrid.Children.Add(downloadButton);


                    var container = new StackPanel();
                    container.Children.Add(topGrid);
                    container.Children.Add(mediaPlayer);

                    var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 5, 0) };

                    var headerLabel = new Label
                    {
                        Content = $"Scene {i + 1}",
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var closeButton = new Button
                    {
                        Content = "✖",
                        Padding = new Thickness(3, 0, 3, 0),
                        Margin = new Thickness(5, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    var tab = new TabItem
                    {
                        Content = container
                    };

                    closeButton.Click += (s, e) =>
                    {
                        ScenesTabControl.Items.Remove(tab);
                        RenumberSceneTabs();
                    };

                    headerPanel.Children.Add(headerLabel);
                    headerPanel.Children.Add(closeButton);
                    tab.Header = headerPanel;

                    ScenesTabControl.Items.Add(tab);
                }

                if (ScenesTabControl.Items.Count > 0)
                {
                    ScenesTabControl.SelectedIndex = 0;
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid prompt and scene count.");
            }
        }


        private void RenumberSceneTabs()
        {
            for (int i = 0; i < ScenesTabControl.Items.Count; i++)
            {
                if (ScenesTabControl.Items[i] is TabItem tabItem &&
                    tabItem.Header is DockPanel panel)
                {
                    var label = panel.Children.OfType<Label>().FirstOrDefault();
                    if (label != null)
                    {
                        label.Content = $"Scene {i + 1}";
                    }

                    // Optionally update sceneIndex references or filenames stored in Tag or Name
                }
            }
        }
    }
}