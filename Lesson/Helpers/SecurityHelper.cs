using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Lesson.Helpers
{
    /// <summary>
    /// مساعد أمني مركزي لحماية المنظومة:
    /// 1. تعقيم أكواد HTML من هجمات XSS في الأسئلة والإجابات
    /// 2. إدارة وتحديد معدل محاولات تسجيل الدخول (Rate Limiting) لمنع التخمين العنيف دون قفل الحساب
    /// </summary>
    public static class SecurityHelper
    {
        #region 1. تعقيم نصوص HTML ضد هجمات XSS

        // قائمة الوسوم الخطرة التي يجب إزالتها ومحتوياتها بالكامل
        private static readonly Regex DangerousTagsRegex = new Regex(
            @"<(script|iframe|object|embed|applet|meta|link|form|button|input)[^>]*?>[\s\S]*?</\1>|<(script|iframe|object|embed|applet|meta|link|form|button|input)[^>]*?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // قائمة الأحداث التفاعلية التي قد تشغل جافاسكريبت مثل onerror, onload, onclick, onmouseover
        private static readonly Regex EventAttributesRegex = new Regex(
            @"\s+on[a-zA-Z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // حظر بروتوكولات جافاسكريبت في الروابط والصور javascript:, vbscript:, data:text/html
        private static readonly Regex DangerousProtocolsRegex = new Regex(
            @"(href|src)\s*=\s*([""'])\s*(javascript|vbscript|data:text/html)[^""']*\2",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// يعقم نص الـ HTML بحيث يسمح بكافة التنسيقات التعليمية والصور والجداول
        /// ويحذف بشكل صارم أي وسوم تنفيذية أو سكربتات خبيثة
        /// </summary>
        public static string SanitizeHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // 1. إزالة أي وسوم تنفيذية وسكربتات
            string sanitized = DangerousTagsRegex.Replace(html, string.Empty);

            // 2. إزالة أي أحداث تفاعلية داخل الوسوم المسموحة (مثل <img onerror=...>)
            sanitized = EventAttributesRegex.Replace(sanitized, string.Empty);

            // 3. إزالة أي بروتوكولات جافاسكريبت في الروابط أو مصادر الصور
            sanitized = DangerousProtocolsRegex.Replace(sanitized, string.Empty);

            return sanitized;
        }

        /// <summary>
        /// تحويل الأرقام العربية المشرقية (٠١٢٣٤٥٦٧٨٩) إلى أرقام إنجليزية قياسية (0123456789)
        /// لتفادي مشاكل التحقق عند استخدام لوحات المفاتيح العربية على الهواتف
        /// </summary>
        public static string NormalizeDigits(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var chars = input.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= '٠' && c <= '٩')
                {
                    chars[i] = (char)('0' + (c - '٠'));
                }
                else if (c >= '۰' && c <= '۹')
                {
                    chars[i] = (char)('0' + (c - '۰'));
                }
            }
            return new string(chars);
        }

        #endregion

        #region 2. نظام تقييد معدل محاولات تسجيل الدخول (Rate Limiter)

        private class AttemptRecord
        {
            public int FailCount { get; set; }
            public DateTime FirstFailAt { get; set; }
            public DateTime? BlockedUntil { get; set; }
        }

        private static readonly ConcurrentDictionary<string, AttemptRecord> AttemptsTracker =
            new ConcurrentDictionary<string, AttemptRecord>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// فحص التقييد المزدوج: يفحص عنوان IP واسم المستخدم معاً
        /// </summary>
        public static bool IsLoginThrottled(string ip, string username, out int remainingSeconds)
        {
            remainingSeconds = 0;
            int ipWait = 0;
            int userWait = 0;

            bool ipThrottled = !string.IsNullOrEmpty(ip) && IsLoginThrottled("ip_" + ip, out ipWait);
            bool userThrottled = !string.IsNullOrEmpty(username) && IsLoginThrottled("usr_" + username, out userWait);

            if (ipThrottled || userThrottled)
            {
                remainingSeconds = Math.Max(ipWait, userWait);
                return true;
            }

            return false;
        }

        /// <summary>
        /// تسجيل محاولة فاشلة لكل من عنوان IP واسم المستخدم
        /// </summary>
        public static void RecordFailedLogin(string ip, string username)
        {
            if (!string.IsNullOrEmpty(ip))
                RecordFailedLogin("ip_" + ip);

            if (!string.IsNullOrEmpty(username))
                RecordFailedLogin("usr_" + username);
        }

        /// <summary>
        /// تصفير محاولات الـ IP واسم المستخدم عند النجاح
        /// </summary>
        public static void ResetLoginAttempts(string ip, string username)
        {
            if (!string.IsNullOrEmpty(ip))
                ResetLoginAttempts("ip_" + ip);

            if (!string.IsNullOrEmpty(username))
                ResetLoginAttempts("usr_" + username);
        }

        /// <summary>
        /// يفحص إذا كان المفتاح محظوراً مؤقتاً بسبب استنفاد المحاولات
        /// </summary>
        public static bool IsLoginThrottled(string key, out int remainingSeconds)
        {
            remainingSeconds = 0;
            if (string.IsNullOrEmpty(key))
                return false;

            AttemptRecord record;
            if (AttemptsTracker.TryGetValue(key, out record))
            {
                lock (record)
                {
                    if (record.BlockedUntil.HasValue)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now < record.BlockedUntil.Value)
                        {
                            remainingSeconds = (int)Math.Ceiling((record.BlockedUntil.Value - now).TotalSeconds);
                            if (remainingSeconds < 1) remainingSeconds = 1;
                            return true;
                        }
                        else
                        {
                            // انتهت فترة الحظر المؤقت — إعادة التهيئة
                            record.BlockedUntil = null;
                            record.FailCount = 0;
                            record.FirstFailAt = now;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// تسجيل محاولة تسجيل دخول فاشلة
        /// </summary>
        public static void RecordFailedLogin(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            DateTime now = DateTime.UtcNow;
            AttemptRecord record = AttemptsTracker.GetOrAdd(key, k => new AttemptRecord
            {
                FailCount = 0,
                FirstFailAt = now,
                BlockedUntil = null
            });

            lock (record)
            {
                // إذا مر أكثر من 3 دقائق على أول فشل دون الوصول للحد الأقصى، نبدأ نافذة جديدة
                if ((now - record.FirstFailAt).TotalMinutes > 3)
                {
                    record.FailCount = 1;
                    record.FirstFailAt = now;
                    record.BlockedUntil = null;
                }
                else
                {
                    record.FailCount++;
                }

                // بعد 10 محاولات فاشلة: تجميد مؤقت لمدة 5 دقائق
                if (record.FailCount >= 10)
                {
                    record.BlockedUntil = now.AddSeconds(300);
                }
                // بعد 5 محاولات فاشلة: تجميد مؤقت لمدة 60 ثانية
                else if (record.FailCount >= 5)
                {
                    record.BlockedUntil = now.AddSeconds(60);
                }
            }
        }

        /// <summary>
        /// تصفير المحاولات عند تسجيل الدخول بنجاح
        /// </summary>
        public static void ResetLoginAttempts(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            AttemptRecord ignored;
            AttemptsTracker.TryRemove(key, out ignored);
        }

        #endregion
    }
}
