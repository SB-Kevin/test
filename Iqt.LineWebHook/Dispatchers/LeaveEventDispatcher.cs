﻿using System.Collections.Generic;
using Iqt.LineWebHook.Models.AzureTable;
using Line.Messaging.Webhooks;

namespace Iqt.LineWebHook.Dispatchers
{
    public class LeaveEventDispatcher : ILineEventDispatcher
    {
        public void Dispatch<T>(IList<T> webhookEvent, LineMessagingApiConfig config) where T : WebhookEvent
        {
            throw new System.NotImplementedException();
        }
    }
}
