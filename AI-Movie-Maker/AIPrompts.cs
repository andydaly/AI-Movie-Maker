using AI_Actions;
using System.Text;
using System.Text.RegularExpressions;

namespace AI_Movie_Maker
{
    public class AIPrompts
    {
        private AITextGenerator AITextGenerator;
        public AIPrompts(AITextGenerator aiTextGenerator) 
        {
            AITextGenerator = aiTextGenerator;
        }

        public List<string> GenerateScenes(string textPrompt, int NumberOfScenes)
        {
            List<string> scenes = new List<string>();
            string response = String.Empty;
            string UserMessage = $"Generate {NumberOfScenes} short scenes based on the following prompt: {textPrompt}";
            StringBuilder promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(UserMessage);
            promptBuilder.AppendLine(ClarificationInfoScene());
            string SystemMessage = "You are a short scene generator which generates descriptive scenes to be used as prompts for AI video Generation using Sora";
            response = AITextGenerator.Ask(promptBuilder.ToString(), SystemMessage);
            var rawScenes = response.Split(new[] { "#######" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var scene in rawScenes)
            {
                string cleaned = Regex.Replace(scene, @"Scene\s+\d+", "", RegexOptions.IgnoreCase).Trim();
                scenes.Add(cleaned);
            }
            return scenes;
        }



        private string ClarificationInfoScene()
        {
            return "Important Instructions:\n"
                + "- If the prompt is not clear, just return the text ERROR.\n"
                + "- Return each of the scenes separated by #######\n"
                + "- Each scene starts with Scene number, not not put a colon after the number\n"
                + "- Be very descriptive about everything in each scene\n"
                + "- Each scene must somehow relate to the last unless there is only 1 scene\n"
                + "- The scene\\scenes are to used as prompts for an AI video Generator, there will be no audio\n"
                + "- Each scene must be treated as a different AI prompt, the AI will not remember previous prompts\n"
                + "- Be very descriptive about recurring characters so they appear looking the same in separate scenes, redesribe the same characters in different scenes, ensure the description is the same\n";
        }
    }
}
