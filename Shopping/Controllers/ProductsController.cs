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
    public class ProductsController : Controller
    {
        private dambalEntities db = new dambalEntities();

        public ActionResult Create()
        {
            ViewBag.TypeId = new SelectList(db.ProductTypes, "TypeId", "TypeName");
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Product product)
        {
            if (ModelState.IsValid)
            {
                product.CreatedAt = DateTime.Now; // ✅ set before saving
                db.Products.Add(product);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(product);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddStock(int ProductId, int Quantity, decimal? CostPrice)
        {
            var product = db.Products.Find(ProductId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction("Index");
            }

            // Add quantity
            product.QtyOnHand += Quantity;

            // Update cost price if provided
            if (CostPrice.HasValue && CostPrice.Value > 0)
            {
                product.CostPrice = CostPrice.Value;
            }

            db.Entry(product).State = EntityState.Modified;
            db.SaveChanges();

            TempData["Success"] = "Stock updated successfully!";
            return RedirectToAction("Index");
        }

        /// NOTIFICATION FOR LOW STOCK
        /// 
        public ActionResult LowStockNotifications()
        {
            var lowStock = db.Products
                             .Where(p => p.QtyOnHand < 5)
                             .Select(p => new
                             {
                                 p.Name,
                                 p.QtyOnHand
                             })
                             .ToList();

            return Json(lowStock, JsonRequestBehavior.AllowGet);
        }



        public ActionResult Index()
        {
            var products = db.Products.Include("ProductType").ToList();

            // Fill dropdown for Product Types
            ViewBag.TypeId = new SelectList(db.ProductTypes, "TypeId", "TypeName");

            return View(products);
        }

        // GET: Sell Form
        public ActionResult Sell()
        {
            var products = db.Products.ToList();
            ViewBag.ProductId = new SelectList(products, "ProductId", "Name");
            return View();
        }

        // POST: Sell Product
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Sell(int ProductId, int Quantity, decimal SalePrice)
        {
            var product = db.Products.Find(ProductId);
            if (product == null)
            {
                ModelState.AddModelError("", "Invalid product selected.");
                return View();
            }

            if (Quantity <= 0 || SalePrice <= 0)
            {
                ModelState.AddModelError("", "Invalid quantity or price.");
                ViewBag.ProductId = new SelectList(db.Products, "ProductId", "Name", ProductId);
                return View();
            }

            if (product.QtyOnHand < Quantity)
            {
                ModelState.AddModelError("", "Not enough stock available.");
                ViewBag.ProductId = new SelectList(db.Products, "ProductId", "Name", ProductId);
                return View();
            }

            // Create Sale
            var sale = new Sale
            {
                SaleDate = DateTime.Now,
                TotalAmount = Quantity * SalePrice
            };
            db.Sales.Add(sale);
            db.SaveChanges(); // Generate SaleId

            // Create Sale Item
            var saleItem = new SaleItem
            {
                SaleId = sale.SaleId,
                ProductId = ProductId,
                Quantity = Quantity,
                SalePrice = SalePrice,
                CostPrice = product.CostPrice,
                date = DateTime.Now,
                LineTotal = Quantity * SalePrice
            };
            db.SaleItems.Add(saleItem);

            // Update product stock
            product.QtyOnHand -= Quantity;
            db.Entry(product).State = EntityState.Modified;

            db.SaveChanges();

            TempData["Success"] = "Sale completed successfully!";
            return RedirectToAction("Index");
        }

        // GET: Multi-product Sell
        public ActionResult SellMultiple()
        {
            ViewBag.Products = new SelectList(db.Products, "ProductId", "Name");
            return View();
        }

        // POST: Save Multi-product Sale
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SellMultiple(List<int> ProductIds, List<int> Quantities, List<decimal> SalePrices)
        {
            if (ProductIds == null || !ProductIds.Any())
            {
                ModelState.AddModelError("", "Please add at least one product.");
                ViewBag.Products = new SelectList(db.Products, "ProductId", "Name");
                return View();
            }

            // Create sale header
            var sale = new Sale
            {
                SaleDate = DateTime.Now,
                TotalAmount = 0
            };
            db.Sales.Add(sale);
            db.SaveChanges(); // generate SaleId

            decimal totalAmount = 0;

            for (int i = 0; i < ProductIds.Count; i++)
            {
                var product = db.Products.Find(ProductIds[i]);
                if (product == null) continue;

                int qty = Quantities[i];
                decimal price = SalePrices[i];

                if (qty <= 0 || price <= 0) continue;
                if (product.QtyOnHand < qty) continue;

                var saleItem = new SaleItem
                {
                    SaleId = sale.SaleId,
                    ProductId = product.ProductId,
                    Quantity = qty,
                    SalePrice = price,
                    CostPrice = product.CostPrice,
                    LineTotal = qty * price,
                    date = DateTime.Now
                };

                db.SaleItems.Add(saleItem);

                // Update stock
                product.QtyOnHand -= qty;
                db.Entry(product).State = EntityState.Modified;

                totalAmount += saleItem.LineTotal ?? 0;

            }

            // Update total
            sale.TotalAmount = totalAmount;
            db.Entry(sale).State = EntityState.Modified;

            db.SaveChanges();

            TempData["Success"] = "Sale completed successfully!";
            return RedirectToAction("Index");
        }
  
        public ActionResult Report(string period = "daily", string selectedDate = null)
        {
            DateTime now = DateTime.Now;
            DateTime reportDate;

            if (!string.IsNullOrEmpty(selectedDate) && DateTime.TryParse(selectedDate, out DateTime parsedDate))
                reportDate = parsedDate;
            else
                reportDate = now;

            IQueryable<SaleItem> sales = db.SaleItems.Include(s => s.Product).Include(s => s.Sale);

            switch (period.ToLower())
            {
                case "daily":
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(reportDate));
                    break;

                case "weekly":
                    DateTime startOfWeek = reportDate.AddDays(-(int)reportDate.DayOfWeek);
                    DateTime endOfWeek = startOfWeek.AddDays(7);

                    sales = sales.Where(s => s.date >= startOfWeek && s.date < endOfWeek);

                    ViewBag.WeekStart = startOfWeek.ToString("MM/dd/yyyy");
                    ViewBag.WeekEnd = endOfWeek.AddDays(-1).ToString("MM/dd/yyyy");
                    break;

                case "monthly":
                    sales = sales.Where(s => s.date.Month == reportDate.Month && s.date.Year == reportDate.Year);
                    ViewBag.Month = reportDate.ToString("MMMM yyyy");
                    break;

                default:
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(now));
                    break;
            }

            var report = sales
                .GroupBy(s => 1)
                .Select(g => new
                {
                    TotalRevenue = g.Sum(x => x.LineTotal),
                    TotalCost = g.Sum(x => x.CostPrice * x.Quantity),
                    Profit = g.Sum(x => x.LineTotal) - g.Sum(x => x.CostPrice * x.Quantity)
                })
                .FirstOrDefault() ?? new { TotalRevenue = (decimal?)0m, TotalCost = 0m, Profit = (decimal?)0m };

            // Summary
            ViewBag.Period = period.ToLower();
            ViewBag.SelectedDate = reportDate.ToString("yyyy-MM-dd");
            ViewBag.TotalRevenue = report.TotalRevenue;
            ViewBag.TotalCost = report.TotalCost;
            ViewBag.Profit = report.Profit;

            // List of filtered sales
            ViewBag.SaleItems = sales.OrderByDescending(s => s.date).ToList();

            return View();
        }

        // TOP SELLING

        // 📌 Stock Report
        public ActionResult Stock()
        {
            var stockReport = db.Products
                .Select(p => new StockReportViewModel
                {
                    ProductName = p.Name,
                    QtyOnHand = p.QtyOnHand,
                    Status = p.QtyOnHand == 0 ? "Out of Stock" :
                             p.QtyOnHand < 5 ? "Low Stock" :
                             "In Stock"
                })
                .ToList();

            return View(stockReport);
        }


        // 📌 Profit & Loss Report
        //public ActionResult ProfitLoss(string period = "monthly")
        //{
        //    DateTime now = DateTime.Now;
        //    IQueryable<SaleItem> sales = db.SaleItems;

        //    if (period == "monthly")
        //        sales = sales.Where(s => s.date.Month == now.Month && s.date.Year == now.Year);
        //    else if (period == "daily")
        //        sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(now));

        //    var report = new
        //    {
        //        TotalRevenue = sales.Sum(s => (decimal?)s.LineTotal) ?? 0,
        //        TotalCost = sales.Sum(s => (decimal?)(s.CostPrice * s.Quantity)) ?? 0,
        //    };

        //    ViewBag.TotalRevenue = report.TotalRevenue;
        //    ViewBag.TotalCost = report.TotalCost;
        //    ViewBag.Profit = report.TotalRevenue - report.TotalCost;

        //    return View();
        //}

        public ActionResult ProfitLoss()
        {
            // Example calculations - adjust based on your models
            decimal totalRevenue = db.Sales.Sum(s => s.TotalAmount);
            decimal totalCost = db.SaleItems.Sum(i => i.CostPrice * i.Quantity);
            decimal profit = totalRevenue - totalCost;

            var today = DateTime.Today;
            decimal totalExpenses = db.Expenses
                .Where(e => e.ExpenseDate.Month == today.Month && e.ExpenseDate.Year == today.Year)
                .Sum(e => e.Amount);

            decimal netProfit = profit - totalExpenses;

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCost = totalCost;
            ViewBag.Profit = profit;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.NetProfit = netProfit;

            return View();
        }


        // 📌 Top-Selling Products
        public ActionResult TopSelling()
        {
            var topProducts = db.SaleItems
                .GroupBy(s => s.Product.Name)
                .Select(g => new TopSellingProductViewModel
                {
                    ProductName = g.Key,
                    TotalSold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => (decimal?)(x.LineTotal) ?? 0) // 👈 handles nulls
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(10)
                .ToList();

            return View(topProducts);
        }


    }
}

