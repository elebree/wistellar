using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace wistellar.core.Conventions
{
    public class TimestampConvention : IPropertyAddedConvention
    {
        public void ProcessPropertyAdded(
            IConventionPropertyBuilder propertyBuilder,
            IConventionContext<IConventionPropertyBuilder> context)
        {
            var propertyInfo = propertyBuilder.Metadata.PropertyInfo;
            if (propertyInfo == null)
                return; // skip shadow properties

            var type = propertyBuilder.Metadata.ClrType;
            if (type != typeof(DateTime) && type != typeof(DateTime?))
                return;

            var dbGeneratedAttr = propertyInfo.GetCustomAttribute<DatabaseGeneratedAttribute>();
            if (dbGeneratedAttr == null)
                return;

            // Use a default SQL that works for most providers
            string? defaultValueSql = dbGeneratedAttr.DatabaseGeneratedOption switch
            {
                DatabaseGeneratedOption.Identity => "CURRENT_TIMESTAMP",
                DatabaseGeneratedOption.Computed => "CURRENT_TIMESTAMP",
                _ => null
            };

            if (defaultValueSql != null)
            {
                propertyBuilder.HasDefaultValueSql(defaultValueSql);

                // Set correct value generation
                if (dbGeneratedAttr.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                    propertyBuilder.ValueGenerated(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd);
                else if (dbGeneratedAttr.DatabaseGeneratedOption == DatabaseGeneratedOption.Computed)
                    propertyBuilder.ValueGenerated(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
            }
        }
    }

    // Plugin to register the convention
    public class TimestampConventionSetPlugin : IConventionSetPlugin
    {
        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            conventionSet.PropertyAddedConventions.Add(new TimestampConvention());
            return conventionSet;
        }
    }
}