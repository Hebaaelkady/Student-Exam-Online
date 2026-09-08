# 🎓 Al-Azhar Online Exam System  
### For Egyptian Students Abroad  

An integrated online exam system designed for Egyptian students studying abroad under the supervision of Al-Azhar Al-Sharif.  

---

## 📖 Overview  

This system allows Al-Azhar students worldwide to take their semester and annual exams online easily and securely. It provides a safe environment for managing exam questions and answers, along with a dedicated admin dashboard for monitoring exams and generating reports.  

---

## ✨ Key Features  

### 1. 📱 Fully Responsive & Modern UI  
- Works perfectly on **smartphones**, tablets, and desktops.  
- Easy-to-use interface with a visual progress tracker showing answered and pending questions.  

### 2. 🔒 Security Features  

Simple but strong security to keep user data and exam content safe:  

- **XSS Protection** – All user inputs (questions, choices, etc.) are automatically cleaned using `SecurityHelper.SanitizeHtml` to prevent malicious code injection while preserving formatting, tables, and images.  
- **Login Rate Limiting** – After 5 failed login attempts, the account is temporarily locked for 60 seconds with a live countdown timer—without permanently blocking students.  
- **Access Control** – Strict authorization checks on `StudentReportController` ensure only admins can access student reports and sensitive data.  
- **Secure Sessions** – Encrypted JWT tokens are stored in `HttpOnly` and `Secure` cookies for safe session management.  
- **CSRF Protection** – All forms and AJAX requests are protected using `[ValidateAntiForgeryToken]`.  
- **Smart Number Input** – Automatically converts Arabic numerals (٠١٢٣) to English numerals (0123) to prevent input errors.  

### 3. ⚡ High Performance & Reliability  
- **Auto-save** – Each answer is saved instantly without page reload.  
- **Accurate Timer** – Syncs with server time and auto-submits the exam when time runs out.  
- **Secure Configuration** – Sensitive files like `ConnectionStrings.config` and `AppSettings.secrets.config` are excluded from the repository.  

---

## 🛠️ Tech Stack  

| Area | Technology / Library |
| :--- | :--- |
| **Backend** | ASP.NET MVC 5 (.NET Framework 4.8) |
| **Database** | Microsoft SQL Server + Entity Framework 6 |
| **Auth & Security** | ASP.NET Identity, JWT (Jose-JWT), CSRF Tokens |
| **API & Communication** | AJAX (HTTP POST) + JSON |
| **Frontend** | HTML5, CSS3 (Flexbox/Grid), JavaScript (ES6), Bootstrap |
| **Fonts & Icons** | Droid Arabic Kufi + Modern Web Fonts |

---

## 📄 Copyright  

Developed & Designed by: **Eng. Heba El-Kady**  
All rights reserved © 2026