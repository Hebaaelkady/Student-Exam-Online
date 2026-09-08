// Models/StudentUnansweredReport.cs
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Lesson.Models
{
    public class SupportTicketViewModel
    {
        public int TicketId { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }

        public int? SubjectId { get; set; }
        public string SubjectName { get; set; }

        public int? ExamId { get; set; }
        public string ExamName { get; set; }

        public string TicketType { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }

        public int? OldGroupId { get; set; }
        public int? NewGroupId { get; set; }

        public DateTime? CreatedAt { get; set; }
        public string ActionTaken { get; set; }
    }
    public class ManageTicketViewModel
    {
        // Ticket
        public int TicketId { get; set; }
        public string TicketType { get; set; }
        public string Description { get; set; }

        // Student
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Exam
        public int? ExamId { get; set; }
        public string ExamName { get; set; }

        // Groups
        public int? OriginalGroupId { get; set; }
        public byte? NewGroupId { get; set; }
        public IEnumerable<SelectListItem> Groups { get; set; }
    }
    public class SupportTicketPageVM
    {
        
              public string SubjectName { get; set; }
        public int TicketId { get; set; }
        public string TicketType { get; set; }
        public string Description { get; set; }

        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public int? Group { get; set; }

        // الحقول الجديدة المطلوبة
        public string NationalId { get; set; }    // الرقم القومي
        public int? StudentNumber { get; set; } // رقم الجلوس
        public string InstituteType { get; set; } // نوع المعهد
        public string Mazhab { get; set; }        // المذهب
        public string InstituteName { get; set; } // المعهد
        public string Country { get; set; }       // الدولة
        public int? Section { get; set; }       // الشعبة
    }


}