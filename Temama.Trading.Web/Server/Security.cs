using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Temama.Trading.Web.Server
{
    public class Security
    {
        // Not used so far
        public const string Issuer = "Temama.Trading.Web";
        public const string Audience = "Hi all";

        // Minutes
        public const int Lifetime = 30;

        // TODO: Generate it each time on app start
        private static string _key = "ThisKey!ShoUld_beGeneratedAutomatically?";

        public static SymmetricSecurityKey GetSymmetricSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_key));
        }
    }
}
