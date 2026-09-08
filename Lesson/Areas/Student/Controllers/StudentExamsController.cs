using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Lesson.Models;

namespace Lesson.Areas.Student.Controllers
{
    
    public class StudentExamsController : Controller
    {
        private LessonsEntities db = new LessonsEntities();

        [Authorize(Roles = "admin,Reporter")]
        public async Task<ActionResult> Index(int? id)
        {
            if(id==null)
            {
                return View(db.StudentExams.Where(f => f.SudentID == null));
            }
            else
            {
            var stdudent = db.AspNetUsers.Where(f => f.StudentNumber == id).FirstOrDefault();
                
               
                if (stdudent != null)
            { ViewBag.StudentName = stdudent.StudentName;
                ViewBag.UserName = stdudent.UserName;
                ViewBag.CountryName = stdudent.CountryName;
                ViewBag.RowName = stdudent.Row.RowName;
                ViewBag.StageName = stdudent.Stage.StageName;
                if(stdudent.Sho3baID!=null)
                {
ViewBag.Sho3baName = stdudent.Sho3ba.Sho3baName;
                }
                if (stdudent.MazhabID != null)
                {
                    ViewBag.MazhabName = stdudent.Mazhab.MazhabName;
                }
                
                
                ViewBag.GroupNoName = stdudent.GroupNo.GroupNoName;
                ViewBag.InstName = stdudent.InstName;
                ViewBag.TypeName = stdudent.InstType.TypeName; 
                var studentExams = db.StudentExams.Include(s => s.Exam).Where(f => f.SudentID == stdudent.Id);
                return View(await studentExams.ToListAsync());
            }
            return View();
 }
        }

        [Authorize(Roles =  "admin")]
        public    ActionResult  Report()
        {
             
                return View(  db.StudentExams.ToList());
            
         
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
