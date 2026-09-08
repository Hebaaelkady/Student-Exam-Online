# 🎓 بوابة امتحانات أبناؤنا في الخارج - الأزهر الشريف
### Al-Azhar Online Student Exam System

نظام إلكتروني متكامل وآمن مخصص لإجراء وإدارة امتحانات الطلاب المصريين الدارسين بنظام "أبناؤنا في الخارج" تحت إشراف قطاع المعاهد الأزهرية - الأزهر الشريف.

---

## 🌟 نظرة عامة (Overview)

تم تصميم وتطوير هذا النظام لتمكين طلاب الأزهر الشريف في مختلف دول العالم من أداء امتحاناتهم الفصلية والسنوية أونلاين بكل يسر وسهولة، مع توفير أعلى معايير الأمان والسرية والموثوقية لنصوص الأسئلة والإجابات، إلى جانب لوحة تحكم وإدارة مخصصة للمشرفين لمتابعة سير الامتحانات وإصدار التقارير اللحظية.

---

## ✨ المميزات الرئيسية (Key Features)

### 1. 📱 تجربة مستخدم عصرية ومتجاوبة بالكامل (Responsive UI/UX)
- تصميم متطور وعصري متوافق تماماً مع **الهواتف الذكية (Mobile)**، الأجهزة اللوحية (Tablets)، والشاشات المكتبية.
- واجهة تصفح مريحة للعين بألوان وهوية الأزهر الشريف الرسمية.
- قائمة علوية (Header) ثابتة وواضحة تعرض بيانات الطالب، المادة، والوقت المتبقي بدقة.
- خريطة تنقل تفاعلية بين الأسئلة تتيح للطالب معرفة الأسئلة المتبقية والمجابة فوراً.

### 2. 🛡️ أمان وحماية متقدمة (Security Hardening)
- **حماية من هجمات XSS:** تعقيم وتطهير فوري لكافة نصوص الأسئلة والخيارات المدخلة باستخدام `SecurityHelper.SanitizeHtml` لمنع حقن أي كود برمجي خبيث مع الحفاظ على التنسيقات والجداول والصور.
- **حماية متقدمة ضد التخمين (Dual-Track Rate Limiter):** نظام ذكي لمراقبة محاولات الدخول الخاطئة على مستوى (IP & Username). في حال تكرار 5 محاولات خاطئة، يتم تفعيل تجميد مؤقت لمدة 60 ثانية مع عداد تنازلي تفاعلي حي دون قفل حساب الطالب نهائياً لتفادي تعطيل الطلاب.
- **تأمين الصلاحيات (Authorization & IDOR Protection):** تأمين كامل لجميع دوال وتقارير وبيانات الطلاب في `StudentReportController` والتحقق الصارم من صلاحيات المشرفين.
- **توكن مشفر (Encrypted JWT):** إدارة جلسات آمنة للمشرفين والطلاب باستخدام JSON Web Tokens المشفرة وتخزينها في كوكيز آمنة (`HttpOnly` و `Secure`).
- **حماية ضد CSRF:** تطبيق `[ValidateAntiForgeryToken]` على كافة الطلبات والنماذج البرمجية واستدعاءات AJAX.
- **تطبيع الأرقام (Digit Normalization):** دعم تلقائي لتحويل الأرقام العربية المشرقية (`٠١٢٣`) إلى أرقام إنجليزية (`0123`) لتفادي أخطاء الإدخال من الهواتف الذكية.

### 3. ⚡ أداء واستقرار عالي
- حفظ تلقائي وفوري لكل إجابة يختارها الطالب دون الحاجة لإعادة تحميل الصفحة.
- مؤقت زمني دقيق يعمل بالتزامن مع توقيت السيرفر الرسمي ويقوم بإغلاق وتسليم الامتحان تلقائياً عند انتهاء الوقت المحدد.
- عزل ملفات التكوين الحساسة وكلمات مرور قواعد البيانات عن المستودع البرمجي (`ConnectionStrings.config` و `AppSettings.secrets.config`).

---

## 🛠️ التقنيات المستخدمة (Tech Stack)

| المجال | التقنية / المكتبة |
| :--- | :--- |
| **Backend Framework** | ASP.NET MVC 5 (.NET Framework 4.8) |
| **Database & ORM** | Microsoft SQL Server + Entity Framework 6 |
| **Authentication & Security** | ASP.NET Identity, JWT (Jose-JWT), Anti-Forgery Tokens |
| **Client-Server Communication** | Asynchronous AJAX (HTTP POST) & JSON API |
| **Frontend UI** | HTML5, CSS3 (Modern Flexbox/Grid), JavaScript (ES6), Bootstrap |
| **Typography & Icons** | Droid Arabic Kufi & Modern Web Fonts |

---

## 🚀 دليل التثبيت والتشغيل المحلي (Setup Guide)

### المتطلبات الأساسية (Prerequisites):
- **Visual Studio 2019** أو **Visual Studio 2022** مع حزمة `.NET desktop development` و `ASP.NET and web development`.
- **Microsoft SQL Server 2016** أو أحدث.
- **IIS Express** أو خادم **IIS** محلي.

### خطوات التشغيل:

1. **استنساخ المستودع (Clone Repository):**
   ```bash
   git clone https://github.com/Hebaaelkady/Student-Exam-Online.git
   cd Student-Exam-Online
   ```

2. **إعداد ملفات الاتصال والمفاتيح السرية:**
   قم بنسخ الملفات الاسترشادية وتعديلها ببيانات قاعدة بياناتك ومفاتيحك الخاصة:
   - قم بإنشاء ملف `Lesson/ConnectionStrings.config` مسترشداً بـ `ConnectionStrings.config.example`:
     ```xml
     <connectionStrings>
         <add name="DefaultConnection"
              connectionString="Data Source=YOUR_SERVER;User ID=YOUR_USER;Password=YOUR_PASSWORD;initial catalog=StudentExamOutside"
              providerName="System.Data.SqlClient" />
         <add name="LessonsEntities"
              connectionString="metadata=res://*/Models.Lessons.csdl|res://*/Models.Lessons.ssdl|res://*/Models.Lessons.msl;provider=System.Data.SqlClient;provider connection string=&quot;data source=YOUR_SERVER;User ID=YOUR_USER;Password=YOUR_PASSWORD;initial catalog=StudentExamOutside;App=EntityFramework&quot;"
              providerName="System.Data.EntityClient" />
     </connectionStrings>
     ```
   - قم بإنشاء ملف `Lesson/AppSettings.secrets.config` مسترشداً بـ `AppSettings.secrets.config.example`:
     ```xml
     <appSettings>
         <add key="JwtSecretKey" value="YOUR_SECURE_JWT_SECRET_KEY_AT_LEAST_32_CHARACTERS" />
         <add key="AdminUserNames" value="admin1,admin2" />
         <add key="EncryptionMasterKey" value="YOUR_AES_256_BASE64_ENCRYPTION_KEY" />
     </appSettings>
     ```

3. **استعادة حزم NuGet (Restore NuGet Packages):**
   افتح المشروع في Visual Studio واضغط بالزر الأيمن على الـ Solution ثم اختر **Restore NuGet Packages**.

4. **البناء والتشغيل (Build & Run):**
   اضغط `Ctrl + Shift + B` لبناء المشروع، ثم `F5` لتشغيل الموقع في المتصفح.

---

## 🔒 الأمان والخصوصية (Security & Privacy)

- لا يحتوي هذا المستودع على أي بيانات اعتماد أو كلمات مرور أو سجلات طلاب فعلية.
- كافة بيانات الاتصال ومفاتيح التشفير تُدار عبر ملفات تكوين محلية مستثناة بالكامل عبر `.gitignore`.

---

## 📄 حقوق الملكية (Copyright)

جميع الحقوق محفوظة © **الأزهر الشريف** - قطاع المعاهد الأزهرية.
الإشراف والتطوير: م. هبة القاضي.
