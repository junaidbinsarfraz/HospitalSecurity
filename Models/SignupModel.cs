using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace HospitalManagament.Models
{
    public class SignupModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
        [Compare("Password", ErrorMessage = "Confirm password doesn't match")]
        public string ConfirmPassword { get; set; }
        public SignupAs signupAs { get; set; }
        public string ContactNo { get; set; }
        public string OTP { get; set; }
    }

    public enum SignupAs
    {
        Patient,
        Doctor,
        CareGiver
    }
}