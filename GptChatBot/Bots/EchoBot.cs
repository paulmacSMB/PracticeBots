// Generated with EchoBot .NET Template version v4.22.0

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System;
using Azure;
using Azure.AI.TextAnalytics;

namespace EchoBot.Bots
{
    public class EchoBot : ActivityHandler
    {

        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _deployment;
        private readonly string _apiKey;
        private readonly string _apiVersion;
        private readonly string _languageEndpoint;
        private readonly TextAnalyticsClient _textAnalyticsClient;

        public EchoBot(IConfiguration config)
        {
           
            _endpoint = config["AzureOpenAI:Endpoint"];
            _deployment = config["AzureOpenAI:Deployment"];
            _apiKey = config["AzureOpenAI:ApiKey"];
            _apiVersion = config["AzureOpenAI:ApiVersion"];
            _languageEndpoint = config["AzureOpenAI:LanguageEndpoint"];

            var credentials = new AzureKeyCredential(_apiKey);

            _textAnalyticsClient = new TextAnalyticsClient(
                new Uri(_languageEndpoint), credentials);
            _httpClient = new HttpClient();
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string userInput = turnContext.Activity.Text;

            var sentiment = await _textAnalyticsClient.AnalyzeSentimentAsync(userInput);

            var tone = sentiment.Value.Sentiment.ToString();
            Console.WriteLine("Detected tone: " + tone);

            string systemPrompt = tone switch
            {
                "Negative" => "Your are a edgy funny not so serious assistant that helps people by telling them to calm down.",
                "Positive" => "You are a worried unhappy assistant who portrays the opposite of the user's mood.",
                _ => "You are normal."
            };

            var request = new
            {
                messages = new[]
                {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userInput }
            },
                temperature = 0.7,
                max_tokens = 1000
            };
            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);

         
            var response = await _httpClient.PostAsync(
                $"{_endpoint}openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}",
                content);

            var responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine("OpenAI raw JSON:\n" + responseString);

            using var doc = JsonDocument.Parse(responseString);
            var botResponse = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            await turnContext.SendActivityAsync(MessageFactory.Text(botResponse), cancellationToken);
        }
        

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
