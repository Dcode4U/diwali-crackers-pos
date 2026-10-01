using System.Security.Claims;
using System.Threading.RateLimiting;
using Diwali.Application;
using Diwali.Domain;
using Diwali.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
var builder=WebApplication.CreateBuilder(args.Where(a=>a is not "--migrate" and not "--seed-admin" and not "--seed-demo").ToArray());
var connection=builder.Configuration.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection.");
var origins=builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??["http://localhost:5173"];
if(!builder.Environment.IsDevelopment()&&(origins.Length==0||origins.Any(o=>!o.StartsWith("https://"))))throw new InvalidOperationException("Production CORS origins must be explicit HTTPS origins.");
var port=Environment.GetEnvironmentVariable("PORT");if(!string.IsNullOrEmpty(port))builder.WebHost.UseUrls($"http://0.0.0.0:{int.Parse(port)}");
builder.Services.AddDbContext<PosDb>(o=>o.UseNpgsql(connection));builder.Services.AddScoped<IPosDb>(sp=>sp.GetRequiredService<PosDb>());
builder.Services.AddScoped<CatalogService>();builder.Services.AddScoped<BillingService>();builder.Services.AddScoped<AdminService>();builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=210000);
builder.Services.AddControllers();builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var protection=builder.Services.AddDataProtection().SetApplicationName("DiwaliPOS");
var keys=builder.Configuration["DataProtection:KeyPath"];if(!string.IsNullOrWhiteSpace(keys))protection.PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
 o.Cookie.Name="diwali.session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
 o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
 o.Events.OnValidatePrincipal=async c=>{var db=c.HttpContext.RequestServices.GetRequiredService<PosDb>();if(!Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out var id)){c.RejectPrincipal();return;}var u=await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==id&&u.IsActive);if(u is null||u.SessionVersion.ToString()!=c.Principal?.FindFirstValue("sv")){c.RejectPrincipal();await c.HttpContext.SignOutAsync();}};
});builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)??ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=600,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();
if(args.Contains("--migrate")||args.Contains("--seed-admin")||args.Contains("--seed-demo")){
 await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PosDb>();
 if(args.Contains("--migrate"))await db.Database.MigrateAsync();
 if(args.Contains("--seed-admin")){
  var name=builder.Configuration["SeedAdmin:Name"]??"Administrator";var username=Rules.Text(builder.Configuration["SeedAdmin:Username"],"Seed admin username",100).ToLowerInvariant();var password=builder.Configuration["SeedAdmin:Password"];Rules.Password(password);
  if(await db.Users.AnyAsync(u=>u.Username==username))throw new InvalidOperationException("Admin already exists; use user management for changes.");
  var user=new User{Name=name,Username=username,Role="Admin"};user.PasswordHash=scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user,password!);db.Users.Add(user);await db.SaveChangesAsync();
 }
 if(args.Contains("--seed-demo")){
  if(!app.Environment.IsDevelopment())throw new InvalidOperationException("Demo data is allowed only in Development.");
  var actor=await db.Users.FirstAsync(u=>u.Role=="Admin");var service=scope.ServiceProvider.GetRequiredService<CatalogService>();
  var data=new[]{("1001","Flower Pot Small","Flower Pots",150m),("1002","Flower Pot Medium","Flower Pots",200m),("1003","Sparklers 10cm","Sparklers",80m),("1004","7 Shots","Shots",250m),("1005","Rocket","Rockets",300m)};
  foreach(var (serial,name,category,price) in data){if(await db.Products.AnyAsync(p=>p.SerialNumber==serial))continue;var c=await db.Categories.FirstOrDefaultAsync(c=>c.Name==category)??await service.SaveCategory(null,new(category,true),default);await service.Save(null,new(serial,name,c.Id,price,price,100,true,null),actor.Id,default);}
 }return;
}
app.Use(async(ctx,next)=>{
 ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="same-origin";ctx.Response.Headers["Cache-Control"]="no-store";
 try{await next();}catch(OperationCanceledException)when(ctx.RequestAborted.IsCancellationRequested){}catch(Exception e){
  var status=e is AppError ae?ae.Status:e is DbUpdateConcurrencyException?409:e is DbUpdateException {InnerException:PostgresException {SqlState:"23505"}}?409:500;
  var message=e is AppError?e.Message:e is DbUpdateConcurrencyException?"This record changed. Refresh and try again.":status==409?"A record with these details already exists.":"Unable to complete the request. Please retry; your bill submission is safe to retry.";
  app.Logger.LogError(e,"Request failed: {Method} {Path}",ctx.Request.Method,ctx.Request.Path);if(!ctx.Response.HasStarted){ctx.Response.StatusCode=status;await ctx.Response.WriteAsJsonAsync(new{message});}
 }
});
if(!app.Environment.IsDevelopment())app.UseHsts();
app.UseCors();
// Require a custom header on every mutation; hostile sites cannot send this without an approved CORS preflight.
app.Use(async(ctx,next)=>{if(ctx.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE"){
 if(ctx.Request.Headers["X-POS-Request"]!="1"){ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new{message="Missing request verification header."});return;}
 var origin=ctx.Request.Headers.Origin.ToString();if(origin.Length>0&&!origins.Contains(origin,StringComparer.Ordinal)){ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new{message="This website is not allowed to access the API."});return;}
}await next();});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.MapControllers();app.MapGet("/health",async(PosDb db,CancellationToken ct)=>await db.Database.CanConnectAsync(ct)?Results.Ok(new{status="healthy"}):Results.StatusCode(503)).AllowAnonymous();
await app.RunAsync();
public partial class Program {}
