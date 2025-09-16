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



        // REPORT: Daily / Weekly / Monthly Profit
        public ActionResult Report(string period = "daily")
        {
            DateTime now = DateTime.Now;
            IQueryable<SaleItem> sales = db.SaleItems.Include(s => s.Product);

            switch (period.ToLower())
            {
                case "daily":
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(now));
                    break;

                case "weekly":
                    DateTime startOfWeek = now.AddDays(-(int)now.DayOfWeek); // Sunday start
                    sales = sales.Where(s => s.date >= startOfWeek && s.date <= now);
                    break;

                case "monthly":
                    sales = sales.Where(s => s.date.Month == now.Month && s.date.Year == now.Year);
                    break;

                default:
                    sales = sales.Where(s => DbFunctions.TruncateTime(s.date) == DbFunctions.TruncateTime(now));
                    break;
            }

            var report = sales
                .GroupBy(s => 1) // single group
                .Select(g => new
                {
                    TotalRevenue = g.Sum(x => x.LineTotal),
                    TotalCost = g.Sum(x => x.CostPrice * x.Quantity),
                    Profit = g.Sum(x => x.LineTotal) - g.Sum(x => x.CostPrice * x.Quantity)
                })
                .FirstOrDefault();

            ViewBag.Period = period;
            ViewBag.TotalRevenue = report?.TotalRevenue ?? 0;
            ViewBag.TotalCost = report?.TotalCost ?? 0;
            ViewBag.Profit = report?.Profit ?? 0;

            return View();
        }

    }
}

