using System.ComponentModel.DataAnnotations;
using System.EnterpriseServices.Internal;
using System.Web.Helpers;
using System.Web.Security;
using Newtonsoft.Json;

namespace DT2.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me on this computer")]
        public bool RememberMe { get; set; }

        public static LoginViewModel JsonDeserialize(FormsAuthenticationTicket ticket)
        {
            return JsonDeserialize(ticket.UserData);
        }

        public static LoginViewModel JsonDeserialize(string json)
        {
            return Json.Decode<LoginViewModel>(json);
        }

        public static string JsonSerialize(LoginViewModel that)
        {
            return Json.Encode(that);
        }

        public string UserNameNoDomain {
            get
            {
                if (this.UserName.Contains("\\"))
                {
                    var usernameParts = this.UserName.Split('\\');
                    return usernameParts[1];
                }
                return this.UserName;
            }
        }

        public string DomainName {
            get
            {
                if (this.UserName.Contains("\\"))
                {
                    var usernameParts = this.UserName.Split('\\');
                    return  usernameParts[0];
                }
                return null;
            }
        }
    }
}
