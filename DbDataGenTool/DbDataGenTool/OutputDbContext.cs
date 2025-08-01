using Microsoft.EntityFrameworkCore;

namespace DbDataGenTool;

public class OutputDbContext : DbContext
{
    public DbSet<ExtractedDataModel> ExtractedDataModels { get; set; }
    public DbSet<ContactInformation> ContactInformation { get; set; }
    public DbSet<IncomeForm> IncomeForms { get; set; }

    public OutputDbContext(DbContextOptions<OutputDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExtractedDataModel>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasOne(e => e.ContactInformation)
                  .WithOne()
                  .HasForeignKey<ContactInformation>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.IncomeForms)
                  .WithOne()
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ContactInformation>(entity =>
        {
            entity.HasKey(c => c.UserId);
        });
        modelBuilder.Entity<IncomeForm>(entity =>
        {
            entity.HasKey(f => new { f.UserId, f.FormType, f.IncomeSource, f.IncomeAmount });
        });
    }
}
