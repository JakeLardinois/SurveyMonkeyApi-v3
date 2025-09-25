using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurveyMonkey.RequestSettings
{
    public class CreateLongLivedAccessCodeSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Code { get; set; }
        public string RedirectUri { get; set; }
        public string GrantType { get; set; }
    }
}
