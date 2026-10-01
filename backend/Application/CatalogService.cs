using Diwali.Domain;
using Microsoft.EntityFrameworkCore;
namespace Diwali.Application;
public class CatalogService(IPosDb db) {
 public async Task<object> Products(string? q,bool admin,int page,CancellationToken ct) {
  var query=db.Products.AsNoTracking().Where(p=>admin||p.IsActive);
  if(!string.IsNullOrWhiteSpace(q)){q=q.Trim().ToUpperInvariant();query=query.Where(p=>p.SerialNumber.Contains(q)||p.Name.ToUpper().Contains(q));}
  page=Math.Max(page,1);return new{total=await query.CountAsync(ct),items=await query.OrderBy(p=>p.SerialNumber==q?0:1).ThenBy(p=>p.SerialNumber).Skip((page-1)*50).Take(50).ToListAsync(ct)};
 }
 public async Task<Product> Get(Guid id,CancellationToken ct)=>await db.Products.SingleOrDefaultAsync(p=>p.Id==id,ct)??throw new AppError("Product not found.",404);
 public async Task<Product> Save(Guid? id,ProductInput input,Guid actor,CancellationToken ct) {
  var serial=Rules.Text(input.SerialNumber,"Serial number",40).ToUpperInvariant();var name=Rules.Text(input.Name,"Product name");Rules.Money(input.Price,"Price");Rules.Money(input.MRP,"MRP");
  if(input.MRP<input.Price)throw new AppError("MRP cannot be below the selling price.");
  if(input.StockQuantity<0||input.StockQuantity>1000000)throw new AppError("Stock must be between 0 and 1,000,000 boxes.");
  if(!await db.Categories.AnyAsync(c=>c.Id==input.CategoryId&&c.IsActive,ct))throw new AppError("Select an active category.");
  if(await db.Products.AnyAsync(p=>p.SerialNumber==serial&&p.Id!=id,ct))throw new AppError("That serial number already exists.",409);
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  var p=id.HasValue?await Get(id.Value,ct):new Product();
  if(id.HasValue&&input.Version!=p.Version)throw new AppError("This product changed. Refresh before saving.",409);
  if(id.HasValue&&input.StockQuantity!=p.StockQuantity)throw new AppError("Use Inventory adjustment to change stock.",409);
  p.SerialNumber=serial;p.Name=name;p.CategoryId=input.CategoryId;p.Price=input.Price;p.MRP=input.MRP;p.IsActive=input.IsActive;p.UpdatedAt=DateTime.UtcNow;
  if(!id.HasValue){p.StockQuantity=input.StockQuantity;db.Products.Add(p);if(p.StockQuantity>0)db.InventoryTransactions.Add(new InventoryTransaction{ProductId=p.Id,TransactionType="Opening",Quantity=p.StockQuantity,PreviousStock=0,NewStock=p.StockQuantity,CreatedBy=actor,Reason="Opening stock"});}
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return p;
 }
 public async Task Deactivate(Guid id,CancellationToken ct){var p=await Get(id,ct);p.IsActive=false;p.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
 public async Task<List<Category>> Categories(bool admin,CancellationToken ct)=>await db.Categories.AsNoTracking().Where(c=>admin||c.IsActive).OrderBy(c=>c.Name).ToListAsync(ct);
 public async Task<Category> SaveCategory(Guid? id,CategoryInput input,CancellationToken ct){var name=Rules.Text(input.Name,"Category",80);if(await db.Categories.AnyAsync(c=>c.Name.ToLower()==name.ToLower()&&c.Id!=id,ct))throw new AppError("Category already exists.",409);var c=id.HasValue?await db.Categories.FindAsync([id.Value],ct)??throw new AppError("Category not found.",404):new Category();c.Name=name;c.IsActive=input.IsActive;if(!id.HasValue)db.Categories.Add(c);await db.SaveChangesAsync(ct);return c;}
 public async Task<Product> Adjust(StockInput input,Guid actor,CancellationToken ct) {
  if(input.Quantity==0||Math.Abs((long)input.Quantity)>1000000)throw new AppError("Enter a non-zero stock adjustment up to 1,000,000 boxes.");var reason=Rules.Text(input.Reason,"Adjustment reason",300);
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  var p=await db.Products.FromSqlInterpolated($"SELECT *, xmin FROM \"Products\" WHERE \"Id\"={input.ProductId} FOR UPDATE").SingleOrDefaultAsync(ct)??throw new AppError("Product not found.",404);
  var previous=p.StockQuantity;var stock=(long)previous+input.Quantity;if(stock<0||stock>1000000)throw new AppError("Resulting stock must be between 0 and 1,000,000 boxes.");
  p.StockQuantity=(int)stock;p.UpdatedAt=DateTime.UtcNow;db.InventoryTransactions.Add(new InventoryTransaction{ProductId=p.Id,TransactionType="Adjustment",Quantity=input.Quantity,PreviousStock=previous,NewStock=p.StockQuantity,CreatedBy=actor,Reason=reason});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return p;
 }
 public async Task<object> Inventory(int page,CancellationToken ct)=>new{items=await db.InventoryTransactions.AsNoTracking().OrderByDescending(i=>i.CreatedAt).Skip((Math.Max(1,page)-1)*50).Take(50).Join(db.Products,i=>i.ProductId,p=>p.Id,(i,p)=>new{i.Id,i.ProductId,ProductName=p.Name,p.SerialNumber,i.TransactionType,i.Quantity,i.PreviousStock,i.NewStock,i.Reason,i.CreatedAt,i.ReferenceBillId}).ToListAsync(ct),total=await db.InventoryTransactions.CountAsync(ct)};
}
