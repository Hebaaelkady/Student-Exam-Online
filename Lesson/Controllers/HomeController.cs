using Lesson.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Lesson.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        // GET: DataPreview/Home
        private LessonsEntities db = new LessonsEntities();
        public ActionResult Index()
        {
            return View();
        }

        public JsonResult GetName()
        {
            
            var c = User.Identity.GetUserId();
            var x1 = db.AspNetUsers.Where(j => j.Id == c).FirstOrDefault();
            if (x1 == null)
            {
                return Json(new
                {
                    redirectUrl = Url.Action("../Account/Login", "Account"),
                    isRedirect = true
                });
            }
            else
            {
                
                return Json(x1.StudentName);
            }
        }
    }
}