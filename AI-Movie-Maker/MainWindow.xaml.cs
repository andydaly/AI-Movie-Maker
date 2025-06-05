using AI_Actions;
using Microsoft.Extensions.Configuration;
using System.Configuration;
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


                // Call your AI generation method
                List<string> AIScenes = AIPrompts.GenerateScenes(textPrompt, sceneCount);

                ScenesTabControl.Items.Clear();

                for (int i = 0; i < sceneCount; i++)
                {
                    string sceneText = (AIScenes != null && i < AIScenes.Count)
                        ? AIScenes[i]
                        : $"[Scene {i + 1} text missing]";

                    // Define scene index for closure safety
                    int sceneIndex = i;

                    // Scene TextBox
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
                    

                    var mediaPlayer = new MediaElement
                    {
                        Width = mediaWidth,
                        Height = mediaHeight,
                        LoadedBehavior = MediaState.Manual,
                        UnloadedBehavior = MediaState.Stop,
                        Margin = new Thickness(1),
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    // Looping behavior
                    mediaPlayer.MediaEnded += (s, e) =>
                    {
                        mediaPlayer.Position = TimeSpan.Zero;
                        mediaPlayer.Play();
                    };

                    // Generate button
                    var generateButton = new Button
                    {
                        Content = "Generate Video",
                        Margin = new Thickness(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    // Handle Generate button click
                    generateButton.Click += async (s, eArgs) =>
                    {
                        string prompt = sceneTextBox.Text;
                        string filename = "scene_video_";
                        int videoLength = 10; // seconds or however you define it

                        generateButton.IsEnabled = false;
                        generateButton.Content = "Generating...";

                        try
                        {
                            // Show loading video first
                            
                            if (System.IO.File.Exists(loadingPath))
                            {
                                mediaPlayer.Source = new Uri(loadingPath, UriKind.Absolute);
                                mediaPlayer.Play();
                            }

                            // Await the actual video generation
                            string videoPath = await AIVideoGenerator.GenerateVideoAsync(
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

                    // Top row: scene TextBox + Generate button
                    var topPanel = new DockPanel();
                    DockPanel.SetDock(generateButton, Dock.Right);
                    topPanel.Children.Add(generateButton);
                    topPanel.Children.Add(sceneTextBox);

                    // Stack the full layout vertically
                    var container = new StackPanel();
                    container.Children.Add(topPanel);
                    container.Children.Add(mediaPlayer);

                    // Tab item with content
                    var tab = new TabItem
                    {
                        Header = $"Scene {i + 1}",
                        Content = container
                    };

                    ScenesTabControl.Items.Add(tab);
                }

                // Auto-select first tab
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
    }
}