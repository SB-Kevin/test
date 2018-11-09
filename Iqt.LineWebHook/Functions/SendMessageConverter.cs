using Iqt.LineWebHook.Models;
using Line.Messaging;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Iqt.LineWebHook.Functions
{
    public static class SendMessageConverter
    {
        public static ISendMessage ConvertToISendMessages(MessageReplyBase messageReply, TraceWriter log)
        {
            ISendMessage sendMessage = null;
            if (messageReply.YesButton != null && messageReply.NoButton != null)
            {
                var confirmReply = messageReply;
                sendMessage = new TemplateMessage(confirmReply.Text, new ConfirmTemplate(
                    confirmReply.Text, new List<ITemplateAction>
                    {
                        new PostbackTemplateAction(confirmReply.YesButton.Display,
                            JsonConvert.SerializeObject(confirmReply.YesButton.Postback)),
                        new PostbackTemplateAction(confirmReply.NoButton.Display,
                            JsonConvert.SerializeObject(confirmReply.NoButton.Postback)),
                    }));
            }
            else if (messageReply.Buttons != null && messageReply.Buttons.Count > 0)
            {
                var buttonsReply = messageReply;
                buttonsReply.Text = string.IsNullOrWhiteSpace(buttonsReply.Text)? " " : buttonsReply.Text;
                bool makeDisplayShowInText = buttonsReply.Buttons.Any(x => x.Display.Length > 15);
                if (buttonsReply.Buttons.Count <= 4)
                {
                    var buttonText = makeDisplayShowInText
                        ? $"{buttonsReply.Text}\r\n" +
                          string.Join("\r\n",
                              buttonsReply.Buttons.Select((x, xidx) => $"({xidx + 1}) {x.Display}"))
                        : buttonsReply.Text;

                    buttonText = buttonText.Length > 120 ? buttonText.Substring(0, 120) : buttonText;

                    sendMessage =
                        new TemplateMessage(buttonsReply.Text, new ButtonsTemplate(
                            buttonText,
                            null,
                            null,
                            buttonsReply.Buttons.Select(x => new PostbackTemplateAction(
                                x.Display,
                                JsonConvert.SerializeObject(x.Postback)) as ITemplateAction).ToList()));
                }
                else
                {
                    if (buttonsReply.Buttons.Count > 30)
                    {
                        log.Warning(
                            $"Max amount of Buttons is 30, the rests will be cut off, Original:{JsonConvert.SerializeObject(buttonsReply)}");
                        buttonsReply.Buttons = buttonsReply.Buttons.Take(30).ToList();
                    }

                    var splittedButtonList = new List<List<ButtonPostbackItem>>();
                    var tmpSubList = new List<ButtonPostbackItem>();

                    foreach (var buttonsReplyButton in buttonsReply.Buttons)
                    {
                        if (tmpSubList.Count >= 3)
                        {
                            splittedButtonList.Add(tmpSubList);
                            tmpSubList = new List<ButtonPostbackItem>();
                        }
                        tmpSubList.Add(buttonsReplyButton);
                    }

                    if (tmpSubList.Count != 0)
                    {
                        while (tmpSubList.Count < 3)
                        {
                            tmpSubList.Add(new ButtonPostbackItem
                            {
                                Display = " ",
                                Postback = new PostbackDataObject
                                {
                                    PostbackType = "None",
                                    PostbackData = string.Empty
                                }
                            });
                        }
                        splittedButtonList.Add(tmpSubList);
                    }

                    sendMessage =
                        new TemplateMessage(buttonsReply.Text, new CarouselTemplate(splittedButtonList.Select(
                            (x, xidx) =>
                            {
                                var buttonText = makeDisplayShowInText
                                    ? $"{buttonsReply.Text}\r\n" +
                                      string.Join("\r\n",
                                          x.Select((y, yidx) =>
                                              $"({xidx * 3 + yidx + 1}) {y.Display}"))
                                    : buttonsReply?.Text;

                                buttonText = buttonText.Length > 120 ? buttonText.Substring(0, 120) : buttonText;

                                return new CarouselColumn(
                                    buttonText,
                                    null,
                                    null,
                                    x.Select(y => new PostbackTemplateAction(
                                            y.Display,
                                            JsonConvert.SerializeObject(y.Postback)) as ITemplateAction)
                                        .ToList()
                                );
                            }).ToList()));
                }
            }
            else if (!string.IsNullOrWhiteSpace(messageReply.Text))
            {
                var textReply = messageReply;
                sendMessage = new TextMessage(textReply.Text);
            }
            else if (!string.IsNullOrWhiteSpace(messageReply.Title) && !string.IsNullOrWhiteSpace(messageReply.Address))
            {
                var locationReply = messageReply;
                sendMessage = new LocationMessage(locationReply.Title, locationReply.Address,
                    Convert.ToDecimal(locationReply.Latitude),
                    Convert.ToDecimal(locationReply.Longitude));
            }

            return sendMessage;
        }
    }
}
