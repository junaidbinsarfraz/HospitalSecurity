//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace HospitalManagament
{
    using System;
    using System.Collections.Generic;
    
    public partial class User
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public User()
        {
            this.Gender = "Male";
            this.LoginAttemptCount = 0;
            this.Events = new HashSet<Event>();
        }
    
        public long Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Salt { get; set; }
        public string UserName { get; set; }
        public Nullable<long> Age { get; set; }
        public string Gender { get; set; }
        public string NRIC { get; set; }
        public string Comments { get; set; }
        public string Address { get; set; }
        public string ContactNo { get; set; }
        public string FullName { get; set; }
        public string Token { get; set; }
        public string OTP { get; set; }
        public Nullable<System.DateTime> OTPExpiration { get; set; }
        public Nullable<bool> IsLogin { get; set; }
        public Nullable<System.DateTime> LastLogin { get; set; }
        public Nullable<bool> IsEnabled { get; set; }
        public string EnableToken { get; set; }
        public Nullable<int> LoginAttemptCount { get; set; }
    
        public virtual Patient Patient { get; set; }
        public virtual Caregiver Caregiver { get; set; }
        public virtual Doctor Doctor { get; set; }
        public virtual Role Role { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Event> Events { get; set; }
    }
}
