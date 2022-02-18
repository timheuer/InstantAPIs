﻿using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Microsoft.AspNetCore.Builder;

public static class WebApplicationExtensions
{

	private static InstantAPIsConfig Configuration { get; set; } = new();

	public static WebApplication MapInstantAPIs<D>(this WebApplication app, Action<InstantAPIsConfigBuilder<D>, D> options = null) where D: DbContext, new()
	{

		var builder = new InstantAPIsConfigBuilder<D>();
		if (options != null)
		{
			var ctx = app.Services.CreateScope().ServiceProvider.GetService(typeof(D)) as D;
			if (ctx is null) throw new ArgumentNullException(nameof(D));
			options(builder, ctx);
			Configuration = builder.Build();
		}

		// Get the tables on the DbContext
		var dbTables = typeof(D).GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Where(x => x.PropertyType.FullName.StartsWith("Microsoft.EntityFrameworkCore.DbSet"))
			.Select(x => new TypeTable { Name = x.Name, InstanceType = x.PropertyType.GenericTypeArguments.First() });

		var requestedTables = Configuration.IncludedTables.Any(t => t.TableName.Equals("all", StringComparison.InvariantCultureIgnoreCase)) && !Configuration.ExcludedTables.Any() ?
			dbTables :
			dbTables.Where(t => Configuration.IncludedTables.Any(i => t.Name.Equals(i.TableName, StringComparison.InvariantCultureIgnoreCase)) &&
				!Configuration.ExcludedTables.Any(e => t.Name.Equals(e, StringComparison.CurrentCultureIgnoreCase))
			).ToArray();

		var allMethods = typeof(MapApiExtensions).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(m => m.Name.StartsWith("Map"));
		foreach (var table in requestedTables)
		{

			// The default URL for an InstantAPI is /api/TABLENAME
			var url = $"/api/{table.Name}";

			// The remaining private static methods in this class build out the Mapped API methods..
			// let's use some reflection to get them
			foreach (var method in allMethods)
			{
				var genericMethod = method.MakeGenericMethod(typeof(D), table.InstanceType);
				genericMethod.Invoke(null, new object[] { app, url });
			}

		}

		return app;
	}

	internal class TypeTable
	{
		public string Name { get; set; }
		public Type InstanceType { get; set; }
	}

}
