using System.Security.Claims;
using Diwali.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Diwali.API.Controllers;
[ApiController,Authorize]
public abstract class PosController:ControllerBase {protected Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);protected bool Admin=>User.IsInRole("Admin");}
[Route("api/auth")]
public class AuthController(AdminService service):PosController {
 [HttpPost("login"),AllowAnonymous,EnableRateLimiting("login")] public async Task<IActionResult> Login(LoginInput input,CancellationToken ct){var u=await service.Login(input,ct);if(u is null)return Unauthorized(new{message="Invalid username or password."});var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Name,u.Name),new Claim(ClaimTypes.Role,u.Role),new Claim("sv",u.SessionVersion.ToString())},CookieAuthenticationDefaults.AuthenticationScheme);await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity),new AuthenticationProperties{IsPersistent=false});return Ok(new{u.Id,u.Name,u.Role});}
 [HttpPost("logout")]public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return NoContent();}
 [HttpGet("me")]public IActionResult Me()=>Ok(new{id=Actor,name=User.Identity!.Name,role=Admin?"Admin":"Cashier"});
}
[Route("api/products")]
public class ProductsController(CatalogService service):PosController {
 [HttpGet]public Task<object> List(string? q,int page=1,CancellationToken ct=default)=>service.Products(q,Admin,page,ct);
 [HttpGet("search")]public Task<object> Search(string? q,CancellationToken ct)=>service.Products(q,false,1,ct);
 [HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid id,CancellationToken ct){var p=await service.Get(id,ct);return !Admin&&!p.IsActive?NotFound():Ok(p);}
 [HttpPost,Authorize(Roles="Admin")]public async Task<IActionResult> Create(ProductInput input,CancellationToken ct)=>Ok(await service.Save(null,input,Actor,ct));
 [HttpPut("{id:guid}"),Authorize(Roles="Admin")]public async Task<IActionResult> Update(Guid id,ProductInput input,CancellationToken ct)=>Ok(await service.Save(id,input,Actor,ct));
 [HttpDelete("{id:guid}"),Authorize(Roles="Admin")]public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await service.Deactivate(id,ct);return NoContent();}
}
[Route("api/categories")]
public class CategoriesController(CatalogService service):PosController {
 [HttpGet]public Task<List<Diwali.Domain.Category>> List(CancellationToken ct)=>service.Categories(Admin,ct);
 [HttpPost,Authorize(Roles="Admin")]public async Task<IActionResult> Create(CategoryInput input,CancellationToken ct)=>Ok(await service.SaveCategory(null,input,ct));
 [HttpPut("{id:guid}"),Authorize(Roles="Admin")]public async Task<IActionResult> Update(Guid id,CategoryInput input,CancellationToken ct)=>Ok(await service.SaveCategory(id,input,ct));
}
[Route("api/bills")]
public class BillsController(BillingService service):PosController {
 [HttpPost]public async Task<IActionResult> Complete(SaleInput input,CancellationToken ct)=>Ok(await service.Complete(input,Actor,ct));
 [HttpGet]public Task<object> List(string? q,DateOnly? date,decimal? amount,int page=1,CancellationToken ct=default)=>service.List(Actor,Admin,q,date,amount,page,ct);
 [HttpGet("{id:guid}"),HttpGet("{id:guid}/print")]public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await service.Get(id,Actor,Admin,ct));
}
[Route("api/inventory"),Authorize(Roles="Admin")]
public class InventoryController(CatalogService service):PosController {
 [HttpGet]public Task<object> Get(int page=1,CancellationToken ct=default)=>service.Inventory(page,ct);
 [HttpPost("adjust")]public async Task<IActionResult> Adjust(StockInput input,CancellationToken ct)=>Ok(await service.Adjust(input,Actor,ct));
}
[Route("api/users"),Authorize(Roles="Admin")]
public class UsersController(AdminService service):PosController {
 [HttpGet]public Task<object> Get(CancellationToken ct)=>service.Users(ct);
 [HttpPost]public async Task<IActionResult> Create(UserInput input,CancellationToken ct)=>Ok(await service.SaveUser(null,input,Actor,ct));
 [HttpPut("{id:guid}")]public async Task<IActionResult> Update(Guid id,UserInput input,CancellationToken ct)=>Ok(await service.SaveUser(id,input,Actor,ct));
}
[Route("api/settings")]
public class SettingsController(AdminService service):PosController {
 [HttpGet]public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await service.Settings(ct));
 [HttpPut,Authorize(Roles="Admin")]public async Task<IActionResult> Update(SettingsInput input,CancellationToken ct)=>Ok(await service.SaveSettings(input,ct));
}
[Route("api/dashboard"),Authorize(Roles="Admin")]
public class DashboardController(AdminService service):PosController {
 [HttpGet("summary")]public Task<object> Get(CancellationToken ct)=>service.Dashboard(ct);
}
