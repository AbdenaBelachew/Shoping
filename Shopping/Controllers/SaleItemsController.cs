using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Shopping.Models;

namespace Shopping.Controllers
{
    public class SaleItemsController : Controller
    {
        private dambalEntities db = new dambalEntities();

        public ActionResult sales()
        {
            var saleItems = db.SaleItems.Include(s => s.Product).Include(s => s.Sale).Where(a => a.date.Year
            == DateTime.Now.Year).ToList();
            return View(saleItems);
        }

        public ActionResult Index(string period = "daily", string selectedDate = null)
        {
            DateTime now = DateTime.Now;
            DateTime reportDate;

            if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out DateTime parsedDate))
                reportDate = parsedDate;
            else
                reportDate = now;

            IQueryable<SaleItem> sales = db.SaleItems
                .Include(s => s.Product)
                .Include(s => s.Sale);

            switch (period.ToLower())
            {
                case "daily":
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(reportDate));
                    break;

                case "weekly":
                    DateTime startOfWeek = reportDate.AddDays(-(int)reportDate.DayOfWeek);
                    DateTime endOfWeek = startOfWeek.AddDays(7);
                    sales = sales.Where(s => s.date >= startOfWeek && s.date < endOfWeek);
                    break;

                case "monthly":
                    sales = sales.Where(s => s.date.Month == reportDate.Month && s.date.Year == reportDate.Year);
                    break;

                default:
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(now));
                    break;
            }

            return View(sales.ToList());
        }


        // GET: SaleItems/Create (sell products)
        public ActionResult Create()
        {
            ViewBag.ProductId = new SelectList(db.Products, "ProductId", "Name");
            return View();
        }

        // POST: SaleItems/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(List<SaleItemViewModel> items)
        {
            if (items == null || !items.Any())
            {
                ModelState.AddModelError("", "No products selected.");
                ViewBag.ProductId = new SelectList(db.Products, "ProductId", "Name");
                return View();
            }

            // Create Sale
            var sale = new Sale
            {
                SaleDate = DateTime.Now,
                TotalAmount = 0
            };
            db.Sales.Add(sale);
            db.SaveChanges(); // generate SaleId

            decimal totalAmount = 0;

            foreach (var item in items)
            {
                var product = db.Products.Find(item.ProductId);
                if (product == null || product.QtyOnHand < item.Quantity)
                {
                    ModelState.AddModelError("", $"Product {item.ProductName} not available or insufficient stock.");
                    ViewBag.ProductId = new SelectList(db.Products, "ProductId", "Name");
                    return View();
                }

                var saleItem = new SaleItem
                {
                    SaleId = sale.SaleId,
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    SalePrice = item.SalePrice,
                    CostPrice = product.CostPrice,
                    LineTotal = item.Quantity * item.SalePrice,
                    date = DateTime.Now
                };

                totalAmount += saleItem.LineTotal ?? 0;

                db.SaleItems.Add(saleItem);

                // Update stock
                product.QtyOnHand -= item.Quantity;
                db.Entry(product).State = EntityState.Modified;
            }

            sale.TotalAmount = totalAmount;
            db.Entry(sale).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = "Sale completed successfully!";
            return RedirectToAction("Index");
        }

        // GET: SaleItems/Report
        public ActionResult Report(string period = "daily")
        {
            DateTime startDate = DateTime.Today;

            switch (period.ToLower())
            {
                case "weekly":
                    startDate = DateTime.Today.AddDays(-7);
                    break;
                case "monthly":
                    startDate = DateTime.Today.AddMonths(-1);
                    break;
            }

            var reportItems = db.SaleItems
                                .Include(s => s.Product)
                                .Include(s => s.Sale)
                                .Where(s => s.date >= startDate)
                                .ToList();

            ViewBag.Period = period;
            return View(reportItems);
        }
    }

    // ViewModel for multi-product sale
    public class SaleItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
    }
}

