using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SurveyMonkey.Containers;
using SurveyMonkey.Helpers;
using SurveyMonkey.RequestSettings;

namespace SurveyMonkey
{
    public partial class SurveyMonkeyApi : IDisposable, ISurveyMonkeyApi
    {
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _accessToken;
        private IWebClient _webClient;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly int _rateLimitDelay = 500;
        private readonly int[] _retrySequence = { 5, 30, 300, 900 };
        private int _requestsMade;

        public int ApiRequestsMade => _requestsMade;

        public IWebProxy Proxy
        {
            get { return _webClient.Proxy; }
            set { _webClient.Proxy = value; }
        }

        public SurveyMonkeyApi(ISurveyMonkeyApiSettings settings)
        {
            _webClient = settings.WebClient ?? new LiveWebClient();
            _webClient.Encoding = Encoding.UTF8;
            _apiKey = settings.ApiKey;
            _accessToken = settings.AccessToken;

            if (settings.RateLimitDelay.HasValue)
            {
                _rateLimitDelay = settings.RateLimitDelay.Value;
            }

            if (settings.RetrySequence != null)
            {
                _retrySequence = settings.RetrySequence;
            }

            if (string.IsNullOrEmpty(settings.ApiUrl))
                _apiUrl = "https://api.surveymonkey.com/v3";
            else
                _apiUrl = settings.ApiUrl;
        }

        private JToken MakeApiFormPostRequest(string endpoint, Verb verb, RequestData data)
        {
            RateLimit();
            ResetWebClient();

            var url = _apiUrl + endpoint;
            _webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            if (!string.IsNullOrEmpty(_accessToken))
                _webClient.Headers.Add("Authorization", "bearer " + _accessToken);

            if (!String.IsNullOrEmpty(_apiKey))
                _webClient.QueryString.Add("api_key", _apiKey);

            string result = AttemptApiRequestWithRetry(url, Verb.POST, data);

            _lastRequestTime = DateTime.UtcNow;

            var parsed = JObject.Parse(result);
            return parsed;
        }

        private JToken MakeApiRequest(string endpoint, Verb verb, RequestData data)
        {
            RateLimit();
            ResetWebClient();

            var url = _apiUrl + endpoint;
            _webClient.Headers.Add("Content-Type", "application/json");
            _webClient.Headers.Add("Authorization", "bearer " + _accessToken);
            if (!String.IsNullOrEmpty(_apiKey))
            {
                _webClient.QueryString.Add("api_key", _apiKey);
            }
            if (verb == Verb.GET)
            {
                foreach (var item in data)
                {
                    _webClient.QueryString.Add(item.Key, item.Value.ToString());
                }
            }
            string result = AttemptApiRequestWithRetry(url, verb, data);

            _lastRequestTime = DateTime.UtcNow;

            var parsed = JObject.Parse(result);
            return parsed;
        }

        private string AttemptApiRequestWithRetry(string url, Verb verb, RequestData data)
        {
            if (_retrySequence == null || _retrySequence.Length == 0)
            {
                if (!String.IsNullOrEmpty(_webClient.Headers.Get("Content-Type")) && _webClient.Headers.Get("Content-Type").Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                    return AttemptApiFormPostRequest(url, verb, data);
                else
                    return AttemptApiRequest(url, verb, data);
            }
            for (int attempt = 0; attempt <= _retrySequence.Length; attempt++)
            {
                try
                {
                    if (!String.IsNullOrEmpty(_webClient.Headers.Get("Content-Type")) && _webClient.Headers.Get("Content-Type").Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                        return AttemptApiFormPostRequest(url, verb, data);
                    else
                        return AttemptApiRequest(url, verb, data);
                }
                catch (WebException webEx)
                {
                    if (webEx.Status == WebExceptionStatus.SecureChannelFailure)
                    {
                        throw new WebException("SSL/TLS error. SurveyMonkey requires TLS 1.2, as of 13 June 2018. "
                            + "Configure this globally with \"ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;\" anywhere before using this library. "
                            + "See https://github.com/bcemmett/SurveyMonkeyApi-v3/issues/66 for details.", webEx);
                    }
                    if (attempt < _retrySequence.Length && (webEx.Response == null || ((HttpWebResponse)webEx.Response).StatusCode == HttpStatusCode.ServiceUnavailable))
                    {
                        Thread.Sleep(_retrySequence[attempt] * 1000);
                    }
                    else
                    {
                        try
                        {
                            var response = new System.IO.StreamReader(webEx.Response.GetResponseStream()).ReadToEnd();
                            var parsedError = JObject.Parse(response);
                            var error = parsedError["error"].ToObject<Error>();
                            string message = String.Format("Http status: {0}, error code {1}. {2}: {3}. See {4} for more information.", error.HttpStatusCode, error.Id, error.Name, error.Message, error.Docs);
                            if (error.Id == "1014")
                            {
                                message += " Ensure your app has sufficient scopes granted to make this request: https://developer.surveymonkey.net/api/v3/#scopes";
                            }
                            throw new WebException(message, webEx);
                        }
                        catch (Exception e)
                        {
                            if (e is WebException)
                            {
                                throw;
                            }
                            //For anything other than our new WebException, swallow so that the original raw WebException is thrown
                        }
                        throw;
                    }
                }
            }
            return String.Empty;
        }

        private string AttemptApiFormPostRequest(string url, Verb verb, RequestData data)
        {
            _requestsMade++;

            if (verb == Verb.GET)
            {
                return _webClient.DownloadString(url);
            }
            var json = JsonConvert.SerializeObject(data);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> kvp in dict)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                {
                    if (sb.Length > 0) sb.Append('&');
                    sb.Append(HttpUtility.UrlEncode(kvp.Key));
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(kvp.Value));
                }
            }
            return _webClient.UploadString(url, verb.ToString(), sb.ToString());
        }

        private string AttemptApiRequest(string url, Verb verb, RequestData data)
        {
            _requestsMade++;

            if (verb == Verb.GET)
            {
                return _webClient.DownloadString(url);
            }
            return _webClient.UploadString(url, verb.ToString(), JsonConvert.SerializeObject(data));
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

        private IEnumerable<IPageableContainer> Page(IPagingSettings settings, string url, Type type, int maxResultsPerPage)
        {
            if (settings.Page.HasValue || settings.PerPage.HasValue)
            {
                var requestData = RequestSettingsHelper.GetPopulatedProperties(settings);
                return PageRequest(url, requestData, type);
            }

            var results = new List<IPageableContainer>();
            bool cont = true;
            int page = 1;
            while (cont)
            {
                settings.Page = page;
                settings.PerPage = maxResultsPerPage;
                var requestData = RequestSettingsHelper.GetPopulatedProperties(settings);
                var newResults = PageRequest(url, requestData, type);
                if (newResults.Any())
                {
                    results.AddRange(newResults);
                }
                if (newResults.Count() < maxResultsPerPage)
                {
                    cont = false;
                }
                page++;
            }
            return results;
        }

        private IEnumerable<IPageableContainer> PageRequest(string url, RequestData requestData, Type type)
        {
            var verb = Verb.GET;
            JToken result = MakeApiRequest(url, verb, requestData);
            var results = result["data"].ToObject(type);
            return (IEnumerable<IPageableContainer>)results;
        }

        private void ResetWebClient()
        {
            _webClient.Headers.Clear();
            _webClient.QueryString.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_webClient != null)
            {
                _webClient.Dispose();
                _webClient = null;
            }
        }
    }
}