using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Shopping.Models;

namespace Shopping.Controllers
{
    public class ExpensesController : Controller
    {
        private dambalEntities db = new dambalEntities();

        public ActionResult Index()
        {
            var expenses = db.Expenses.OrderByDescending(e => e.ExpenseDate).ToList();
            return View(expenses);
        }

        // Create expense (GET)
        public ActionResult Create()
        {
            return View();
        }

        // Create expense (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Create(Expens expense)
        {
            if (ModelState.IsValid)
            {
                db.Expenses.Add(expense);
                db.SaveChanges();

                return Json(new { success = true });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors)
                                          .Select(e => e.ErrorMessage)
                                          .ToList();

            return Json(new { success = false, errors });
        }

        // GET: Expenses/Edit/5
        public ActionResult Edit(int id)
        {
            var expense = db.Expenses.Find(id);
            if (expense == null) return HttpNotFound();
            return View(expense);
        }

        // POST: Expenses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Expens expense)
        {
            if (ModelState.IsValid)
            {
                db.Entry(expense).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(expense);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteConfirmed(int id)
        {
            try
            {
                var expense = db.Expenses.Find(id);
                if (expense == null)
                    return Json(new { success = false, message = "Expense not found." });

                db.Expenses.Remove(expense);
                db.SaveChanges();

                return Json(new { success = true, message = "Expense deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


    }
}