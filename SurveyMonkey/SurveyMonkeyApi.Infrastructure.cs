﻿using System;
using System.Collections.Generic;
using SurveyMonkey.Containers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SurveyMonkey
{
    public partial class SurveyMonkeyApi
    {
        private string _apiKey;
        private string _oAuthToken;
        private IWebClient _webClient;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private int _rateLimitDelay = 500;

        public SurveyMonkeyApi(string apiKey, string oAuthToken)
        {
            _webClient = new LiveWebClient();
            SetupWebClient(apiKey, oAuthToken);
        }

        internal SurveyMonkeyApi(string apiKey, string oAuthToken, IWebClient webClient)
        {
            _webClient = webClient;
            SetupWebClient(apiKey, oAuthToken);
        }

        private void SetupWebClient(string apiKey, string oAuthToken)
        {
            _apiKey = apiKey;
            _oAuthToken = oAuthToken;
            _webClient.Encoding = Encoding.UTF8;
        }

        private JToken MakeApiRequest(string endpoint, Verb verb, RequestData data)
        {
            RateLimit();
            ResetWebClient();
            string result;

            _webClient.Headers.Add("Content-Type", "application/json");
            _webClient.Headers.Add("Authorization", "bearer " + _oAuthToken);
            _webClient.QueryString.Add("api_key", _apiKey);
            if (verb == Verb.GET)
            {
                foreach (var item in data)
                {
                    _webClient.QueryString.Add(item.Key, item.Value.ToString());
                }
                result = _webClient.DownloadString(endpoint);
            }
            else
            {
                var settings = JsonConvert.SerializeObject(data);
                result = _webClient.UploadString(endpoint, verb.ToString(), settings);
            }
                
            _lastRequestTime = DateTime.UtcNow;

            var parsed = JObject.Parse(result);
            return parsed;
        }

        private void RateLimit()
        {
            TimeSpan timeSpan = DateTime.UtcNow - _lastRequestTime;
            int elapsedTime = (int)timeSpan.TotalMilliseconds;
            int remainingTime = _rateLimitDelay - elapsedTime;
            if ((_lastRequestTime != DateTime.MinValue) && (remainingTime > 0))
            {
                Thread.Sleep(remainingTime);
            }
            _lastRequestTime = DateTime.UtcNow; //Also setting here as otherwise if an exception is thrown while making the request it wouldn't get set at all
        }

        private void ResetWebClient()
        {
            _webClient.Headers.Clear();
            _webClient.QueryString.Clear();
        }
    }

    internal class RequestData : Dictionary<string, object>
    {
    }
}