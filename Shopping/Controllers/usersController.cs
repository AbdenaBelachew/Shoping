using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Shopping.Models;
using System.Diagnostics; // For logging

namespace Shopping.Controllers
{
    public class usersController : Controller
    {
        private dambalEntities db = new dambalEntities();

        // Hashing utility
        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // GET: Users
        public ActionResult Index()
        {
            return View(db.users.ToList());
        }

        // GET: Users/Create
        public ActionResult Create()
        {
            return View(new user());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(user user, string password)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("Password", "Password is required.");
                    return View(user);
                }

                user.password_hash = HashPassword(password);
                user.created_at = DateTime.Now;
                user.updated_at = DateTime.Now;
                user.IsActive = true;
                user.MustChangePassword = true;

                db.users.Add(user);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(user);
        }

        // GET: Users/Login
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            string hashed = HashPassword(password);

            // Get users from DB
            var users = db.users.AsEnumerable(); // fetch all to use TryParse safely

            // Find user with valid licence
            var user = users.FirstOrDefault(u =>
            {
                int licenceYear;
                bool validLicence = int.TryParse(u.Licence, out licenceYear) && licenceYear >= DateTime.Now.Year;
                bool validUser = string.Equals(u.username, username, StringComparison.OrdinalIgnoreCase)
                                 && u.password_hash == hashed;
                return validUser && validLicence;
            });

            if (user != null)
            {
                Session["UserId"] = user.UserId;
                Session["Username"] = user.username;
                Session["Role"] = user.role;

                if (user.MustChangePassword)
                {
                    return RedirectToAction("ChangePassword");
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Invalid username, password, or licence expired";
            return View();
        }


        // GET: Users/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        // POST: Users/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            int userId = Convert.ToInt32(Session["UserId"]);
            var user = db.users.Find(userId);

            if (user == null) return RedirectToAction("Login");

            if (HashPassword(currentPassword) != user.password_hash)
            {
                ViewBag.Error = "Current password is incorrect.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "New password and confirmation do not match.";
                return View();
            }

            user.password_hash = HashPassword(newPassword);
            user.MustChangePassword = false;
            user.updated_at = DateTime.Now;
            db.SaveChanges();

            return RedirectToAction("Index", "Home");
        }

        // GET: Users/EditLicense/5
        public ActionResult EditLicense(int? id)
        {
            if (id == null)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Invalid user ID." }, JsonRequestBehavior.AllowGet);
                }
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                var user = db.users.Find(id);
                if (user == null)
                {
                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = false, message = $"User with ID {id} not found." }, JsonRequestBehavior.AllowGet);
                    }
                    return HttpNotFound();
                }

                if (Request.IsAjaxRequest())
                {
                    return Json(new
                    {
                        success = true,
                        UserId = user.UserId,
                        Licence = user.Licence,
                        full_name = user.full_name
                    }, JsonRequestBehavior.AllowGet);
                }

                return View(user);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Debug.WriteLine($"EditLicense GET Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = $"Server error: {ex.Message}" }, JsonRequestBehavior.AllowGet);
                }
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // POST: Users/EditLicense/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditLicense(int id, string Licence)
        {
            try
            {
                var user = db.users.Find(id);
                if (user == null)
                {
                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);
                    }
                    return HttpNotFound();
                }

                if (ModelState.IsValid)
                {
                    user.Licence = Licence;
                    user.updated_at = DateTime.Now;
                    db.Entry(user).State = EntityState.Modified;
                    db.SaveChanges();

                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = true, message = "License updated successfully." }, JsonRequestBehavior.AllowGet);
                    }
                    return RedirectToAction("Index");
                }

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = "Invalid input. Please try again." }, JsonRequestBehavior.AllowGet);
                }
                return View(user);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Debug.WriteLine($"EditLicense POST Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = $"Server error: {ex.Message}" }, JsonRequestBehavior.AllowGet);
                }
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // GET: Users/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var user = db.users.Find(id);
            if (user == null)
                return HttpNotFound();

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost]
        public ActionResult DeleteConfirmed(int id)
        {
            var user = db.users.Find(id);
            if (user == null)
                return HttpNotFound();

            db.users.Remove(user);
            db.SaveChanges();

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = "User deleted successfully." });
            }

            return RedirectToAction("Index");
        }


        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "users");
        }
    }
}