namespace SkillSwap_Platform.HelperClass
{
    public class EmailHelper
    {
        /// <summary>
        /// Given a plan name and a “base” subject, returns a Subject line
        /// prefixed with “[{Plan} Support · {SLA} SLA] …” and a set of
        /// headers you can push into your MailMessage.
        /// </summary>
        public static (string FormattedSubject, IDictionary<string, string> Headers)
            BuildSupportSubject(string planName, string baseSubject)
        {
            // Decide SLA based on plan
            var sla = planName switch
            {
                "Free" => "120h",
                "Plus" => "72h",
                "Pro" => "48h",
                "Growth" => "24h",
                _ => "120h"
            };

            // Prefix the subject
            var formatted = $"[{planName} Support · {sla} SLA] {baseSubject}";

            // Expose metadata for downstream ticketing / routing
            var headers = new Dictionary<string, string>
            {
                ["X-SkillSwap-Plan"] = planName,
                ["X-SkillSwap-SLA"] = sla
            };

            return (formatted, headers);
        }
    }
}
