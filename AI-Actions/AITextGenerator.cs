using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AI_Actions
{
    public class AITextGenerator
    {
        private AzureOpenAIClient OpenAIClient { get; set; }
        private ChatClient ChatClient { get; set; }

        public AITextGenerator(string endpoint, string apiKey, string deploymentName)
        {
            OpenAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            ChatClient = OpenAIClient.GetChatClient(deploymentName);
        }

        public string Ask(string userQuestionText, string systemInstructionText)
        {

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemInstructionText),
                new UserChatMessage(userQuestionText),
            };

            var response = ChatClient.CompleteChat(messages);
            return response.Value.Content[0].Text;
        }
    }
}
