using Diwali.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Diwali.Application;
public interface IPosDb {DbSet<Product> Products {get;} DbSet<Category> Categories {get;} DbSet<Bill> Bills {get;} DbSet<BillItem> BillItems {get;} DbSet<User> Users {get;} DbSet<InventoryTransaction> InventoryTransactions {get;} DbSet<ShopSettings> Settings {get;} DatabaseFacade Database {get;} Task<int> SaveChangesAsync(CancellationToken ct=default);}
public class AppError(string message,int status=400):Exception(message) { public int Status {get;}=status; }
public record LoginInput(string Username,string Password);
public record ProductInput(string SerialNumber,string Name,Guid CategoryId,decimal Price,decimal MRP,int StockQuantity,bool IsActive,uint? Version);
public record CategoryInput(string Name,bool IsActive);
public record SaleLine(Guid ProductId,int Quantity,decimal ExpectedPrice);
public record SaleInput(Guid SubmissionId,decimal DiscountPercentage,List<SaleLine> Items);
public record StockInput(Guid ProductId,int Quantity,string Reason);
public record UserInput(string Name,string Username,string? Password,string Role,bool IsActive);
public record SettingsInput(string Name,string Address,string Phone,int LowStockThreshold);
public static class Rules {
 public static string Text(string? value,string field,int max=160) {var s=value?.Trim()??"";if(s.Length==0||s.Length>max)throw new AppError($"{field} is required (maximum {max} characters).");return s;}
 public static void Money(decimal value,string field) {if(value<0||value>10000000||decimal.Round(value,2)!=value)throw new AppError($"{field} must be between 0 and 10,000,000 with at most two decimal places.");}
 public static void Password(string? value) {if(value is null||value.Length<12||value.Length>128)throw new AppError("Password must contain 12–128 characters.");}
 public static decimal Round(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero);
}
