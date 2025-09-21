using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Shopping.Models;

namespace Shopping.Controllers
{
    public class HomeController : Controller
    {
        private dambalEntities db = new dambalEntities();

        public ActionResult Index()
        {  // Example calculations - adjust based on your models

            decimal products = db.Products.Sum(a => a.QtyOnHand );
            decimal totalRevenue = db.Sales.Sum(s => s.TotalAmount);
            decimal totalCost = db.SaleItems.Sum(i => i.CostPrice * i.Quantity);
            decimal profit = totalRevenue - totalCost;

            var today = DateTime.Today;
            decimal totalExpenses = db.Expenses
                .Where(e => e.ExpenseDate.Month == today.Month && e.ExpenseDate.Year == today.Year)
                .Sum(e => e.Amount);

            decimal netProfit = profit - totalExpenses;

            ViewBag.TotalProducts = products;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCost = totalCost;
            ViewBag.Profit = profit;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.totalcosts = totalExpenses + totalCost;
            ViewBag.NetProfit = netProfit;
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}