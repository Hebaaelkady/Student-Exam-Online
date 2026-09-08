using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using Lesson.Models;

namespace Lesson.Helpers
{
    /// <summary>
    /// نموذج خفيف لبيانات السؤال المخزنة في الذاكرة
    /// </summary>
    public class CachedQuestionItem
    {
        public int QuestionsID { get; set; }
        public int? QuestionType { get; set; }
    }

    /// <summary>
    /// مدير التخزين المؤقت الذكي لأسئلة الامتحانات (Smart In-Memory Caching).
    /// يراعي خصوصية كل امتحان:
    /// كل (مذهب، صف، شعبة، نوع معهد لغات أو عادي، ومجموعة) له ExamID مستقل تماماً،
    /// ويتم تخزين كل امتحان في مفتاح منفصل Exam_Questions_{examId}،
    /// مما يمنع أي تداخل بين الامتحانات ويحقق أقصى سرعة لآلاف الطلاب في نفس اللحظة.
    /// </summary>
    public static class ExamCacheHelper
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// جلب قائمة الأسئلة للامتحان من الذاكرة أو من قاعدة البيانات وتخزينها
        /// </summary>
        public static async Task<List<CachedQuestionItem>> GetExamQuestionItemsAsync(LessonsEntities db, int examId)
        {
            string cacheKey = $"Exam_Questions_{examId}";

            // 1. الفحص في الذاكرة السريعة (0.1ms)
            var cached = HttpRuntime.Cache[cacheKey] as List<CachedQuestionItem>;
            if (cached != null)
            {
                return cached;
            }

            // 2. إذا لم تكن في الذاكرة، يتم الاستعلام من قاعدة البيانات مرة واحدة فقط
            var questions = await db.ChoiseQuestions
                .Where(j => j.ExamID == examId)
                .Select(q => new CachedQuestionItem
                {
                    QuestionsID  = q.QuestionsID,
                    QuestionType = q.QuestionType
                })
                .AsNoTracking()
                .ToListAsync();

            if (questions != null && questions.Count > 0)
            {
                // حفظ الأسئلة في ذاكرة السيرفر لمدة 4 ساعات (فترة الامتحان)
                HttpRuntime.Cache.Insert(
                    cacheKey,
                    questions,
                    null,
                    DateTime.Now.AddHours(4),
                    Cache.NoSlidingExpiration,
                    CacheItemPriority.High,
                    null);
            }

            return questions;
        }

        /// <summary>
        /// جلب عدد أسئلة الامتحان بسرعة البرق من الذاكرة دون استعلام COUNT(*) على الداتابيز
        /// </summary>
        public static async Task<int> GetExamQuestionCountAsync(LessonsEntities db, int examId)
        {
            var questions = await GetExamQuestionItemsAsync(db, examId);
            return questions != null ? questions.Count : 0;
        }

        /// <summary>
        /// مسح كاش امتحان معين في حال قام المشرف بتعديل الأسئلة
        /// </summary>
        public static void InvalidateExamCache(int examId)
        {
            string cacheKey = $"Exam_Questions_{examId}";
            HttpRuntime.Cache.Remove(cacheKey);
        }
    }
}
