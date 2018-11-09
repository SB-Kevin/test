using CloudServiceRedis;
using EventGridHelper;
using Iqt.LineWebHook.Models;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Iqt.LineWebHook.Functions.HttpTrigger
{
    public static class ReplyFunction
    {
        [FunctionName("Reply")]
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
            var redisConnectionString = Environment.GetEnvironmentVariable("RedisConnectionString", EnvironmentVariableTarget.Process);

            if (string.IsNullOrWhiteSpace(eventGridEndpoint) || string.IsNullOrWhiteSpace(eventGridKey) || string.IsNullOrWhiteSpace(redisConnectionString))
                throw new Exception(
                    "Lacks of Environment Variable: 'EventGridTopicEndpoint' or 'EventGridTopicKey' or 'RedisConnectionString'");

            Parallel.ForEach(eventGridEvents, eventGridEvent =>
            {
                try
                {
                    var traceableEventData = (TraceableEventData)eventGridEvent.Data;

                    if (traceableEventData.Data is MessagesEventReply messagesEventReply)
                    {
                        var redisHelper = new AzureRedisHelper(redisConnectionString);
                        var configString = redisHelper.GetRedis(traceableEventData.TraceToken) ??
                                           throw new Exception($"Get empty serialized LineMessagingApiConfig string with key '{traceableEventData.TraceToken}'");
                        var config = JsonConvert.DeserializeObject<LineMessagingApiConfig>(configString) ??
                                     throw new Exception($"Failed to deserialize LineMessagingApiConfig string with '{configString}'");

                        Parallel.ForEach(messagesEventReply.MessageReplys.GroupBy(x => x.ReplyToken), messageReplyGroupByReplyToken =>
                        {
                            var sendMessages = messageReplyGroupByReplyToken.Select(messageReply => SendMessageConverter.ConvertToISendMessages(messageReply, log)).ToList();
                            var lineId = messageReplyGroupByReplyToken.First().LineId;
                            var lineClient = new LineMessagingClient(config.ChannelAccessToken);
                            try
                            {
                                lineClient.ReplyMessageAsync(
                                    messageReplyGroupByReplyToken.Key,
                                    sendMessages
                                ).GetAwaiter().GetResult();
                            }
                            catch (LineResponseException ex)
                            {
                                log.Warning(ex.ToString());
                                try
                                {
                                    lineClient.PushMessageAsync(
                                        lineId,
                                        sendMessages
                                    ).GetAwaiter().GetResult();
                                }
                                catch (Exception exi)
                                {
                                    log.Warning(exi.ToString());
                                }
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
                    log.Error(ex.ToString());
                }
            });

            return new OkObjectResult("Ok");
        }
    }
}
