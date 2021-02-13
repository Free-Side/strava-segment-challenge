using System;
using System.Collections.Immutable;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SegmentChallengeWeb.Persistence {
    public class SegmentChallengeDbContext : DbContext {
        protected static IImmutableDictionary<Type, ValueConverter> ValueConverters { get; } =
            ImmutableDictionary.Create<Type, ValueConverter>()
                .Add(
                    typeof(DateTime),
                    new ValueConverter<DateTime, DateTime>(
                        dt => dt,
                        dt => new DateTime(dt.Ticks, DateTimeKind.Utc)));

        private readonly DbConnection connection;

        public SegmentChallengeDbContext(DbConnection connection) : base() {
            if (connection == null) {
                throw new ArgumentNullException(nameof(connection));
            }

            this.connection = connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            var allTypes = typeof(SegmentChallengeDbContext).Assembly.GetTypes();
            foreach (var modelType in allTypes.Where(IsDatabaseModel)) {
                this.AddEntityType(modelBuilder, modelType);
            }

            base.OnModelCreating(modelBuilder);
        }

        protected void AddEntityType(ModelBuilder modelBuilder, Type modelType) {
            var dbType = modelType.GetCustomAttribute<DatabaseTypeAttribute>();
            var entity = modelBuilder.Entity(modelType);

            // We have to manually register our properties because we violate certain
            // EntityFramework conventions.
            foreach (var property in modelType.GetProperties()) {
                // Ignore read only properties.
                if (!property.CanWrite) {
                    continue;
                }

                var entityProperty = entity.Property(property.Name);

                if (ValueConverters.TryGetValue(UnderlyingType(property.PropertyType), out var converter)) {
                    entityProperty.HasConversion(converter);
                }

                if (UnderlyingType(property.PropertyType) == typeof(DateTime)) {
                    // pretend like we support milliseconds
                    entityProperty.HasColumnType("datetime(3)");
                }
            }

            entity.ToTable(modelType.Name);
            entity.HasKey(dbType.PrimaryKey ?? new[] { "Id" });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
            base.OnConfiguring(
                optionsBuilder.UseMySql(
                    this.connection,
                    options => options.ServerVersion(this.connection.ServerVersion)
                )
            );
        }

        private static Type UnderlyingType(Type type) {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ?
                Nullable.GetUnderlyingType(type) :
                type;
        }

        private static Boolean IsDatabaseModel(Type modelType) {
            var dbType = modelType.GetCustomAttribute<DatabaseTypeAttribute>();
            return dbType != null;
        }
    }
}
