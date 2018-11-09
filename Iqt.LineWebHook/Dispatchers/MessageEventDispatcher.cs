using EventGridHelper;
using Iqt.LineWebHook.Models;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging.Webhooks;
using Microsoft.Azure.EventGrid.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using CloudServiceRedis;
using Newtonsoft.Json;
using WebHookModelEventSourceType = Iqt.LineWebHook.Models.EventSourceType;
using MessagingEventSourceType = Line.Messaging.Webhooks.EventSourceType;

namespace Iqt.LineWebHook.Dispatchers
{
    public class MessageEventDispatcher : ILineEventDispatcher
    {
        public void Dispatch<T>(IList<T> webhookEvents, LineMessagingApiConfig config) where T : WebhookEvent
        {
            var messageList = webhookEvents.OfType<MessageEvent>().Where(x => x.Message.Type == EventMessageType.Text)
                .Select(x => new Message
                {
                    Type = MessageType.Text,
                    Text = (x.Message as TextEventMessage)?.Text,
                    Source = x.Source.Type == MessagingEventSourceType.User ? new EventSource { Type = WebHookModelEventSourceType.User, UserId = x.Source.UserId } :
                             x.Source.Type == MessagingEventSourceType.Group ? new EventSource { Type = WebHookModelEventSourceType.Group, UserId = x.Source.UserId, GroupId = x.Source.Id } :
                             x.Source.Type == MessagingEventSourceType.Room ? new EventSource { Type = WebHookModelEventSourceType.Room, UserId = x.Source.UserId, RoomId = x.Source.Id } : default(EventSource),
                    ReplyToken = x.ReplyToken,
                    Timestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(x.Timestamp)
                }).ToList();

            var eventGridEndpoint = Environment.GetEnvironmentVariable("EventGridTopicEndpoint", EnvironmentVariableTarget.Process);
            var eventGridKey = Environment.GetEnvironmentVariable("EventGridTopicKey", EnvironmentVariableTarget.Process);
            var redisConnectionString = Environment.GetEnvironmentVariable("RedisConnectionString", EnvironmentVariableTarget.Process);

            if (string.IsNullOrWhiteSpace(eventGridEndpoint) || string.IsNullOrWhiteSpace(eventGridKey) || string.IsNullOrWhiteSpace(redisConnectionString))
                throw new Exception(
                    "Lacks of Environment Variable: 'EventGridTopicEndpoint' or 'EventGridTopicKey' or 'RedisConnectionString'");

            var traceToken = $"line-message-{Guid.NewGuid()}";

            var redisHelper = new AzureRedisHelper(redisConnectionString);

            redisHelper.SetRedis(traceToken, JsonConvert.SerializeObject(config),
                (int) new TimeSpan(days: 0, hours: 1, minutes: 0, seconds: 0).TotalSeconds);

            var publishHelper = new EventGridPublishHelper(new Uri(eventGridEndpoint), eventGridKey);
            publishHelper.PublishEventAsync(
                new List<EventGridEvent>
                {
                    EventGridUtility.CreateTraceableEventGridEvent($"/line/webhook/message/", new MessagesEvent
                    {
                        ChannelId = config.ChannelId,
                        Messages = messageList
                    }, traceToken)
                }
            ).GetAwaiter().GetResult();
        }
    }
}
