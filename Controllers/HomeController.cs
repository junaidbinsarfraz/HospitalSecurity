using HospitalManagament.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using reCAPTCHA.MVC;
using System.Text.RegularExpressions;
using System.Web.Helpers;
using Twilio;
using Twilio.Clients;
using Twilio.Types;
using Twilio.Rest.Api.V2010.Account;
using System.Net.Mail;

//captcha https://stackoverflow.com/questions/4611122/how-to-implement-recaptcha-for-asp-net-mvc

namespace HospitalManagament.Controllers
{
    public class HomeController : Controller
    {
        int verificationCode;
        double verificationCodedExpirationSeconds = 120;
        int maxLoginAttempt = 3;

        // Show dashboard of admin
        public ActionResult Index()
        {
            if (HttpContext.Session["LoggedInUser"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            else
            {
                HospitalManagementContext db = new HospitalManagementContext();

                HttpContext.Session["TotalPatientList"] = db.Users.Include(u => u.Patient).Where(u => u.Patient != null).Where(u => u.Patient.Status == "Admitted").ToList();
                HttpContext.Session["TotalPatients"] = db.Users.Count(u => u.Patient != null && u.Patient.Status == "Admitted");
                HttpContext.Session["TotalCaregiverList"] = db.Users.Include(u => u.Caregiver).Where(u => u.Caregiver != null).ToList();
                HttpContext.Session["TotalCareGivers"] = db.Users.Count(u => u.Caregiver != null);
                HttpContext.Session["TotalDoctorList"] = db.Users.Include(u => u.Doctor).Where(u => u.Doctor != null).ToList();
                HttpContext.Session["TotalDoctors"] = db.Users.Count(u => u.Doctor != null);

                return View();
            }
        }

        // Show security dashboard of admin
        public ActionResult Security()
        {
            if (HttpContext.Session["LoggedInUser"] == null)
            {
                return RedirectToAction("Login", "Home");
            }
            else
            {
                HospitalManagementContext db = new HospitalManagementContext();

                HttpContext.Session["RecetlyRegisteredUserList"] = db.Users.OrderByDescending(u => u.Id).Take(10).ToList();
                HttpContext.Session["TotalLoginUsers"] = db.Users.Count(u => u.IsLogin != null && u.IsLogin == true);
                HttpContext.Session["TotalLogoutUsers"] = db.Users.Count(u => u.IsLogin == null || u.IsLogin == false);
                HttpContext.Session["TotalInactive"] = db.Users.Count(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1);
                HttpContext.Session["InactiveUserList"] = db.Users.Where(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1).ToList();
                HttpContext.Session["FailLoginAttemptList"] = db.LoginAttempts.Where(l => l.IsPassed == false).ToList();
                HttpContext.Session["AllUserList"] = db.Users.ToList();

                return View();
            }
        }

        // Show success resigtration page
        [HttpGet]
        public ActionResult Success()
        {
            if (HttpContext.Session["LoggedInUser"] != null)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return View();
            }
        }

        // Show verify
        [HttpGet]
        public ActionResult Verify()
        {
            if (HttpContext.Session["LoggedInUser"] != null)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return View();
            }
        }

        // Do verify
        [HttpPost]
        public ActionResult Verify(LoginModel loginModel)
        {
            if (ModelState.IsValid)
            {

                LoginModel loginModelOld = (LoginModel)HttpContext.Session["LoginModel"];

                if (loginModel.VerificationCode != loginModelOld.VerificationCode)
                {
                    ModelState.AddModelError("", "Code not matched");
                    return View();
                }

                //stupid otp expiration

                if (HttpContext.Session["VerificationCodeExpiration"] != null
                    && (DateTime.Now - (DateTime)HttpContext.Session["VerificationCodeExpiration"]).TotalSeconds > this.verificationCodedExpirationSeconds)
                {
                    ModelState.AddModelError("", "Code expired");
                    return View();
                }

                HttpContext.Session["VerificationCodeExpiration"] = null;

                loginModel = loginModelOld;

                HospitalManagementContext dataContext = new HospitalManagementContext();

                // Check credentials
                User user = dataContext.Users.FirstOrDefault(u => u.Email == loginModel.Email);

                user.IsLogin = true;
                user.LastLogin = DateTime.Now;

                dataContext.SaveChanges();

                LoginAttempt loginAttempt = new LoginAttempt();

                loginAttempt.AttemptDateTime = DateTime.Now;
                loginAttempt.Email = loginModel.Email;
                loginAttempt.IsPassed = true;
                loginAttempt.IpAddress = Request.UserHostAddress;

                dataContext.LoginAttempts.Add(loginAttempt);

                dataContext.SaveChanges();

                if (user != null)
                {
                    HttpContext.Session["LoginModel"] = null;
                    HttpContext.Session["LoggedInUser"] = user;
                    // Check if admin
                    if (user.Role.Name == "Admin")
                    {
                        HttpContext.Session["Role"] = "Admin";
                        HttpContext.Session["TotalPatientList"] = dataContext.Users.Where(u => u.Patient != null).Where(u => u.Patient.Status == "Admitted").ToList();
                        HttpContext.Session["TotalPatients"] = dataContext.Users.Count(u => u.Patient != null && u.Patient.Status == "Admitted");
                        HttpContext.Session["TotalCaregiverList"] = dataContext.Users.Where(u => u.Caregiver != null).ToList();
                        HttpContext.Session["TotalCareGivers"] = dataContext.Users.Count(u => u.Caregiver != null);
                        HttpContext.Session["TotalDoctorList"] = dataContext.Users.Where(u => u.Doctor != null).ToList();
                        HttpContext.Session["TotalDoctors"] = dataContext.Users.Count(u => u.Doctor != null);
                        HttpContext.Session["RecetlyRegisteredUserList"] = dataContext.Users.OrderByDescending(u => u.Id).Take(10).ToList();
                        HttpContext.Session["TotalLoginUsers"] = dataContext.Users.Count(u => u.IsLogin != null && u.IsLogin == true);
                        HttpContext.Session["TotalLogoutUsers"] = dataContext.Users.Count(u => u.IsLogin == null || u.IsLogin == false);
                        HttpContext.Session["TotalInactive"] = dataContext.Users.Count(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1);

                        return RedirectToAction("Index", "Home");
                    }

                    else if (user.Role.Name == "Patient")
                    {
                        HttpContext.Session["Patient"] = user.Patient;
                        HttpContext.Session["PatientId"] = user.Patient.Id;
                        HttpContext.Session["Doctor"] = null;
                        HttpContext.Session["DoctorId"] = -1;
                        HttpContext.Session["Role"] = "Patient";
                        return RedirectToAction("Index", "Patient");
                    }

                    else if (user.Role.Name == "Caregiver")
                    {
                        HttpContext.Session["Role"] = "Caregiver";
                        return RedirectToAction("Index", "CareGiver");
                    }

                    else if (user.Role.Name == "Doctor")
                    {
                        HttpContext.Session["Patient"] = null;
                        HttpContext.Session["PatientId"] = -1;
                        HttpContext.Session["Doctor"] = user.Doctor;
                        HttpContext.Session["DoctorId"] = user.Doctor.Id;
                        HttpContext.Session["Role"] = "Doctor";
                        return RedirectToAction("Index", "Doctor");
                    }
                }
            }

            return View();
        }

        // Show login page if user is not loggedin
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // Do login for a user and redirect to specific page w.r.t. user role
        [HttpPost]
        public ActionResult Login(LoginModel loginModel)
        {
            if (ModelState.IsValid)
            {
                HospitalManagementContext dataContext = new HospitalManagementContext();

                User user = dataContext.Users.FirstOrDefault(u => u.Email == loginModel.Email);

                if (user != null)
                {
                    if (user.IsEnabled != null && user.IsEnabled.Value == false)
                    {
                        ModelState.AddModelError("", "Your account is disabled");
                        return View();
                    }

                    string salt = "";

                    if (user.Salt != null)
                    {
                        salt = user.Salt;
                    }

                    string sha256 = Crypto.SHA256(loginModel.Password + salt);

                    // Check credentials
                    user = dataContext.Users.FirstOrDefault(u => u.Email == loginModel.Email && u.Password == sha256);

                    if (user != null)
                    {
                        // Perform 2 factor authentication
                        //if (user.ContactNo != null || user.ContactNo != "")
                        //{
                        //    string messageId = this.SendSms(user.ContactNo);

                        //    if (messageId != null)
                        //    {
                        //        loginModel.VerificationCode = this.verificationCode;
                        //        HttpContext.Session["LoginModel"] = loginModel;
                        //        HttpContext.Session["VerificationCodeExpiration"] = DateTime.Now;
                        //        return RedirectToAction("Verify", "Home");
                        //    }
                        //}

                        user.IsLogin = true;
                        user.LastLogin = DateTime.Now;

                        dataContext.SaveChanges();

                        LoginAttempt loginAttempt = new LoginAttempt();

                        loginAttempt.AttemptDateTime = DateTime.Now;
                        loginAttempt.Email = loginModel.Email;
                        loginAttempt.IsPassed = true;
                        loginAttempt.IpAddress = Request.UserHostAddress;

                        dataContext.LoginAttempts.Add(loginAttempt);

                        dataContext.SaveChanges();

                        HttpContext.Session["LoggedInUser"] = user;
                        // Check if admin
                        if (user.Role.Name == "Admin")
                        {
                            HttpContext.Session["Role"] = "Admin";
                            HttpContext.Session["TotalPatientList"] = dataContext.Users.Where(u => u.Patient != null).Where(u => u.Patient.Status == "Admitted").ToList();
                            HttpContext.Session["TotalPatients"] = dataContext.Users.Count(u => u.Patient != null && u.Patient.Status == "Admitted");
                            HttpContext.Session["TotalCaregiverList"] = dataContext.Users.Where(u => u.Caregiver != null).ToList();
                            HttpContext.Session["TotalCareGivers"] = dataContext.Users.Count(u => u.Caregiver != null);
                            HttpContext.Session["TotalDoctorList"] = dataContext.Users.Where(u => u.Doctor != null).ToList();
                            HttpContext.Session["TotalDoctors"] = dataContext.Users.Count(u => u.Doctor != null);
                            HttpContext.Session["RecetlyRegisteredUserList"] = dataContext.Users.OrderByDescending(u => u.Id).Take(10).ToList();
                            HttpContext.Session["TotalLoginUsers"] = dataContext.Users.Count(u => u.IsLogin != null && u.IsLogin == true);
                            HttpContext.Session["TotalLogoutUsers"] = dataContext.Users.Count(u => u.IsLogin == null || u.IsLogin == false);
                            HttpContext.Session["TotalInactive"] = dataContext.Users.Count(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1);

                            return RedirectToAction("Index", "Home");
                        }

                        else if (user.Role.Name == "Patient")
                        {
                            HttpContext.Session["Patient"] = user.Patient;
                            HttpContext.Session["PatientId"] = user.Patient.Id;
                            HttpContext.Session["Doctor"] = null;
                            HttpContext.Session["DoctorId"] = -1;
                            HttpContext.Session["Role"] = "Patient";
                            return RedirectToAction("Index", "Patient");
                        }

                        else if (user.Role.Name == "Caregiver")
                        {
                            HttpContext.Session["Role"] = "Caregiver";
                            return RedirectToAction("Index", "CareGiver");
                        }

                        else if (user.Role.Name == "Doctor")
                        {
                            HttpContext.Session["Patient"] = null;
                            HttpContext.Session["PatientId"] = -1;
                            HttpContext.Session["Doctor"] = user.Doctor;
                            HttpContext.Session["DoctorId"] = user.Doctor.Id;
                            HttpContext.Session["Role"] = "Doctor";
                            return RedirectToAction("Index", "Doctor");
                        }
                    }
                    // Invalid credentials
                    else
                    {
                        user = dataContext.Users.FirstOrDefault(u => u.Email == loginModel.Email);
                        // Log login attempt
                        int loginAttemptCount = user.LoginAttemptCount == null ? 0 : user.LoginAttemptCount.Value;

                        if (loginAttemptCount >= maxLoginAttempt)
                        {
                            // Disable the account

                            user.IsEnabled = false;
                            user.EnableToken = GenerateRandomString(8);

                            var resetLink = "<a href='" + Url.Action("EnableAccount", "Home", new { email = user.Email, token = user.EnableToken }, "http") + "'>Enable Account Now</a>";
                            string body = "Someone try to login to your account. We disabled your account.<br/><br/><b>You can enable your account by clicking below link</b><br/>" + resetLink;

                            try
                            {
                                SendEMail(user.Email, "Invalid Login Attempt", body);
                            }
                            catch (Exception e)
                            {
                                // Do what you want
                            }
                        }

                        user.LoginAttemptCount = loginAttemptCount + 1;

                        dataContext.SaveChanges();

                        // Add database entry
                        LoginAttempt loginAttempt = new LoginAttempt();

                        loginAttempt.AttemptDateTime = DateTime.Now;
                        loginAttempt.Email = loginModel.Email;
                        loginAttempt.Password = loginModel.Password;
                        loginAttempt.IsPassed = false;
                        loginAttempt.IpAddress = Request.UserHostAddress;

                        dataContext.LoginAttempts.Add(loginAttempt);

                        dataContext.SaveChanges();

                        ModelState.AddModelError("", "Invalid username or password");
                    }
                }
                // Invalid credentials
                else
                {
                    // Add database entry
                    LoginAttempt loginAttempt = new LoginAttempt();

                    loginAttempt.AttemptDateTime = DateTime.Now;
                    loginAttempt.Email = loginModel.Email;
                    loginAttempt.Password = loginModel.Password;
                    loginAttempt.IsPassed = false;
                    loginAttempt.IpAddress = Request.UserHostAddress;

                    dataContext.LoginAttempts.Add(loginAttempt);

                    dataContext.SaveChanges();

                    ModelState.AddModelError("", "Invalid username or password");
                }
            }

            return View();
        }

        public ActionResult EnableAccount(string email, string token)
        {
            // Check token validity
            HospitalManagementContext dataContext = new HospitalManagementContext();

            User user = dataContext.Users.FirstOrDefault(u => u.Email == email && u.EnableToken == token && u.IsEnabled == false);

            if (user != null)
            {
                user.EnableToken = null;
                user.IsEnabled = true;
                user.LoginAttemptCount = 0;

                dataContext.SaveChanges();

                HttpContext.Session["email"] = email;
            }
            else
            {
                // Invalid token
                return RedirectToAction("InvalidToken", "Home");
            }

            return RedirectToAction("Login", "Home");
        }

        [HttpGet]
        public ActionResult ResetPassword(string email, string token)
        {
            // Check token validity
            HospitalManagementContext dataContext = new HospitalManagementContext();

            User user = dataContext.Users.FirstOrDefault(u => u.Email == email && u.Token == token);

            if (user != null)
            {
                HttpContext.Session["email"] = email;
            }
            else
            {
                // Invalid token
                return RedirectToAction("InvalidToken", "Home");
            }

            return View();
        }

        // Reset password
        [HttpPost]
        public ActionResult ResetPassword(SignupModel signupModel)
        {
            if (ModelState.IsValid)
            {
                // Password strength
                string regix = "^(?=.*[A-Z].*[A-Z])(?=.*[!@#$&*])(?=.*[0-9].*[0-9])(?=.*[a-z].*[a-z].*[a-z]).{8}$";

                if (!Regex.IsMatch(signupModel.Password, regix))
                {
                    ModelState.AddModelError("Password", "Password should contain 2 uppercases, 1 special case, 2 digits and 3 lowercases.");
                    return View();
                }

                // Check if user already exists
                HospitalManagementContext dataContext = new HospitalManagementContext();
                // Check credentials

                string email = (string)HttpContext.Session["email"];

                User user = dataContext.Users.FirstOrDefault(u => u.Email == email);

                if (user != null)
                {
                    if (signupModel.OTP != user.OTP)
                    {
                        ModelState.AddModelError("OTP", "OTP not matched");
                        return View();
                    }

                    if ((DateTime.Now - user.OTPExpiration.Value).TotalSeconds > this.verificationCodedExpirationSeconds)
                    {
                        ModelState.AddModelError("OTP", "OTP is expired");
                        return View();
                    }

                    // Save token in database
                    user.Token = null;
                    user.OTP = null;

                    user.Salt = GenerateRandomString(10);
                    user.Password = Crypto.SHA256(signupModel.Password + user.Salt);

                    dataContext.SaveChanges();

                    HttpContext.Session["email"] = null;

                    return RedirectToAction("Login", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Email does not exists");
                }
            }

            return View();
        }

        [HttpGet]
        public ActionResult InvalidToken()
        {
            return View();
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // Forgot password
        [HttpPost]
        public ActionResult ForgotPassword(LoginModel loginModel)
        {
            if (ModelState.IsValid)
            {
                // Check if user already exists
                HospitalManagementContext dataContext = new HospitalManagementContext();

                // Check credentials
                string email = loginModel.Email;

                User user = dataContext.Users.FirstOrDefault(u => u.Email == email);

                if (user != null)
                {
                    var token = Guid.NewGuid().ToString();

                    string sRandomOTP = GenerateRandomString(8);

                    var resetLink = "<a href='" + Url.Action("ResetPassword", "Home", new { email = email, token = token }, "http") + "'>Reset Password</a>";

                    // Send email
                    string subject = "Password Reset";
                    string body = "<b>Please find the Password Reset Token</b><br/>" + resetLink + "<br/><br/>OTP:<br/>" + sRandomOTP; //edit it
                    try
                    {
                        SendEMail(email, subject, body);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("Fail", "Unable to send email");
                        return View();
                    }

                    // Save otp token in database
                    user.Token = token;
                    user.OTP = sRandomOTP;
                    user.OTPExpiration = DateTime.Now;

                    dataContext.SaveChanges();

                    ModelState.AddModelError("Success", "Email Sent");
                }
                else
                {
                    ModelState.AddModelError("Fail", "Email does not exists");
                }
            }

            return View();
        }

        public static string GenerateRandomString(int length)
        {
            string alphabets = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string small_alphabets = "abcdefghijklmnopqrstuvwxyz";
            string numbers = "1234567890";

            string characters = alphabets + small_alphabets + numbers;

            string otp = string.Empty;

            for (int i = 0; i < length; i++)
            {
                string character = string.Empty;
                do
                {
                    int index = new Random().Next(0, characters.Length);
                    character = characters.ToCharArray()[index].ToString();
                } while (otp.IndexOf(character) != -1);
                otp += character;
            }
            return otp;
        }

        private void SendEMail(string emailid, string subject, string body)
        {
            System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient();
            client.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
            client.EnableSsl = true;
            client.Host = "smtp.gmail.com";
            client.Port = 587;

            string MailAccount = System.Configuration.ConfigurationManager.AppSettings["MailAccount"];
            string MailPassword = System.Configuration.ConfigurationManager.AppSettings["MailPassword"];

            System.Net.NetworkCredential credentials = new System.Net.NetworkCredential(MailAccount, MailPassword);
            client.UseDefaultCredentials = false;
            client.Credentials = credentials;

            System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();
            msg.From = new MailAddress(MailAccount);
            msg.To.Add(new MailAddress(emailid));

            msg.Subject = subject;
            msg.IsBodyHtml = true;
            msg.Body = body;

            client.Send(msg);
        }

        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        // Do login for a user and redirect to specific page w.r.t. user role
        [HttpPost]
        [CaptchaValidator]
        public ActionResult Register(SignupModel signupModel, bool captchaValid)
        {
            if (ModelState.IsValid)
            {
                // Do signup
                // 2 uppercases, 1 special case, 2 digits, 3 lower case and length 8
                string regex = "^(?=.*?[A-Z].*?[A-Z])(?=.*[!@#$&*])(?=.*?[0-9].*?[0-9])(?=.*?[a-z].*?[a-z].*?[a-z]).{8,}$";
                //string regex = "^(?=.*?[0-9].*?[0-9])(?=.*[!@#$%])[0-9a-zA-Z!@#$%0-9]{8,}$";

                if (!Regex.IsMatch(signupModel.Password, regex))
                {
                    ModelState.AddModelError("Password", "Password should contain 2 uppercases, 1 special case, 2 digits and 3 lowercases.");
                    return View();
                }

                HospitalManagementContext dataContext = new HospitalManagementContext();
                // Check credentials
                User user = dataContext.Users.FirstOrDefault(u => u.Email == signupModel.Email);

                if (user != null)
                {
                    ModelState.AddModelError("Email", "User already exists");
                    return View();
                }

                user = new User();

                user.ContactNo = signupModel.ContactNo;
                user.Salt = GenerateRandomString(10);
                user.Password = Crypto.SHA256(signupModel.Password + user.Salt);
                user.Email = signupModel.Email;
                user.IsEnabled = true;
                user.LoginAttemptCount = 0;

                // add role as patient 
                if (signupModel.signupAs == SignupAs.Patient)
                {
                    user.Role = dataContext.Roles.ToList().Where(u => u.Name == "Patient").FirstOrDefault();
                    // Entry date and time
                    user.Patient = new Patient();

                    user.Patient.EntryDate = DateTime.Now;
                    user.Patient.EntryTime = DateTime.Now.TimeOfDay;
                    user.Patient.Status = "Admitted";
                }
                else if (signupModel.signupAs == SignupAs.Doctor)
                {
                    user.Doctor = new Doctor();
                    user.Role = dataContext.Roles.ToList().Where(u => u.Name == "Doctor").FirstOrDefault();
                }
                else
                {
                    user.Caregiver = new Caregiver();
                    user.Role = dataContext.Roles.ToList().Where(u => u.Name == "Caregiver").FirstOrDefault();
                }

                dataContext.Users.Add(user);

                dataContext.SaveChanges();

                return RedirectToAction("Success", "Home");

            }
            if (!captchaValid)
            {
                Console.WriteLine("Invalid recaptcha");
            }
            return View();
        }

        public string SendSms(string toNumber)
        {
            var accountSid = System.Configuration.ConfigurationManager.AppSettings["SMSAccountIdentification"]; // Your Account SID from twilio.com/console
            var authToken = System.Configuration.ConfigurationManager.AppSettings["SMSAccountPassword"];   // Your Auth Token from twilio.com/console
            try
            {

                TwilioClient.Init(accountSid, authToken);
                var restClient = new TwilioRestClient(accountSid, authToken);

                verificationCode = GenerateRandomNo();

                string body = "Your verification code is " + verificationCode + ". It expires in 2 minutes.";

                // Get from UI
                //var to = new PhoneNumber("+15017250604");
                var to = new PhoneNumber(toNumber);
                var message = MessageResource.Create(
                    to,
                    from: new PhoneNumber(System.Configuration.ConfigurationManager.AppSettings["SMSAccountFrom"]),
                    body: body);

                Console.WriteLine(message.Sid);
                return message.Sid;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private int GenerateRandomNo()
        {
            int _min = 1000;
            int _max = 9999;
            Random _rdm = new Random();
            return _rdm.Next(_min, _max);
        }

        // Do logout
        [HttpGet]
        public ActionResult LogOut(bool ajax)
        {
            HospitalManagementContext dataContext = new HospitalManagementContext();

            User loginUser = (User)HttpContext.Session["LoggedInUser"];

            User user = dataContext.Users.FirstOrDefault(u => u.Id == loginUser.Id);
            
            user.IsLogin = false;

            dataContext.SaveChanges();

            HttpContext.Session["LoggedInUser"] = null;
            HttpContext.Session["Role"] = null;
            HttpContext.Session["TotalCareGivers"] = null;
            HttpContext.Session["TotalPatients"] = null;
            HttpContext.Session["TotalDoctors"] = null;
            HttpContext.Session["TotalPatientList"] = null;
            HttpContext.Session["TotalDoctorList"] = null;
            HttpContext.Session["TotalCaregiverList"] = null;
            HttpContext.Session["RecetlyRegisteredUserList"] = null;

            if (!ajax)
            {
                return RedirectToAction("Login", "Home");
            }
            else
            {
                return Json(new
                {
                    Status = "logout"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // Fetch data for Filled line chart
        [HttpGet]
        public ActionResult CountGenderPerMonthFilledLine()
        {
            HospitalManagementContext dataContext = new HospitalManagementContext();

            List<CountGendersPerMonth> GenderMonth = dataContext.Database.SqlQuery<CountGendersPerMonth>("Select u.Gender, p.EntryDate ActualDate, datename(month, DATEPART(MONTH, p.EntryDate)) month, DATEPART(MONTH, p.EntryDate) monthnumber, COUNT(p.User_Id) count from [HospitalManagement].[dbo].[Patients] p,[HospitalManagement].[dbo].[Users] u where u.Id = p.User_Id and p.Status = 'Admitted' group by DATEPART(MONTH, p.EntryDate), p.EntryDate, u.Gender").ToList();

            // Create months list
            var labels = new List<string>() { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

            var MaleCount = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            var FemaleCount = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            for (int i = 0; i < GenderMonth.Count; i++)
            {
                if (GenderMonth[i].Gender == "Male")
                {
                    MaleCount[GenderMonth[i].MonthNumber - 1] = GenderMonth[i].Count;
                }
                else
                {
                    FemaleCount[GenderMonth[i].MonthNumber - 1] = GenderMonth[i].Count;
                }
            }

            return Json(new
            {
                labels = labels,
                MaleCount = MaleCount,
                FemaleCount = FemaleCount
            }, JsonRequestBehavior.AllowGet);
        }

        // Fetch data for hollow line chart
        [HttpGet]
        public ActionResult CountGenderPerMonthHollow()
        {
            HospitalManagementContext dataContext = new HospitalManagementContext();

            List<CountGendersPerMonth> GenderMonthFinal = new List<CountGendersPerMonth>()
            {
                new CountGendersPerMonth(0 , 0, "January"),
                new CountGendersPerMonth(0 , 0, "February"),
                new CountGendersPerMonth(0 , 0, "March"),
                new CountGendersPerMonth(0 , 0, "April"),
                new CountGendersPerMonth(0 , 0, "May"),
                new CountGendersPerMonth(0 , 0, "June"),
                new CountGendersPerMonth(0 , 0, "July"),
                new CountGendersPerMonth(0 , 0, "August"),
                new CountGendersPerMonth(0 , 0, "September"),
                new CountGendersPerMonth(0 , 0, "October"),
                new CountGendersPerMonth(0 , 0, "November"),
                new CountGendersPerMonth(0 , 0, "December")
            };

            List<CountGendersPerMonth> GenderMonth = dataContext.Database.SqlQuery<CountGendersPerMonth>("select datename(month, p.EntryDate) Month, count(case when u.Gender = 'Female' then 1 end) FemaleCount, count(case when u.Gender = 'Male' then 1 end) MaleCount from [HospitalManagement].[dbo].[Patients] p left join [HospitalManagement].[dbo].[Users] u on p.User_Id = u.Id where p.Status = 'Admitted' group by datename(month, p.EntryDate);").ToList();

            for (int i = 0; i < GenderMonthFinal.Count; i++)
            {
                for (int j = 0; j < GenderMonth.Count; j++)
                {
                    if (GenderMonth[j].Month == GenderMonthFinal[i].Month)
                    {
                        GenderMonthFinal[i].MaleCount = GenderMonth[j].MaleCount;
                        GenderMonthFinal[i].FemaleCount = GenderMonth[j].FemaleCount;
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(GenderMonthFinal), "application/json");
        }

        // Fetch data for hollow line chart
        [HttpGet]
        public ActionResult CountLoginAttemptsPerMonthHollow()
        {
            HospitalManagementContext dataContext = new HospitalManagementContext();

            List<CountLoginAttemptPerMonth> countLoginAttemptPerMonthFinal = new List<CountLoginAttemptPerMonth>()
            {
                new CountLoginAttemptPerMonth(0 , 0, "January"),
                new CountLoginAttemptPerMonth(0 , 0, "February"),
                new CountLoginAttemptPerMonth(0 , 0, "March"),
                new CountLoginAttemptPerMonth(0 , 0, "April"),
                new CountLoginAttemptPerMonth(0 , 0, "May"),
                new CountLoginAttemptPerMonth(0 , 0, "June"),
                new CountLoginAttemptPerMonth(0 , 0, "July"),
                new CountLoginAttemptPerMonth(0 , 0, "August"),
                new CountLoginAttemptPerMonth(0 , 0, "September"),
                new CountLoginAttemptPerMonth(0 , 0, "October"),
                new CountLoginAttemptPerMonth(0 , 0, "November"),
                new CountLoginAttemptPerMonth(0 , 0, "December")
            };

            List<CountLoginAttemptPerMonth> countLoginAttemptPerMonth = dataContext.Database.SqlQuery<CountLoginAttemptPerMonth>("select DATENAME(month, DateAdd( month , Month(l.AttemptDateTime) , 0 ) - 1) Month, count(case when l.IsPassed = 0 then 1 end) FailAttempts, count(case when l.IsPassed = 1 then 1 end) SuccessAttempts from [HospitalManagement].[dbo].[LoginAttempts] l where YEAR(l.AttemptDateTime) = ' " + DateTime.Now.Year + " ' GROUP BY MONTH(l.AttemptDateTime);").ToList();

            for (int i = 0; i < countLoginAttemptPerMonthFinal.Count; i++)
            {
                for (int j = 0; j < countLoginAttemptPerMonth.Count; j++)
                {
                    if (countLoginAttemptPerMonth[j].Month == countLoginAttemptPerMonthFinal[i].Month)
                    {
                        countLoginAttemptPerMonthFinal[i].FailAttempts = countLoginAttemptPerMonth[j].FailAttempts;
                        countLoginAttemptPerMonthFinal[i].SuccessAttempts = countLoginAttemptPerMonth[j].SuccessAttempts;
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(countLoginAttemptPerMonthFinal), "application/json");
        }

        // Fetch data for pie chart to show atmost top 5 patients w.r.t disease percentage
        public ActionResult PatientDiseasePercentage()
        {
            HospitalManagementContext DataContext = new HospitalManagementContext();

            List<DiseasePercentage> DiseasePercentages = DataContext.Database.SqlQuery<DiseasePercentage>("select top 5 count(p.Disease) Count, p.Disease Label from[HospitalManagement].[dbo].Patients p where p.Status = 'Admitted' group by p.Disease order by count(p.Disease) desc").ToList();

            // Set color of each percecnage section

            int TotalPatients = DataContext.Patients.Where(p => p.Status == "Admitted").ToList().Count;

            if (DiseasePercentages.Count > 0)
            {
                DiseasePercentages[0].Value = Math.Round(((double)(DiseasePercentages[0].Count) / TotalPatients) * 100, 2);
                DiseasePercentages[0].Color = "#13dafe";
                DiseasePercentages[0].Highlight = "#13dafe";
            }
            if (DiseasePercentages.Count > 1)
            {
                DiseasePercentages[1].Value = Math.Round(((double)(DiseasePercentages[1].Count) / TotalPatients) * 100, 2);
                DiseasePercentages[1].Color = "#6164c1";
                DiseasePercentages[1].Highlight = "#6164c1";
            }
            if (DiseasePercentages.Count > 2)
            {
                DiseasePercentages[2].Value = Math.Round(((double)(DiseasePercentages[2].Count) / TotalPatients) * 100, 2);
                DiseasePercentages[2].Color = "#99d683";
                DiseasePercentages[2].Highlight = "#99d683";
            }

            if (DiseasePercentages.Count > 3)
            {
                DiseasePercentages[3].Value = Math.Round(((double)(DiseasePercentages[3].Count) / TotalPatients) * 100, 2);
                DiseasePercentages[3].Color = "#ffca4a";
                DiseasePercentages[3].Highlight = "#ffca4a";
            }
            if (DiseasePercentages.Count > 4)
            {
                DiseasePercentages[4].Value = Math.Round(((double)(DiseasePercentages[4].Count) / TotalPatients) * 100, 2);
                DiseasePercentages[4].Color = "#4c5667";
                DiseasePercentages[4].Highlight = "#4c5667";
            }

            return Content(JsonConvert.SerializeObject(DiseasePercentages), "application/json");

        }

        // Get updated count list of patients, caregiver and doctor
        [HttpGet]
        public ActionResult GetUpdatedCountsAndList()
        {
            User user = (User)HttpContext.Session["LoggedInUser"];

            if (user != null && user.Role.Name == "Admin")
            {
                HospitalManagementContext db = new HospitalManagementContext();

                HttpContext.Session["TotalPatientList"] = db.Users.Include(u => u.Patient).Where(u => u.Patient != null).Where(u => u.Patient.Status == "Admitted").ToList();
                HttpContext.Session["TotalPatients"] = db.Users.Count(u => u.Patient != null && u.Patient.Status == "Admitted");
                HttpContext.Session["TotalCaregiverList"] = db.Users.Include(u => u.Caregiver).Where(u => u.Caregiver != null).ToList();
                HttpContext.Session["TotalCareGivers"] = db.Users.Count(u => u.Caregiver != null);
                HttpContext.Session["TotalDoctorList"] = db.Users.Include(u => u.Doctor).Where(u => u.Doctor != null).ToList();
                HttpContext.Session["TotalDoctors"] = db.Users.Count(u => u.Doctor != null);
                HttpContext.Session["RecetlyRegisteredUserList"] = db.Users.OrderByDescending(u => u.Id).Take(10).ToList();
                HttpContext.Session["TotalLoginUsers"] = db.Users.Count(u => u.IsLogin != null && u.IsLogin == true);
                HttpContext.Session["TotalLogoutUsers"] = db.Users.Count(u => u.IsLogin == null || u.IsLogin == false);
                HttpContext.Session["TotalInactive"] = db.Users.Count(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1);
                HttpContext.Session["InactiveUserList"] = db.Users.Where(u => (u.IsLogin == null || u.IsLogin == false) && u.LastLogin != null && DbFunctions.DiffDays(u.LastLogin.Value, DateTime.Now) > 1).ToList();
                HttpContext.Session["FailLoginAttemptList"] = db.LoginAttempts.Where(l => l.IsPassed == false).ToList();
            }

            return Json(new
            {
                Status = "updated"
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult DeleteUser(long id)
        {
            HospitalManagementContext db = new HospitalManagementContext();

            User user = db.Users.Where(u => u.Id == id).FirstOrDefault();

            if (user != null)
            {
                if (user.Role.Name == "Caregiver")
                {
                    user.Caregiver.Patient = null;
                    db.Caregivers.Remove(user.Caregiver);
                }
                else if (user.Role.Name == "Doctor")
                {
                    var doctor = db.Doctors.Include(d => d.Messages).SingleOrDefault(d => d.Id == user.Doctor.Id);

                    foreach (var message in doctor.Messages.ToList())
                    {
                        db.Messages.Remove(message);
                    }

                    db.Doctors.Remove(user.Doctor);
                }
                else if (user.Role.Name == "Patient")
                {
                    try
                    {
                        if (user.Patient.Caregiver != null)
                        {
                            Caregiver caregiver = db.Caregivers.ToList().Where(u => u.Patient.Id == user.Patient.Id).First();
                            caregiver.Patient = null;
                        }
                    }
                    catch (Exception e)
                    {
                    }

                    var patient = db.Patients.Include(p => p.Messages).SingleOrDefault(p => p.Id == user.Patient.Id);

                    foreach (var message in patient.Messages.ToList())
                    {
                        db.Messages.Remove(message);
                    }

                    db.Patients.Remove(user.Patient);
                }

                db.Users.Remove(user);
                db.SaveChanges();
            }

            return RedirectToAction("Security", "Home");
        }
    }
}