using CloudServiceStorage;
using Iqt.LineWebHook.Dispatchers;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Iqt.LineWebHook.Functions.HttpTrigger
{
    public static class WebHookFunction
    {
        [FunctionName("WebHook")]
        public async static Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            var queryDictionary = QueryHelpers.ParseQuery(req.RequestUri.Query);

            if (!queryDictionary.TryGetValue("channel_id", out var channelId) || string.IsNullOrWhiteSpace(channelId))
                return new BadRequestObjectResult("Lacks of parameter: 'channel_id'");

            var storageConnectString =
                Environment.GetEnvironmentVariable("AzureStorageConnectionString", EnvironmentVariableTarget.Process);

            if (string.IsNullOrWhiteSpace(storageConnectString))
                throw new Exception(
                    "Lacks of Environment Variable: 'AzureStorageConnectionString'");

            var azureTableHelper = new AzureTableHelper<LineMessagingApiConfig>(storageConnectString, "LineMessagingApiConfigs");
            var lineConfig = azureTableHelper.Select($"ibo-line-{channelId}", "channel-config");
            
            if (lineConfig == null)
                return new BadRequestObjectResult("Invalid 'channel_id'");

            var events = await req.GetWebhookEventsAsync(lineConfig.ChannelSecret);

            Parallel.ForEach(events.GroupBy(x => x.Type), webhookEventGroup =>
            {
                LineEventDispatcherFactory.Create(webhookEventGroup.Key).Dispatch(webhookEventGroup.ToList(), lineConfig);
            });

            return new OkObjectResult("OK");
        }
    }
}
