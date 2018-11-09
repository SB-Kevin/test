using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Converters;

namespace Iqt.LineWebHook.Models
{
    public class Postback
    {
        public string ChannelId { get; set; }
        public EventSource Source { get; set; }
        public string ReplyToken { get; set; }
        public DateTime Timestamp { get; set; }
        public PostbackDataObject PostbackData { get; set; }
    }

    public class MessagesEvent
    {
        public string ChannelId { get; set; }
        public List<Message> Messages { get; set; }
    }

    public enum EventSourceType
    {
        User, Group, Room
    }

    public class EventSource
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public EventSourceType Type { get; set; }
        public string UserId { get; set; }
        public string GroupId { get; set; }
        public string RoomId { get; set; }
    }

    public enum MessageType
    {
        Text, Image, Video, Audio, File, Location, Sticker
    }

    public class Message
    {
        public string Id { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public MessageType Type { get; set; }
        public EventSource Source { get; set; }
        public string ReplyToken { get; set; }
        public DateTime Timestamp { get; set; }
        public string Text { get; set; }
        [JsonIgnore]
        public string ContentUrl
        {
            get => $"https://api.line.me/v2/bot/message/{Id}/content";
        }
        public ulong? FileSize { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string PackageId { get; set; }
        public string StickerId { get; set; }
    }

    public class MessagesEventReply
    {
        public string ChannelId { get; set; }
        public List<MessageReplyBase> MessageReplys { get; set; }
    }

    // ToDo: 用JsonObjectDictionary來改造，使他可以用物件繼承的模式來處理不同類型答案
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class MessageReplyBase
    {
        public string Type { get; set; }
        public string LineId { get; set; }
        public string ChannelId { get; set; }
        public string ReplyToken { get; set; }
        public string Text { get; set; }
        public ButtonPostbackItem YesButton { get; set; }
        public ButtonPostbackItem NoButton { get; set; }
        public List<ButtonPostbackItem> Buttons { get; set; }
        public string Title { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class ButtonPostbackItem
    {
        public string Display { get; set; }
        public PostbackDataObject Postback { get; set; }
    }

    public class PostbackDataObject
    {
        public string PostbackActionType { get; set; }
        public string PostbackType { get; set; }
        public object PostbackData { get; set; }
    }
}
