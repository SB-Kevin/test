using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace Iqt.LineWebHook.Models.AzureTable
{
    public class LineMessagingApiConfig : TableEntity
    {
        public string ChannelAccessToken { get; set; }
        public string ChannelId { get; set; }
        public string ChannelSecret { get; set; }
    }
}
