﻿using System.Threading.Tasks;
using Noobot.Domain.Configuration;
using Noobot.Domain.MessagingPipeline;
using SlackAPI;

namespace Noobot.Domain.Slack
{
    public class SlackConnector : ISlackConnector
    {
        private readonly IConfigReader _configReader;
        private readonly IPipelineManager _pipelineManager;
        private SlackSocketClient _client;

        public SlackConnector(IConfigReader configReader, IPipelineManager pipelineManager)
        {
            _configReader = configReader;
            _pipelineManager = pipelineManager;
        }

        public async Task<InitialConnectionStatus> Connect()
        {
            var tcs = new TaskCompletionSource<InitialConnectionStatus>();

            var config = _configReader.GetConfig();
            _client = new SlackSocketClient(config.Slack.ApiToken);

            LoginResponse loginResponse;
            _client.Connect(response =>
            {
                //This is called once the client has emitted the RTM start command
                loginResponse = response;
            },
            () =>
            {
                //This is called once the Real Time Messaging client has connected to the end point
                _pipelineManager.Initialise();

                _client.OnMessageReceived += message =>
                {
                    var pipeline = _pipelineManager.GetPipeline();
                    var incomingMessage = new IncomingMessage
                    {
                        MessageId = message.id,
                        Text = message.text,
                        UserId = message.user,
                        Channel = message.channel
                    };

                    pipeline.Invoke(incomingMessage);
                };

                var connectionStatus = new InitialConnectionStatus();
                tcs.SetResult(connectionStatus);
            });

            return await tcs.Task;
        }

        public void Disconnect()
        {
            if (_client != null && _client.IsConnected)
            {
                _client.CloseSocket();
            }
        }
    }
}