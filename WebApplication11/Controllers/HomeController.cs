using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WebApplication11.Models;

namespace WebApplication11.Controllers
{
    public class HomeController : Controller
    {
        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool IsPhoneNumberValid(string phoneNumber)
        {
            string pattern = @"^((8|\+7)[\- ]?)?(\(?\d{3}\)?[\- ]?)?[\d\- ]{7,10}$";
            return Regex.IsMatch(phoneNumber, pattern);
        }

        private Rpm2Context db;
        public HomeController (Rpm2Context pm2Context)
        {
            db = pm2Context;
        }

        public IActionResult EasyData()
        {
            var userid = HttpContext.Session.GetInt32("UserID");
            var user = db.Users.FirstOrDefault(u => u.IdUser == userid);

            if (user.RoleId == 1)
            {
                return View();
            }
            return NotFound();
        }

        public async Task<IActionResult> Index()
        {
            return View(await db.Catalogs.ToListAsync());
        }

        [HttpGet]
        public IActionResult Registration()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string phoneNumber, string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "������ �� ���������.";
                return View("Registration");
            }

            if (!IsPhoneNumberValid(phoneNumber))
            {
                ViewBag.ErrorMessage = "�������� ������ ������.";
                return View("Registration");
            }

            // �������� �� ������������� ������������
            if (db.Users.Any(u => u.Email == email || u.Phone == phoneNumber))
            {
                ViewBag.ErrorMessage = "������������ � ����� email ��� ������� �������� ��� ����������.";
                return View("Registration");
            }

            // ����������� ������
            string hashedPassword = ComputeSha256Hash(password);

            // ���������� ������������
            var user = new User
            {
                Phone = phoneNumber,
                Email = email,
                Password = hashedPassword,
                RoleId = 2
            };

            db.Users.Add(user);
            db.SaveChanges();

            // ��������������� �� �������� ����� ��� �������� �����������
            return RedirectToAction("Authorization");
        }

        [HttpGet]
        public IActionResult Authorization()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            // ��������, ���������� �� ������������ � ����� email
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                ViewBag.ErrorMessage = "������������ � ����� email �� ������.";
                return View("Authorization");
            }

            // �������� ������
            string hashedPassword = ComputeSha256Hash(password);
            if (hashedPassword != user.Password)
            {
                ViewBag.ErrorMessage = "�������� ������.";
                return View("Authorization");
            }

            HttpContext.Session.SetInt32("UserID", user.IdUser);
            HttpContext.Session.SetString("IsAuthenticated", "true");
            HttpContext.Session.SetInt32("RoleID", user.RoleId);

            return RedirectToAction("Profile");
        }

        public IActionResult Profile()
        {
            // �������� �� �����������
            var isAuthenticated = HttpContext.Session.GetString("IsAuthenticated");
            if (isAuthenticated != "true")
            {
                return RedirectToAction("Authorization");  // ���� ������������ �� �����������, �������������� �� �������� �����������
            }

            int? isuser = HttpContext.Session.GetInt32("UserID");

            var user = db.Users.SingleOrDefault(u => u.IdUser == isuser);

            return View(user);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();  // ������� ������
            return RedirectToAction("Authorization");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> Catalog(string filter, string search, string sort)
        {
            var catalogs = db.Catalogs.AsQueryable();
            var userid = HttpContext.Session.GetInt32("UserID");

            // ����������
            if (!string.IsNullOrEmpty(filter))
            {
                catalogs = catalogs.Where(c => c.CategoryName == filter);
            }

            // �����
            if (!string.IsNullOrEmpty(search))
            {
                catalogs = catalogs.Where(c => c.ProductName.Contains(search) || c.Description.Contains(search));
            }

            // ����������
            if (!string.IsNullOrEmpty(sort))
            {
                catalogs = sort switch
                {
                    "price-asc" => catalogs.OrderBy(c => c.Price),
                    "price-desc" => catalogs.OrderByDescending(c => c.Price),
                    _ => catalogs
                };
            }

            if (userid != null)
            {
                var cartItems = await db.Carts.Where(c => c.UserId == userid).ToListAsync();

                foreach (var item in catalogs)
                {
                    item.IsInCart = cartItems.Any(c => c.ProductId == item.IdProduct);
                }
            }

            return View(await catalogs.ToListAsync());
        }


        public IActionResult AboutUs()
        {
            return View();
        }

        public async Task<IActionResult> Cart()
        {
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null)
            {
                return RedirectToAction("Authorization");
            }

            // �������� ��� ������ �� ������� ������������ � �������������� ���������� (Product)
            var cartItems = await db.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)  // ���������� ����� (Catalog)
                .ToListAsync();

            // �������� ������� � �������������
            return View(cartItems);
        }


        [HttpPost]
        public IActionResult AddToCart(int productId)
        {
            int? isuser = HttpContext.Session.GetInt32("UserID");
            if (isuser != null)
            {
                var productToCart = new Cart
                {
                    UserId = isuser.Value,
                    ProductId = productId
                };

                db.Carts.Add(productToCart);
                db.SaveChanges();
                return RedirectToAction("Catalog");
            }
            else
            {
                return RedirectToAction("Authorization");
            }
        }

        [HttpPost]
        public IActionResult UpdateCartQuantity(int productId, int quantity)
        {
            int? isuser = HttpContext.Session.GetInt32("UserID");
            if (isuser != null)
            {
                var cartItem = db.Carts.FirstOrDefault(c => c.UserId == isuser.Value && c.ProductId == productId);
                if (cartItem != null)
                {
                    cartItem.Quantity = quantity;  // ���������� ���������� ������
                    db.SaveChanges();
                }
                return RedirectToAction("Cart");
            }
            else
            {
                return RedirectToAction("Authorization");
            }
        }

        [HttpPost]
        public IActionResult RemoveCartItem(int productId, string redirectTo)
        {
            int? isuser = HttpContext.Session.GetInt32("UserID");
            if (isuser != null)
            {
                var cartItem = db.Carts.FirstOrDefault(c => c.UserId == isuser.Value && c.ProductId == productId);
                if (cartItem != null)
                {
                    db.Carts.Remove(cartItem);
                    db.SaveChanges();
                }

                // ���������, ���� ����� �������������
                if (redirectTo == "Cart")
                {
                    return RedirectToAction("Cart"); // �������������� �� �������� �������
                }
                else
                {
                    return RedirectToAction("Catalog"); // �������������� �� �������
                }
            }
            else
            {
                return RedirectToAction("Authorization");
            }
        }

        public IActionResult Order()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
