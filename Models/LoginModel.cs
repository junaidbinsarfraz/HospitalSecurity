﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HospitalManagament.Models
{
    public class LoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public int VerificationCode { get; set; }
        
    }
}