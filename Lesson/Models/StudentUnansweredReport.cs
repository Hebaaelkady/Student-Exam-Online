// Models/StudentUnansweredReport.cs
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Lesson.Models
{
    public class StudentReportPageVM
    {
        public List<StudentUnansweredReport> Report { get; set; }
        public List<SupportTicket> MyTickets { get; set; }

        public int TotalExams { get; set; }
        public int TotalUnanswered { get; set; }
        public int RemainingTickets { get; set; }
        public int TicketsToday { get; set; }

        public AspNetUser Student { get; set; }
    }

    
        public class StudentUnansweredReport
        {
            // بيانات الطالب
            public int StudentNumber { get; set; }
            public string StudentName { get; set; }
            public string UserName { get; set; }
            public string ClassName { get; set; }
            public string CountryName { get; set; }

            // بيانات الامتحان
            public int ExamID { get; set; }
            public string ExamName { get; set; }
            public string SubjectName { get; set; }
            public DateTime? ExamDate { get; set; }
            public TimeSpan? TimeFrom { get; set; }
            public TimeSpan? TimeTo { get; set; }

            // بيانات السؤال غير المجاب
            public int QuestionsID { get; set; }
            public string QuestionTitle { get; set; }
            public int QuestionType { get; set; }
            public double QuestionDegree { get; set; }

            // معلومات التوقيت
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
            public string StudentExamID { get; set; }

            // للإحصائيات
            public int TotalQuestions { get; set; }
            public int UnansweredCount { get; set; }
            public int AnsweredCount { get; set; }
            public float TotalPossibleDegree { get; set; }

            // خاصية مساعدة - حالة الامتحان
            public bool IsExamFinished
            {
                get
                {
                    return EndAt.HasValue && EndAt < DateTime.Now;
                }
            }

            // للحصول على نوع السؤال كنص
            public string QuestionTypeText
            {
                get
                {
                    switch (QuestionType)
                    {
                        case 1:
                            return "اختيار من متعدد";
                        case 2:
                            return "صح وخطأ";
                        case 3:
                            return "مقالي";
                        default:
                            return "نوع غير محدد";
                    }
                }
            }

            // خاصية للحصول على وقت بداية الامتحان للطالب
            public string StartTimeFormatted => StartAt?.ToString("hh:mm tt") ?? "لم يبدأ";

            // خاصية للحصول على وقت نهاية الامتحان للطالب
            public string EndTimeFormatted => EndAt?.ToString("hh:mm tt") ?? "لم ينتهي";

            // خاصية لوقت الامتحان الرسمي
            public string OfficialExamTime =>
                TimeFrom.HasValue && TimeTo.HasValue
                ? $"{TimeFrom.Value.ToString(@"hh\:mm")} - {TimeTo.Value.ToString(@"hh\:mm")}"
                : "غير محدد";

            // خاصية لتحديد حالة الامتحان
            public string ExamStatus
            {
                get
                {
                    if (!StartAt.HasValue)
                        return "لم يبدأ";

                    if (EndAt.HasValue && EndAt < DateTime.Now)
                        return "منتهي";

                    if (StartAt.HasValue && StartAt <= DateTime.Now)
                        return "نشط";

                    return "مستقبلي";
                }
            }
        }
    public class StudentExamReport
    {
        public int StudentNumber { get; set; }
        public string StudentName { get; set; }

        public int ExamID { get; set; }
        public string ExamName { get; set; }
        public string SubjectName { get; set; }
        public string ClassName { get; set; }

        public DateTime? ExamDate { get; set; }
        public TimeSpan? TimeFrom { get; set; }
        public TimeSpan? TimeTo { get; set; }

        public DateTime? StudentStartTime { get; set; }
        public DateTime? StudentEndTime { get; set; }
        public bool IsActive { get; set; }
        public string ExamStatus { get; set; }

        // خواص مساعدة للعرض
        public string ExamDateFormatted => ExamDate?.ToString("yyyy/MM/dd") ?? "غير محدد";
        public string OfficialTime =>
            TimeFrom.HasValue && TimeTo.HasValue
            ? $"{TimeFrom.Value.ToString(@"hh\:mm")} - {TimeTo.Value.ToString(@"hh\:mm")}"
            : "غير محدد";

        public string StudentTime =>
            StudentStartTime.HasValue
            ? StudentStartTime.Value.ToString("hh:mm tt")
            : "لم يبدأ";

        public string StudentEndTimeFormatted =>
            StudentEndTime.HasValue
            ? StudentEndTime.Value.ToString("hh:mm tt")
            : "مستمر";

        public string Duration
        {
            get
            {
                if (!StudentStartTime.HasValue) return "-";
                if (!StudentEndTime.HasValue) return "مستمر";

                var duration = StudentEndTime.Value - StudentStartTime.Value;
                return $"{(int)duration.TotalHours}:{duration.Minutes:00}";
            }
        }
    }
    // كلاس للبحث عن الطالب
    public class StudentSearchModel
    {
        public int? StudentNumber { get; set; }
        public string StudentName { get; set; }
        public int? Term { get; set; } = 1; // 1 للفصل الأول، 2 للفصل الثاني
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SubjectId { get; set; }
    }


    public class StudentSupportTicketVM
    {
        public int? SubjectId { get; set; }
        public int? ExamId { get; set; }

        public string TicketType { get; set; }
        public string Description { get; set; }

        public List<SelectListItem> Subjects { get; set; }
    }



    public class StudentReportViewModel
    {
        public string SubjectName { get; set; }

        public int TotalQuestions { get; set; }
        public int AnsweredQuestions { get; set; }
        public int NotAnsweredQuestions { get; set; }

        public List<QuestionStatusVM> Questions { get; set; }

        // نموذج إرسال مشكلة
        public StudentIssueVM Issue { get; set; }
    }
    public class StudentIssueVM
    {
        public string IssueType { get; set; } // Dropbox - Technical - Other
        public string Description { get; set; }
    }

    public class QuestionStatusVM
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public bool IsAnswered { get; set; }
    }

}