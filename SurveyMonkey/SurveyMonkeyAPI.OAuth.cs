using Newtonsoft.Json.Linq;
using SurveyMonkey.RequestSettings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SurveyMonkey
{
    public partial class SurveyMonkeyApi
    {
        public string CreateLongLivedAccessToken(CreateLongLivedAccessCodeSettings settings)
        {
            string endPoint = String.Format("/oauth/token");
            var verb = Verb.POST;
            var requestData = Helpers.RequestSettingsHelper.GetPopulatedProperties(settings);
            JToken result = MakeApiFormPostRequest(endPoint, verb, requestData);
            var longLivedAccessToken = result.Value<string>("access_token");
            return longLivedAccessToken;
        }

        public async Task<string> CreateLongLivedAccessTokenAsync(CreateLongLivedAccessCodeSettings settings)
        {
            string endPoint = String.Format("/oauth/token");
            var verb = Verb.POST;
            var requestData = Helpers.RequestSettingsHelper.GetPopulatedProperties(settings);
            JToken result = await MakeApiFormPostRequestAsync(endPoint, verb, requestData);
            var longLivedAccessToken = result.Value<string>("access_token");
            return longLivedAccessToken;
        }
    }
}
