using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;

namespace Diska.Services
{
    public interface IShippingService
    {
        decimal CalculateCost(string governorate, string city);
        Task ImportFromExcelAsync(IFormFile file);
    }

    public class ShippingService : IShippingService
    {
        private readonly ApplicationDbContext _context;

        public ShippingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public decimal CalculateCost(string governorate, string city)
        {
            if (string.IsNullOrEmpty(governorate)) return 0;

            // 1. البحث عن سعر خاص للمدينة
            var cityRate = _context.ShippingRates
                .FirstOrDefault(r => r.Governorate == governorate && r.City == city);

            if (cityRate != null) return cityRate.Cost;

            // 2. البحث عن سعر عام للمحافظة
            var govRate = _context.ShippingRates
                .FirstOrDefault(r => r.Governorate == governorate && (string.IsNullOrEmpty(r.City) || r.City == "All"));

            if (govRate != null) return govRate.Cost;

            return 50; // سعر افتراضي
        }

        public async Task ImportFromExcelAsync(IFormFile file)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                stream.Position = 0;

                // مسح الأسعار القديمة
                _context.Database.ExecuteSqlRaw("TRUNCATE TABLE ShippingRates");

                var extension = Path.GetExtension(file.FileName).ToLower();

                if (extension == ".csv")
                {
                    // قراءة ملف CSV
                    using (var reader = new StreamReader(stream))
                    {
                        bool isHeader = true;
                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            var values = line.Split(',');

                            // تجاهل الهيدر
                            if (isHeader)
                            {
                                if (values[0].Trim().Equals("Governorate", StringComparison.OrdinalIgnoreCase))
                                {
                                    isHeader = false; continue;
                                }
                                isHeader = false;
                            }

                            if (values.Length >= 3 && decimal.TryParse(values[2].Trim(), out decimal cost))
                            {
                                _context.ShippingRates.Add(new ShippingRate
                                {
                                    Governorate = values[0].Trim(),
                                    City = values[1].Trim(),
                                    Cost = cost
                                });
                            }
                        }
                    }
                }
                else
                {
                    // قراءة ملف Excel
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        while (reader.Read())
                        {
                            if (reader.GetValue(0)?.ToString() == "Governorate") continue;

                            var rate = new ShippingRate
                            {
                                Governorate = reader.GetValue(0)?.ToString(),
                                City = reader.GetValue(1)?.ToString(),
                                Cost = Convert.ToDecimal(reader.GetValue(2))
                            };
                            _context.ShippingRates.Add(rate);
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}