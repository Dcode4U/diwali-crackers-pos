namespace Diwali.Domain;
public class Product {
 public Guid Id {get;set;}=Guid.NewGuid(); public string SerialNumber {get;set;}=""; public string Name {get;set;}="";
 public Guid CategoryId {get;set;} public decimal Price {get;set;} public decimal MRP {get;set;} public int StockQuantity {get;set;}
 public bool IsActive {get;set;}=true; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime UpdatedAt {get;set;}=DateTime.UtcNow;
 public uint Version {get;set;}
}
public class Category {public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get;set;}=""; public bool IsActive {get;set;}=true; public DateTime CreatedAt {get;set;}=DateTime.UtcNow;}
public class User {public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get;set;}=""; public string Username {get;set;}=""; public string PasswordHash {get;set;}=""; public string Role {get;set;}="Cashier"; public bool IsActive {get;set;}=true; public int SessionVersion {get;set;} public DateTime CreatedAt {get;set;}=DateTime.UtcNow;}
public class Bill {
 public Guid Id {get;set;}=Guid.NewGuid(); public string BillNumber {get;set;}=""; public DateTime BillDate {get;set;}=DateTime.UtcNow;
 public decimal Subtotal {get;set;} public decimal DiscountPercentage {get;set;} public decimal DiscountAmount {get;set;} public decimal FinalAmount {get;set;}
 public Guid CreatedBy {get;set;} public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public Guid SubmissionId {get;set;}
 public string RequestHash {get;set;}=""; public string ShopName {get;set;}=""; public string ShopAddress {get;set;}=""; public string ShopPhone {get;set;}="";
 public List<BillItem> Items {get;set;}=[];
}
public class BillItem {public Guid Id {get;set;}=Guid.NewGuid(); public Guid BillId {get;set;} public Guid ProductId {get;set;} public string SerialNumber {get;set;}=""; public string ProductName {get;set;}=""; public decimal PriceAtSale {get;set;} public int Quantity {get;set;} public decimal Total {get;set;}}
public class InventoryTransaction {public Guid Id {get;set;}=Guid.NewGuid(); public Guid ProductId {get;set;} public string TransactionType {get;set;}=""; public int Quantity {get;set;} public Guid? ReferenceBillId {get;set;} public int PreviousStock {get;set;} public int NewStock {get;set;} public Guid CreatedBy {get;set;} public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public string Reason {get;set;}="";}
public class ShopSettings {public int Id {get;set;}=1; public string Name {get;set;}="Diwali Crackers"; public string Address {get;set;}=""; public string Phone {get;set;}=""; public int LowStockThreshold {get;set;}=10;}
