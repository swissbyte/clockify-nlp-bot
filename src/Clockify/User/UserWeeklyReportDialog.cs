using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bot.Clockify.Client;
using Bot.Clockify.Fill;
using Bot.Clockify.Models;
using Bot.Common;
using Bot.Common.ChannelData.Telegram;
using Bot.Common.Recognizer;
using Bot.Data;
using Bot.States;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace Bot.Clockify.User
{
    public class UserWeeklyReportDialog : ComponentDialog
    {
        private readonly ITokenRepository _tokenRepository;
        private readonly ITimeEntryStoreService _timeEntryStoreService;
        private readonly UserState _userState;
        private readonly IClockifyMessageSource _messageSource;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IClockifyService _clockifyService;
        private readonly ILogger<UserWeeklyReportDialog> _logger;

        private const string TaskWaterfall = "TaskWaterfall";

        private const string Telegram = "telegram";

        public UserWeeklyReportDialog(ITimeEntryStoreService timeEntryStoreService,
            UserState userState, ITokenRepository tokenRepository,
            IClockifyMessageSource messageSource, IDateTimeProvider dateTimeProvider,
            ILogger<UserWeeklyReportDialog> logger, IClockifyService clockifyService)
        {
            _timeEntryStoreService = timeEntryStoreService;
            _userState = userState;
            _tokenRepository = tokenRepository;
            _messageSource = messageSource;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
            _clockifyService = clockifyService;
            AddDialog(new WaterfallDialog(TaskWaterfall, new List<WaterfallStep>
            {
                PromptForTaskAsync
            }));
            Id = nameof(UserWeeklyReportDialog);
        }


        internal class ChartDataset
        {
            public List<int> data { get; set; }
        }

        internal class ChartData
        {
            public List<string> labels { get; set; }
            public List<ChartDataset> datasets { get; set; }
        }

        internal class ChartDto
        {
            public string type { get; set; }
            public ChartData data { get; set; }
            public ChartOptions? options { get; set; }
        }

        public class ChartTitle
        {
            public bool display { get; set; }
            public string text { get; set; }
        }

        public class ChartOptions
        {
            public ChartTitle title { get; set; }
        }

        private async Task<DialogTurnResult> PromptForTaskAsync(WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            string messageText = "";

            var userProfile =
                await StaticUserProfileHelper.GetUserProfileAsync(_userState, stepContext.Context, cancellationToken);
            var tokenData = await _tokenRepository.ReadAsync(userProfile.ClockifyTokenId!);
            string clockifyToken = tokenData.Value;
            stepContext.Values["ClockifyTokenId"] = userProfile.ClockifyTokenId;
            var luisResult = (TimeSurveyBotLuis)stepContext.Options;

            //Default messageText
            //messageText = string.Format(_messageSource.SetWorkingHoursFeedback, workingHours);


            TimeZoneInfo userTimeZone = userProfile.TimeZone;
            var userNow = TimeZoneInfo.ConvertTime(_dateTimeProvider.DateTimeUtcNow(), userTimeZone);

            var monday = userNow.Date.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var friday = userNow.Date.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday);

            var workspaces = await _clockifyService.GetWorkspacesAsync(clockifyToken);
            var chartUri = "";

            foreach (var workspace in workspaces)
            {
                var summary =
                    _clockifyService.GetSummaryReportForWorkspace(clockifyToken, monday, friday, workspace.Id);

                //var chart =
                //  "https://quickchart.io/chart?c={type:'bar',data:{labels:['Q1','Q2','Q3','Q4'], datasets:[{data:[50,60,70,180]}]}}";


                var chart = new ChartDto
                {
                    data = new ChartData
                    {
                        labels = summary.Result.groupOne.Select(x => x.name).ToList(),
                        datasets = new List<ChartDataset>
                        {
                            new ChartDataset
                            {
                                data = summary.Result.groupOne.Select(x => x.duration / 60 / 60).ToList()
                            }
                        }
                    },
                    type = "doughnut",
                    options = new ChartOptions
                    {
                        title = new ChartTitle
                        {
                            display = true,
                            text = $"Report for {monday} to {friday}\n" + $"Generated: {userNow}"
                        }
                    }
                };


                chartUri = JsonConvert.SerializeObject(chart);
            }


            chartUri = "https://quickchart.io/chart?c=" + chartUri;
            //Inform user and exit the conversation.
            return await InformAndExit(stepContext, cancellationToken, chartUri);
        }


        private async Task<DialogTurnResult> InformAndExit(DialogContext stepContext,
            CancellationToken cancellationToken, string chartUri)
        {
            string platform = stepContext.Context.Activity.ChannelId;


            await stepContext.Context.SendActivityAsync(
                MessageFactory.Attachment(new Attachment("image/png", chartUri)), cancellationToken);


            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }


        private static IMessageActivity GetExitMessageActivity(string messageText, string platform)
        {
            IMessageActivity ma;
            switch (platform.ToLower())
            {
                case Telegram:
                    ma = Activity.CreateMessageActivity();
                    var sendMessageParams = new SendMessageParameters(messageText, new ReplyKeyboardRemove());
                    var channelData = new SendMessage(sendMessageParams);
                    ma.ChannelData = JsonConvert.SerializeObject(channelData);
                    return ma;
                default:
                    ma = MessageFactory.Text(messageText);
                    ma.SuggestedActions = new SuggestedActions { Actions = new List<CardAction>() };
                    return ma;
            }

            ;
        }
    }
}