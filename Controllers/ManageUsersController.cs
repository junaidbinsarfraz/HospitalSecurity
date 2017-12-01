using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Globalization;
using System.Web.Helpers;

namespace HospitalManagament.Controllers
{
    public class ManageUsersController : Controller
    {
        private HospitalManagementContext db = new HospitalManagementContext();

        // GET: ManagePatients
        public ActionResult Index()
        {
            var users = db.Users.Include(u => u.Caregiver).Include(u => u.Patient);
            return View(users.ToList().Where(x => x.UserName != "Admin"));
        }

        // GET: ManagePatients/Details/5
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        // GET: ManagePatients/Edit/5
        public ActionResult Edit(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        // POST: ManagePatients/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public ActionResult Edit(User user)
        {
            if (ModelState.IsValid)
            {
                User oldUser = db.Users.FirstOrDefault(u => u.Id == user.Id);

                if (oldUser.Email != user.Email)
                {
                    User existingUser = db.Users.FirstOrDefault(u => u.Email == user.Email);

                    if (existingUser != null)
                    {
                        ModelState.AddModelError("", "Email already exists");
                        return View();
                    }
                }

                oldUser.FullName = user.FullName;
                oldUser.UserName = user.UserName;
                oldUser.NRIC = user.NRIC;
                oldUser.Age = user.Age;
                oldUser.ContactNo = user.ContactNo;
                oldUser.Email = user.Email;
                oldUser.Gender = user.Gender;
                oldUser.Address = user.Address;
                oldUser.Comments = user.Comments;

                db.SaveChanges();

                return RedirectToAction("Index");
            }
            return View(user);
        }

        // GET: ManagePatients/Delete/5
        public ActionResult Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            User user = db.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }

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

                foreach(var message in patient.Messages.ToList()) 
                {
                    db.Messages.Remove(message);
                }

                db.Patients.Remove(user.Patient);
            }

            db.Users.Remove(user);

            db.SaveChanges();

            return RedirectToAction("Index");
        }

        // POST: ManagePatients/Delete/5
        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(long id)
        {
            User user = db.Users.Find(id);
            db.Users.Remove(user);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
