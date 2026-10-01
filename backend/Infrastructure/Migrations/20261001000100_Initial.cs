using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Diwali.Infrastructure.Migrations;
[DbContext(typeof(PosDb))]
[Migration("20261001000100_Initial")]
public class Initial:Migration {
 protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
CREATE SEQUENCE bill_number_seq START 1;
CREATE TABLE "Categories" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "IsActive" boolean NOT NULL, "CreatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX "IX_Categories_Name" ON "Categories" ("Name");
CREATE TABLE "Users" ("Id" uuid PRIMARY KEY, "Name" text NOT NULL, "Username" text NOT NULL, "PasswordHash" text NOT NULL, "Role" text NOT NULL CHECK ("Role" IN ('Admin','Cashier')), "IsActive" boolean NOT NULL, "SessionVersion" integer NOT NULL, "CreatedAt" timestamptz NOT NULL);
CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
CREATE TABLE "Products" ("Id" uuid PRIMARY KEY, "SerialNumber" text NOT NULL, "Name" text NOT NULL, "CategoryId" uuid NOT NULL REFERENCES "Categories"("Id"), "Price" numeric(18,2) NOT NULL, "MRP" numeric(18,2) NOT NULL, "StockQuantity" integer NOT NULL, "IsActive" boolean NOT NULL, "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL, CONSTRAINT "CK_Product_Price" CHECK ("Price">=0 AND "MRP">="Price"), CONSTRAINT "CK_Product_Stock" CHECK ("StockQuantity">=0));
CREATE UNIQUE INDEX "IX_Products_SerialNumber" ON "Products"("SerialNumber");
CREATE INDEX "IX_Products_CategoryId" ON "Products"("CategoryId");
CREATE TABLE "Bills" ("Id" uuid PRIMARY KEY, "BillNumber" text NOT NULL, "BillDate" timestamptz NOT NULL, "Subtotal" numeric(18,2) NOT NULL CHECK ("Subtotal">=0), "DiscountPercentage" numeric(18,2) NOT NULL CHECK ("DiscountPercentage" BETWEEN 0 AND 100), "DiscountAmount" numeric(18,2) NOT NULL, "FinalAmount" numeric(18,2) NOT NULL CHECK ("FinalAmount">=0), "CreatedBy" uuid NOT NULL REFERENCES "Users"("Id"), "CreatedAt" timestamptz NOT NULL, "SubmissionId" uuid NOT NULL, "RequestHash" text NOT NULL, "ShopName" text NOT NULL, "ShopAddress" text NOT NULL, "ShopPhone" text NOT NULL);
CREATE UNIQUE INDEX "IX_Bills_BillNumber" ON "Bills"("BillNumber");
CREATE UNIQUE INDEX "IX_Bills_CreatedBy_SubmissionId" ON "Bills"("CreatedBy","SubmissionId");
CREATE INDEX "IX_Bills_BillDate" ON "Bills"("BillDate");
CREATE TABLE "BillItems" ("Id" uuid PRIMARY KEY, "BillId" uuid NOT NULL REFERENCES "Bills"("Id"), "ProductId" uuid NOT NULL REFERENCES "Products"("Id"), "SerialNumber" text NOT NULL, "ProductName" text NOT NULL, "PriceAtSale" numeric(18,2) NOT NULL CHECK ("PriceAtSale">=0), "Quantity" integer NOT NULL CHECK ("Quantity">0), "Total" numeric(18,2) NOT NULL);
CREATE INDEX "IX_BillItems_BillId" ON "BillItems"("BillId");
CREATE INDEX "IX_BillItems_ProductId" ON "BillItems"("ProductId");
CREATE TABLE "InventoryTransactions" ("Id" uuid PRIMARY KEY, "ProductId" uuid NOT NULL REFERENCES "Products"("Id"), "TransactionType" text NOT NULL, "Quantity" integer NOT NULL, "ReferenceBillId" uuid REFERENCES "Bills"("Id"), "PreviousStock" integer NOT NULL CHECK ("PreviousStock">=0), "NewStock" integer NOT NULL CHECK ("NewStock">=0), "CreatedBy" uuid NOT NULL REFERENCES "Users"("Id"), "CreatedAt" timestamptz NOT NULL, "Reason" text NOT NULL);
CREATE INDEX "IX_InventoryTransactions_ProductId" ON "InventoryTransactions"("ProductId");
CREATE INDEX "IX_InventoryTransactions_ReferenceBillId" ON "InventoryTransactions"("ReferenceBillId");
CREATE INDEX "IX_InventoryTransactions_CreatedBy" ON "InventoryTransactions"("CreatedBy");
CREATE INDEX "IX_InventoryTransactions_CreatedAt" ON "InventoryTransactions"("CreatedAt");
CREATE TABLE "Settings" ("Id" integer PRIMARY KEY, "Name" text NOT NULL, "Address" text NOT NULL, "Phone" text NOT NULL, "LowStockThreshold" integer NOT NULL);
INSERT INTO "Settings" VALUES (1,'Diwali Crackers','','',10);
""");
 protected override void Down(MigrationBuilder migrationBuilder)=>throw new NotSupportedException("Destructive rollback is intentionally blocked. Restore a reviewed database backup.");
 protected override void BuildTargetModel(ModelBuilder modelBuilder){modelBuilder.HasAnnotation("ProductVersion","10.0.0");InitialModel.Configure(modelBuilder);}
}
