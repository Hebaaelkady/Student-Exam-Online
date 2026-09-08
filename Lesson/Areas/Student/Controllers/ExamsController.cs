using System; 
using System.Data;
using System.Data.Entity;
using System.Linq; 
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Lesson.Models; 
using Lesson.Helpers;
using Microsoft.AspNet.Identity;
using Microsoft.Owin.Security;
using System.IO; 
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;

namespace Lesson.Areas.Student.Controllers
{
 
    public class ExamsController : Controller
    {
        private LessonsEntities db = new LessonsEntities();

        /// <summary>
        /// يستخرج الـ Claims من JWT Cookie بعد التحقق الكامل من التوقيع.
        /// يرجع null إذا كان التوكن غير موجود أو منتهي الصلاحية أو محرَّف.
        /// </summary>
        private ClaimsPrincipal GetJwtPrincipal()
        {
            var jwtCookie = Request.Cookies["jwtToken"];
            if (jwtCookie == null || string.IsNullOrEmpty(jwtCookie.Value))
                return null;

            // ✅ التحقق الحقيقي من التوقيع — لا مجال لتزوير التوكن
            return JwtHelper.ValidateAndGetClaims(jwtCookie.Value);
        }

        [NoCache]
        public async Task<ActionResult> Main()
        {
            // ✅ التحقق الكامل من التوكن — يرفض أي توكن محرَّف أو منتهي
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Redirect("~/Account/Login");

            var userId        = JwtHelper.GetClaim(principal, "nameid");
            var examIdClaim   = JwtHelper.GetClaim(principal, "examId");
            var TimeFromClaim = JwtHelper.GetClaim(principal, "TimeFrom");
            var TimeToClaim   = JwtHelper.GetClaim(principal, "TimeTo");
            var SubjectNameClaim = JwtHelper.GetClaim(principal, "SubjectName");
            var Sho3baNameClaim  = JwtHelper.GetClaim(principal, "Sho3baName");
            var MazhabNameClaim  = JwtHelper.GetClaim(principal, "MazhabName");

            if (userId == null)
            {
                ViewBag.stat = "الطالب غير مسجل";
                return View();
            }

            var currentDate = DateTime.Now.Date;
            var currentTime = DateTime.Now.TimeOfDay;

            if (TimeFromClaim == null || TimeToClaim == null || examIdClaim == null)
            {
                ViewBag.stat = "خطأ في توقيت الامتحان.";
                return View();
            }

            TimeSpan isTimeFromValid = TimeSpan.Parse(TimeFromClaim);
            TimeSpan isTimeToValid   = TimeSpan.Parse(TimeToClaim);

            if (isTimeFromValid <= currentTime && isTimeToValid >= currentTime)
            {
                ViewBag.subject    = SubjectNameClaim;
                ViewBag.Sho3baName = Sho3baNameClaim;
                ViewBag.MazhabName = MazhabNameClaim;
                int y = int.Parse(examIdClaim);
                var studentExam = await db.StudentExams.Where(j => j.ExamID == y && j.SudentID == userId).ToListAsync();

                // ✅ جلب عدد الأسئلة بسرعة البرق من الكاش الخاص بـ ExamID هذا
                int questionCount = await ExamCacheHelper.GetExamQuestionCountAsync(db, y);

                if (studentExam.Count() > 0)
                {
                    var lastExam = studentExam.OrderByDescending(k => k.StartAt).FirstOrDefault();

                    if (lastExam.IsActive == true)
                    {
                        ViewBag.show  = 2; // استمرار الامتحان
                        ViewBag.Tokon = lastExam.StudentExamID;
                        ViewBag.CountQuestions = questionCount;
                    }
                    else
                    {
                        ViewBag.stat = $"لقد تم اداء امتحان مادة {SubjectNameClaim}";
                    }
                }
                else
                {
                    ViewBag.CountQuestions = questionCount;
                    ViewBag.show = 1; // أول مرة يؤدي الامتحان
                }
            }
            else
            {
                ViewBag.stat = "ليس ليك مواد للامتحان الان";
            }

            return View();
        }

        public async Task<JsonResult> GetName()
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Json(new { redirectUrl = Url.Action("Login", "Account", new { area = "" }), isRedirect = true });

            var userId = JwtHelper.GetClaim(principal, "nameid");
            if (userId == null)
                return Json(new { redirectUrl = Url.Action("Login", "Account", new { area = "" }), isRedirect = true });

            var user = await db.AspNetUsers
                .Include(u => u.Stage)
                .Include(u => u.Row)
                .Include(u => u.InstType)
                .Include(u => u.GroupNo)
                .FirstOrDefaultAsync(j => j.Id == userId);

            if (user == null)
                return Json(new { redirectUrl = Url.Action("Login", "Account", new { area = "" }), isRedirect = true });

            return Json(new
            {
                StudentName = user.StudentName,
                InstName    = user.InstName,
                StageName   = user.Stage?.StageName   ?? "",
                RowName     = user.Row?.RowName        ?? "",
                CountryName = user.CountryName,
                InstType    = user.InstType?.TypeName  ?? "",
                GroupNoName = user.GroupNo?.GroupNoName ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Main(int? Tokon1)
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Redirect("~/Account/Login");

            var userId       = JwtHelper.GetClaim(principal, "nameid");
            var examIdStr    = JwtHelper.GetClaim(principal, "examId");
            var TimeFromClaim = JwtHelper.GetClaim(principal, "TimeFrom");
            var TimeToClaim   = JwtHelper.GetClaim(principal, "TimeTo");

            if (userId == null)
            {
                ViewBag.stat = "الطالب غير مسجل";
                return View();
            }

            if (TimeFromClaim == null || TimeToClaim == null || examIdStr == null)
            {
                ViewBag.stat = "خطأ في توقيت الامتحان.";
                return View();
            }

            int examIdClaim       = int.Parse(examIdStr);
            var currentTime       = DateTime.Now.TimeOfDay;
            TimeSpan isTimeFromValid = TimeSpan.Parse(TimeFromClaim);
            TimeSpan isTimeToValid   = TimeSpan.Parse(TimeToClaim);

            if (isTimeFromValid <= currentTime && isTimeToValid >= currentTime)
            {
                var StudentExams = await db.StudentExams
                    .FirstOrDefaultAsync(j => j.ExamID == examIdClaim && j.SudentID == userId);

                if (StudentExams != null && StudentExams.IsActive.Value)
                {
                    return RedirectToAction("../Exams/Exam", new { id = StudentExams.StudentExamID });
                }
                else if (StudentExams == null)
                {
                    StudentExam newStudentExam = new StudentExam
                    {
                        ExamID              = examIdClaim,
                        SudentID            = userId,
                        StartAt             = DateTime.Now,
                        IsActive            = true,
                        LastClickOnQuestion = DateTime.Now,
                        RegisterBy          = Request.UserHostAddress,
                        TokonExpireTime     = DateTime.Now.Date.Add(isTimeToValid),
                    };

                    db.StudentExams.Add(newStudentExam);
                    await db.SaveChangesAsync();

                    // ✅ جلب أسئلة هذا الامتحان تحديداً من الذاكرة السريعة دون استعلام متكرر
                    var cachedQuestions = await ExamCacheHelper.GetExamQuestionItemsAsync(db, examIdClaim);
                    var randomizedQuestions = cachedQuestions.OrderBy(d => Guid.NewGuid()).ToList();

                    foreach (var question in randomizedQuestions)
                    {
                        db.StudentChoiceExams.Add(new StudentChoiceExam
                        {
                            QuestionsID   = question.QuestionsID,
                            StudentExamID = newStudentExam.StudentExamID,
                            InsertedDate  = DateTime.Now,
                        });
                    }

                    await db.SaveChangesAsync();
                    return RedirectToAction("../Exams/Exam", new { id = newStudentExam.StudentExamID });
                }
                else
                {
                    return RedirectToAction("../Exams/Main");
                }
            }
            else
            {
                ViewBag.stat = "الوقت الحالي غير متاح للاختبار.";
                return View();
            }
        }


        [NoCache]
        public async Task<ActionResult> Exam(Guid id)
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Redirect("~/Account/Login");

            var userId       = JwtHelper.GetClaim(principal, "nameid");
            var examIdStr    = JwtHelper.GetClaim(principal, "examId");
            var TimeFromClaim = JwtHelper.GetClaim(principal, "TimeFrom");
            var TimeToClaim   = JwtHelper.GetClaim(principal, "TimeTo");

            if (userId == null)
            {
                ViewBag.stat = "الطالب غير مسجل";
                return View();
            }

            if (TimeFromClaim == null || TimeToClaim == null || examIdStr == null)
            {
                ViewBag.stat = "خطأ في توقيت الامتحان.";
                return View();
            }

            int examIdClaim       = int.Parse(examIdStr);
            var currentTime       = DateTime.Now.TimeOfDay;
            TimeSpan isTimeFromValid = TimeSpan.Parse(TimeFromClaim);
            TimeSpan isTimeToValid   = TimeSpan.Parse(TimeToClaim);

            if (isTimeFromValid <= currentTime && isTimeToValid >= currentTime)
            {
                if (id != Guid.Empty)
                {
                    // ✅ التحقق من ملكية الامتحان وأن الامتحان لا يزال نشطًا — يمنع الدخول بعد انتهاء/إغلاق الامتحان
                    var ownershipCheck = await db.StudentExams
                        .FirstOrDefaultAsync(s => s.StudentExamID == id && s.SudentID == userId);
                    if (ownershipCheck == null || ownershipCheck.IsActive != true)
                        return RedirectToAction("../Exams/Main");

                    var studentChoiceExams = await db.StudentChoiceExams
                        .Include(s => s.ChoiseQuestion.QuationType)
                        .Include(s => s.ChoiseQuestion.AnswerChoices)
                        .Include(s => s.ChoiseQuestion.Exam.SubjectsStageRow)
                        .Include(s => s.ChoiseQuestion.Exam.ExamsInstTypes)
                        .Where(g => g.StudentExamID == id)
                        .OrderBy(k => k.ChoiseQuestion.QuestionType)
                        .ToListAsync();

                    ViewBag.NoQus         = studentChoiceExams.Count;
                    ViewBag.counts        = studentChoiceExams.Count;
                    ViewBag.StudentExamID = id;

                    var timeRemaining = (isTimeToValid - currentTime).TotalSeconds;
                    if (timeRemaining <= 0)
                    {
                        ViewBag.TokonExpireDate = 0;
                        ViewBag.Message = "تم انتهاء مدة الامتحان";
                        return RedirectToAction("../Exams/Main");
                    }

                    ViewBag.TokonExpireDate = (int)Math.Max(0, Math.Floor(timeRemaining));

                    // ✅ استخدام الكاش الذكي الخاص بـ ExamID هذا الامتحان
                    var cachedQuestions = await ExamCacheHelper.GetExamQuestionItemsAsync(db, examIdClaim);

                    if (studentChoiceExams.Count == 0)
                    {
                        var randomizedQuestions = cachedQuestions.OrderBy(d => Guid.NewGuid()).ToList();
                        foreach (var item in randomizedQuestions)
                        {
                            db.StudentChoiceExams.Add(new StudentChoiceExam
                            {
                                QuestionsID   = item.QuestionsID,
                                StudentExamID = id,
                                InsertedDate  = DateTime.Now,
                            });
                        }

                        await db.SaveChangesAsync();
                        var reloaded = await db.StudentChoiceExams
                            .Include(s => s.ChoiseQuestion.QuationType)
                            .Include(s => s.ChoiseQuestion.AnswerChoices)
                            .Include(s => s.ChoiseQuestion.Exam.SubjectsStageRow)
                            .Include(s => s.ChoiseQuestion.Exam.ExamsInstTypes)
                            .Where(g => g.StudentExamID == id)
                            .OrderBy(k => k.ChoiseQuestion.QuestionType)
                            .ToListAsync();
                        ViewBag.counts = reloaded.Count;
                        ViewBag.NoQus  = reloaded.Count;
                        return View(reloaded);
                    }
                    else if (studentChoiceExams.Count < cachedQuestions.Count)
                    {
                        var randomizedQuestions = cachedQuestions.OrderBy(d => Guid.NewGuid()).ToList();
                        foreach (var item in randomizedQuestions)
                        {
                            if (!studentChoiceExams.Any(k => k.QuestionsID == item.QuestionsID))
                            {
                                db.StudentChoiceExams.Add(new StudentChoiceExam
                                {
                                    QuestionsID   = item.QuestionsID,
                                    StudentExamID = id,
                                    InsertedDate  = DateTime.Now,
                                });
                            }
                        }

                        await db.SaveChangesAsync();
                        var reloaded = await db.StudentChoiceExams
                            .Include(s => s.ChoiseQuestion.QuationType)
                            .Include(s => s.ChoiseQuestion.AnswerChoices)
                            .Include(s => s.ChoiseQuestion.Exam.SubjectsStageRow)
                            .Include(s => s.ChoiseQuestion.Exam.ExamsInstTypes)
                            .Where(g => g.StudentExamID == id)
                            .OrderBy(k => k.ChoiseQuestion.QuestionType)
                            .ToListAsync();
                        ViewBag.counts = reloaded.Count;
                        ViewBag.NoQus  = reloaded.Count;
                        return View(reloaded);
                    }
                    else
                    {
                        ViewBag.counts = studentChoiceExams.Count;
                        ViewBag.NoQus  = studentChoiceExams.Count;
                        return View(studentChoiceExams);
                    }
                }
                else
                {
                    return RedirectToAction("../Exams/Main");
                }
            }
            else
            {
                return RedirectToAction("../Exams/Main");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public async Task<JsonResult> SaveQuestion(int? StudentChoiceExamID, string AnswerStudent, int? QuestionType)
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Json(new { messagealert = "غير مصرح", redirectUrl = Url.Action("Login", "Account", new { area = "" }), isRedirect = true });

            var userId       = JwtHelper.GetClaim(principal, "nameid");
            var examIdStr    = JwtHelper.GetClaim(principal, "examId");
            var TimeFromClaim = JwtHelper.GetClaim(principal, "TimeFrom");
            var TimeToClaim   = JwtHelper.GetClaim(principal, "TimeTo");

            if (userId == null)
                return Json(new { messagealert = "الطالب غير مسجل", redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });

            if (TimeFromClaim == null || TimeToClaim == null || examIdStr == null)
                return Json(new { messagealert = "خطأ في توقيت الامتحان", redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });

            var currentTime = DateTime.Now.TimeOfDay;
            TimeSpan isTimeFromValid, isTimeToValid;
            try
            {
                isTimeFromValid = TimeSpan.Parse(TimeFromClaim);
                isTimeToValid   = TimeSpan.Parse(TimeToClaim);
            }
            catch (FormatException)
            {
                return Json(new { messagealert = "خطأ في توقيت الامتحان", redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });
            }

            if (!(isTimeFromValid <= currentTime && isTimeToValid >= currentTime))
                return Json(new { messagealert = "خطأ في توقيت الامتحان", redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });

            if (StudentChoiceExamID == null)
                return Json(new { redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });

            try
            {
                var ex = await db.StudentChoiceExams.FindAsync(StudentChoiceExamID);
                if (ex == null)
                    return Json(new { messagealert = "خطأ في استرجاع السؤال." });

                // ✅ التحقق من أن السؤال ينتمي لامتحان هذا الطالب تحديدًا وأن الامتحان لا يزال نشطًا
                if (ex.StudentExam?.SudentID != userId || ex.StudentExam?.IsActive != true)
                    return Json(new { messagealert = "الامتحان غير متاح أو تم إغلاقه", redirectUrl = Url.Action("../Exams/Main", "Exams"), isRedirect = true });

                if (QuestionType == 1) // اختيار من متعدد
                {
                    if (int.TryParse(AnswerStudent, out int answerId))
                    {
                        ex.AnswerStudent = answerId;
                        var correctAnswer = await db.AnswerChoices
                            .FirstOrDefaultAsync(h => h.AnswerID == answerId && h.AnswerTrue == true);
                        ex.GradeStudent = correctAnswer != null ? ex.ChoiseQuestion.degree : 0;
                    }
                }
                else if (QuestionType == 2) // صح/خطأ
                {
                    ex.AnswerStudentText = AnswerStudent;
                    bool studentAnswer = AnswerStudent == "1";
                    ex.GradeStudent = (studentAnswer == ex.ChoiseQuestion.Answertruefalse)
                        ? ex.ChoiseQuestion.degree : 0;
                }
                else
                {
                    // حماية من Stored XSS في حال إدخال نص حر
                    ex.AnswerStudentText = string.IsNullOrEmpty(AnswerStudent) ? null : HttpUtility.HtmlEncode(AnswerStudent);
                }

                ex.GradeFinal = ex.ChoiseQuestion.degree;
                ex.TimeSolved = DateTime.Now;
                ex.RegisterBy = Request.UserHostAddress;

                // تحديث وقت آخر إجابة على مستوى الامتحان
                if (ex.StudentExam != null)
                {
                    ex.StudentExam.LastClickOnQuestion = DateTime.Now;
                    db.Entry(ex.StudentExam).State = EntityState.Modified;
                }

                db.Entry(ex).State = EntityState.Modified;
                await db.SaveChangesAsync();

                return Json(new { messagealert = "تم حفظ الإجابة بنجاح" });
            }
            catch (Exception)
            {
                return Json(new { messagealert = "حدث خطأ أثناء حفظ الإجابة." });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [NoCache]
        public async Task<JsonResult> CloseExam(Guid? StudentExamID)
        {
            // ✅ التحقق الكامل من JWT قبل إغلاق الامتحان
            var principal = GetJwtPrincipal();
            if (principal == null)
            {
                return Json(new
                {
                    redirectUrl = Url.Action("Login", "Account", new { area = "" }),
                    isRedirect = true
                });
            }

            var userId = JwtHelper.GetClaim(principal, "nameid");
            if (string.IsNullOrEmpty(userId) || StudentExamID == null)
            {
                return Json(new
                {
                    redirectUrl = Url.Action("../Exams/Main", "Exams"),
                    isRedirect = true
                });
            }

            // ✅ التحقق من أن الامتحان ينتمي لنفس الطالب لمنع إغلاق امتحانات الآخرين
            var x1 = await db.StudentExams.FirstOrDefaultAsync(j => j.StudentExamID == StudentExamID && j.SudentID == userId && j.IsActive == true);
            if (x1 == null)
            {
                return Json(new
                {
                    redirectUrl = Url.Action("../Exams/Main", "Exams"),
                    isRedirect = true
                });
            }

            x1.IsActive = false;
            x1.EndAt = DateTime.Now;
            x1.StatusID = 1;
            db.Entry(x1).State = EntityState.Modified;
            await db.SaveChangesAsync();

            return Json(new
            {
                redirectUrl = Url.Action("../Exams/Main", "Exams"),
                isRedirect = true
            });
        }

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        //public JsonResult EndExam(  Guid? StudentExamID  )
        //{
        //    var messagealert = "";

        //    var x1 = db.StudentExams.Where(j => j.StudentExamID== StudentExamID  && j.IsActive == true).FirstOrDefault();
        //    if (x1 == null)
        //    {
        //        return Json(messagealert);
        //    }
        //    else
        //    {
        //        var c = User.Identity.GetUserId();
        //        var ty = db.AspNetUsers.Where(f => f.Id == c).FirstOrDefault();
        //        if (ty != null)
        //        {
        //            if (x1.StudentChoiceExams.Sum(f => f.GradeStudent) < 26)
        //            {

        //                ty.Third = true;
        //                db.Entry(ty).State = EntityState.Modified;
        //                db.SaveChanges();
        //            }
        //        }
        //        x1.IsActive = false;
        //        x1.EndAt = DateTime.Now;
        //        db.Entry(x1).State = EntityState.Modified;
        //        db.SaveChanges();
        //        messagealert = "تم الحفظ بنجاح";
        //        //var y = User.Identity.GetUserId();
        //        //var x12 = db.AspNetUsers.Where(j => j.Id == y).FirstOrDefault();
        //        //x12.EndExam = false;

        //        //db.Entry(x12).State = EntityState.Modified;
        //        //db.SaveChanges();
        //        //CreateFile(StudentExamID);

        //            return Json(new
        //        {
        //            redirectUrl = Url.Action("../Exams/Main", "Exams"),
        //            isRedirect = true
        //        });
        //    }
        //}
        // مفتاح فك التشفير يُقرأ من Web.config
        private string EncryptionMasterKey() =>
            System.Web.Configuration.WebConfigurationManager.AppSettings["EncryptionMasterKey"]
            ?? throw new InvalidOperationException("EncryptionMasterKey غير موجود في Web.config");


        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(encryptedText))
                return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(encryptedText);
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionMasterKey(), new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        byte[] decryptedBytes = ms.ToArray();
                        string unicodeStr = Encoding.Unicode.GetString(decryptedBytes);
                        // فحص إذا كان فك التشفير بنظام Unicode سليم
                        if (!unicodeStr.Contains("\0") && !unicodeStr.Contains("\ufffd"))
                        {
                            return unicodeStr;
                        }
                        // محاولة بديلة بنظام UTF-8
                        string utf8Str = Encoding.UTF8.GetString(decryptedBytes);
                        if (!utf8Str.Contains("\ufffd"))
                        {
                            return utf8Str;
                        }
                        return unicodeStr;
                    }
                }
            }
            catch
            {
                // إذا فشل فك التشفير (نص غير مشفر أو تنسيق مختلف) يُعاد النص كما هو
                return encryptedText;
            }
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
