using System.Web.Configuration;
// Controllers/StudentReportController.cs
using Lesson.Models;
using Lesson.Helpers;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace Lesson.Areas.Student.Controllers
{
    public class StudentReportController : Controller
    {
        private LessonsEntities db = new LessonsEntities();

        /// <summary>
        /// يستخرج الـ Claims من JWT Cookie بعد التحقق الكامل من التوقيع.
        /// </summary>
        private ClaimsPrincipal GetJwtPrincipal()
        {
            var jwtCookie = Request.Cookies["jwtToken"];
            if (jwtCookie == null || string.IsNullOrEmpty(jwtCookie.Value))
                return null;
            return JwtHelper.ValidateAndGetClaims(jwtCookie.Value);
        }

        /// <summary>
        /// يتحقق من أن المستخدم الحالي مشرف (Admin / Reporter) عبر Identity Role أو JWT Claim
        /// </summary>
        private bool IsAdminUser()
        {
            if (User.Identity.IsAuthenticated && (User.IsInRole("admin") || User.IsInRole("Reporter")))
                return true;

            var principal = GetJwtPrincipal();
            if (principal != null)
            {
                var typeClaim = JwtHelper.GetClaim(principal, "type");
                if (string.Equals(typeClaim, "admin", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }


        // دالة لجلب بيانات الطالب عبر AJAX
        [HttpGet]
        public JsonResult GetStudentData(int studentNumber)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var student = db.AspNetUsers
                    .Include(u => u.Row)
                    .Include(u => u.Stage)
                    .FirstOrDefault(u => u.StudentNumber == studentNumber);

                if (student == null)
                {
                    return Json(new { success = false, message = "الطالب غير موجود" });
                }

                var data = new
                {
                    studentName = student.StudentName,
                    userName = student.UserName,
                    countryName = student.CountryName,
                    className = student.Row != null && student.Stage != null
                                ? student.Row.RowName + " - " + student.Stage.StageName
                                : "غير محدد",
                    phoneNumber = student.PhoneNumber,
                    groupId = student.GroupID
                };

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // دالة لجلب تفاصيل الامتحان
        [HttpGet]
        public JsonResult GetExamDetails(int examId, int studentNumber)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var student = db.AspNetUsers.FirstOrDefault(u => u.StudentNumber == studentNumber);
                if (student == null)
                {
                    return Json(new { success = false, message = "الطالب غير موجود" });
                }

                var exam = db.Exams.FirstOrDefault(e => e.ExamsID == examId);
                if (exam == null)
                {
                    return Json(new { success = false, message = "الامتحان غير موجود" });
                }

                var subject = db.SubjectsStageRows
                    .Where(ssr => ssr.SubjectsStageRowID == exam.SubjectsStageRowID)
                    .Select(ssr => ssr.Subject)
                    .FirstOrDefault();

                var classData = db.SubjectsStageRows
                    .Where(ssr => ssr.SubjectsStageRowID == exam.SubjectsStageRowID)
                    .Select(ssr => new {
                        ssr.StageRow1.Row.RowName,
                        ssr.StageRow1.Stage.StageName
                    })
                    .FirstOrDefault();

                var studentExam = db.StudentExams
                    .FirstOrDefault(se => se.SudentID == student.Id && se.ExamID == examId);

                var status = "غير محدد";
                if (studentExam != null)
                {
                    if (studentExam.EndAt.HasValue && studentExam.EndAt < DateTime.Now)
                        status = "منتهي";
                    else if (studentExam.StartAt.HasValue)
                        status = "نشط";
                    else
                        status = "لم يبدأ";
                }

                var data = new
                {
                    examName = exam.ExamName,
                    subjectName = subject?.SubjectName ?? "غير محدد",
                    className = classData != null ? classData.RowName + " - " + classData.StageName : "غير محدد",
                    studentNumber = student.StudentNumber,
                    studentName = student.StudentName,
                    status = status
                };

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // دالة لجلب تفاصيل كاملة للامتحان
        [HttpGet]
        public JsonResult GetFullExamDetails(int examId)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var exam = db.Exams
                    .Include(e => e.SubjectsStageRow.Subject)
                    .FirstOrDefault(e => e.ExamsID == examId);

                if (exam == null)
                {
                    return Json(new { success = false, message = "الامتحان غير موجود" });
                }

                var questions = db.ChoiseQuestions
                    .Where(q => q.ExamID == examId)
                    .ToList();

                var studentAnswers = db.StudentChoiceExams
                    .Where(sce => sce.StudentExam.ExamID == examId)
                    .ToList();

                var html = $@"
            <div class='row'>
                <div class='col-md-6'>
                    <h5>معلومات الامتحان</h5>
                    <p><strong>اسم الامتحان:</strong> {exam.ExamName}</p>
                    <p><strong>المادة:</strong> {exam.SubjectsStageRow?.Subject?.SubjectName ?? "غير محدد"}</p>
                    <p><strong>التاريخ:</strong> {exam.TimeDate?.ToString("yyyy/MM/dd")}</p>
                    <p><strong>الوقت:</strong> {exam.TimeFrom?.ToString(@"hh\:mm")} - {exam.TimeTo?.ToString(@"hh\:mm")}</p>
                </div>
                <div class='col-md-6'>
                    <h5>إحصائيات</h5>
                    <p><strong>عدد الأسئلة:</strong> {questions.Count}</p>
                  
                </div>
            </div>
            <hr/>
            <h5>الأسئلة</h5>
            <div class='table-responsive'>
                <table class='table table-bordered'>
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>نص السؤال</th>
                            <th>النوع</th>
               
                            <th>مجاب</th>
                        </tr>
                    </thead>
                    <tbody>";

                int counter = 1;
           
                foreach (var question in questions)
                {
                    var isAnswered = studentAnswers.Any(sa => sa.QuestionsID == question.QuestionsID);

                    // نعمل encode للنص
                    var encodedTitle = WebUtility.HtmlEncode(
                        question.QuationTitle  
                    );

                    html += $@"
<tr>
    <td>{counter++}</td>
    <td>{encodedTitle}...</td>
    <td>{(question.QuestionType == 1 ? "اختياري" : "مقالي")}</td>
    <td><span class='badge {(isAnswered ? "bg-success" : "bg-danger")}'>{(isAnswered ? "نعم" : "لا")}</span></td>
</tr>";
                }


                html += @"
                    </tbody>
                </table>
            </div>";

                return Json(new { success = true, html = html }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        // دالة واحدة موحدة لتحديث بيانات الطالب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateStudentData(UpdateStudentFullModel model)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" });
            try
            {
                var student = db.AspNetUsers
                    .Include(u => u.Row)
                    .Include(u => u.Stage)
                    .Include(u => u.Mazhab)
                    .Include(u => u.Sho3ba)
                    .Include(u => u.InstType)
                    .Include(u => u.GroupNo)
                    .FirstOrDefault(u => u.StudentNumber == model.StudentNumber);

                if (student == null)
                {
                    return Json(new { success = false, message = "الطالب غير موجود" });
                }

                // تسجيل البيانات القديمة
                var oldData = new
                {
                    StudentName = student.StudentName,
                    UserName = student.UserName,
                    Email = student.Email,
                    PhoneNumber = student.PhoneNumber,
                    CountryName = student.CountryName,
                    InstName = student.InstName,
                    RowName = student.Row != null ? student.Row.RowName : "غير محدد",
                    StageName = student.Stage != null ? student.Stage.StageName : "غير محدد",
                    MazhabName = student.Mazhab != null ? student.Mazhab.MazhabName : "غير محدد",
                    Sho3baName = student.Sho3ba != null ? student.Sho3ba.Sho3baName : "غير محدد",
                    InstTypeName = student.InstType != null ? student.InstType.TypeName : "غير محدد",
                    GroupName = student.GroupNo != null ? student.GroupNo.GroupNoName : "غير محدد"
                };

                // تحديث البيانات الأساسية
                if (!string.IsNullOrEmpty(model.StudentName))
                    student.StudentName = model.StudentName;

                if (!string.IsNullOrEmpty(model.UserName))
                    student.UserName = model.UserName;

                if (!string.IsNullOrEmpty(model.Email))
                    student.Email = model.Email;

                if (!string.IsNullOrEmpty(model.PhoneNumber))
                    student.PhoneNumber = model.PhoneNumber;

                if (!string.IsNullOrEmpty(model.CountryName))
                    student.CountryName = model.CountryName;

                if (!string.IsNullOrEmpty(model.InstName))
                    student.InstName = model.InstName;

                // تحديث البيانات الرقمية
                if (model.RowId.HasValue && model.RowId > 0)
                    student.RowID = model.RowId.Value;

                if (model.StageId.HasValue && model.StageId > 0)
                    student.StageID = model.StageId.Value;

                if (model.MazhabId.HasValue && model.MazhabId > 0)
                    student.MazhabID = model.MazhabId.Value;

                if (model.Sho3baId.HasValue && model.Sho3baId > 0)
                    student.Sho3baID = model.Sho3baId.Value;

                if (model.InstTypeId.HasValue && model.InstTypeId > 0)
                    student.InstTypeID = model.InstTypeId.Value;

                if (model.NewGroupId.HasValue && model.NewGroupId > 0)
                    student.GroupID = model.NewGroupId.Value;

                db.SaveChanges();

                // إنشاء تذكرة دعم للتعديل
                var ticket = new SupportTicket
                {
                    StudentId = student.Id,
                    TicketType = "Student Data Updated",
                    Subject = "تعديل بيانات الطالب",
                    Description = $"تم تعديل بيانات الطالب {model.StudentName} (رقم: {model.StudentNumber})<br/><br/>" +
                                 $"<strong>التعديلات:</strong><br/>" +
                                 $"الاسم: {oldData.StudentName} → {model.StudentName}<br/>" +
                                 $"الرقم القومي: {oldData.UserName} → {model.UserName}<br/>" +
                                 $"البريد: {oldData.Email} → {model.Email}<br/>" +
                                 $"الهاتف: {oldData.PhoneNumber} → {model.PhoneNumber}<br/>" +
                                 $"البلد: {oldData.CountryName} → {model.CountryName}<br/>" +
                                 $"المعهد: {oldData.InstName} → {model.InstName}<br/>" +
                                 $"الصف: {oldData.RowName} → {model.RowId}<br/>" +
                                 $"المرحلة: {oldData.StageName} → {model.StageId}<br/>" +
                                 $"المذهب: {oldData.MazhabName} → {model.MazhabId}<br/>" +
                                 $"الشعبة: {oldData.Sho3baName} → {model.Sho3baId}<br/>" +
                                 $"نوع المعهد: {oldData.InstTypeName} → {model.InstTypeId}<br/>" +
                                 $"المجموعة: {oldData.GroupName} → {model.NewGroupId}<br/><br/>" +
                                 $"<strong>ملاحظات:</strong> {model.Notes ?? "لا توجد ملاحظات"}",
                    CreatedBy = User.Identity.Name,
                    Status = "Closed",
                    CreatedAt = DateTime.Now
                };
                db.SupportTickets.Add(ticket);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "تم تحديث بيانات الطالب بنجاح"
                });
            }
            catch (Exception ex)
            {
                // تسجيل الخطأ
                System.Diagnostics.Debug.WriteLine($"خطأ في UpdateStudentData: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = $"حدث خطأ أثناء تحديث البيانات: {ex.Message}"
                });
            }
        }

        // Model لتحديث بيانات الطالب
        public class UpdateStudentModel
        {
            public int StudentNumber { get; set; }
            public string StudentName { get; set; }
            public string UserName { get; set; }
            public string CountryName { get; set; }
            public string ClassName { get; set; }
            public string PhoneNumber { get; set; }
            public byte? NewGroupId { get; set; }
            public string Notes { get; set; }
        }
        // صفحة البحث الرئيسية
        public ActionResult Index()
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });

            var model = new StudentSearchModel
            {
                Term = 1,
                FromDate = DateTime.Now.AddMonths(-3),
                ToDate = DateTime.Now
            };

            return View(model);
        }


        private List<StudentUnansweredReport> GetTerm1Report(StudentSearchModel search)
        {
            var report = new List<StudentUnansweredReport>();

            // التحقق من وجود رقم الطالب
            if (!search.StudentNumber.HasValue)
            {
                return report; // ترجع قائمة فارغة
            }

            // طريقة أبسط بدون GroupJoin معقد
            // أولاً: الحصول على جميع امتحانات الطالب
            var studentExamsQuery = from u in db.AspNetUsers
                                    join se in db.StudentExams on u.Id equals se.SudentID
                                    join e in db.Exams on se.ExamID equals e.ExamsID
                                    where e.TimeDate != null && u.StudentNumber == search.StudentNumber.Value
                                    select new { u, se, e };

            var studentExams = studentExamsQuery.ToList();

            // إذا لم يكن هناك امتحانات للطالب
            if (!studentExams.Any())
            {
                return report; // ترجع قائمة فارغة
            }

            foreach (var exam in studentExams)
            {
                // الحصول على جميع أسئلة هذا الامتحان
                var questions = db.ChoiseQuestions
                    .Where(q => q.ExamID == exam.e.ExamsID)
                    .ToList();

                // الحصول على إجابات الطالب لهذا الامتحان
                var studentAnswers = db.StudentChoiceExams
                    .Where(sce => sce.StudentExamID == exam.se.StudentExamID)
                    .ToList();

                // تحديد الأسئلة التي لم يتم الإجابة عليها
                var unansweredQuestions = from q in questions
                                          join sa in studentAnswers on q.QuestionsID equals sa.QuestionsID into saGroup
                                          from sa in saGroup.DefaultIfEmpty()
                                          where sa == null || (sa.AnswerStudent == null && sa.AnswerStudentText == null)
                                          select q;

                // الحصول على بيانات المادة والصف
                var subjectData = (from ssr in db.SubjectsStageRows
                                   join sub in db.Subjects on ssr.SubjectsID equals sub.SubjectID
                                   join sr in db.StageRows on ssr.StageRow equals sr.StageRowID
                                   join r in db.Rows on sr.RowID equals r.RowID
                                   join st in db.Stages on sr.StageID equals st.StageID
                                   where ssr.SubjectsStageRowID == exam.e.SubjectsStageRowID
                                   select new { sub, r, st }).FirstOrDefault();

                if (subjectData != null && search.SubjectId.HasValue)
                {
                    if (subjectData.sub.SubjectID != search.SubjectId.Value)
                    {
                        continue; // تخطي إذا كانت المادة لا تطابق البحث
                    }
                }

                // إضافة الأسئلة غير المجابة للتقرير
                foreach (var question in unansweredQuestions)
                {
                    var reportItem = new StudentUnansweredReport
                    {
                        StudentNumber = exam.u.StudentNumber ?? 0,
                        StudentName = exam.u.StudentName,
                        UserName = exam.u.UserName,
                        CountryName = exam.u.CountryName,
                        ExamID = exam.e.ExamsID,
                        ExamName = exam.e.ExamName,
                        SubjectName = subjectData != null && subjectData.sub != null ? subjectData.sub.SubjectName : "غير محدد",
                        ClassName = subjectData != null ? subjectData.r.RowName + " - " + subjectData.st.StageName : "غير محدد",
                        ExamDate = exam.e.TimeDate,
                        TimeFrom = exam.e.TimeFrom,
                        TimeTo = exam.e.TimeTo,
                        QuestionsID = question.QuestionsID,
                        QuestionTitle = question.QuationTitle,
                        QuestionType = question.QuestionType ?? 0,
                        QuestionDegree = question.degree ?? 0,
                        StudentExamID = exam.se.StudentExamID.ToString(),
                        StartAt = exam.se.StartAt,
                        EndAt = exam.se.EndAt
                    };

                    report.Add(reportItem);
                }
            }

            // حساب الإحصائيات
            CalculateStatistics(report, 1);

            // ترتيب النتائج
            report = report.OrderByDescending(r => r.ExamDate)
                          .ThenBy(r => r.StudentNumber)
                          .ThenBy(r => r.ExamName)
                          .ThenBy(r => r.QuestionsID)
                          .ToList();

            return report;
        }
        // الحصول على تقرير الفصل الثاني
        private List<StudentUnansweredReport> GetTerm2Report(StudentSearchModel search)
        {
            var report = new List<StudentUnansweredReport>();

            // طريقة أبسط للفصل الثاني
            var studentExamsQuery = from u in db.AspNetUsers
                                    join se in db.StudentExamsTerm2 on u.Id equals se.SudentID
                                    join e in db.ExamsTerm2 on se.ExamID equals e.ExamsTerm2ID
                                    where e.TimeDate != null
                                    select new { u, se, e };

            // تطبيق فلترات البحث
            if (search.StudentNumber.HasValue)
            {
                studentExamsQuery = studentExamsQuery.Where(x => x.u.StudentNumber == search.StudentNumber.Value);
            }

            if (!string.IsNullOrEmpty(search.StudentName))
            {
                studentExamsQuery = studentExamsQuery.Where(x => x.u.StudentName.Contains(search.StudentName) ||
                                                                 x.u.UserName.Contains(search.StudentName));
            }

            if (search.FromDate.HasValue)
            {
                studentExamsQuery = studentExamsQuery.Where(x => x.e.TimeDate >= search.FromDate.Value);
            }

            if (search.ToDate.HasValue)
            {
                studentExamsQuery = studentExamsQuery.Where(x => x.e.TimeDate <= search.ToDate.Value);
            }

            var studentExams = studentExamsQuery.ToList();

            foreach (var exam in studentExams)
            {
                // الحصول على جميع أسئلة هذا الامتحان
                var questions = db.ChoiseQuestionTerm2
                    .Where(q => q.ExamID == exam.e.ExamsTerm2ID)
                    .ToList();

                // الحصول على إجابات الطالب لهذا الامتحان
                var studentAnswers = db.StudentChoiceExamTerm2
                    .Where(sce => sce.StudentExamID == exam.se.StudentExamID)
                    .ToList();

                // تحديد الأسئلة التي لم يتم الإجابة عليها
                var unansweredQuestions = from q in questions
                                          join sa in studentAnswers on q.QuestionsID equals sa.QuestionsID into saGroup
                                          from sa in saGroup.DefaultIfEmpty()
                                          where sa == null || (sa.AnswerStudent == null && sa.AnswerStudentText == null)
                                          select q;

                // الحصول على بيانات المادة والصف
                var subjectData = (from ssr in db.SubjectsStageRows
                                   join sub in db.Subjects on ssr.SubjectsID equals sub.SubjectID
                                   join sr in db.StageRows on ssr.StageRow equals sr.StageRowID
                                   join r in db.Rows on sr.RowID equals r.RowID
                                   join st in db.Stages on sr.StageID equals st.StageID
                                   where ssr.SubjectsStageRowID == exam.e.SubjectsStageRowID
                                   select new { sub, r, st }).FirstOrDefault();

                if (subjectData != null && search.SubjectId.HasValue)
                {
                    if (subjectData.sub.SubjectID != search.SubjectId.Value)
                    {
                        continue;
                    }
                }

                // إضافة الأسئلة غير المجابة للتقرير
                foreach (var question in unansweredQuestions)
                {
                    var reportItem = new StudentUnansweredReport
                    {
                        StudentNumber = exam.u.StudentNumber ?? 0,
                        StudentName = exam.u.StudentName,
                        UserName = exam.u.UserName,
                        CountryName = exam.u.CountryName,
                        ExamID = exam.e.ExamsTerm2ID,
                        ExamName = exam.e.ExamName,
                        SubjectName = subjectData?.sub.SubjectName ?? "غير محدد",
                        ClassName = subjectData != null ? subjectData.r.RowName + " - " + subjectData.st.StageName : "غير محدد",
                        ExamDate = exam.e.TimeDate,
                        TimeFrom = exam.e.TimeFrom,
                        TimeTo = exam.e.TimeTo,
                        QuestionsID = question.QuestionsID,
                        QuestionTitle = question.QuationTitle,
                        QuestionType = question.QuestionType ?? 0,
                        QuestionDegree = question.degree ?? 0,
                        StudentExamID = exam.se.StudentExamID.ToString(),
                        StartAt = exam.se.StartAt,
                        EndAt = exam.se.EndAt
                    };

                    report.Add(reportItem);
                }
            }

            // حساب الإحصائيات
            CalculateStatistics(report, 2);

            // ترتيب النتائج
            report = report.OrderByDescending(r => r.ExamDate)
                          .ThenBy(r => r.StudentNumber)
                          .ThenBy(r => r.ExamName)
                          .ThenBy(r => r.QuestionsID)
                          .ToList();

            return report;
        }

        // حساب الإحصائيات - طريقة أبسط
        private void CalculateStatistics(List<StudentUnansweredReport> report, int term)
        {
            if (!report.Any()) return;

            // تجميع حسب الامتحان
            var examGroups = report.GroupBy(r => r.ExamID);

            foreach (var group in examGroups)
            {
                int examId = group.Key;

                try
                {
                    // الحصول على عدد الأسئلة الكلي للامتحان
                    int totalQuestions = 0;
                    float totalDegree = 0;

                    if (term == 1)
                    {
                        totalQuestions = db.ChoiseQuestions.Count(q => q.ExamID == examId);
                        totalDegree = (float)(db.ChoiseQuestions
                            .Where(q => q.ExamID == examId)
                            .Sum(q => q.degree) ?? 0);
                    }
                    else
                    {
                        totalQuestions = db.ChoiseQuestionTerm2.Count(q => q.ExamID == examId);
                        totalDegree = (float)(db.ChoiseQuestionTerm2
                            .Where(q => q.ExamID == examId)
                            .Sum(q => q.degree) ?? 0);
                    }

                    int unansweredCount = group.Count();

                    foreach (var item in group)
                    {
                        item.TotalQuestions = totalQuestions;
                        item.UnansweredCount = unansweredCount;
                        item.AnsweredCount = totalQuestions - unansweredCount;
                        item.TotalPossibleDegree = totalDegree;
                    }
                }
                catch (Exception ex)
                {
                    // في حالة حدوث خطأ، نعطي قيم افتراضية
                    foreach (var item in group)
                    {
                        item.TotalQuestions = 0;
                        item.UnansweredCount = group.Count();
                        item.AnsweredCount = 0;
                        item.TotalPossibleDegree = 0;
                    }
                }
            }
        }

        // الحصول على معلومات الطالب
        private StudentUnansweredReport GetStudentInfo(int studentNumber)
        {
            try
            {
                var student = db.AspNetUsers
                    .Where(u => u.StudentNumber == studentNumber)
                    .Select(u => new
                    {
                        u.StudentNumber,
                        u.StudentName,
                        u.UserName,
                        u.CountryName,
                        RowName = u.Row != null ? u.Row.RowName : "غير محدد",
                        StageName = u.Stage != null ? u.Stage.StageName : "غير محدد"
                    })
                    .FirstOrDefault();

                if (student != null)
                {
                    return new StudentUnansweredReport
                    {
                        StudentNumber = student.StudentNumber ?? 0,
                        StudentName = student.StudentName,
                        UserName = student.UserName,
                        ClassName = student.RowName + " - " + student.StageName,
                        CountryName = student.CountryName,
                        TotalQuestions = 0,
                        UnansweredCount = 0,
                        AnsweredCount = 0,
                        TotalPossibleDegree = 0
                    };
                }
            }
            catch
            {
                // تجاهل الخطأ
            }

            return null;
        }

        // API لجلب قائمة المواد
        public JsonResult GetSubjects()
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var subjects = db.Subjects
                    .OrderBy(s => s.SubjectName)
                    .Select(s => new
                    {
                        id = s.SubjectID,
                        name = s.SubjectName
                    })
                    .ToList();

                return Json(subjects, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        // تقرير مبسط للإجابة (نعم/لا)
        public ActionResult QuickReport(int? studentNumber, int term = 1)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });

            var claimsPrincipal = User as ClaimsPrincipal;
            var userType = claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "UserType")?.Value;
            if (User.IsInRole("Support") )
               
                return RedirectToAction("Login", "Account", new { area = "" });

            if (!studentNumber.HasValue)
            {
                return RedirectToAction("Index");
            }

            try
            {
                var student = db.AspNetUsers
                    .Where(u => u.StudentNumber == studentNumber)
                    .FirstOrDefault();

                if (student == null)
                {
                    return HttpNotFound();
                }

                // الحصول على إحصائيات سريعة
                bool hasUnanswered = false;

                if (term == 1)
                {
                    hasUnanswered = db.StudentChoiceExams
                        .Any(sce => db.StudentExams
                            .Any(se => se.SudentID == student.Id &&
                                        se.StudentExamID == sce.StudentExamID &&
                                        (sce.AnswerStudent == null && sce.AnswerStudentText == null)));
                }
                else
                {
                    hasUnanswered = db.StudentChoiceExamTerm2
                        .Any(sce => db.StudentExamsTerm2
                            .Any(se => se.SudentID == student.Id &&
                                        se.StudentExamID == sce.StudentExamID &&
                                        (sce.AnswerStudent == null && sce.AnswerStudentText == null)));
                }

                ViewBag.StudentName = student.StudentName;
                ViewBag.StudentNumber = student.StudentNumber;
                ViewBag.HasUnanswered = hasUnanswered;
                ViewBag.Message = hasUnanswered ? "يوجد أسئلة غير مجابة" : "تم الإجابة على جميع الأسئلة";

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "حدث خطأ: " + ex.Message;
                return View();
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendSupport(StudentSupportTicketVM model)
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var studentId = JwtHelper.GetClaim(principal, "nameid");
            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var ticket = new SupportTicket
            {
                StudentId = studentId,
                ExamId = model.ExamId, // الآن سيحفظ ExamID
                TicketType = model.TicketType,
                Subject = "بلاغ من الطالب",
                Description = model.Description,
                CreatedBy = User.Identity.Name,
                Status = "Open",
            };

            db.SupportTickets.Add(ticket);
            db.SaveChanges();

            TempData["Success"] = "✅ تم إرسال المشكلة بنجاح";
            return RedirectToAction("MyReport");
        }

        public ActionResult MyReport()
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var userId = JwtHelper.GetClaim(principal, "nameid");

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account", new { area = "" });

            // الطالب
            var student = db.AspNetUsers
                .Include("Row")
                .Include("Stage")
                .FirstOrDefault(u => u.Id == userId);

            if (student == null)
                return HttpNotFound();

            // التقرير
            var search = new StudentSearchModel
            {
                StudentNumber = student.StudentNumber,
                Term = 1
            };
            var report = GetTerm1Report(search);

            // امتحانات الطالب (Dropdown)
            var studentExams = db.StudentExams
                .Where(se => se.SudentID == userId)
                .Include("Exam.SubjectsStageRow.Subject")
                .ToList();

            ViewBag.Subjects = studentExams
                .Select(se => new SelectListItem
                {
                    Value = se.ExamID.ToString(),
                    Text = se.Exam.SubjectsStageRow.Subject.SubjectName + " - " + se.Exam.ExamName
                })
                .Distinct()
                .ToList();

            // Tickets
            DateTime today = DateTime.Today;

            int todayTicketsCount = db.SupportTickets.Count(t =>
                t.StudentId == userId &&
                DbFunctions.TruncateTime(t.CreatedAt) == today
            );

            var myTickets = db.SupportTickets
                .Where(t => t.StudentId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            // ViewModel
            var model = new StudentReportPageVM
            {
                Student = student,
                Report = report,
                MyTickets = myTickets,
                TotalExams = report.Select(r => r.ExamID).Distinct().Count(),
                TotalUnanswered = report.Count,
                TicketsToday = todayTicketsCount,
                RemainingTickets = Math.Max(0, 2 - todayTicketsCount)
            };
            ViewBag.RemainingTickets = Math.Max(0, 2 - todayTicketsCount);
            ViewBag.TicketsToday = todayTicketsCount;
            // ViewBag للبيانات العامة
            ViewBag.Student = student;
            ViewBag.TotalExams = report.Select(r => r.ExamID).Distinct().Count();
            ViewBag.TotalUnanswered = report.Count;
            // ViewBag إضافي لو مستخدم في View
            ViewBag.TotalAnswered = report.Any() ? report.First().AnsweredCount : 0;
            if (report.Any())
            {
                ViewBag.FirstStudent = report.First();
            }
            else
            {
                // إنشاء كائن افتراضي إذا كانت القائمة فارغة
                ViewBag.FirstStudent = new StudentUnansweredReport
                {
                    StudentNumber = student.StudentNumber ?? 0,
                    StudentName = student.StudentName,
                    UserName = student.UserName,
                    CountryName = student.CountryName,
                    ClassName = student.Row != null && student.Stage != null
                               ? student.Row.RowName + " - " + student.Stage.StageName
                               : "غير محدد"
                };
            }
            return View(model);
        }

        public string EncryptionMasterKey() => WebConfigurationManager.AppSettings["EncryptionMasterKey"] ?? "r4u7x!A%D*G-KaPdRgUkXp2s5v8y/B?E";

        public string Decrypt(string encryptedText)
        {
            if (!string.IsNullOrEmpty(encryptedText))
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
                        encryptedText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            return encryptedText;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManageTicket(ManageTicketViewModel model, string actionType)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });
           
            var ticket = db.SupportTickets.Find(model.TicketId);
            var user = db.AspNetUsers.Find(model.StudentId);

            // تعديل بيانات الطالب
            user.UserName = model.StudentName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;

            if (actionType == "OpenExam")
            {
                // فتح الامتحان
                ticket.Status = "Closed";
                ticket.ActionTaken = "Exam opened";
            }

            if (actionType == "ChangeGroup")
            {
                // حفظ المجموعة الأصلية
                if (ticket.OldGroupId == null)
                    ticket.OldGroupId = user.GroupID;

                user.GroupID = model.NewGroupId;

                ticket.Status = "Closed";
                ticket.ActionTaken = "Group changed";
            }

            if (actionType == "EditStudent")
            {
                ticket.Status = "Closed";
                ticket.ActionTaken = "Student data updated";
            }

            db.SaveChanges();
            return RedirectToAction("Index");
        }
        public ActionResult ArchivedTickets(int page = 1)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });
            int pageSize = 10;

            var query = db.SupportTickets
                .Where(t => t.Status == "Archived")
                .OrderByDescending(t => t.ClosedAt ?? t.CreatedAt);

            int totalCount = query.Count();

            var tickets = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return View(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ArchiveTicket(int ticketId)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });
            var ticket = db.SupportTickets.Find(ticketId);

            if (ticket == null)
                return HttpNotFound();

            ticket.Status = "Archived";
            ticket.ClosedAt = DateTime.Now; // لو عندك العمود ده

            db.SaveChanges();

            TempData["Success"] = "✅ تم إرسال الطلب إلى الأرشيف";

            return RedirectToAction("OpenTickets");
        }


        public ActionResult OpenTickets()
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Json(new { isSupport = false });

            var userId = JwtHelper.GetClaim(principal, "examId");

            var tickets = db.SupportTickets
                .Where(t => t.Status == "Open")
                .Select(t => new SupportTicketPageVM
                {
                    TicketId = t.TicketId,
                    TicketType = t.TicketType,
                    Description = t.Description,
                    SubjectName = t.Exam.SubjectsStageRow.Subject.SubjectName,
                    StudentId = t.StudentId,
                    StudentName = t.AspNetUser.StudentName,
                    Email = t.AspNetUser.Email,
                    PhoneNumber = t.AspNetUser.PhoneNumber,
                    NationalId = t.AspNetUser.UserName,
                    StudentNumber = t.AspNetUser.StudentNumber,
                    Group=t.AspNetUser.GroupID,
                    // جلب أسماء من الجداول المرتبطة
                    InstituteType = t.AspNetUser.InstType.TypeName, // من جدول InstType
                    Mazhab = t.AspNetUser.Mazhab.MazhabName,           // من جدول Mazhab
                    InstituteName = t.AspNetUser.InstName,
                    Country = t.AspNetUser.CountryName,

                }) 
                .ToList();
            // قائمة المذاهب
            ViewBag.Mazhabs = db.Mazhabs
                .Select(m => new SelectListItem
                {
                    Value = m.MazhabID.ToString(),
                    Text = m.MazhabName
                }).ToList();
            ViewBag.Sho3baID = db.Sho3ba
               .Select(m => new SelectListItem
               {
                   Value = m.Sho3baID.ToString(),
                   Text = m.Sho3baName
               }).ToList();
            // قائمة أنواع المعهد
            ViewBag.InstTypes = db.InstTypes
                .Select(i => new SelectListItem
                {
                    Value = i.InstTypeID.ToString(),
                    Text = i.TypeName
                }).ToList();

            ViewBag.Groups = db.GroupNoes
                .Select(g => new SelectListItem
                {
                    Value = g.GroupNoID.ToString(),
                    Text = g.GroupNoName
                })
                .ToList();

            return View(tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TicketAction(
      int ticketId,
    string actionType,
    string studentName,
    string email,
    string phone,
    int? instituteType,  // جديد
    int? mazhab,         // جديد
    int? studentNumber,
    string nationalId,
    string instituteName,
    string country,
    int? section,
    byte? newGroupId)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });

            var ticket = db.SupportTickets.Find(ticketId);
            var user = db.AspNetUsers.Find(ticket.StudentId);

            // تعديل بيانات الطالب
            user.StudentName = studentName;

            user.StudentNumber = studentNumber;
            user.UserName = nationalId; // الرقم القومي
            user.InstTypeID = instituteType;
            user.MazhabID = mazhab;
            user.InstName = instituteName;
            user.CountryName = country;
            user.MazhabID = section;

            if (actionType == "OpenExam")
            {
                ticket.Status = "Closed";
                ticket.ActionTaken = "Exam Opened";

                // تحديث StudentExams
                var studentExam = db.StudentExams
         .FirstOrDefault(se => se.SudentID == ticket.StudentId && se.ExamID == ticket.ExamId);

                if (studentExam != null)
                {
                    // إنشاء سجل جديد إذا لم يكن موجود
                    // تعديل السجل الموجود
                    studentExam.IsActive = true;

                }
                else
                {

                }
            }

            if (actionType == "ChangeGroup" && newGroupId.HasValue)
            {
                if (ticket.OldGroupId == null)
                    ticket.OldGroupId = user.GroupID;

                user.GroupID = newGroupId.Value;

                ticket.Status = "Closed";
                ticket.ActionTaken = "Group Changed";
            }
            if (actionType == "EditStudent" && newGroupId.HasValue)
            {


                ticket.Status = "Closed";
                ticket.ActionTaken = " Changed";
            }
            db.SaveChanges();

            return RedirectToAction("OpenTickets", new { groupId = user.GroupID });
        }

        public ActionResult MyTickets()
        {
            var principal = GetJwtPrincipal();
            if (principal == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var studentId = JwtHelper.GetClaim(principal, "nameid");

            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var tickets = db.SupportTickets
                .Where(t => t.StudentId == studentId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            return View(tickets);
        }
        // دالة للحصول على تقرير الامتحانات للطالب
        private List<StudentExamReport> GetStudentExamsReport(StudentSearchModel search)
        {
            var examsReport = new List<StudentExamReport>();

            if (!search.StudentNumber.HasValue && string.IsNullOrEmpty(search.StudentName))
                return examsReport;

            if (search.Term == 1)
            {
                // الفصل الأول
                var query = from u in db.AspNetUsers
                            join se in db.StudentExams on u.Id equals se.SudentID
                            join e in db.Exams on se.ExamID equals e.ExamsID
                            join ssr in db.SubjectsStageRows on e.SubjectsStageRowID equals ssr.SubjectsStageRowID
                            join sub in db.Subjects on ssr.SubjectsID equals sub.SubjectID
                            where e.TimeDate != null
                            select new { u, se, e, sub };

                if (search.StudentNumber.HasValue)
                {
                    query = query.Where(x => x.u.StudentNumber == search.StudentNumber.Value);
                }

                if (!string.IsNullOrEmpty(search.StudentName))
                {
                    query = query.Where(x => x.u.StudentName.Contains(search.StudentName) ||
                                             x.u.UserName.Contains(search.StudentName));
                }

                if (search.FromDate.HasValue)
                {
                    query = query.Where(x => x.e.TimeDate >= search.FromDate.Value);
                }

                if (search.ToDate.HasValue)
                {
                    query = query.Where(x => x.e.TimeDate <= search.ToDate.Value);
                }

                var results = query.ToList();

                foreach (var item in results)
                {
                    // الحصول على بيانات الصف
                    var classData = (from ssr in db.SubjectsStageRows
                                     join sr in db.StageRows on ssr.StageRow equals sr.StageRowID
                                     join r in db.Rows on sr.RowID equals r.RowID
                                     join st in db.Stages on sr.StageID equals st.StageID
                                     where ssr.SubjectsStageRowID == item.e.SubjectsStageRowID
                                     select new { r, st }).FirstOrDefault();

                    var examReport = new StudentExamReport
                    {
                        StudentNumber = item.u.StudentNumber ?? 0,
                        StudentName = item.u.StudentName,
                        ExamID = item.e.ExamsID,
                        ExamName = item.e.ExamName,
                        SubjectName = item.sub.SubjectName,
                        ClassName = classData != null ? classData.r.RowName + " - " + classData.st.StageName : "غير محدد",
                        ExamDate = item.e.TimeDate,
                        TimeFrom = item.e.TimeFrom,
                        TimeTo = item.e.TimeTo,
                        StudentStartTime = item.se.StartAt,
                        StudentEndTime = item.se.EndAt,
                        IsActive = item.se.IsActive ?? false,
                        ExamStatus = GetExamStatusForStudent(item.se.StartAt, item.se.EndAt,
                                                            item.e.TimeDate, item.e.TimeFrom, item.e.TimeTo)
                    };

                    examsReport.Add(examReport);
                }
            }
            else
            {
                // الفصل الثاني - نفس المنطق مع تعديل الجداول
                var query = from u in db.AspNetUsers
                            join se in db.StudentExamsTerm2 on u.Id equals se.SudentID
                            join e in db.ExamsTerm2 on se.ExamID equals e.ExamsTerm2ID
                            join ssr in db.SubjectsStageRows on e.SubjectsStageRowID equals ssr.SubjectsStageRowID
                            join sub in db.Subjects on ssr.SubjectsID equals sub.SubjectID
                            where e.TimeDate != null
                            select new { u, se, e, sub };

                // نفس الفلاتر
                if (search.StudentNumber.HasValue)
                {
                    query = query.Where(x => x.u.StudentNumber == search.StudentNumber.Value);
                }

                if (!string.IsNullOrEmpty(search.StudentName))
                {
                    query = query.Where(x => x.u.StudentName.Contains(search.StudentName) ||
                                             x.u.UserName.Contains(search.StudentName));
                }

                if (search.FromDate.HasValue)
                {
                    query = query.Where(x => x.e.TimeDate >= search.FromDate.Value);
                }

                if (search.ToDate.HasValue)
                {
                    query = query.Where(x => x.e.TimeDate <= search.ToDate.Value);
                }

                var results = query.ToList();

                foreach (var item in results)
                {
                    var classData = (from ssr in db.SubjectsStageRows
                                     join sr in db.StageRows on ssr.StageRow equals sr.StageRowID
                                     join r in db.Rows on sr.RowID equals r.RowID
                                     join st in db.Stages on sr.StageID equals st.StageID
                                     where ssr.SubjectsStageRowID == item.e.SubjectsStageRowID
                                     select new { r, st }).FirstOrDefault();

                    var examReport = new StudentExamReport
                    {
                        StudentNumber = item.u.StudentNumber ?? 0,
                        StudentName = item.u.StudentName,
                        ExamID = item.e.ExamsTerm2ID,
                        ExamName = item.e.ExamName,
                        SubjectName = item.sub.SubjectName,
                        ClassName = classData != null ? classData.r.RowName + " - " + classData.st.StageName : "غير محدد",
                        ExamDate = item.e.TimeDate,
                        TimeFrom = item.e.TimeFrom,
                        TimeTo = item.e.TimeTo,
                        StudentStartTime = item.se.StartAt,
                        StudentEndTime = item.se.EndAt,
                        IsActive = item.se.IsActive ?? false,
                        ExamStatus = GetExamStatusForStudent(item.se.StartAt, item.se.EndAt,
                                                            item.e.TimeDate, item.e.TimeFrom, item.e.TimeTo)
                    };

                    examsReport.Add(examReport);
                }
            }

            return examsReport.OrderByDescending(e => e.ExamDate).ThenBy(e => e.ExamName).ToList();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateReport(StudentSearchModel searchModel)
        {
            if (!IsAdminUser())
                return RedirectToAction("Login", "Account", new { area = "" });
            if (!searchModel.StudentNumber.HasValue && string.IsNullOrEmpty(searchModel.StudentName))
            {
                ModelState.AddModelError("", "يرجى إدخال رقم الطالب أو اسمه");
                return View("Index", searchModel);
            }

            List<StudentUnansweredReport> report;

            if (searchModel.Term == 2)
            {
                report = GetTerm2Report(searchModel);
            }
            else
            {
                report = GetTerm1Report(searchModel);
            }

            // الحصول على تقرير الامتحانات للطالب
            var studentExamsReport = GetStudentExamsReport(searchModel);

            // الحصول على بيانات الطالب (إذا كان البحث برقم طالب واحد)
            AspNetUser student = null;
            if (searchModel.StudentNumber.HasValue)
            {
                student = db.AspNetUsers
                    .Include(u => u.Row)
                    .Include(u => u.Stage)
                    .FirstOrDefault(u => u.StudentNumber == searchModel.StudentNumber.Value);

                // تمرير بيانات الطالب إلى View
                ViewBag.Student = student;
            }
            ViewBag.Mazhabs = db.Mazhabs
    .Select(m => new SelectListItem
    {
        Value = m.MazhabID.ToString(),
        Text = m.MazhabName
    })
    .ToList();

            ViewBag.Sho3bas = db.Sho3ba
                .Select(s => new SelectListItem
                {
                    Value = s.Sho3baID.ToString(),
                    Text = s.Sho3baName
                })
                .ToList();

            ViewBag.InstTypes = db.InstTypes
                .Select(i => new SelectListItem
                {
                    Value = i.InstTypeID.ToString(),
                    Text = i.TypeName
                })
                .ToList();

            ViewBag.Rows = db.Rows
                .Select(r => new SelectListItem
                {
                    Value = r.RowID.ToString(),
                    Text = r.RowName
                })
                .ToList();

            ViewBag.Stages = db.Stages
                .Select(s => new SelectListItem
                {
                    Value = s.StageID.ToString(),
                    Text = s.StageName
                })
                .ToList();
            // إحصائيات عامة
            ViewBag.TotalExams = report.GroupBy(r => r.ExamID).Count();
            ViewBag.TotalUnanswered = report.Count;
            ViewBag.TotalStudents = report.GroupBy(r => r.StudentNumber).Count();

            // تمرير تقرير الامتحانات للعرض
            ViewBag.StudentExamsReport = studentExamsReport;

            // تمرير قائمة المجموعات للعرض
            ViewBag.Groups = db.GroupNoes
                .Select(g => new SelectListItem
                {
                    Value = g.GroupNoID.ToString(),
                    Text = g.GroupNoName
                })
                .ToList();

            return View("ReportResult", report);
        }

        // الحصول على جميع بيانات الطالب
        // الحصول على جميع بيانات الطالب
        [HttpGet]
        public JsonResult GetStudentFullData(int studentNumber)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var student = db.AspNetUsers
                    .Include(u => u.Row)
                    .Include(u => u.Stage)
                    .Include(u => u.Mazhab)
                    .Include(u => u.Sho3ba)
                    .Include(u => u.InstType)
                    .Include(u => u.GroupNo)
                    .FirstOrDefault(u => u.StudentNumber == studentNumber);

                if (student == null)
                {
                    return Json(new { success = false, message = "الطالب غير موجود" });
                }

                var data = new
                {
                    studentName = student.StudentName,
                    userName = student.UserName,
                    email = student.Email,
                    phoneNumber = student.PhoneNumber,
                    countryName = student.CountryName,
                    instName = student.InstName,
                    rowId = student.RowID,
                    stageId = student.StageID,
                    mazhabId = student.MazhabID,
                    sho3baId = student.Sho3baID,
                    instTypeId = student.InstTypeID,
                    groupId = student.GroupID,
                    // إضافة أسماء للعرض فقط
                    rowName = student.Row != null ? student.Row.RowName : "",
                    stageName = student.Stage != null ? student.Stage.StageName : "",
                    mazhabName = student.Mazhab != null ? student.Mazhab.MazhabName : "",
                    sho3baName = student.Sho3ba != null ? student.Sho3ba.Sho3baName : "",
                    instTypeName = student.InstType != null ? student.InstType.TypeName : "",
                    groupName = student.GroupNo != null ? student.GroupNo.GroupNoName : ""
                };

                return Json(new { success = true, data = data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
       
        // النموذج المحدث
        // النموذج المحدث
        public class UpdateStudentFullModel
        {
            public int StudentNumber { get; set; }
            public string StudentName { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string CountryName { get; set; }
            public string InstName { get; set; }
            public byte? RowId { get; set; }          // ✅ تغيير من byte? إلى int?
            public byte? StageId { get; set; }        // ✅ تغيير من byte? إلى int?
            public int? MazhabId { get; set; }
            public int? Sho3baId { get; set; }
            public int? InstTypeId { get; set; }
            public byte? NewGroupId { get; set; }
            public string Notes { get; set; }
        }
        // دالة مساعدة لتحديد حالة الامتحان
        private string GetExamStatusForStudent(DateTime? studentStart, DateTime? studentEnd,
                                              DateTime? examDate, TimeSpan? examFrom, TimeSpan? examTo)
        {
            if (!studentStart.HasValue)
                return "لم يبدأ";

            if (studentEnd.HasValue)
                return "منتهي";

            // التحقق من وقت الامتحان الرسمي
            if (examDate.HasValue && examFrom.HasValue && examTo.HasValue)
            {
                var examStart = examDate.Value.Date + examFrom.Value;
                var examEnd = examDate.Value.Date + examTo.Value;
                var now = DateTime.Now;

                if (now < examStart)
                    return "لم يحن موعده";
                else if (now >= examStart && now <= examEnd)
                    return "نشط";
                else if (now > examEnd)
                    return "انتهى الوقت";
            }

            return "نشط";
        }

        [HttpPost]
        public JsonResult GetNotfication()
        {
            if (!IsAdminUser())
                return Json(new { isSupport = false }, JsonRequestBehavior.AllowGet);
            var principal = GetJwtPrincipal();
            if (principal == null)
                return Json(new { isSupport = false });

            var userId = JwtHelper.GetClaim(principal, "nameid");

            bool isSupport = db.AspNetUsers.Any(u => u.Id == userId && u.CountryName == null);

            return Json(new { isSupport = isSupport }, JsonRequestBehavior.AllowGet);
        }
        // دالة فتح الامتحان للطالب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult OpenExam(int examId, int studentNumber)
        {
            if (!IsAdminUser())
                return Json(new { success = false, message = "غير مصرح لك بالوصول" });
            try
            {
                // البحث عن الطالب
                var student = db.AspNetUsers
                    .FirstOrDefault(u => u.StudentNumber == studentNumber);

                if (student == null)
                {
                    return Json(new { success = false, message = "الطالب غير موجود" });
                }

                // البحث عن الامتحان
                var exam = db.Exams
                    .FirstOrDefault(e => e.ExamsID == examId);

                if (exam == null)
                {
                    return Json(new { success = false, message = "الامتحان غير موجود" });
                }

                // البحث عن سجل امتحان الطالب
                var studentExam = db.StudentExams
                    .FirstOrDefault(se => se.SudentID == student.Id && se.ExamID == examId);

                if (studentExam == null)
                {
                     
                }
                else
                {
                    // تعديل السجل الموجود
                    //studentExam.StartAt = DateTime.Now; // إعادة البداية
                    studentExam.EndAt = null; // إلغاء النهاية السابقة
                    studentExam.IsActive = true; // تفعيل الامتحان
                }
                db.SaveChanges();





                return Json(new
                {
                    success = true,
                    message = "تم فتح الامتحان بنجاح للطالب"
                });
            }
            catch (Exception ex)
            {
                // تسجيل الخطأ
               

                return Json(new
                {
                    success = false,
                    message = "حدث خطأ أثناء فتح الامتحان: " + ex.Message
                });
            }
        }

    
        // دالة للتحقق من صلاحيات فتح الامتحان
        [HttpGet]
        public JsonResult CanOpenExam(int examId, int studentNumber)
        {
            if (!IsAdminUser())
                return Json(new { canOpen = false, reason = "غير مصرح لك بالوصول" }, JsonRequestBehavior.AllowGet);
            try
            {
                var student = db.AspNetUsers
                    .FirstOrDefault(u => u.StudentNumber == studentNumber);

                if (student == null)
                {
                    return Json(new
                    {
                        canOpen = false,
                        reason = "الطالب غير موجود"
                    }, JsonRequestBehavior.AllowGet);
                }

                // التحقق من حالة الامتحان
                var exam = db.Exams.Find(examId);
                if (exam == null)
                {
                    return Json(new
                    {
                        canOpen = false,
                        reason = "الامتحان غير موجود"
                    }, JsonRequestBehavior.AllowGet);
                }

                // التحقق من وقت الامتحان
                if (exam.TimeTo.HasValue && DateTime.Now > exam.TimeDate?.Date + exam.TimeTo.Value)
                {
                    return Json(new
                    {
                        canOpen = false,
                        reason = "انتهى وقت الامتحان الرسمي"
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    canOpen = true,
                    examName = exam.ExamName,
                    studentName = student.StudentName
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    canOpen = false,
                    reason = "حدث خطأ: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

    }
}

  