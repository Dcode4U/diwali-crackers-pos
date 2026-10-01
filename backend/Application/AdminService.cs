using Diwali.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Diwali.Application;
public class AdminService(IPosDb db,IPasswordHasher<User> hasher) {
 public async Task<User?> Login(LoginInput input,CancellationToken ct) {
  var username=(input.Username??"").Trim().ToLowerInvariant();if(username.Length>100||(input.Password?.Length??0)>128)return null;
  var u=await db.Users.SingleOrDefaultAsync(u=>u.Username==username&&u.IsActive,ct);
  // A dummy hash makes unknown-user requests use the password hashing work factor too.
  var target=u??new User();var hash=u?.PasswordHash??DummyHash;
  var result=hasher.VerifyHashedPassword(target,hash,input.Password??"");if(u is null||result==PasswordVerificationResult.Failed)return null;
  if(result==PasswordVerificationResult.SuccessRehashNeeded){u.PasswordHash=hasher.HashPassword(u,input.Password!);await db.SaveChangesAsync(ct);}return u;
 }
 private static readonly string DummyHash=new PasswordHasher<User>(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions{IterationCount=210000})).HashPassword(new User(),Guid.NewGuid().ToString());
 public async Task<object> Users(CancellationToken ct)=>await db.Users.AsNoTracking().OrderBy(u=>u.Name).Select(u=>new{u.Id,u.Name,u.Username,u.Role,u.IsActive,u.CreatedAt}).ToListAsync(ct);
 public async Task<object> SaveUser(Guid? id,UserInput input,Guid actor,CancellationToken ct) {
  var name=Rules.Text(input.Name,"Name",100);var username=Rules.Text(input.Username,"Username",100).ToLowerInvariant();if(input.Role!="Admin"&&input.Role!="Cashier")throw new AppError("Role must be Admin or Cashier.");
  if(id==actor&&(!input.IsActive||input.Role!="Admin"))throw new AppError("You cannot deactivate or demote your own account.");
  if(await db.Users.AnyAsync(u=>u.Username==username&&u.Id!=id,ct))throw new AppError("Username already exists.",409);
  var u=id.HasValue?await db.Users.FindAsync([id.Value],ct)??throw new AppError("User not found.",404):new User();
  if(!id.HasValue||!string.IsNullOrEmpty(input.Password)){Rules.Password(input.Password);u.PasswordHash=hasher.HashPassword(u,input.Password!);}
  u.Name=name;u.Username=username;u.Role=input.Role;u.IsActive=input.IsActive;u.SessionVersion++;if(!id.HasValue)db.Users.Add(u);await db.SaveChangesAsync(ct);return new{u.Id,u.Name,u.Username,u.Role,u.IsActive};
 }
 public async Task<ShopSettings> Settings(CancellationToken ct)=>await db.Settings.AsNoTracking().SingleAsync(ct);
 public async Task<ShopSettings> SaveSettings(SettingsInput input,CancellationToken ct){var s=await db.Settings.SingleAsync(ct);s.Name=Rules.Text(input.Name,"Shop name");s.Address=Rules.Text(input.Address,"Address",500);s.Phone=Rules.Text(input.Phone,"Phone",40);if(input.LowStockThreshold<0||input.LowStockThreshold>1000000)throw new AppError("Invalid low-stock threshold.");s.LowStockThreshold=input.LowStockThreshold;await db.SaveChangesAsync(ct);return s;}
 public async Task<object> Dashboard(CancellationToken ct) {
  var zone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");var today=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,zone).Date;var start=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(today,DateTimeKind.Unspecified),zone);
  var bills=await db.Bills.AsNoTracking().Where(b=>b.BillDate>=start.AddDays(-6)&&b.BillDate<start.AddDays(1)).Select(b=>new{b.BillDate,b.FinalAmount,b.DiscountAmount,Boxes=b.Items.Sum(i=>i.Quantity)}).ToListAsync(ct);
  var current=bills.Where(b=>b.BillDate>=start).ToList();var settings=await Settings(ct);
  return new{todaySales=current.Sum(b=>b.FinalAmount),billCount=current.Count,boxesSold=current.Sum(b=>b.Boxes),discountGiven=current.Sum(b=>b.DiscountAmount),productCount=await db.Products.CountAsync(p=>p.IsActive,ct),lowStockCount=await db.Products.CountAsync(p=>p.IsActive&&p.StockQuantity<=settings.LowStockThreshold,ct),lowStock=await db.Products.AsNoTracking().Where(p=>p.IsActive&&p.StockQuantity<=settings.LowStockThreshold).OrderBy(p=>p.StockQuantity).Take(20).ToListAsync(ct),sales=Enumerable.Range(0,7).Select(i=>new{date=today.AddDays(i-6).ToString("yyyy-MM-dd"),amount=bills.Where(b=>b.BillDate>=start.AddDays(i-6)&&b.BillDate<start.AddDays(i-5)).Sum(b=>b.FinalAmount)})};
 }
}
