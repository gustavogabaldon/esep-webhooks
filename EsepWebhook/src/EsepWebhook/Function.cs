using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace EsepWebhook
{
    // Define strongly-typed classes for deserialization
    public class Issue
    {
        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }
    }

    public class GitHubEvent
    {
        [JsonProperty("issue")]
        public Issue Issue { get; set; }
    }

    public class Function
    {
        private static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// Handler for processing GitHub webhook events and sending notifications to Slack.
        /// </summary>
        /// <param name="input">API Gateway Proxy Request</param>
        /// <param name="context">Lambda Context</param>
        /// <returns>API Gateway Proxy Response</returns>
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            try
            {
                // Log the received event
                context.Logger.LogLine($"FunctionHandler received: {JsonConvert.SerializeObject(input)}");

                // Extract the JSON body from the request
                string body = input.Body;
                context.Logger.LogLine($"Request Body: {body}");

                if (string.IsNullOrEmpty(body))
                {
                    throw new Exception("Request body is empty.");
                }

                // Deserialize the JSON body into GitHubEvent
                GitHubEvent gitHubEvent = JsonConvert.DeserializeObject<GitHubEvent>(body);

                // Validate the issue URL
                if (gitHubEvent?.Issue?.HtmlUrl == null)
                {
                    throw new Exception("Issue URL not found in the request body.");
                }

                string issueUrl = gitHubEvent.Issue.HtmlUrl;
                context.Logger.LogLine($"Issue URL: {issueUrl}");

                // Prepare the Slack message payload
                var slackMessage = new { text = $"Issue Created: {issueUrl}" };
                string payload = JsonConvert.SerializeObject(slackMessage);

                // Get the Slack webhook URL from environment variables
                string slackUrl = Environment.GetEnvironmentVariable("SLACK_URL");
                if (string.IsNullOrEmpty(slackUrl))
                {
                    throw new Exception("SLACK_URL environment variable is not set.");
                }

                context.Logger.LogLine($"Slack Webhook URL: {slackUrl}");

                // Send the message to Slack asynchronously
                var webRequest = new HttpRequestMessage(HttpMethod.Post, slackUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var response = await client.SendAsync(webRequest);

                // Read and log the response from Slack
                string responseContent = await response.Content.ReadAsStringAsync();
                context.Logger.LogLine($"Slack Response: {responseContent}");

                // Return a successful response
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Notification sent to Slack"
                };
            }
            catch (Exception ex)
            {
                // Log the error
                context.Logger.LogError($"Error: {ex.ToString()}");

                // Return an error response
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Server Error: {ex.Message}"
                };
            }
        }
    }
}
