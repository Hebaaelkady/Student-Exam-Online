using System;
using System.Text;
using System.Web.Configuration;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Jwt;
using Owin;
using Lesson.Models;

namespace Lesson
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            app.CreatePerOwinContext(ApplicationDbContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            // قراءة المفتاح السري من Web.config — لا يوجد أي مفتاح مكشوف في الكود
            var secretKey = WebConfigurationManager.AppSettings["JwtSecretKey"];
            var key       = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // JWT Authentication
            app.UseJwtBearerAuthentication(new JwtBearerAuthenticationOptions
            {
                AuthenticationMode    = Microsoft.Owin.Security.AuthenticationMode.Active,
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateIssuerSigningKey  = true,
                    ValidIssuer              = "AlazharExams",
                    ValidAudience            = "AlazharStudents",
                    IssuerSigningKey         = key,
                    ValidateLifetime         = true
                }
            });
        }

    }
}