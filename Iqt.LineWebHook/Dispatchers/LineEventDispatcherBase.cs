using System.Collections.Generic;
using Iqt.LineWebHook.Models.AzureTable;
using Iqt.LineWebHook.Models.Exceptions;
using Line.Messaging.Webhooks;

namespace Iqt.LineWebHook.Dispatchers
{
    public static class LineEventDispatcherFactory
    {
        public static ILineEventDispatcher Create(WebhookEventType eventType)
        {
            switch (eventType)
            {
                case WebhookEventType.Message:
                    return new MessageEventDispatcher();
                case WebhookEventType.Postback:
                    return new PostbackEventDispatcher();
                case WebhookEventType.Join:
                    return new JoinEventDispatcher();
                case WebhookEventType.Leave:
                    return new LeaveEventDispatcher();
                case WebhookEventType.AccountLink:
                    return new AccountLinkEventDispatcher();
                case WebhookEventType.Beacon:
                    return new BeaconEventDispatcher();
                case WebhookEventType.Follow:
                    return new FollowEventDispatcher();
                case WebhookEventType.Unfollow:
                    return new UnfollowEventDispatcher();
                default:
                    throw new UnsupportedWebhookEventTypeException();
            }
        }
    }
    public interface ILineEventDispatcher
    {
        void Dispatch<T>(IList<T> webhookEvent, LineMessagingApiConfig config) where T : WebhookEvent;
    }
}
