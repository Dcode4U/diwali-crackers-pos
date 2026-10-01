using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Diwali.Infrastructure.Migrations;
[DbContext(typeof(PosDb))]
public class PosDbModelSnapshot:ModelSnapshot {
 protected override void BuildModel(ModelBuilder modelBuilder){modelBuilder.HasAnnotation("ProductVersion","10.0.0");InitialModel.Configure(modelBuilder);}
}
