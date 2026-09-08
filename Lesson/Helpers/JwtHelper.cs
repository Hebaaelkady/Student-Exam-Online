using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Web.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Lesson.Helpers
{
    /// <summary>
    /// مساعد مركزي لإنشاء والتحقق من JWT Tokens.
    /// يقرأ المفتاح السري من Web.config (appSettings: JwtSecretKey).
    /// </summary>
    public static class JwtHelper
    {
        // يقرأ المفتاح من Web.config — لا يوجد أي مفتاح مكشوف في الكود
        private static byte[] GetKey()
        {
            var secretKey = WebConfigurationManager.AppSettings["JwtSecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("مفتاح JWT غير موجود في Web.config. أضف JwtSecretKey في appSettings.");
            return Encoding.UTF8.GetBytes(secretKey);
        }

        private static string Issuer => "AlazharExams";
        private static string Audience => "AlazharStudents";

        // ─────────────────────────────────────────
        // توليد توكن للطالب
        // ─────────────────────────────────────────
        public static string GenerateStudentToken(
            string userId,
            int examId,
            DateTime examDate,
            TimeSpan timeFrom,
            TimeSpan timeTo,
            string subjectName,
            string sho3baName,
            string mazhabName)
        {
            userId      = userId      ?? string.Empty;
            sho3baName  = sho3baName  ?? string.Empty;
            mazhabName  = mazhabName  ?? string.Empty;
            subjectName = subjectName ?? string.Empty;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("nameid",      userId),
                new Claim("examId",      examId.ToString()),
                new Claim("examDate",    examDate.ToString("yyyy-MM-dd")),
                new Claim("TimeFrom",    timeFrom.ToString()),
                new Claim("TimeTo",      timeTo.ToString()),
                new Claim("SubjectName", subjectName),
            };

            if (!string.IsNullOrEmpty(sho3baName))
                claims.Add(new Claim("Sho3baName", sho3baName));
            if (!string.IsNullOrEmpty(mazhabName))
                claims.Add(new Claim("MazhabName", mazhabName));

            return BuildToken(claims, hours: 5);
        }

        // ─────────────────────────────────────────
        // توليد توكن للمشرف (Admin)
        // ─────────────────────────────────────────
        public static string GenerateAdminToken(string userId, byte groupId)
        {
            userId = userId ?? string.Empty;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("nameid",      userId),
                new Claim("examId", groupId.ToString()),
                new Claim("type",   "admin"),
            };

            return BuildToken(claims, hours: 4);
        }

        // ─────────────────────────────────────────
        // التحقق من التوكن واستخراج الـ Claims
        // يرجع null إذا كان التوكن غير صالح أو منتهي الصلاحية
        // ─────────────────────────────────────────
        public static ClaimsPrincipal ValidateAndGetClaims(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = GetKey();

                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(key),
                    ValidateIssuer           = true,
                    ValidIssuer              = Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero   // لا هامش زمني — دقة كاملة
                };

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParams, out validatedToken);
                return principal;
            }
            catch
            {
                // توكن مزور أو منتهي الصلاحية أو محرَّف
                return null;
            }
        }

        // ─────────────────────────────────────────
        // دالة مساعدة: استخراج قيمة Claim بالاسم
        // ─────────────────────────────────────────
        public static string GetClaim(ClaimsPrincipal principal, string claimType)
        {
            if (principal == null || string.IsNullOrEmpty(claimType))
                return null;

            // إذا كان المطلوب معرف المستخدم، نبحث بكل الأشكال المحتملة
            if (claimType.Equals("nameid", StringComparison.OrdinalIgnoreCase) ||
                claimType == ClaimTypes.NameIdentifier)
            {
                return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst("nameid")?.Value
                    ?? principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst("sub")?.Value;
            }

            return principal.FindFirst(claimType)?.Value
                ?? principal.FindFirst(c => c.Type.EndsWith("/" + claimType, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        // ─────────────────────────────────────────
        // بناء التوكن الداخلي
        // ─────────────────────────────────────────
        private static string BuildToken(IList<Claim> claims, int hours)
        {
            var key           = GetKey();
            var tokenHandler  = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject            = new ClaimsIdentity(claims),
                Expires            = DateTime.UtcNow.AddHours(hours),
                Issuer             = Issuer,
                Audience           = Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
