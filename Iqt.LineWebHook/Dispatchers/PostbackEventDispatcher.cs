using CloudServiceRedis;
using EventGridHelper;
using Iqt.LineWebHook.Models;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging.Webhooks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using MessagingEventSourceType = Line.Messaging.Webhooks.EventSourceType;
using MessagingPostbackEvent = Line.Messaging.Webhooks.PostbackEvent;
using WebHookModelEventSourceType = Iqt.LineWebHook.Models.EventSourceType;
using WebHookModelPostback = Iqt.LineWebHook.Models.Postback;

namespace Iqt.LineWebHook.Dispatchers
{
    public class PostbackEventDispatcher : ILineEventDispatcher
    {
        public void Dispatch<T>(IList<T> webhookEvents, LineMessagingApiConfig config) where T : WebhookEvent
        {
            var postbackList = webhookEvents.OfType<MessagingPostbackEvent>()
                .Select(x => new WebHookModelPostback
                {
                    ChannelId = config.ChannelId,
                    PostbackData = JsonConvert.DeserializeObject<PostbackDataObject>(x.Postback.Data),
                    Source = x.Source.Type == MessagingEventSourceType.User ? new EventSource { Type = WebHookModelEventSourceType.User, UserId = x.Source.UserId } :
                        x.Source.Type == MessagingEventSourceType.Group ? new EventSource { Type = WebHookModelEventSourceType.Group, UserId = x.Source.UserId, GroupId = x.Source.Id } :
                        x.Source.Type == MessagingEventSourceType.Room ? new EventSource { Type = WebHookModelEventSourceType.Room, UserId = x.Source.UserId, RoomId = x.Source.Id } : default(EventSource),
                    ReplyToken = x.ReplyToken,
                    Timestamp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(x.Timestamp)
                })
                .ToList();

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
                postbackList.Select(x =>
                    EventGridUtility.CreateTraceableEventGridEvent($"/line/webhook/postback/", x, traceToken)).ToList()
            ).GetAwaiter().GetResult();
        }
    }
}
