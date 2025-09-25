using SurveyMonkey;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveyMonkeyTests
{
    public class SurveyMonkeyApiSettings : ISurveyMonkeyApiSettings
    {
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string AccessToken { get; set; }
        public int? RateLimitDelay { get; set; }
        public int[] RetrySequence { get; set; }
        public IWebClient WebClient { get; set; }
    }
}
