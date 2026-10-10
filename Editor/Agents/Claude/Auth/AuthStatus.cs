using System.Collections.Generic;

namespace DTech.Parley.Editor.Agents.Claude
{
    internal sealed class AuthStatus
    {
        public bool LoggedIn { get; set; }
        public string AuthMethod { get; set; }
        public string ApiProvider { get; set; }
        public string Email { get; set; }
        public string OrgName { get; set; }
        public string SubscriptionType { get; set; }
        public string ConfigDirectory { get; set; }
        public string Raw { get; set; }
        public string Error { get; set; }

        public string Summary
        {
            get
            {
                if (!string.IsNullOrEmpty(Error))
                {
                    return Error;
                }

                if (!LoggedIn)
                {
                    return "Not signed in";
                }

                List<string> parts = new ();
                foreach (string part in new[] { AuthMethod, ApiProvider == "firstParty" ? null : ApiProvider, Email, OrgName, SubscriptionType })
                {
                    if (!string.IsNullOrEmpty(part))
                    {
                        parts.Add(part);
                    }
                }

                return "Signed in · " + string.Join(" · ", parts);
            }
        }
    }
}