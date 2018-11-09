using CloudServiceStorage;
using EventGridHelper;
using Iqt.LineWebHook.Models;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Iqt.LineWebHook.Functions.HttpTrigger
{
    public static class PushMessageFunction
    {
        [FunctionName("PushMessage")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            string requestContent = await req.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(requestContent))
                return new BadRequestObjectResult("Empty content is given");

            var eventGridSubscriber = new TraceableEventGridSubscriber();
            eventGridSubscriber.AddOrUpdateCustomEventMapping(typeof(MessagesEventReply).FullName, typeof(MessagesEventReply));
            var eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(requestContent).ToList();

            var subscriptionValidationEvent =
                eventGridEvents.FirstOrDefault(x => x.Data is SubscriptionValidationEventData);
            if (subscriptionValidationEvent != null)
            {
                var eventData = (SubscriptionValidationEventData)subscriptionValidationEvent.Data;
                log.Info($"Got SubscriptionValidation event data, validationCode: {eventData.ValidationCode},  validationUrl: {eventData.ValidationUrl}, topic: {subscriptionValidationEvent.Topic}");

                return new OkObjectResult(new SubscriptionValidationResponse
                {
                    ValidationResponse = eventData.ValidationCode
                });
            }

            var eventGridEndpoint = Environment.GetEnvironmentVariable("EventGridTopicEndpoint", EnvironmentVariableTarget.Process);
            var eventGridKey = Environment.GetEnvironmentVariable("EventGridTopicKey", EnvironmentVariableTarget.Process);
            var storageConnectString =
                Environment.GetEnvironmentVariable("AzureStorageConnectionString", EnvironmentVariableTarget.Process);

            if (string.IsNullOrWhiteSpace(eventGridEndpoint) || string.IsNullOrWhiteSpace(eventGridKey) || string.IsNullOrWhiteSpace(storageConnectString))
                throw new Exception(
                    "Lacks of Environment Variable: 'EventGridTopicEndpoint' or 'EventGridTopicKey' or 'AzureStorageConnectionString'");

            var cachedConfig = new ConcurrentDictionary<string, LineMessagingApiConfig>();
            Parallel.ForEach(eventGridEvents, eventGridEvent =>
            {
                try
                {
                    var traceableEventData = (TraceableEventData)eventGridEvent.Data;
                    if (traceableEventData.Data is MessagesEventReply messagesEventReply)
                    {
                        if (!cachedConfig.TryGetValue(messagesEventReply.ChannelId, out LineMessagingApiConfig config))
                        {
                            var azureTableHelper = new AzureTableHelper<LineMessagingApiConfig>(
                                storageConnectString,
                                "LineMessagingApiConfigs");
                            config = azureTableHelper.Select($"ibo-line-{messagesEventReply.ChannelId}", "channel-config");
                            if (!cachedConfig.TryAdd(messagesEventReply.ChannelId, config))
                                log.Warning("Failed to cached line config");
                        }

                        Parallel.ForEach(messagesEventReply.MessageReplys, async messageReply =>
                        {
                            var sendMessage = SendMessageConverter.ConvertToISendMessages(messageReply, log);
                            var lineClient = new LineMessagingClient(config.ChannelAccessToken);
                            try
                            {
                                await lineClient.PushMessageAsync(
                                    messageReply.LineId,
                                    new List<ISendMessage>
                                    {
                                        sendMessage
                                    }
                                );
                            }
                            catch (Exception ex)
                            {
                                log.Warning(ex.ToString());
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex.ToString());
                }
            });

            return new OkObjectResult("Ok");
        }
    }
}
