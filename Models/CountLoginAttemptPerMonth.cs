using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HospitalManagament.Models
{
    public class CountLoginAttemptPerMonth
    {
        public int FailAttempts { get; set; }
        public int SuccessAttempts { get; set; }
        public string Month { get; set; }
        
        public CountLoginAttemptPerMonth()
        {
        }

        public CountLoginAttemptPerMonth(int FailAttempts, int SuccessAttempts, string Month)
        {
            this.FailAttempts = FailAttempts;
            this.SuccessAttempts = SuccessAttempts;
            this.Month = Month;
        }
    }
}