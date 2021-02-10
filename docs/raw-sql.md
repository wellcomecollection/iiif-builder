Taking some time to understand the pros and cons of various approaches to raw sql before diving in and hacking stuff up.

The other day I did this in EF Core:

```
var sql = "SELECT * FROM manifestations OFFSET floor(random()*(select count(*) from manifestations)) LIMIT 1";
return ctx.Manifestations.FromSqlRaw(sql).ToList();
```

This is fine because `FromSqlRaw` is executed on the `DbSet<Manifestation>` and returns EF-tracked and known objects of that type, even though they are from raw sql.

In old EF, you could also drop down and use it as a free-form object mapper. I was doing this a lot, e.g.,

```
        public Dictionary<string, int> GetTotalsByAssetType()
        {
            const string sql = "SELECT AssetType, COUNT(*) AS AssetCount FROM FlatManifestations GROUP BY AssetType";
            return Database.SqlQuery<AssetTotal>(sql).Where(at => at.AssetType != null).ToDictionary(at => at.AssetType, at => at.AssetCount);
        }
```

in EF Core there's no longer `Database.SqlQuery<WhateverYouLike>(..)` - you can still do something similar, but you need to always execute on a DbSet for your type, even if it's synthetic:

https://docs.microsoft.com/en-us/ef/core/modeling/keyless-entity-types?tabs=data-annotations#example

I could do that... but it doesn't feel right to register types with EF just to do this kind of arbitrary mapping.

There are quite a few discussions/complaints about this, why you can't do it any more.

This answer is the most sensible I've read as to why you can't do this in EF:

https://github.com/dotnet/efcore/issues/11624#issuecomment-407636439

The rest of the discussion there is good, too.

A simple alternative for `Database.SqlQuery<..>` didn't make it into EF 5.0, but there are several suggestions for an Extension method that drops down to ADO.NET, e.g.,

https://github.com/dotnet/efcore/issues/1862#issuecomment-597022290.

I'll add something along these lines to the solution, so that the code stays clean in the DBContext class.

Done - see `Utils.Database.DbContextExtensions`
