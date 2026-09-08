using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Lesson.Models;
using Lesson.Helpers;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Security.Principal;
using System.Web.UI;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Web.Providers.Entities;

namespace Lesson.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private ApplicationUserManager _userManager;

        private LessonsEntities db = new LessonsEntities();
        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        [OutputCache(NoStore = true, Location = OutputCacheLocation.None)]
        public ActionResult Login(string returnUrl)

        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        private ApplicationSignInManager _signInManager;

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set { _signInManager = value; }
        }
         
        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            string clientIp = Request.UserHostAddress ?? "unknown";
            string cleanUserName = SecurityHelper.NormalizeDigits(model != null ? model.UserName : null);
            string cleanSitting = SecurityHelper.NormalizeDigits(model != null ? model.IDNumber : null);

            if (model != null)
            {
                model.UserName = cleanUserName;
                model.IDNumber = cleanSitting;
            }

            // ✅ فحص تقييد المحاولات (Rate Limiting) لحماية الدخول من التخمين دون قفل الحساب
            int waitSeconds;
            if (SecurityHelper.IsLoginThrottled(clientIp, cleanUserName, out waitSeconds))
            {
                string throttleMsg = string.Format("لقد استنفدت عدة محاولات غير صحيحة، يرجى الانتظار {0} ثانية ثم إعادة المحاولة.", waitSeconds);
                ViewBag.message = throttleMsg;
                ViewBag.WaitSeconds = waitSeconds;
                ModelState.AddModelError("", throttleMsg);
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // قراءة أسماء المشرفين من Web.config بدلًا من كتابتها في الكود
            var adminNames = System.Web.Configuration.WebConfigurationManager
                .AppSettings["AdminUserNames"]
                ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) 
                ?? new string[0];
            bool isAdmin = Array.Exists(adminNames, a =>
                a.Trim().Equals(cleanUserName, StringComparison.OrdinalIgnoreCase));

            if (isAdmin)
            {
                model.Password = cleanSitting;
                // محاولة تسجيل الدخول بالباسورد المدخل
                var result = await SignInManager.PasswordSignInAsync(cleanUserName, model.Password, model.RememberMe, shouldLockout: false);
                if (result == SignInStatus.Success)
                {
                    SecurityHelper.ResetLoginAttempts(clientIp, cleanUserName);
                    var user = await UserManager.FindByNameAsync(cleanUserName);

                    if (user == null)
                    {
                        ViewBag.message = "المستخدم غير موجود";
                        ModelState.AddModelError("", "المستخدم غير موجود");
                        return View(model);
                    }
                    var studentInfo1 = db.AspNetUsers.FirstOrDefault(k => k.Id == user.Id);
              
                    var token = Helpers.JwtHelper.GenerateAdminToken(user.Id, studentInfo1.GroupID.Value);
                    HttpCookie jwtCookie = new HttpCookie("jwtToken", token)
                    {
                        HttpOnly = true, // يمنع الوصول من JavaScript
                        Secure   = Request.IsSecureConnection, // آمن على HTTPS ويعمل محلياً على localhost HTTP
                        Expires  = DateTime.UtcNow.AddHours(4)
                    };

                    Response.Cookies.Add(jwtCookie);

                    // تحويل المستخدم مباشرة لصفحة الامتحانات
                    return RedirectToAction("../Student/StudentReport/Index");
                }

                SecurityHelper.RecordFailedLogin(clientIp, cleanUserName);
                ViewBag.message = "كلمة السر غير صحيحة";
                ModelState.AddModelError("", "كلمة السر غير صحيحة");
                return View(model);
            }
            else
            {
                int sittingNo;
                if (!int.TryParse(cleanSitting, out sittingNo))
                {
                    SecurityHelper.RecordFailedLogin(clientIp, cleanUserName);
                    ViewBag.message = "رقم الجلوس يجب أن يتكون من أرقام فقط";
                    ModelState.AddModelError("", "رقم الجلوس يجب أن يتكون من أرقام فقط");
                    return View(model);
                }

                // تسجيل دخول الطالب
                var studentInfo = db.AspNetUsers.FirstOrDefault(k => k.UserName == cleanUserName && k.StudentNumber == sittingNo);

                if (studentInfo != null)
                {
                    var currentDate = DateTime.Now.Date;
                    var currentTime = DateTime.Now.TimeOfDay;
                    // البحث عن الامتحان المناسب للطالب
                    var exam = db.Exams
                        .FirstOrDefault(l => l.TimeDate == currentDate
                                             && l.TimeFrom <= currentTime
                                             && l.TimeTo >= currentTime
                                             && l.GroupNo == studentInfo.GroupID
                                             && l.SubjectsStageRow.StageRow1.RowID == studentInfo.RowID
                                             && l.SubjectsStageRow.StageRow1.StageID == studentInfo.StageID
                                             && (l.SubjectsStageRow.Sho3baID == studentInfo.Sho3baID || l.SubjectsStageRow.Sho3baID == 3 || l.SubjectsStageRow.Sho3baID == null)
                                             && (l.MazhabID == studentInfo.MazhabID || l.MazhabID == null)
                                             && (l.InstCode == null || l.InstCode == studentInfo.InstCode)
                                             && l.ExamsInstTypes.Any(k => k.InstTypeID == studentInfo.InstTypeID));

                    if (exam != null)
                    {
                        var result = await SignInManager.PasswordSignInAsync(cleanUserName, "123456", model.RememberMe, shouldLockout: false);
                        if (result == SignInStatus.Success)
                        {
                            SecurityHelper.ResetLoginAttempts(clientIp, cleanUserName);
                     
                            var token = Helpers.JwtHelper.GenerateStudentToken(
                                studentInfo.Id,
                                exam.ExamsID,
                                exam.TimeDate.Value,
                                exam.TimeFrom.Value,
                                exam.TimeTo.Value,
                                exam.SubjectsStageRow.Subject.SubjectName,
                                studentInfo.Sho3ba?.Sho3baName,
                                studentInfo.Mazhab?.MazhabName);

                            HttpCookie jwtCookie = new HttpCookie("jwtToken", token)
                            {
                                HttpOnly = true,
                                Secure   = Request.IsSecureConnection, // آمن على HTTPS ويعمل محلياً على localhost HTTP
                                Expires  = DateTime.UtcNow.AddHours(4)
                            };

                            Response.Cookies.Add(jwtCookie);

                            // الانتقال لصفحة الامتحان
                            return RedirectToAction("../Student/Exams/Main");
                        }

                        SecurityHelper.RecordFailedLogin(clientIp, cleanUserName);
                        ViewBag.message = "فشل تسجيل الدخول. حاول مرة أخرى.";
                        ModelState.AddModelError("", "فشل تسجيل الدخول. حاول مرة أخرى.");
                        return View(model);
                    }
                    else
                    {
                        ViewBag.message = "تاريخ امتحانك ليس اليوم يرجي مراجعة تاريخ امتحانك";
                        return View(model);
                    }
                }
                else
                {
                    SecurityHelper.RecordFailedLogin(clientIp, cleanUserName);
                    ViewBag.message = "عفوا انت غير مسجل يرجي التاكد من الرقم القومي ورقم الجلوس";
                    return View(model);
                }
            }
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            base.OnException(filterContext);

            var action = filterContext.RequestContext.RouteData.Values["action"] as string;
            var controller = filterContext.RequestContext.RouteData.Values["controller"] as string;

            if ((filterContext.Exception is HttpAntiForgeryException) &&
                action == "Login" &&
                controller == "Account" &&
                filterContext.RequestContext.HttpContext.User != null &&
                filterContext.RequestContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.ExceptionHandled = true;

                // redirect/show error/whatever?
                filterContext.Result = new RedirectResult("../Student/Exams/Maine");
                 
            }
        }
 

   
  
         
 
        //
        // POST: /Account/LogOff
        [HttpPost]
        [NoCache]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            SignInManager.AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

            // Remove the JWT token cookie if you added it
            if (Request.Cookies["jwtToken"] != null)
            {
                var cookie = new HttpCookie("jwtToken")
                {
                    Expires = DateTime.UtcNow.AddDays(-1) // Set expiration to past date to delete
                };
                Response.Cookies.Add(cookie);
            }

            // Redirect to the login page (or any page you prefer)
            return RedirectToAction("Login", "Account");
        }
         
        

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Login", "Account");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}