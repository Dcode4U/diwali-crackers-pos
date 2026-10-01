using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diwali.Domain;
using Microsoft.EntityFrameworkCore;
namespace Diwali.Application;
public class BillingService(IPosDb db) {
 public async Task<Bill> Complete(SaleInput input,Guid userId,CancellationToken ct) {
  if(input.SubmissionId==Guid.Empty)throw new AppError("A submission identifier is required.");
  if(input.DiscountPercentage<0||input.DiscountPercentage>100||decimal.Round(input.DiscountPercentage,2)!=input.DiscountPercentage)throw new AppError("Discount must be 0–100%, with at most two decimal places.");
  if(input.Items is null||input.Items.Count==0||input.Items.Count>200)throw new AppError("Add between 1 and 200 products to the bill.");
  if(input.Items.Any(i=>i.Quantity<=0||i.Quantity>1000000)||input.Items.Select(i=>i.ProductId).Distinct().Count()!=input.Items.Count)throw new AppError("Each product must appear once with a valid quantity.");
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{input.DiscountPercentage,Items=input.Items.OrderBy(i=>i.ProductId)}))));
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  // Same key waits for the first transaction, then returns its committed bill.
  var lockKey=$"bill:{userId}:{input.SubmissionId}";
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",ct);
  var existing=await db.Bills.Include(b=>b.Items).SingleOrDefaultAsync(b=>b.CreatedBy==userId&&b.SubmissionId==input.SubmissionId,ct);
  if(existing is not null){if(existing.RequestHash!=hash)throw new AppError("This submission was already used for a different bill. Start a new bill.",409);await tx.CommitAsync(ct);return existing;}
  var settings=await db.Settings.SingleAsync(ct);
  var bill=new Bill{CreatedBy=userId,SubmissionId=input.SubmissionId,RequestHash=hash,DiscountPercentage=input.DiscountPercentage,ShopName=settings.Name,ShopAddress=settings.Address,ShopPhone=settings.Phone};
  // Deterministic row locking protects both last-box sales and concurrent stock edits.
  foreach(var line in input.Items.OrderBy(i=>i.ProductId)) {
   var p=await db.Products.FromSqlInterpolated($"SELECT *, xmin FROM \"Products\" WHERE \"Id\"={line.ProductId} FOR UPDATE").SingleOrDefaultAsync(ct);
   if(p is null||!p.IsActive)throw new AppError("A selected product is unavailable. Refresh the product list.",409);
   if(p.Price!=line.ExpectedPrice)throw new AppError($"The price of {p.Name} changed to ₹{p.Price:0.00}. Refresh and review the bill.",409);
   if(p.StockQuantity<line.Quantity)throw new AppError($"Only {p.StockQuantity} boxes remaining for {p.Name}.",409);
   var previous=p.StockQuantity;p.StockQuantity-=line.Quantity;p.UpdatedAt=DateTime.UtcNow;
   bill.Items.Add(new BillItem{ProductId=p.Id,SerialNumber=p.SerialNumber,ProductName=p.Name,PriceAtSale=p.Price,Quantity=line.Quantity,Total=p.Price*line.Quantity});
   db.InventoryTransactions.Add(new InventoryTransaction{ProductId=p.Id,TransactionType="Sale",Quantity=-line.Quantity,ReferenceBillId=bill.Id,PreviousStock=previous,NewStock=p.StockQuantity,CreatedBy=userId,Reason="Bill sale"});
  }
  bill.Subtotal=bill.Items.Sum(i=>i.Total);bill.DiscountAmount=Rules.Round(bill.Subtotal*input.DiscountPercentage/100);bill.FinalAmount=bill.Subtotal-bill.DiscountAmount;
  // Sequence values are atomic. Gaps on rollback are intentional; numbers never repeat.
  var seq=await db.Database.SqlQueryRaw<long>("SELECT nextval('bill_number_seq') AS \"Value\"").SingleAsync(ct);
  bill.BillNumber=$"INV-{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(bill.BillDate,"Asia/Kolkata").Year}-{seq:D6}";
  db.Bills.Add(bill);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return bill;
 }
 public IQueryable<Bill> Visible(Guid userId,bool admin)=>db.Bills.AsNoTracking().Where(b=>admin||b.CreatedBy==userId);
 public async Task<Bill> Get(Guid id,Guid userId,bool admin,CancellationToken ct)=>await Visible(userId,admin).Include(b=>b.Items).SingleOrDefaultAsync(b=>b.Id==id,ct)??throw new AppError("Bill not found.",404);
 public async Task<object> List(Guid userId,bool admin,string? q,DateOnly? date,decimal? amount,int page,CancellationToken ct) {
  var query=Visible(userId,admin);
  if(!string.IsNullOrWhiteSpace(q)){q=q.Trim().ToLower();query=query.Where(b=>b.BillNumber.ToLower().Contains(q)||b.Items.Any(i=>i.ProductName.ToLower().Contains(q)||i.SerialNumber.Contains(q)));}
  if(date.HasValue){var start=DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue),DateTimeKind.Unspecified);var utc=TimeZoneInfo.ConvertTimeToUtc(start,TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));query=query.Where(b=>b.BillDate>=utc&&b.BillDate<utc.AddDays(1));}
  if(amount.HasValue)query=query.Where(b=>b.FinalAmount==amount);
  page=Math.Max(1,page);return new{total=await query.CountAsync(ct),items=await query.OrderByDescending(b=>b.BillDate).Skip((page-1)*30).Take(30).Select(b=>new{b.Id,b.BillNumber,b.BillDate,b.Subtotal,b.DiscountPercentage,b.DiscountAmount,b.FinalAmount}).ToListAsync(ct)};
 }
}
